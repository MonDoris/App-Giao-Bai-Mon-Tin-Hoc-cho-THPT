using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Authoring;

/// <summary>Soạn bài học của giáo viên: danh sách, xem, tạo, sửa, xuất bản, lưu trữ, xóa, sắp xếp (AC-111..117, 121).</summary>
public sealed class LessonAuthoringUseCase
{
    private readonly ILessonAuthoringRepository _repository;
    private readonly IAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LessonAuthoringUseCase(
        ILessonAuthoringRepository repository, IAssignmentRepository assignments, IUnitOfWork unitOfWork, IClock clock)
    {
        _repository = repository;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<TeacherLessonListDto> ListAsync(CurrentUser user, bool includeArchived, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var rows = await _repository.ListLessonsAsync(includeArchived, cancellationToken);
        return new TeacherLessonListDto(rows
            .OrderBy(r => r.Order)
            .Select(r => new TeacherLessonSummaryDto(
                r.Id, r.Order, r.Title, r.Description, r.IsPublished, r.IsArchived, r.ExerciseCount,
                r.PublishedExerciseCount, r.AssignmentCount, r.Version, r.UpdatedUtc, r.CreatedByName))
            .ToList());
    }

    public async Task<TeacherLessonDto> GetAsync(CurrentUser user, string? id, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var lesson = await LoadAsync(id, cancellationToken);
        return await ToDtoAsync(lesson, cancellationToken);
    }

    public async Task<TeacherLessonDto> CreateAsync(CurrentUser user, LessonInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var errors = new FieldErrors();
        var data = Validate(input, errors);

        string? manualId = null;
        if (!string.IsNullOrWhiteSpace(input.Id))
        {
            manualId = input.Id.Trim();
            if (!IdRules.IsValid(manualId))
            {
                errors.Add("id", "Mã chỉ gồm chữ thường không dấu, số và dấu gạch ngang, tối đa 100 ký tự.");
            }
        }

        errors.ThrowIfAny();

        string id;
        if (manualId is not null)
        {
            if (await _repository.LessonIdExistsAsync(manualId, cancellationToken))
            {
                throw new ConflictException("DUPLICATE_ID", "Mã bài học đã tồn tại, hãy chọn mã khác.");
            }

            id = manualId;
        }
        else
        {
            id = await SlugGenerator.UniqueAsync(
                SlugGenerator.Slugify(data!.Title, fallback: "bai-hoc"),
                async candidate => await _repository.LessonIdExistsAsync(candidate, cancellationToken));
        }

        var lesson = new Lesson
        {
            Id = id,
            Order = await _repository.NextLessonOrderAsync(cancellationToken),
            IsPublished = false,
            IsArchived = false,
            CreatedByUserId = user.Id,
            Version = 1,
            UpdatedUtc = _clock.UtcNow,
        };
        Apply(lesson, data!);
        _repository.AddLesson(lesson);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, id, cancellationToken);
    }

    public async Task<TeacherLessonDto> UpdateAsync(CurrentUser user, string? id, LessonInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var lesson = await LoadAsync(id, cancellationToken);
        var errors = new FieldErrors();
        var data = Validate(input, errors);
        if (input.Version is null)
        {
            errors.Add("version", "Thiếu phiên bản (version) của bài học đang sửa.");
        }

        errors.ThrowIfAny();

        if (lesson.Version != input.Version)
        {
            throw new ConflictException("CONFLICT", "Bài học đã được người khác chỉnh sửa, hãy tải lại.");
        }

        Apply(lesson, data!);
        Touch(lesson);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, lesson.Id, cancellationToken);
    }

    public async Task<TeacherLessonDto> SetPublishedAsync(CurrentUser user, string? id, bool isPublished, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var lesson = await LoadAsync(id, cancellationToken);
        lesson.IsPublished = isPublished;
        Touch(lesson);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, lesson.Id, cancellationToken);
    }

    /// <summary>Lưu trữ giữ nguyên cờ xuất bản; khôi phục đưa bài về đúng trạng thái xuất bản/bản nháp trước đó.</summary>
    public async Task<TeacherLessonDto> SetArchivedAsync(CurrentUser user, string? id, bool isArchived, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var lesson = await LoadAsync(id, cancellationToken);
        lesson.IsArchived = isArchived;
        Touch(lesson);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, lesson.Id, cancellationToken);
    }

    public async Task DeleteAsync(CurrentUser user, string? id, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var lesson = await LoadAsync(id, cancellationToken);
        var dependencies = await _repository.GetLessonDependenciesAsync(lesson.Id, cancellationToken);
        if (dependencies.Any)
        {
            throw new ConflictException(
                "HAS_DEPENDENCIES",
                $"Bài học đã được giao ({dependencies.Assignments} lượt giao) hoặc đã có {dependencies.Submissions} bài nộp. "
                + "Hãy lưu trữ bài học thay vì xóa.");
        }

        _repository.RemoveLesson(lesson);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>ids phải là hoán vị đầy đủ của mọi bài học hiện có; cập nhật Order = 1..n trong một giao dịch.</summary>
    public async Task ReorderAsync(CurrentUser user, IReadOnlyList<string>? ids, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var lessons = await _repository.GetAllLessonsAsync(cancellationToken);
        await ReorderHelper.ApplyAsync(
            ids, lessons, l => l.Id, (l, order) => l.Order = order, _unitOfWork, "bài học", cancellationToken);
    }

    private async Task<Lesson> LoadAsync(string? id, CancellationToken cancellationToken)
    {
        if (!IdRules.IsValid(id))
        {
            throw new NotFoundException("Không tìm thấy bài học.");
        }

        return await _repository.GetLessonForEditAsync(id!, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài học.");
    }

    private async Task<TeacherLessonDto> ToDtoAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        var exerciseIds = lesson.Exercises.Select(e => e.Id).ToList();
        var lessonDeps = await _repository.GetLessonDependenciesAsync(lesson.Id, cancellationToken);
        var affected = await _assignments.CountStudentsReachedAsync(exerciseIds, cancellationToken);
        var exercises = new List<TeacherExerciseDto>();
        foreach (var e in lesson.Exercises.OrderBy(x => x.Order))
        {
            var deps = await _repository.GetExerciseDependenciesAsync(e.Id, cancellationToken);
            exercises.Add(AuthoringMapper.ToDto(e, deps, 0));
        }

        return AuthoringMapper.ToDto(lesson, lessonDeps, affected, exercises);
    }

    private void Touch(Lesson lesson)
    {
        lesson.Version++;
        lesson.UpdatedUtc = _clock.UtcNow;
    }

    private static void Apply(Lesson lesson, LessonData data)
    {
        lesson.Title = data.Title;
        lesson.Description = data.Description;
        lesson.SummaryJson = AuthoringMapper.ToJson(data.Summary);
        lesson.NotesJson = AuthoringMapper.ToJson(data.Notes);
        lesson.ConclusionJson = AuthoringMapper.ToJson(data.Conclusion);
    }

    private sealed record LessonData(string Title, string Description, List<string> Summary, List<string> Notes, List<string> Conclusion);

    private static LessonData? Validate(LessonInputDto input, FieldErrors errors)
    {
        var title = errors.Text("title", "Tiêu đề", input.Title, 200);
        var description = errors.Text("description", "Mô tả", input.Description, 500);
        var summary = errors.Blocks("summary", "Tóm tắt", input.Summary, 1, 50, 5000);
        var notes = errors.Blocks("notes", "Lưu ý", input.Notes, 1, 50, 5000);
        var conclusion = errors.Blocks("conclusion", "Tổng kết", input.Conclusion, 1, 50, 5000);
        if (title is null || description is null || summary is null || notes is null || conclusion is null)
        {
            return null;
        }

        return new LessonData(title, description, summary, notes, conclusion);
    }
}

/// <summary>Sắp xếp lại một danh sách theo hoán vị Id: kiểm tra đủ/không trùng rồi đặt Order 1..n qua bước đệm (không vi phạm unique).</summary>
internal static class ReorderHelper
{
    public static async Task ApplyAsync<T>(
        IReadOnlyList<string>? ids,
        IReadOnlyList<T> items,
        Func<T, string> idOf,
        Action<T, int> setOrder,
        IUnitOfWork unitOfWork,
        string label,
        CancellationToken cancellationToken)
    {
        if (ids is null)
        {
            throw new ValidationFailedException("ids", $"Thiếu danh sách {label} cần sắp xếp.");
        }

        var existing = items.Select(idOf).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != existing.Count || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count || ids.Any(i => !existing.Contains(i)))
        {
            throw new ValidationFailedException("ids", $"Danh sách {label} phải gồm đầy đủ các {label} hiện có, mỗi mục một lần.");
        }

        var byId = items.ToDictionary(idOf, StringComparer.Ordinal);
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                // Bước đệm: đặt Order âm tạm thời để không vướng ràng buộc duy nhất khi hoán đổi.
                for (var i = 0; i < ids.Count; i++)
                {
                    setOrder(byId[ids[i]], -(i + 1));
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                for (var i = 0; i < ids.Count; i++)
                {
                    setOrder(byId[ids[i]], i + 1);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);
    }
}

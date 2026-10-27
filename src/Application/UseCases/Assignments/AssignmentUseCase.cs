using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Assignments;

/// <summary>Giao bài cho học sinh (AC-128, 130): giao, sửa hạn/ghi chú/đối tượng, hủy giao, liệt kê.</summary>
public sealed class AssignmentUseCase
{
    private const int MaxExercisesPerRequest = 200;
    private const int MaxNoteLength = 500;

    private readonly ILessonRepository _lessons;
    private readonly IAssignmentRepository _assignments;
    private readonly IClassRepository _classes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AssignmentUseCase(
        ILessonRepository lessons,
        IAssignmentRepository assignments,
        IClassRepository classes,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _lessons = lessons;
        _assignments = assignments;
        _classes = classes;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AssignResultDto> AssignAsync(CurrentUser user, AssignInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var now = _clock.UtcNow;
        var errors = new FieldErrors();
        var exerciseIds = (input.ExerciseIds ?? Array.Empty<string>()).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
        if (exerciseIds.Count == 0)
        {
            errors.Add("exerciseIds", "Hãy chọn ít nhất một bài tập để giao.");
        }
        else if (exerciseIds.Count > MaxExercisesPerRequest)
        {
            errors.Add("exerciseIds", $"Mỗi lần giao tối đa {MaxExercisesPerRequest} bài tập.");
        }

        var (all, classIds) = ReadTarget(input.Target, errors);
        var note = errors.Text("note", "Ghi chú", input.Note, MaxNoteLength, required: false, allowMultiline: true);
        var due = NormalizeDue(input.DueAt);
        if (due is { } d && d <= now)
        {
            errors.Add("dueAt", "Hạn nộp phải sau thời điểm hiện tại.");
        }

        errors.ThrowIfAny();
        await EnsureClassesExistAsync(classIds, cancellationToken);

        var exercises = new List<Exercise>();
        foreach (var id in exerciseIds)
        {
            var exercise = IdRules.IsValid(id) ? await _lessons.GetExerciseAsync(id, cancellationToken) : null;
            if (exercise is null)
            {
                throw new NotFoundException("Không tìm thấy bài tập cần giao.");
            }

            if (!exercise.IsPublished || exercise.IsArchived || exercise.Lesson is { IsPublished: false } || exercise.Lesson is { IsArchived: true })
            {
                throw new ValidationFailedException(
                    "exerciseIds",
                    $"Bài tập \"{exercise.Title}\" chưa xuất bản hoặc đã lưu trữ nên chưa giao được.",
                    "EXERCISE_NOT_PUBLISHED");
            }

            exercises.Add(exercise);
        }

        var existing = await _assignments.GetActiveForExercisesAsync(exerciseIds, cancellationToken);
        var touched = new List<Assignment>();
        foreach (var exercise in exercises)
        {
            // Giao lại cùng đối tượng thì cập nhật hạn/ghi chú thay vì tạo trùng.
            var same = existing.FirstOrDefault(a => a.ExerciseId == exercise.Id && SameTarget(a, all, classIds));
            if (same is not null)
            {
                same.DueAtUtc = due;
                same.Note = NullIfEmpty(note);
                touched.Add(same);
                continue;
            }

            var assignment = new Assignment
            {
                ExerciseId = exercise.Id,
                AssignedToAll = all,
                DueAtUtc = due,
                Note = NullIfEmpty(note),
                CreatedByUserId = user.Id,
                CreatedUtc = now,
            };
            if (!all)
            {
                foreach (var classId in classIds)
                {
                    assignment.Classes.Add(new AssignmentClass { ClassId = classId });
                }
            }

            _assignments.Add(assignment);
            touched.Add(assignment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var studentCount = await _assignments.CountStudentsReachedAsync(exerciseIds, cancellationToken);
        var rows = await _assignments.ListByIdsAsync(touched.Select(t => t.Id), cancellationToken);
        return new AssignResultDto(rows.Select(ToDto).ToList(), studentCount);
    }

    public async Task<AssignmentListDto> ListAsync(
        CurrentUser user, string? lessonId, int? classId, bool includeCancelled, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var rows = await _assignments.ListAsync(lessonId, classId, includeCancelled, cancellationToken);
        return new AssignmentListDto(rows.Select(ToDto).ToList());
    }

    public async Task<AssignmentDto> UpdateAsync(CurrentUser user, int id, UpdateAssignmentInputDto input, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var assignment = await _assignments.GetByIdAsync(id, cancellationToken);
        if (assignment is null || !assignment.IsActive)
        {
            throw new NotFoundException("Không tìm thấy lượt giao bài.");
        }

        var errors = new FieldErrors();
        var (all, classIds) = ReadTarget(input.Target, errors);
        var note = errors.Text("note", "Ghi chú", input.Note, MaxNoteLength, required: false, allowMultiline: true);
        errors.ThrowIfAny();
        await EnsureClassesExistAsync(classIds, cancellationToken);

        // Khi sửa được phép đặt hạn về quá khứ (giao diện cảnh báo).
        assignment.DueAtUtc = NormalizeDue(input.DueAt);
        assignment.Note = NullIfEmpty(note);
        assignment.AssignedToAll = all;
        var wanted = all ? new HashSet<int>() : classIds.ToHashSet();
        foreach (var link in assignment.Classes.Where(c => !wanted.Contains(c.ClassId)).ToList())
        {
            assignment.Classes.Remove(link);
        }

        foreach (var classId in wanted.Where(c => assignment.Classes.All(l => l.ClassId != c)))
        {
            assignment.Classes.Add(new AssignmentClass { AssignmentId = assignment.Id, ClassId = classId });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var rows = await _assignments.ListByIdsAsync(new[] { id }, cancellationToken);
        return ToDto(rows.First());
    }

    /// <summary>Đã có bài nộp thì hủy mềm (giữ dữ liệu); chưa có thì xóa hẳn.</summary>
    public async Task DeleteAsync(CurrentUser user, int id, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var assignment = await _assignments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lượt giao bài.");

        if (await _assignments.HasSubmissionsAsync(id, cancellationToken))
        {
            assignment.CancelledUtc ??= _clock.UtcNow;
        }
        else
        {
            _assignments.Remove(assignment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static (bool All, List<int> ClassIds) ReadTarget(AssignmentTargetDto? target, FieldErrors errors)
    {
        var all = target?.All ?? false;
        var classIds = (target?.ClassIds ?? Array.Empty<int>()).Distinct().ToList();
        if (!all && classIds.Count == 0)
        {
            errors.Add("target", "Hãy chọn \"Tất cả học sinh\" hoặc ít nhất một lớp.");
        }

        return (all, all ? new List<int>() : classIds);
    }

    private async Task EnsureClassesExistAsync(List<int> classIds, CancellationToken cancellationToken)
    {
        if (classIds.Count == 0)
        {
            return;
        }

        var found = await _classes.GetExistingAsync(classIds, cancellationToken);
        if (found.Count != classIds.Count)
        {
            throw new ValidationFailedException("target", "Có lớp không tồn tại.");
        }
    }

    private static DateTime? NormalizeDue(DateTime? due) => due is null
        ? null
        : due.Value.Kind == DateTimeKind.Local ? due.Value.ToUniversalTime() : DateTime.SpecifyKind(due.Value, DateTimeKind.Utc);

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static bool SameTarget(Assignment a, bool all, List<int> classIds)
    {
        if (a.AssignedToAll != all)
        {
            return false;
        }

        return all || a.Classes.Select(c => c.ClassId).OrderBy(x => x).SequenceEqual(classIds.OrderBy(x => x));
    }

    private static AssignmentDto ToDto(AssignmentListRow row)
    {
        var a = row.Assignment;
        return new AssignmentDto(
            a.Id,
            a.ExerciseId,
            row.ExerciseTitle,
            row.LessonId,
            row.LessonTitle,
            a.AssignedToAll,
            row.Classes.Select(c => c.Id).ToList(),
            row.Classes.Select(c => c.Name).ToList(),
            a.DueAtUtc,
            a.Note,
            a.CreatedUtc,
            a.CancelledUtc,
            row.AssignedStudents,
            row.SubmittedStudents,
            row.PassedStudents);
    }
}

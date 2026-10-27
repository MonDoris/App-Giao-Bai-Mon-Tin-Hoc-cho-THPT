using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases.Progress;

/// <summary>Theo dõi tiến độ (AC-141, 142). Kết quả do trình duyệt học sinh chấm nên chưa được xác minh phía server (QD-32).</summary>
public sealed class ProgressUseCase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;
    public const string NotAssigned = "NotAssigned";

    private readonly IProgressRepository _progress;
    private readonly IUserRepository _users;
    private readonly ILessonRepository _lessons;
    private readonly ISubmissionRepository _submissions;

    public ProgressUseCase(
        IProgressRepository progress, IUserRepository users, ILessonRepository lessons, ISubmissionRepository submissions)
    {
        _progress = progress;
        _users = users;
        _lessons = lessons;
        _submissions = submissions;
    }

    public async Task<ProgressDto> GetAsync(
        CurrentUser user, int? classId, string? lessonId, string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        if (!IdRules.IsValid(lessonId))
        {
            throw new NotFoundException("Không tìm thấy bài học.");
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
        var snapshot = await _progress.GetAsync(classId, lessonId!, search?.Trim(), sort == "passed" ? "passed" : "name", page, pageSize, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài học.");

        var exercises = snapshot.Exercises
            .OrderBy(e => e.Order)
            .Select(e => new ProgressExerciseDto(
                e.Id,
                e.Title,
                snapshot.Assignments.Where(a => a.ExerciseId == e.Id && a.DueAtUtc is not null).Min(a => a.DueAtUtc)))
            .ToList();

        var rows = snapshot.Students.Select(s =>
        {
            var classIdsOfStudent = s.Classes.Select(c => c.Id).ToHashSet();
            var cells = snapshot.Exercises.OrderBy(e => e.Order).Select(e =>
            {
                var assigned = snapshot.Assignments.Any(a =>
                    a.ExerciseId == e.Id && (a.AssignedToAll || a.ClassIds.Any(classIdsOfStudent.Contains)));
                snapshot.Stats.TryGetValue((s.Id, e.Id), out var stats);
                snapshot.TestCounts.TryGetValue(e.Id, out var total);
                var status = assigned ? AssignmentRules.StatusOf(stats) : NotAssigned;
                return new ProgressCellDto(
                    e.Id,
                    status,
                    stats?.BestPassedTests ?? 0,
                    total,
                    stats?.Attempts ?? 0,
                    stats?.LastSubmittedUtc,
                    (stats?.AnyPassed ?? false) && !(stats?.AnyPassedOnTime ?? false),
                    snapshot.ViewedSolutions.Contains((s.Id, e.Id)));
            }).ToList();
            return new ProgressRowDto(s.Id, s.Username, s.DisplayName, string.Join(", ", s.Classes.Select(c => c.Name)), cells);
        }).ToList();

        return new ProgressDto(exercises, rows, snapshot.TotalStudents);
    }

    /// <summary>Các lần nộp (kèm mã nguồn đầy đủ) của một học sinh cho một bài tập, cũ nhất trước.</summary>
    public async Task<SubmissionDetailListDto> GetStudentSubmissionsAsync(
        CurrentUser user, int studentId, string? exerciseId, CancellationToken cancellationToken = default)
    {
        user.RequireTeacher();
        var student = await _users.GetByIdAsync(studentId, cancellationToken);
        if (student is null || student.Role != Roles.Student || !IdRules.IsValid(exerciseId)
            || await _lessons.GetExerciseAsync(exerciseId!, cancellationToken) is null)
        {
            throw new NotFoundException("Không tìm thấy học sinh hoặc bài tập.");
        }

        var all = await _submissions.GetByStudentExerciseAsync(studentId, exerciseId!, cancellationToken);
        return new SubmissionDetailListDto(all.Select((s, i) => SubmissionMapper.ToDetail(s, i + 1)).ToList());
    }
}

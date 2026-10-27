using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases.Student;

/// <summary>Danh sách mọi bài tập được giao cho học sinh đang đăng nhập (AC-131), sắp theo hạn nộp tăng dần (không hạn ở cuối).</summary>
public sealed class GetMyAssignmentsUseCase
{
    private readonly ILessonRepository _lessons;
    private readonly IAssignmentRepository _assignments;
    private readonly ISubmissionRepository _submissions;
    private readonly IClock _clock;

    public GetMyAssignmentsUseCase(
        ILessonRepository lessons, IAssignmentRepository assignments, ISubmissionRepository submissions, IClock clock)
    {
        _lessons = lessons;
        _assignments = assignments;
        _submissions = submissions;
        _clock = clock;
    }

    public async Task<MyAssignmentListDto> ExecuteAsync(CurrentUser user, CancellationToken cancellationToken = default)
    {
        user.RequireStudent();
        var rows = await _assignments.GetVisibleForStudentAsync(user.Id, cancellationToken);
        var effective = AssignmentRules.PickEffective(rows).Values.ToList();
        var stats = await _submissions.GetStatsAsync(user.Id, cancellationToken);
        var viewed = await _submissions.GetViewedSolutionExerciseIdsAsync(user.Id, cancellationToken);
        var testCounts = await _lessons.GetTestCountsAsync(effective.Select(r => r.ExerciseId), cancellationToken);
        var now = _clock.UtcNow;

        var items = effective
            .OrderBy(r => r.DueAtUtc is null)
            .ThenBy(r => r.DueAtUtc)
            .ThenBy(r => r.LessonOrder)
            .ThenBy(r => r.ExerciseOrder)
            .Select(r =>
            {
                stats.TryGetValue(r.ExerciseId, out var s);
                testCounts.TryGetValue(r.ExerciseId, out var total);
                var info = AssignmentRules.BuildInfo(r, s, viewed.Contains(r.ExerciseId), total, now);
                return new MyAssignmentDto(
                    r.AssignmentId,
                    r.ExerciseId,
                    r.ExerciseTitle,
                    r.LessonId,
                    r.LessonTitle,
                    r.Difficulty.ToLowerInvariant(),
                    r.DueAtUtc,
                    r.Note,
                    info.Status,
                    info.IsOverdue,
                    info.BestPassedTests,
                    info.TotalTests,
                    info.Attempts,
                    s?.LastSubmittedUtc,
                    info.PassedLate,
                    info.HasViewedSolution);
            })
            .ToList();
        return new MyAssignmentListDto(items);
    }
}

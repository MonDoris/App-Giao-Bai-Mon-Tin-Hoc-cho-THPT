using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases.Lessons;

/// <summary>
/// Lấy danh sách bài học (trang chủ). Học sinh chỉ thấy bài đã xuất bản và số bài tập được giao (QD-26);
/// giáo viên thấy mọi bài kèm cờ xuất bản/lưu trữ.
/// </summary>
public sealed class GetLessonsUseCase
{
    private readonly ILessonRepository _lessons;
    private readonly IAssignmentRepository _assignments;
    private readonly ISubmissionRepository _submissions;

    public GetLessonsUseCase(ILessonRepository lessons, IAssignmentRepository assignments, ISubmissionRepository submissions)
    {
        _lessons = lessons;
        _assignments = assignments;
        _submissions = submissions;
    }

    public async Task<LessonListDto> ExecuteAsync(CurrentUser user, CancellationToken cancellationToken = default)
    {
        var headers = (await _lessons.GetLessonHeadersAsync(cancellationToken)).OrderBy(h => h.Order).ToList();

        if (user.IsTeacher)
        {
            return new LessonListDto(headers
                .Select(h => new LessonSummaryDto(h.Id, h.Order, h.Title, h.Description, h.ExerciseCount, 0, h.IsPublished, h.IsArchived))
                .ToList());
        }

        var rows = await _assignments.GetVisibleForStudentAsync(user.Id, cancellationToken);
        var effective = AssignmentRules.PickEffective(rows).Values.ToList();
        var stats = await _submissions.GetStatsAsync(user.Id, cancellationToken);

        var items = headers
            .Where(h => h.IsPublished && !h.IsArchived)
            .Select(h =>
            {
                var mine = effective.Where(r => r.LessonId == h.Id).ToList();
                var passed = mine.Count(r => stats.TryGetValue(r.ExerciseId, out var s) && s.AnyPassed);
                return new LessonSummaryDto(h.Id, h.Order, h.Title, h.Description, mine.Count, passed);
            })
            .ToList();
        return new LessonListDto(items);
    }
}

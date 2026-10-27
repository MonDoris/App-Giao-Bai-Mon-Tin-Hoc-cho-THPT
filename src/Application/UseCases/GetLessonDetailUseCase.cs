using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases;

/// <summary>Lấy chi tiết một bài học (không bao gồm đáp án), lọc theo quyền của người gọi.</summary>
public sealed class GetLessonDetailUseCase
{
    private readonly ILessonRepository _lessons;
    private readonly IAssignmentRepository _assignments;
    private readonly ISubmissionRepository _submissions;
    private readonly IClock _clock;

    public GetLessonDetailUseCase(
        ILessonRepository lessons, IAssignmentRepository assignments, ISubmissionRepository submissions, IClock clock)
    {
        _lessons = lessons;
        _assignments = assignments;
        _submissions = submissions;
        _clock = clock;
    }

    /// <exception cref="NotFoundException">ID sai định dạng, không tồn tại, hoặc (với học sinh) bài chưa xuất bản/đã lưu trữ.</exception>
    /// <exception cref="DataInvalidException">Dữ liệu bài học trong DB bị lỗi.</exception>
    public async Task<LessonDetailDto> ExecuteAsync(CurrentUser user, string? id, CancellationToken cancellationToken = default)
    {
        if (!IdRules.IsValid(id))
        {
            throw new NotFoundException("Không tìm thấy bài học.");
        }

        var lesson = await _lessons.GetLessonWithExercisesAsync(id!, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài học.");

        if (user.IsTeacher)
        {
            return LessonMapper.ToDetail(lesson, withFlags: true);
        }

        if (!lesson.IsPublished || lesson.IsArchived)
        {
            throw new NotFoundException("Không tìm thấy bài học.");
        }

        var rows = await _assignments.GetVisibleForStudentAsync(user.Id, cancellationToken);
        var effective = AssignmentRules.PickEffective(rows.Where(r => r.LessonId == lesson.Id));
        var stats = await _submissions.GetStatsAsync(user.Id, cancellationToken);
        var viewed = await _submissions.GetViewedSolutionExerciseIdsAsync(user.Id, cancellationToken);
        var now = _clock.UtcNow;

        return LessonMapper.ToDetail(
            lesson,
            include: e => e.IsPublished && !e.IsArchived && effective.ContainsKey(e.Id),
            assignmentOf: e =>
            {
                stats.TryGetValue(e.Id, out var s);
                return AssignmentRules.BuildInfo(effective[e.Id], s, viewed.Contains(e.Id), e.TestCases.Count, now);
            });
    }
}

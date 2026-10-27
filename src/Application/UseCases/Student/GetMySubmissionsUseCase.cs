using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases.Student;

/// <summary>Lịch sử nộp của chính học sinh đang đăng nhập (AC-138). Bài nộp của người khác luôn trả 404 (chống IDOR).</summary>
public sealed class GetMySubmissionsUseCase
{
    private const int MaxItems = 50;

    private readonly ISubmissionRepository _submissions;

    public GetMySubmissionsUseCase(ISubmissionRepository submissions)
    {
        _submissions = submissions;
    }

    public async Task<SubmissionListDto> ListAsync(CurrentUser user, string? exerciseId, CancellationToken cancellationToken = default)
    {
        user.RequireStudent();
        if (!IdRules.IsValid(exerciseId))
        {
            return new SubmissionListDto(Array.Empty<SubmissionSummaryDto>());
        }

        var all = await _submissions.GetByStudentExerciseAsync(user.Id, exerciseId!, cancellationToken);
        var items = all
            .Select((s, i) => new SubmissionSummaryDto(s.Id, i + 1, s.PassedTests, s.TotalTests, s.IsPassed, s.IsLate, s.SubmittedUtc))
            .Reverse()
            .Take(MaxItems)
            .ToList();
        return new SubmissionListDto(items);
    }

    public async Task<SubmissionDetailDto> GetAsync(CurrentUser user, long id, CancellationToken cancellationToken = default)
    {
        user.RequireStudent();
        var submission = await _submissions.GetOwnedAsync(id, user.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài nộp.");
        var all = await _submissions.GetByStudentExerciseAsync(user.Id, submission.ExerciseId, cancellationToken);
        var attemptNo = all.ToList().FindIndex(s => s.Id == submission.Id) + 1;
        return SubmissionMapper.ToDetail(submission, attemptNo);
    }
}

using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases;

/// <summary>
/// Lấy đáp án của một bài tập (chỉ gọi sau khi học sinh xác nhận trên giao diện). Học sinh phải được giao bài tập đó (AC-129);
/// lần xem đầu tiên được ghi nhận để giáo viên thấy (AC-140). Giáo viên xem được mọi đáp án và không bị ghi nhận.
/// </summary>
public sealed class GetSolutionUseCase
{
    private readonly ILessonRepository _lessons;
    private readonly IAssignmentRepository _assignments;
    private readonly ISubmissionRepository _submissions;
    private readonly IClock _clock;

    public GetSolutionUseCase(
        ILessonRepository lessons, IAssignmentRepository assignments, ISubmissionRepository submissions, IClock clock)
    {
        _lessons = lessons;
        _assignments = assignments;
        _submissions = submissions;
        _clock = clock;
    }

    /// <exception cref="NotFoundException">ID sai, không tồn tại, không được giao hoặc bài tập không có đáp án.</exception>
    public async Task<SolutionDto> ExecuteAsync(CurrentUser user, string? exerciseId, CancellationToken cancellationToken = default)
    {
        if (!IdRules.IsValid(exerciseId))
        {
            throw new NotFoundException("Không tìm thấy đáp án.");
        }

        var exercise = await _lessons.GetExerciseAsync(exerciseId!, cancellationToken);
        if (exercise is null)
        {
            throw new NotFoundException("Không tìm thấy đáp án.");
        }

        if (user.IsStudent)
        {
            var rows = await _assignments.GetVisibleForStudentAsync(user.Id, cancellationToken);
            if (rows.All(r => r.ExerciseId != exercise.Id))
            {
                throw new NotFoundException("Không tìm thấy đáp án.");
            }
        }

        if (!exercise.HasSolution)
        {
            throw new NotFoundException("Không tìm thấy đáp án.");
        }

        if (user.IsStudent)
        {
            await _submissions.RecordSolutionViewAsync(user.Id, exercise.Id, _clock.UtcNow, cancellationToken);
        }

        return new SolutionDto(exercise.Id, exercise.SolutionCode!, exercise.SolutionExplanation);
    }
}

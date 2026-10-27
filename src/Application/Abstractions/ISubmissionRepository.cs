using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Abstractions;

/// <summary>Thống kê nộp bài của một học sinh cho một bài tập.</summary>
public sealed record ExerciseStats(
    string ExerciseId,
    int Attempts,
    int BestPassedTests,
    bool AnyPassed,
    bool AnyPassedOnTime,
    DateTime? LastSubmittedUtc);

public interface ISubmissionRepository
{
    void Add(Submission submission);

    /// <summary>Thống kê theo bài tập của một học sinh (mọi bài tập đã có bài nộp).</summary>
    Task<IReadOnlyDictionary<string, ExerciseStats>> GetStatsAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetViewedSolutionExerciseIdsAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Ghi nhận lần xem đáp án đầu tiên (idempotent, tự lưu).</summary>
    Task RecordSolutionViewAsync(int studentId, string exerciseId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Các lần nộp của một học sinh cho một bài tập, sắp theo thời gian tăng dần (không theo dõi).</summary>
    Task<IReadOnlyList<Submission>> GetByStudentExerciseAsync(int studentId, string exerciseId, CancellationToken cancellationToken = default);

    /// <summary>Một bài nộp theo Id nếu thuộc đúng học sinh này; ngược lại null.</summary>
    Task<Submission?> GetOwnedAsync(long id, int studentId, CancellationToken cancellationToken = default);

    Task<int> CountAttemptsAsync(int studentId, string exerciseId, CancellationToken cancellationToken = default);
}

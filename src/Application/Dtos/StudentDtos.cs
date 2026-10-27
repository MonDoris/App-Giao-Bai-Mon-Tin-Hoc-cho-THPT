namespace TinHoc12.Application.Dtos;

public sealed record MyAssignmentDto(
    int AssignmentId,
    string ExerciseId,
    string ExerciseTitle,
    string LessonId,
    string LessonTitle,
    string Difficulty,
    DateTime? DueAt,
    string? Note,
    string Status,
    bool IsOverdue,
    int BestPassedTests,
    int TotalTests,
    int Attempts,
    DateTime? LastSubmittedAt,
    bool PassedLate,
    bool HasViewedSolution);

public sealed record MyAssignmentListDto(IReadOnlyList<MyAssignmentDto> Assignments);

public sealed record TestResultInputDto(string? Status, int? TimeMs);

public sealed record SubmitRequest(string? Code, int? PassedTests, int? TotalTests, IReadOnlyList<TestResultInputDto>? Results);

public sealed record SubmitResultDto(long SubmissionId, int AttemptNo, bool IsPassed, bool IsLate, DateTime SubmittedAt, string Status);

public sealed record SubmissionSummaryDto(
    long Id,
    int AttemptNo,
    int PassedTests,
    int TotalTests,
    bool IsPassed,
    bool IsLate,
    DateTime SubmittedAt);

public sealed record SubmissionListDto(IReadOnlyList<SubmissionSummaryDto> Submissions);

public sealed record SubmissionDetailDto(
    long Id,
    int AttemptNo,
    string Code,
    int PassedTests,
    int TotalTests,
    bool IsPassed,
    bool IsLate,
    DateTime SubmittedAt,
    IReadOnlyList<TestResultInputDto>? Results);

public sealed record SubmissionDetailListDto(IReadOnlyList<SubmissionDetailDto> Submissions);

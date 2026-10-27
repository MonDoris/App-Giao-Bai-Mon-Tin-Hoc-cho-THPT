namespace TinHoc12.Application.Dtos;

// ---------------------------------------------------------------- Soạn bài
public sealed record TeacherLessonSummaryDto(
    string Id,
    int Order,
    string Title,
    string Description,
    bool IsPublished,
    bool IsArchived,
    int ExerciseCount,
    int PublishedExerciseCount,
    int AssignmentCount,
    int Version,
    DateTime? UpdatedAt,
    string? CreatedByName);

public sealed record TeacherLessonListDto(IReadOnlyList<TeacherLessonSummaryDto> Lessons);

public sealed record LessonInputDto(
    string? Id,
    string? Title,
    string? Description,
    IReadOnlyList<string>? Summary,
    IReadOnlyList<string>? Notes,
    IReadOnlyList<string>? Conclusion,
    int? Version);

public sealed record ExerciseInputDto(
    string? Id,
    string? Title,
    string? Difficulty,
    string? Statement,
    string? InputFormat,
    string? OutputFormat,
    string? Constraints,
    IReadOnlyList<ExampleDto>? Examples,
    string? StarterCode,
    string? SolutionCode,
    string? SolutionExplanation,
    bool? ConfirmEveryTime,
    int? TimeLimitMs,
    IReadOnlyList<TestCaseDto>? TestCases,
    int? Version);

public sealed record TeacherExerciseDto(
    string Id,
    string LessonId,
    int Order,
    string Title,
    string Difficulty,
    string Statement,
    string InputFormat,
    string OutputFormat,
    string? Constraints,
    IReadOnlyList<ExampleDto> Examples,
    string StarterCode,
    string? SolutionCode,
    string? SolutionExplanation,
    bool ConfirmEveryTime,
    int TimeLimitMs,
    IReadOnlyList<TestCaseDto> TestCases,
    bool IsPublished,
    bool IsArchived,
    int Version,
    DateTime? UpdatedAt,
    int AssignmentCount,
    int SubmissionCount,
    int AffectedStudents);

public sealed record TeacherLessonDto(
    string Id,
    int Order,
    string Title,
    string Description,
    IReadOnlyList<string> Summary,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Conclusion,
    bool IsPublished,
    bool IsArchived,
    int Version,
    DateTime? UpdatedAt,
    string? CreatedByName,
    int AssignmentCount,
    int SubmissionCount,
    int AffectedStudents,
    IReadOnlyList<TeacherExerciseDto> Exercises);

public sealed record PublishInputDto(bool IsPublished);

public sealed record ArchiveInputDto(bool IsArchived);

public sealed record ReorderInputDto(IReadOnlyList<string>? Ids);

// ---------------------------------------------------------------- Giao bài
public sealed record AssignmentTargetDto(bool All, IReadOnlyList<int>? ClassIds);

/// <summary>DueAt bắt buộc; StartAt bỏ trống = bắt đầu ngay lúc giao.</summary>
public sealed record AssignInputDto(
    IReadOnlyList<string>? ExerciseIds,
    AssignmentTargetDto? Target,
    DateTime? DueAt,
    string? Note,
    DateTime? StartAt = null);

public sealed record UpdateAssignmentInputDto(AssignmentTargetDto? Target, DateTime? DueAt, string? Note, DateTime? StartAt = null);

public sealed record AssignmentDto(
    int Id,
    string ExerciseId,
    string ExerciseTitle,
    string LessonId,
    string LessonTitle,
    bool AssignedToAll,
    IReadOnlyList<int> ClassIds,
    IReadOnlyList<string> ClassNames,
    DateTime? DueAt,
    string? Note,
    DateTime CreatedAt,
    DateTime? CancelledAt,
    int AssignedStudents,
    int SubmittedStudents,
    int PassedStudents,
    DateTime? StartAt);

public sealed record AssignmentListDto(IReadOnlyList<AssignmentDto> Assignments);

public sealed record AssignResultDto(IReadOnlyList<AssignmentDto> Assignments, int StudentCount);

// ---------------------------------------------------------------- Học sinh & lớp
public sealed record StudentDto(
    int Id,
    string Username,
    string DisplayName,
    IReadOnlyList<int> ClassIds,
    IReadOnlyList<string> ClassNames,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    DateTime? LastLoginAt);

public sealed record StudentListDto(IReadOnlyList<StudentDto> Students, int Total, int Page, int PageSize);

public sealed record CreateStudentRequest(string? Username, string? DisplayName, IReadOnlyList<int>? ClassIds, string? Password);

public sealed record CreateStudentResultDto(
    int Id,
    string Username,
    string DisplayName,
    IReadOnlyList<int> ClassIds,
    string? TemporaryPassword);

public sealed record ImportRowDto(string? Username, string? DisplayName);

public sealed record ImportRequest(int? ClassId, IReadOnlyList<ImportRowDto>? Rows);

public sealed record ImportRowResultDto(int Row, string Status, string? Reason, string Username, string? TemporaryPassword);

public sealed record ImportResultDto(IReadOnlyList<ImportRowResultDto> Results);

public sealed record UpdateStudentRequest(bool? IsActive, string? DisplayName, IReadOnlyList<int>? ClassIds);

public sealed record ResetPasswordResultDto(string TemporaryPassword);

public sealed record ClassDto(int Id, string Name, int MemberCount);

public sealed record ClassListDto(IReadOnlyList<ClassDto> Classes);

public sealed record ClassInputDto(string? Name);

// ---------------------------------------------------------------- Tiến độ
public sealed record ProgressExerciseDto(string Id, string Title, DateTime? DueAt);

public sealed record ProgressCellDto(
    string ExerciseId,
    string Status,
    int BestPassedTests,
    int TotalTests,
    int Attempts,
    DateTime? LastSubmittedAt,
    bool PassedLate,
    bool HasViewedSolution);

public sealed record ProgressRowDto(
    int StudentId,
    string Username,
    string DisplayName,
    string ClassName,
    IReadOnlyList<ProgressCellDto> Cells);

public sealed record ProgressDto(
    IReadOnlyList<ProgressExerciseDto> Exercises,
    IReadOnlyList<ProgressRowDto> Rows,
    int Total);

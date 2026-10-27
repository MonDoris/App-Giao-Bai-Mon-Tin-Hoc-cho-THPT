using System.Text.Json.Serialization;

namespace TinHoc12.Application.Dtos;

/// <summary>
/// Dòng danh sách bài học. Học sinh: exerciseCount = số bài tập được giao, passedCount = số bài đã đạt.
/// Giáo viên: có thêm isPublished/isArchived (học sinh không có hai trường này).
/// </summary>
public sealed record LessonSummaryDto(
    string Id,
    int Order,
    string Title,
    string Description,
    int ExerciseCount,
    int PassedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsPublished = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsArchived = null);

public sealed record LessonListDto(IReadOnlyList<LessonSummaryDto> Lessons);

public sealed record ExampleDto(string Input, string Output, string? Explanation);

public sealed record TestCaseDto(string Input, string ExpectedOutput, bool IsHidden);

/// <summary>Thông tin giao bài và tiến độ của một bài tập đối với một học sinh (chỉ có trong phản hồi cho học sinh).</summary>
public sealed record AssignmentInfoDto(
    int AssignmentId,
    DateTime? DueAt,
    string? Note,
    string Status,
    bool IsOverdue,
    int Attempts,
    int BestPassedTests,
    int TotalTests,
    bool HasViewedSolution,
    bool PassedLate);

public sealed record ExerciseDto(
    string Id,
    int Order,
    string Title,
    string Difficulty,
    string Statement,
    string InputFormat,
    string OutputFormat,
    string? Constraints,
    IReadOnlyList<ExampleDto> Examples,
    string StarterCode,
    bool HasSolution,
    bool ConfirmEveryTime,
    int TimeLimitMs,
    IReadOnlyList<TestCaseDto> TestCases,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AssignmentInfoDto? Assignment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsPublished = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsArchived = null);

public sealed record LessonDetailDto(
    string Id,
    string Title,
    IReadOnlyList<string> Summary,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Conclusion,
    IReadOnlyList<ExerciseDto> Exercises,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsPublished = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsArchived = null);

public sealed record SolutionDto(string ExerciseId, string Code, string? Explanation);

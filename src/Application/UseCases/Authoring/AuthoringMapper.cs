using System.Text.Encodings.Web;
using System.Text.Json;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Authoring;

/// <summary>Chuyển entity sang DTO soạn bài (đọc dung sai: dữ liệu SQL cũ lỗi vẫn mở được để giáo viên sửa) và ngược lại.</summary>
internal static class AuthoringMapper
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    // Lưu tiếng Việt nguyên dạng (không \uXXXX) để dễ đọc khi xem bằng SQL; dữ liệu không nhúng vào HTML.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, WriteOptions);

    public static IReadOnlyList<string> ReadStrings(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json ?? "[]", ReadOptions)?.Where(s => s is not null).ToList()
                ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public static IReadOnlyList<ExampleDto> ReadExamples(string? json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<List<ExampleDto>>(json ?? "[]", ReadOptions) ?? new List<ExampleDto>();
            return raw.Where(r => r is not null).Select(r => new ExampleDto(r.Input ?? string.Empty, r.Output ?? string.Empty, r.Explanation)).ToList();
        }
        catch (JsonException)
        {
            return new List<ExampleDto>();
        }
    }

    public static TeacherExerciseDto ToDto(Exercise e, Dependencies dependencies, int affectedStudents) => new(
        e.Id,
        e.LessonId,
        e.Order,
        e.Title,
        e.Difficulty,
        e.Statement,
        e.InputFormat,
        e.OutputFormat,
        e.Constraints,
        ReadExamples(e.ExamplesJson),
        e.StarterCode ?? string.Empty,
        e.SolutionCode,
        e.SolutionExplanation,
        e.ConfirmEveryTime,
        e.TimeLimitMs,
        e.TestCases.OrderBy(t => t.Order).Select(t => new TestCaseDto(t.Input, t.ExpectedOutput, t.IsHidden)).ToList(),
        e.IsPublished,
        e.IsArchived,
        e.Version,
        e.UpdatedUtc,
        dependencies.Assignments,
        dependencies.Submissions,
        affectedStudents);

    public static TeacherLessonDto ToDto(
        Lesson l, Dependencies dependencies, int affectedStudents, IReadOnlyList<TeacherExerciseDto> exercises) => new(
        l.Id,
        l.Order,
        l.Title,
        l.Description,
        ReadStrings(l.SummaryJson),
        ReadStrings(l.NotesJson),
        ReadStrings(l.ConclusionJson),
        l.IsPublished,
        l.IsArchived,
        l.Version,
        l.UpdatedUtc,
        l.CreatedBy?.DisplayName,
        dependencies.Assignments,
        dependencies.Submissions,
        affectedStudents,
        exercises);
}

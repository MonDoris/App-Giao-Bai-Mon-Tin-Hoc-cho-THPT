using System.Text.Json;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Mapping;

/// <summary>Chuyển entity sang DTO, đồng thời kiểm tra tính hợp lệ dữ liệu của một bài (AC-42).</summary>
public static class LessonMapper
{
    private static readonly Dictionary<string, string> Difficulties = new(StringComparer.Ordinal)
    {
        ["Easy"] = "easy",
        ["Medium"] = "medium",
        ["Hard"] = "hard",
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// include: chọn bài tập nào được hiển thị (null = tất cả). assignmentOf: thông tin giao bài cho học sinh (null = không có).
    /// withFlags: thêm cờ xuất bản/lưu trữ (dùng cho giáo viên). Chỉ các bài tập được hiển thị mới bị kiểm tra dữ liệu.
    /// </summary>
    public static LessonDetailDto ToDetail(
        Lesson lesson,
        Func<Exercise, bool>? include = null,
        Func<Exercise, AssignmentInfoDto?>? assignmentOf = null,
        bool withFlags = false)
    {
        Require(lesson.Title, "Title");
        var summary = ParseStrings(lesson.SummaryJson, "Summary", minimum: 0);
        var notes = ParseStrings(lesson.NotesJson, "Notes", minimum: 1);
        var conclusion = ParseStrings(lesson.ConclusionJson, "Conclusion", minimum: 0);

        var exercises = lesson.Exercises
            .Where(e => include?.Invoke(e) ?? true)
            .OrderBy(e => e.Order)
            .Select(e => ToExercise(e, assignmentOf?.Invoke(e), withFlags))
            .ToList();

        return new LessonDetailDto(
            lesson.Id,
            lesson.Title,
            summary,
            notes,
            conclusion,
            exercises,
            withFlags ? lesson.IsPublished : null,
            withFlags ? lesson.IsArchived : null);
    }

    private static ExerciseDto ToExercise(Exercise e, AssignmentInfoDto? assignment, bool withFlags)
    {
        var prefix = $"Exercises[{e.Id}]";
        Require(e.Title, $"{prefix}.Title");
        Require(e.Statement, $"{prefix}.Statement");
        Require(e.InputFormat, $"{prefix}.InputFormat");
        Require(e.OutputFormat, $"{prefix}.OutputFormat");

        if (!Difficulties.TryGetValue(e.Difficulty ?? string.Empty, out var difficulty))
        {
            throw new DataInvalidException($"{prefix}.Difficulty", "Độ khó phải là Easy, Medium hoặc Hard.");
        }

        var examples = ParseExamples(e.ExamplesJson, $"{prefix}.Examples");

        var testCases = e.TestCases
            .OrderBy(t => t.Order)
            .Select(t => new TestCaseDto(t.Input ?? string.Empty, t.ExpectedOutput ?? string.Empty, t.IsHidden))
            .ToList();
        if (testCases.Count == 0)
        {
            throw new DataInvalidException($"{prefix}.TestCases", "Bài tập chưa có test case nào.");
        }

        return new ExerciseDto(
            e.Id,
            e.Order,
            e.Title,
            difficulty,
            e.Statement,
            e.InputFormat,
            e.OutputFormat,
            e.Constraints,
            examples,
            e.StarterCode ?? string.Empty,
            e.HasSolution,
            e.ConfirmEveryTime,
            e.TimeLimitMs > 0 ? e.TimeLimitMs : 5000,
            testCases,
            assignment,
            withFlags ? e.IsPublished : null,
            withFlags ? e.IsArchived : null);
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DataInvalidException(field, $"Thiếu dữ liệu bắt buộc: {field}.");
        }
    }

    private static IReadOnlyList<string> ParseStrings(string? json, string field, int minimum)
    {
        List<string>? list;
        try
        {
            list = JsonSerializer.Deserialize<List<string>>(json ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            throw new DataInvalidException(field, $"Cột {field} không phải mảng JSON hợp lệ.");
        }

        if (list is null || list.Any(s => s is null))
        {
            throw new DataInvalidException(field, $"Cột {field} không phải mảng JSON hợp lệ.");
        }

        if (list.Count < minimum)
        {
            throw new DataInvalidException(field, $"Cột {field} cần có ít nhất {minimum} mục.");
        }

        return list;
    }

    private static IReadOnlyList<ExampleDto> ParseExamples(string? json, string field)
    {
        List<ExampleRaw>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<ExampleRaw>>(json ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            throw new DataInvalidException(field, "Cột Examples không phải mảng JSON hợp lệ.");
        }

        if (raw is null || raw.Count == 0 || raw.Any(r => r is null || r.Input is null || r.Output is null))
        {
            throw new DataInvalidException(field, "Cột Examples cần có ít nhất 1 ví dụ với input và output.");
        }

        return raw.Select(r => new ExampleDto(r.Input!, r.Output!, r.Explanation)).ToList();
    }

    private sealed class ExampleRaw
    {
        public string? Input { get; set; }
        public string? Output { get; set; }
        public string? Explanation { get; set; }
    }
}

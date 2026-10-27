using System.Text.Json;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Lessons;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Tests;

internal static class UseCases
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static GetLessonsUseCase Lessons(World w) => new(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo);

    public static GetLessonDetailUseCase Detail(World w) => new(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock);

    public static GetSolutionUseCase Solution(World w) => new(w.LessonRepo, w.AssignmentRepo, w.SubmissionRepo, w.Clock);

    public static string Json(object value) => JsonSerializer.Serialize(value, Web);
}

public class GetLessonsUseCaseTests
{
    [Fact]
    public async Task Teacher_sees_all_lessons_sorted_by_order_with_flags()
    {
        var w = new World();
        var teacher = w.AddUser("gv", Roles.Teacher);
        w.AddLesson("bai-03", 3);
        w.AddLesson("bai-01", 1);
        w.AddLesson("bai-02", 2, published: false);

        var result = await UseCases.Lessons(w).ExecuteAsync(World.AsTeacher(teacher));

        Assert.Equal(new[] { "bai-01", "bai-02", "bai-03" }, result.Lessons.Select(l => l.Id));
        Assert.All(result.Lessons, l => Assert.Equal(2, l.ExerciseCount));
        Assert.Equal(new bool?[] { true, false, true }, result.Lessons.Select(l => l.IsPublished));
    }

    [Fact]
    public async Task Student_sees_only_published_lessons_with_assigned_exercise_counts()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        w.AddLesson("bai-01", 1);
        w.AddLesson("bai-02", 2, published: false);
        w.AddLesson("bai-03", 3, archived: true);
        w.Assign("bai-01-bt1", all: true);

        var result = await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(hs));

        var only = Assert.Single(result.Lessons);
        Assert.Equal("bai-01", only.Id);
        Assert.Equal(1, only.ExerciseCount);
        Assert.Equal(0, only.PassedCount);
        Assert.Null(only.IsPublished);
    }

    [Fact]
    public async Task Returns_empty_list_when_no_lessons()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        Assert.Empty((await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(hs))).Lessons);
    }

    [Fact]
    public async Task Student_list_json_has_exactly_the_documented_fields_and_teacher_adds_flags()
    {
        var w = new World();
        var hs = w.AddUser("hs01");
        var gv = w.AddUser("gv", Roles.Teacher);
        w.AddLesson();

        using var student = JsonDocument.Parse(UseCases.Json(await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(hs))));
        var names = student.RootElement.GetProperty("lessons")[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "description", "exerciseCount", "id", "order", "passedCount", "title" }, names);

        using var teacher = JsonDocument.Parse(UseCases.Json(await UseCases.Lessons(w).ExecuteAsync(World.AsTeacher(gv))));
        var tnames = teacher.RootElement.GetProperty("lessons")[0].EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Contains("isPublished", tnames);
        Assert.Contains("isArchived", tnames);
    }

    [Fact]
    public async Task Passed_count_comes_from_submissions_of_this_student_only()
    {
        var w = new World();
        var a = w.AddUser("hs01");
        var b = w.AddUser("hs02");
        w.AddLesson();
        var asg = w.Assign("bai-01-bt1", all: true);
        w.Submissions.Add(new Submission { Id = 1, AssignmentId = asg.Id, ExerciseId = "bai-01-bt1", StudentId = a.Id, PassedTests = 2, TotalTests = 2, IsPassed = true, SubmittedUtc = w.Clock.UtcNow });

        Assert.Equal(1, (await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(a))).Lessons[0].PassedCount);
        Assert.Equal(0, (await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(b))).Lessons[0].PassedCount);
    }
}

public class GetLessonDetailUseCaseTests
{
    private static (World W, User Teacher, User Student) Create()
    {
        var w = new World();
        var teacher = w.AddUser("gv", Roles.Teacher);
        var student = w.AddUser("hs01");
        w.AddLesson();
        w.Assign("bai-01-bt1", all: true);
        w.Assign("bai-01-bt2", all: true);
        return (w, teacher, student);
    }

    [Fact]
    public async Task Returns_detail_with_sorted_exercises_and_test_cases()
    {
        var (w, _, hs) = Create();
        w.Lessons[0].Exercises.First(e => e.Order == 2).Order = 1;
        w.Lessons[0].Exercises.First(e => e.Id == "bai-01-bt1").Order = 2;

        var dto = await UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01");

        Assert.Equal("bai-01", dto.Id);
        Assert.Equal(new[] { "Tóm tắt" }, dto.Summary);
        Assert.Equal(new[] { "Lưu ý 1" }, dto.Notes);
        Assert.Equal(new[] { 1, 2 }, dto.Exercises.Select(e => e.Order));
        var first = dto.Exercises[1];
        Assert.Equal("medium", first.Difficulty);
        Assert.True(first.HasSolution);
        Assert.False(dto.Exercises[0].HasSolution);
        Assert.Equal(new[] { "1", "2" }, first.TestCases.Select(t => t.Input));
        Assert.Equal(new[] { false, true }, first.TestCases.Select(t => t.IsHidden));
        Assert.Equal("x", first.Examples[0].Explanation);
        Assert.Equal(5000, first.TimeLimitMs);
        Assert.NotNull(first.Assignment);
        Assert.Equal("NotStarted", first.Assignment!.Status);
    }

    [Fact]
    public async Task Detail_json_never_contains_solution_content_for_either_role()
    {
        var (w, gv, hs) = Create();
        foreach (var user in new[] { World.AsStudent(hs), World.AsTeacher(gv) })
        {
            var json = UseCases.Json(await UseCases.Detail(w).ExecuteAsync(user, "bai-01"));
            Assert.DoesNotContain("print(1)", json);
            Assert.DoesNotContain("Giải thích", json);
            Assert.DoesNotContain("\"solution", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Student_sees_only_assigned_exercises_and_gets_404_for_unpublished_lesson()
    {
        var (w, gv, hs) = Create();
        w.Assignments.RemoveAll(a => a.ExerciseId == "bai-01-bt2");
        var dto = await UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01");
        Assert.Equal(new[] { "bai-01-bt1" }, dto.Exercises.Select(e => e.Id));

        w.AddLesson("bai-02", 2, published: false);
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-02"));
        var draft = await UseCases.Detail(w).ExecuteAsync(World.AsTeacher(gv), "bai-02");
        Assert.False(draft.IsPublished);
        Assert.Equal(2, draft.Exercises.Count);
    }

    [Fact]
    public async Task Unpublished_or_archived_exercise_is_hidden_from_student_even_when_assigned()
    {
        var (w, _, hs) = Create();
        w.Lessons[0].Exercises.First(e => e.Id == "bai-01-bt1").IsPublished = false;
        w.Lessons[0].Exercises.First(e => e.Id == "bai-01-bt2").IsArchived = true;
        var dto = await UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01");
        Assert.Empty(dto.Exercises);
    }

    [Fact]
    public async Task Unknown_id_throws_not_found()
    {
        var (w, _, hs) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "khong-co"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("' OR 1=1--")]
    [InlineData("Bai-01")]
    [InlineData("bai 01")]
    [InlineData("../etc/passwd")]
    public async Task Malformed_id_throws_not_found(string? id)
    {
        var (w, _, hs) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), id));
    }

    [Fact]
    public async Task Id_longer_than_100_chars_throws_not_found_but_exactly_100_is_accepted()
    {
        var (w, _, hs) = Create();
        w.AddLesson(new string('a', 101), 2);
        w.AddLesson(new string('b', 100), 3);
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), new string('a', 101)));
        Assert.Equal(new string('b', 100), (await UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), new string('b', 100))).Id);
    }

    [Fact]
    public async Task Invalid_notes_json_throws_data_invalid_with_field()
    {
        var (w, _, hs) = Create();
        w.Lessons[0].NotesJson = "khong phai json";
        var ex = await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01"));
        Assert.Equal("Notes", ex.Field);
    }

    [Fact]
    public async Task Empty_notes_and_missing_title_throw_data_invalid()
    {
        var (w, _, hs) = Create();
        w.Lessons[0].NotesJson = "[]";
        var ex = await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01"));
        Assert.Equal("Notes", ex.Field);

        w.Lessons[0].NotesJson = "[\"x\"]";
        w.Lessons[0].Title = "  ";
        ex = await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01"));
        Assert.Equal("Title", ex.Field);
    }

    [Fact]
    public async Task Broken_exercise_data_throws_data_invalid()
    {
        var (w, _, hs) = Create();
        w.Lessons[0].Exercises.First().TestCases.Clear();
        var ex = await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01"));
        Assert.EndsWith(".TestCases", ex.Field);

        var w2 = new World();
        var hs2 = w2.AddUser("hs01");
        w2.AddLesson();
        w2.Assign("bai-01-bt1", all: true);
        w2.Lessons[0].Exercises.First().ExamplesJson = "{oops";
        ex = await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w2).ExecuteAsync(World.AsStudent(hs2), "bai-01"));
        Assert.EndsWith(".Examples", ex.Field);

        w2.Lessons[0].Exercises.First().ExamplesJson = "[{\"input\":\"1\",\"output\":\"1\"}]";
        w2.Lessons[0].Exercises.First().Difficulty = "Impossible";
        ex = await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w2).ExecuteAsync(World.AsStudent(hs2), "bai-01"));
        Assert.EndsWith(".Difficulty", ex.Field);
    }

    [Fact]
    public async Task One_broken_lesson_does_not_affect_others()
    {
        var (w, _, hs) = Create();
        var broken = w.AddLesson("bai-02", 2);
        broken.SummaryJson = "[1, 2";
        await Assert.ThrowsAsync<DataInvalidException>(() => UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-02"));
        Assert.Equal("bai-01", (await UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01")).Id);
        Assert.Equal(2, (await UseCases.Lessons(w).ExecuteAsync(World.AsStudent(hs))).Lessons.Count);
    }

    [Fact]
    public async Task Lesson_without_exercises_is_valid()
    {
        var (w, _, hs) = Create();
        w.Lessons[0].Exercises.Clear();
        Assert.Empty((await UseCases.Detail(w).ExecuteAsync(World.AsStudent(hs), "bai-01")).Exercises);
    }
}

public class GetSolutionUseCaseTests
{
    private static (World W, User Student, User Teacher) Create()
    {
        var w = new World();
        var student = w.AddUser("hs01");
        var teacher = w.AddUser("gv", Roles.Teacher);
        w.AddLesson();
        w.Assign("bai-01-bt1", all: true);
        w.Assign("bai-01-bt2", all: true);
        return (w, student, teacher);
    }

    [Fact]
    public async Task Returns_solution_and_records_first_view_once_for_students()
    {
        var (w, hs, _) = Create();
        var useCase = UseCases.Solution(w);
        var dto = await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1");
        await useCase.ExecuteAsync(World.AsStudent(hs), "bai-01-bt1");

        Assert.Equal("bai-01-bt1", dto.ExerciseId);
        Assert.Equal("print(1)", dto.Code);
        Assert.Equal("Giải thích", dto.Explanation);
        var view = Assert.Single(w.Views);
        Assert.Equal(hs.Id, view.StudentId);
    }

    [Fact]
    public async Task Teacher_gets_solution_of_unassigned_exercise_without_being_recorded()
    {
        var (w, _, gv) = Create();
        w.Assignments.Clear();
        var dto = await UseCases.Solution(w).ExecuteAsync(World.AsTeacher(gv), "bai-01-bt1");
        Assert.Equal("print(1)", dto.Code);
        Assert.Empty(w.Views);
    }

    [Fact]
    public async Task Student_gets_404_for_unassigned_exercise_and_for_exercise_without_solution()
    {
        var (w, hs, _) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Solution(w).ExecuteAsync(World.AsStudent(hs), "bai-01-bt2"));
        w.Assignments.RemoveAll(a => a.ExerciseId == "bai-01-bt1");
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Solution(w).ExecuteAsync(World.AsStudent(hs), "bai-01-bt1"));
        Assert.Empty(w.Views);
    }

    [Fact]
    public async Task Whitespace_only_solution_counts_as_no_solution()
    {
        var (w, hs, _) = Create();
        w.Lessons[0].Exercises.First(e => e.Id == "bai-01-bt1").SolutionCode = "   ";
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Solution(w).ExecuteAsync(World.AsStudent(hs), "bai-01-bt1"));
    }

    [Theory]
    [InlineData("khong-co")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("' OR 1=1--")]
    public async Task Unknown_or_malformed_id_throws_not_found(string? id)
    {
        var (w, hs, _) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Solution(w).ExecuteAsync(World.AsStudent(hs), id));
    }

    [Fact]
    public async Task Id_longer_than_100_chars_throws_not_found()
    {
        var (w, hs, _) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Solution(w).ExecuteAsync(World.AsStudent(hs), new string('a', 101)));
    }

    [Fact]
    public async Task Unpublished_exercise_solution_is_hidden_even_if_assigned()
    {
        var (w, hs, _) = Create();
        w.Lessons[0].Exercises.First(e => e.Id == "bai-01-bt1").IsPublished = false;
        await Assert.ThrowsAsync<NotFoundException>(() => UseCases.Solution(w).ExecuteAsync(World.AsStudent(hs), "bai-01-bt1"));
    }
}

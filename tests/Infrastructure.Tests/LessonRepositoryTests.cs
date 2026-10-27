using Microsoft.EntityFrameworkCore;
using TinHoc12.Infrastructure.Repositories;

namespace TinHoc12.Infrastructure.Tests;

public class LessonRepositoryTests
{
    [Fact]
    public async Task Headers_are_sorted_by_order_and_count_exercises()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.AddRange(TestDb.Lesson("b-3", 3, 1), TestDb.Lesson("b-1", 1, 2), TestDb.Lesson("b-2", 2, 0));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var headers = await new LessonRepository(ctx).GetLessonHeadersAsync();

        Assert.Equal(new[] { "b-1", "b-2", "b-3" }, headers.Select(h => h.Id));
        Assert.Equal(new[] { 2, 0, 1 }, headers.Select(h => h.ExerciseCount));
    }

    [Fact]
    public async Task Lesson_detail_returns_exercises_and_test_cases_sorted_with_hidden_flags()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            var lesson = TestDb.Lesson("b-1", 1);
            lesson.Exercises.Add(TestDb.Exercise("b-1-bt2", "b-1", 2));
            lesson.Exercises.Add(TestDb.Exercise("b-1-bt1", "b-1", 1));
            seed.Lessons.Add(lesson);
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var lesson2 = await new LessonRepository(ctx).GetLessonWithExercisesAsync("b-1");

        Assert.NotNull(lesson2);
        Assert.Equal(new[] { "b-1-bt1", "b-1-bt2" }, lesson2!.Exercises.Select(e => e.Id));
        var tests = lesson2.Exercises.First().TestCases.ToList();
        Assert.Equal(new[] { 1, 2, 3 }, tests.Select(t => t.Order));
        Assert.Equal(new[] { false, false, true }, tests.Select(t => t.IsHidden));
    }

    [Theory]
    [InlineData("khong-co")]
    [InlineData("")]
    [InlineData("' OR 1=1--")]
    public async Task Unknown_ids_return_null(string id)
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var repo = new LessonRepository(ctx);
        Assert.Null(await repo.GetLessonWithExercisesAsync(id));
        Assert.Null(await repo.GetExerciseAsync(id));
    }

    [Fact]
    public async Task Get_exercise_returns_solution_data()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var ex = await new LessonRepository(ctx).GetExerciseAsync("b-1-bt1");
        Assert.NotNull(ex);
        Assert.Equal("print(1)", ex!.SolutionCode);
    }

    [Fact]
    public async Task Deleting_lesson_cascades_to_exercises_and_test_cases()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.Add(TestDb.Lesson("b-1", 1, 2));
            await seed.SaveChangesAsync();
        }

        await using (var ctx = db.CreateContext())
        {
            var lesson = await ctx.Lessons.SingleAsync(l => l.Id == "b-1");
            ctx.Lessons.Remove(lesson);
            await ctx.SaveChangesAsync();
        }

        await using var check = db.CreateContext();
        Assert.Equal(0, await check.Lessons.CountAsync());
        Assert.Equal(0, await check.Exercises.CountAsync());
        Assert.Equal(0, await check.TestCases.CountAsync());
    }

    [Fact]
    public async Task Duplicate_lesson_order_is_rejected()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        ctx.Lessons.AddRange(TestDb.Lesson("b-1", 1), TestDb.Lesson("b-2", 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_exercise_order_within_lesson_is_rejected()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var lesson = TestDb.Lesson("b-1", 1);
        lesson.Exercises.Add(TestDb.Exercise("b-1-a", "b-1", 1));
        lesson.Exercises.Add(TestDb.Exercise("b-1-b", "b-1", 1));
        ctx.Lessons.Add(lesson);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Defaults_are_applied_for_flags_and_time_limit()
    {
        using var db = new TestDb();
        await using (var seed = db.CreateContext())
        {
            seed.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
            await seed.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        var ex = await ctx.Exercises.SingleAsync();
        Assert.False(ex.ConfirmEveryTime);
        Assert.Equal(5000, ex.TimeLimitMs);
    }
}

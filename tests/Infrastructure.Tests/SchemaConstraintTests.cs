using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Tests;

internal static class Data
{
    public static User User(string name, string role = "Student") => new()
    {
        Username = name,
        DisplayName = name,
        PasswordHash = "PBKDF2-SHA512$1$AAAA$BBBB",
        Role = role,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        CreatedUtc = DateTime.UtcNow,
    };

    public static async Task<(User Teacher, User Hs1, User Hs2)> BasicUsersAsync(AppDbContext ctx)
    {
        var teacher = User("giaovien", "Teacher");
        var a = User("hs01");
        var b = User("hs02");
        ctx.Users.AddRange(teacher, a, b);
        await ctx.SaveChangesAsync();
        return (teacher, a, b);
    }

    public static Assignment Assignment(string exerciseId, int createdBy, bool all = true, params int[] classIds)
    {
        var a = new Assignment { ExerciseId = exerciseId, AssignedToAll = all, CreatedByUserId = createdBy, CreatedUtc = DateTime.UtcNow };
        foreach (var c in classIds)
        {
            a.Classes.Add(new AssignmentClass { ClassId = c });
        }

        return a;
    }

    public static Submission Submission(int assignmentId, string exerciseId, int studentId, int passed, int total, bool late = false) => new()
    {
        AssignmentId = assignmentId,
        ExerciseId = exerciseId,
        StudentId = studentId,
        Code = "print(1)",
        PassedTests = passed,
        TotalTests = total,
        IsPassed = passed == total,
        IsLate = late,
        SubmittedUtc = DateTime.UtcNow,
    };
}

/// <summary>Kiểm thử ràng buộc schema bổ sung (AC-154) trên SQLite in-memory.</summary>
public class SchemaConstraintTests
{
    [Fact]
    public async Task Role_other_than_teacher_or_student_is_rejected_by_the_database()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        ctx.Users.Add(Data.User("quantri", "Admin"));
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_username_is_rejected_even_when_only_the_case_differs()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        ctx.Users.Add(Data.User("hs01"));
        await ctx.SaveChangesAsync();
        ctx.Users.Add(Data.User("HS01"));
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_class_name_is_rejected_ignoring_case()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        ctx.Classes.Add(new SchoolClass { Name = "12A1", CreatedUtc = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        ctx.Classes.Add(new SchoolClass { Name = "12a1", CreatedUtc = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Theory]
    [InlineData(6, 5)]
    [InlineData(-1, 5)]
    [InlineData(0, 0)]
    public async Task Invalid_submission_counts_are_rejected_by_the_database(int passed, int total)
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (teacher, hs, _) = await Data.BasicUsersAsync(ctx);
        ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
        await ctx.SaveChangesAsync();
        var a = Data.Assignment("b-1-bt1", teacher.Id);
        ctx.Assignments.Add(a);
        await ctx.SaveChangesAsync();

        ctx.Submissions.Add(Data.Submission(a.Id, "b-1-bt1", hs.Id, passed, total));
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Valid_submission_is_stored_and_exercise_with_assignments_cannot_be_deleted()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (teacher, hs, _) = await Data.BasicUsersAsync(ctx);
        ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
        await ctx.SaveChangesAsync();
        var a = Data.Assignment("b-1-bt1", teacher.Id);
        ctx.Assignments.Add(a);
        await ctx.SaveChangesAsync();
        ctx.Submissions.Add(Data.Submission(a.Id, "b-1-bt1", hs.Id, 3, 3));
        await ctx.SaveChangesAsync();

        await using var ctx2 = db.CreateContext();
        ctx2.Exercises.Remove(await ctx2.Exercises.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());

        await using var ctx3 = db.CreateContext();
        ctx3.Lessons.Remove(await ctx3.Lessons.Include(l => l.Exercises).SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx3.SaveChangesAsync());
    }

    [Fact]
    public async Task Lesson_without_assignments_still_cascades_but_submissions_and_assignments_use_restrict()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (teacher, _, _) = await Data.BasicUsersAsync(ctx);
        ctx.Lessons.AddRange(TestDb.Lesson("b-1", 1, 1), TestDb.Lesson("b-2", 2, 1));
        await ctx.SaveChangesAsync();
        ctx.Assignments.Add(Data.Assignment("b-2-bt1", teacher.Id));
        await ctx.SaveChangesAsync();

        await using var del = db.CreateContext();
        del.Lessons.Remove(await del.Lessons.SingleAsync(l => l.Id == "b-1"));
        await del.SaveChangesAsync();

        await using var check = db.CreateContext();
        Assert.Equal(1, await check.Lessons.CountAsync());
        Assert.Equal(1, await check.Exercises.CountAsync());
        Assert.Equal(3, await check.TestCases.CountAsync());
    }

    [Fact]
    public async Task Composite_keys_prevent_duplicate_memberships_and_solution_views()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (_, hs, _) = await Data.BasicUsersAsync(ctx);
        var c = new SchoolClass { Name = "12A1", CreatedUtc = DateTime.UtcNow };
        ctx.Classes.Add(c);
        ctx.Lessons.Add(TestDb.Lesson("b-1", 1, 1));
        await ctx.SaveChangesAsync();
        ctx.ClassMembers.Add(new ClassMember { ClassId = c.Id, StudentId = hs.Id });
        ctx.SolutionViews.Add(new SolutionView { StudentId = hs.Id, ExerciseId = "b-1-bt1", FirstViewedUtc = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        await using var again = db.CreateContext();
        again.ClassMembers.Add(new ClassMember { ClassId = c.Id, StudentId = hs.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
        await using var again2 = db.CreateContext();
        again2.SolutionViews.Add(new SolutionView { StudentId = hs.Id, ExerciseId = "b-1-bt1", FirstViewedUtc = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => again2.SaveChangesAsync());
    }

    [Fact]
    public async Task Draft_state_false_is_persisted_and_not_replaced_by_the_database_default()
    {
        using var db = new TestDb();
        await using (var ctx = db.CreateContext())
        {
            var draft = TestDb.Lesson("nhap", 1, 1);
            draft.IsPublished = false;
            draft.Exercises.First().IsPublished = false;
            ctx.Lessons.Add(draft);
            await ctx.SaveChangesAsync();
        }

        await using var check = db.CreateContext();
        Assert.False((await check.Lessons.SingleAsync()).IsPublished);
        Assert.False((await check.Exercises.SingleAsync()).IsPublished);
    }

    [Fact]
    public async Task Rows_inserted_by_old_sql_without_new_columns_get_published_defaults()
    {
        using var db = new TestDb();
        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO Lessons (Id, \"Order\", Title, Description, Summary, Notes, Conclusion) VALUES ('sql-cu', 5, 't', 'd', '[]', '[\"a\"]', '[]')";
            cmd.ExecuteNonQuery();
        }

        await using var ctx = db.CreateContext();
        var lesson = await ctx.Lessons.SingleAsync();
        Assert.True(lesson.IsPublished);
        Assert.False(lesson.IsArchived);
        Assert.Equal(1, lesson.Version);
        Assert.Null(lesson.CreatedByUserId);
    }

    [Fact]
    public async Task Deleting_a_user_sets_creator_columns_to_null_and_removes_memberships()
    {
        using var db = new TestDb();
        await using var ctx = db.CreateContext();
        var (teacher, hs, _) = await Data.BasicUsersAsync(ctx);
        var lesson = TestDb.Lesson("b-1", 1, 0);
        lesson.CreatedByUserId = teacher.Id;
        var c = new SchoolClass { Name = "12A1", CreatedUtc = DateTime.UtcNow };
        c.Members.Add(new ClassMember { StudentId = hs.Id });
        ctx.Lessons.Add(lesson);
        ctx.Classes.Add(c);
        await ctx.SaveChangesAsync();

        await using var del = db.CreateContext();
        del.Users.Remove(await del.Users.SingleAsync(u => u.Username == "giaovien"));
        del.Users.Remove(await del.Users.SingleAsync(u => u.Username == "hs01"));
        await del.SaveChangesAsync();

        await using var check = db.CreateContext();
        Assert.Null((await check.Lessons.SingleAsync()).CreatedByUserId);
        Assert.Equal(0, await check.ClassMembers.CountAsync());
    }

    [Fact]
    public async Task Whole_model_tables_exist_with_expected_names()
    {
        using var db = new TestDb();
        var tables = new List<string>();
        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                tables.Add(r.GetString(0));
            }
        }

        foreach (var t in new[] { "Lessons", "Exercises", "TestCases", "Users", "Classes", "ClassMembers", "Assignments", "AssignmentClasses", "Submissions", "SolutionViews" })
        {
            Assert.Contains(t, tables);
        }

        await Task.CompletedTask;
    }
}

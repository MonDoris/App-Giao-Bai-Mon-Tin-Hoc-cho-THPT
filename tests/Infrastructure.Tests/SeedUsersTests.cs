using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Infrastructure.Repositories;
using TinHoc12.Infrastructure.Security;

namespace TinHoc12.Infrastructure.Tests;

/// <summary>Kiểm thử tài khoản mẫu (AC-101, 102, 129): script idempotent, hash xác thực được bởi ứng dụng, giao bài đúng.</summary>
public class SeedUsersTests
{
    private static string Database => Path.Combine(Paths.RepoRoot(), "database");

    private static void Run(SqliteConnection connection, string file)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = File.ReadAllText(Path.Combine(Database, file));
        cmd.ExecuteNonQuery();
    }

    private static TestDb Seeded(int times = 1)
    {
        var db = new TestDb();
        for (var i = 0; i < times; i++)
        {
            Run(db.Connection, "seed-sample.sqlite.sql");
            Run(db.Connection, "seed-users.sqlite.sql");
        }

        return db;
    }

    [Fact]
    public async Task Running_twice_gives_exactly_4_users_2_classes_3_members_and_6_assignments()
    {
        using var db = Seeded(times: 2);
        await using var ctx = db.CreateContext();

        Assert.Equal(4, await ctx.Users.CountAsync());
        Assert.Equal(2, await ctx.Classes.CountAsync());
        Assert.Equal(3, await ctx.ClassMembers.CountAsync());
        Assert.Equal(6, await ctx.Assignments.CountAsync());
        Assert.Equal(4, await ctx.Assignments.CountAsync(a => a.AssignedToAll));
        Assert.Equal(2, await ctx.AssignmentClasses.CountAsync());
        Assert.Equal(3, await ctx.Lessons.CountAsync());
        Assert.Equal(6, await ctx.Exercises.CountAsync());
        Assert.True(await ctx.Lessons.AllAsync(l => l.IsPublished && !l.IsArchived && l.Version == 1));
        Assert.True(await ctx.Exercises.AllAsync(e => e.IsPublished && !e.IsArchived));
    }

    [Fact]
    public async Task Sample_hashes_verify_with_the_applications_hasher_and_flags_are_correct()
    {
        using var db = Seeded();
        await using var ctx = db.CreateContext();
        var hasher = new Pbkdf2PasswordHasher();
        var users = await ctx.Users.ToDictionaryAsync(u => u.Username);

        Assert.Equal(PasswordVerification.Success, hasher.Verify(users["giaovien"].PasswordHash, "GiaoVien@2026"));
        foreach (var name in new[] { "hs01", "hs02", "hs03" })
        {
            Assert.Equal(PasswordVerification.Success, hasher.Verify(users[name].PasswordHash, "HocSinh@2026"));
            Assert.Equal("Student", users[name].Role);
        }

        Assert.Equal("Teacher", users["giaovien"].Role);
        Assert.Equal(PasswordVerification.Failed, hasher.Verify(users["hs01"].PasswordHash, "GiaoVien@2026"));
        Assert.All(users.Values, u =>
        {
            Assert.False(u.MustChangePassword);
            Assert.True(u.IsActive);
            Assert.StartsWith("PBKDF2-SHA512$", u.PasswordHash);
        });
        Assert.Equal(3, users.Where(u => u.Key.StartsWith("hs")).Select(u => u.Value.PasswordHash).Distinct().Count());
    }

    [Fact]
    public async Task Class_based_visibility_hs01_hs02_see_6_exercises_and_hs03_sees_4()
    {
        using var db = Seeded();
        await using var ctx = db.CreateContext();
        var repo = new AssignmentRepository(ctx);
        var ids = await ctx.Users.ToDictionaryAsync(u => u.Username, u => u.Id);

        var hs01 = await repo.GetVisibleForStudentAsync(ids["hs01"]);
        var hs02 = await repo.GetVisibleForStudentAsync(ids["hs02"]);
        var hs03 = await repo.GetVisibleForStudentAsync(ids["hs03"]);

        Assert.Equal(6, hs01.Select(r => r.ExerciseId).Distinct().Count());
        Assert.Equal(6, hs02.Count);
        Assert.Equal(4, hs03.Count);
        Assert.DoesNotContain(hs03, r => r.LessonId == "bai-03-vong-lap");
        Assert.Equal(3, await ctx.Users.CountAsync(u => u.Role == "Student"));
    }

    [Theory]
    [InlineData("seed-users.sql")]
    [InlineData("seed-users.sqlite.sql")]
    [InlineData("mo-khoa-tai-khoan.sql")]
    public void Scripts_never_contain_plain_text_passwords(string file)
    {
        var text = File.ReadAllText(Path.Combine(Database, file));
        Assert.DoesNotContain("GiaoVien@2026", text);
        Assert.DoesNotContain("HocSinh@2026", text);
    }

    [Fact]
    public void Tsql_script_is_transactional_idempotent_by_construction_and_declares_the_sample_data()
    {
        var text = File.ReadAllText(Path.Combine(Database, "seed-users.sql"));
        Assert.Contains("BEGIN TRANSACTION", text);
        Assert.Contains("ROLLBACK", text);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(text, @"IF NOT EXISTS \(SELECT 1 FROM \[Users\]").Count);
        foreach (var name in new[] { "giaovien", "hs01", "hs02", "hs03", "12A1", "12A2" })
        {
            Assert.Contains($"N'{name}'", text);
        }

        Assert.Contains("PBKDF2-SHA512$120000$", text);
    }

    [Fact]
    public async Task Unlock_script_logic_clears_counters_only()
    {
        using var db = Seeded();
        await using (var ctx = db.CreateContext())
        {
            var user = await ctx.Users.SingleAsync(u => u.Username == "hs01");
            user.FailedLoginCount = 4;
            user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(10);
            await ctx.SaveChangesAsync();
        }

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET FailedLoginCount = 0, LockoutEndUtc = NULL WHERE Username = lower('HS01')";
        Assert.Equal(1, cmd.ExecuteNonQuery());

        await using var check = db.CreateContext();
        var after = await check.Users.SingleAsync(u => u.Username == "hs01");
        Assert.Equal(0, after.FailedLoginCount);
        Assert.Null(after.LockoutEndUtc);
        Assert.StartsWith("PBKDF2", after.PasswordHash);
    }
}

internal static class Paths
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "TinHoc12.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo.");
    }
}

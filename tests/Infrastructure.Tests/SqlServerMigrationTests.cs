using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TinHoc12.Application.Abstractions;
using TinHoc12.Infrastructure.Persistence;
using TinHoc12.Infrastructure.Security;

namespace TinHoc12.Infrastructure.Tests;

/// <summary>
/// Kiểm thử migration và script T-SQL trên SQL Server LocalDB thật (AC-153, AC-154, AC-101). Tự bỏ qua khi chưa đặt TINHOC12_TEST_SQLSERVER.
/// Tạo một CSDL tạm rồi xóa sau khi chạy.
/// </summary>
public class SqlServerMigrationTests
{
    // Chuỗi kết nối KHÔNG viết cứng: đặt biến môi trường TINHOC12_TEST_SQLSERVER (không kèm Database=), ví dụ trỏ tới LocalDB.
    private static readonly string? ServerSetting = Environment.GetEnvironmentVariable("TINHOC12_TEST_SQLSERVER");
    private static string Server => (ServerSetting ?? string.Empty).TrimEnd(';') + ";Connect Timeout=10";

    private static bool LocalDbAvailable()
    {
        if (string.IsNullOrWhiteSpace(ServerSetting))
        {
            return false;
        }

        try
        {
            using var connection = new SqlConnection(Server + ";Database=master");
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Exec(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }

    private static string Script(string name) => File.ReadAllText(Path.Combine(Paths.RepoRoot(), "database", name));

    private static AppDbContext Context(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);

    [SkippableFact]
    public void Migration_keeps_v2_data_scripts_run_twice_and_constraints_hold_on_sql_server()
    {
        Skip.IfNot(LocalDbAvailable(), "Bỏ qua: chưa đặt biến môi trường TINHOC12_TEST_SQLSERVER (chuỗi kết nối SQL Server/LocalDB không kèm Database=) hoặc không kết nối được.");

        var name = "TinHoc12_MigTest_" + Guid.NewGuid().ToString("N")[..8];
        var master = Server + ";Database=master";
        var db = Server + ";Database=" + name;
        Exec(master, $"CREATE DATABASE [{name}]");
        try
        {
            // 1) Áp migration InitialCreate (schema v2) + dữ liệu mẫu v2 + script thêm bài cũ.
            using (var ctx = Context(db))
            {
                ctx.GetService<IMigrator>().Migrate("20261019021148_InitialCreate");
            }

            Exec(db, Script("sqlserver/seed-sample.sql"));
            Exec(db, Script("scripts/them-bai-moi.sql"));
            var lessons = Scalar<int>(db, "SELECT COUNT(*) FROM Lessons");
            var exercises = Scalar<int>(db, "SELECT COUNT(*) FROM Exercises");
            var tests = Scalar<int>(db, "SELECT COUNT(*) FROM TestCases");
            Assert.Equal(4, lessons);

            // 2) Áp migration mới lên DB đã có dữ liệu: không mất dữ liệu, mặc định đã xuất bản.
            using (var ctx = Context(db))
            {
                ctx.Database.Migrate();
                ctx.Database.Migrate(); // lần 2 không lỗi
                Assert.Empty(ctx.Database.GetPendingMigrations());
            }

            Assert.Equal(lessons, Scalar<int>(db, "SELECT COUNT(*) FROM Lessons"));
            Assert.Equal(exercises, Scalar<int>(db, "SELECT COUNT(*) FROM Exercises"));
            Assert.Equal(tests, Scalar<int>(db, "SELECT COUNT(*) FROM TestCases"));
            Assert.Equal(lessons, Scalar<int>(db, "SELECT COUNT(*) FROM Lessons WHERE IsPublished = 1 AND IsArchived = 0 AND Version = 1 AND CreatedByUserId IS NULL"));
            Assert.Equal(exercises, Scalar<int>(db, "SELECT COUNT(*) FROM Exercises WHERE IsPublished = 1 AND IsArchived = 0 AND Version = 1"));

            // 3) Script tài khoản mẫu chạy hai lần, hash xác thực được.
            Exec(db, Script("sqlserver/seed-users.sql"));
            Exec(db, Script("sqlserver/seed-users.sql"));
            Assert.Equal(4, Scalar<int>(db, "SELECT COUNT(*) FROM Users"));
            Assert.Equal(2, Scalar<int>(db, "SELECT COUNT(*) FROM Classes"));
            Assert.Equal(3, Scalar<int>(db, "SELECT COUNT(*) FROM ClassMembers"));
            Assert.Equal(6, Scalar<int>(db, "SELECT COUNT(*) FROM Assignments"));
            Assert.Equal(0, Scalar<int>(db, "SELECT COUNT(*) FROM Users WHERE MustChangePassword = 1"));
            var hasher = new Pbkdf2PasswordHasher();
            using (var ctx = Context(db))
            {
                var teacher = ctx.Users.Single(u => u.Username == "giaovien");
                Assert.Equal(PasswordVerification.Success, hasher.Verify(teacher.PasswordHash, "GiaoVien@2026"));
                Assert.All(ctx.Users.Where(u => u.Role == "Student").ToList(), u => Assert.Equal(PasswordVerification.Success, hasher.Verify(u.PasswordHash, "HocSinh@2026")));
            }

            // 4) INFORMATION_SCHEMA: bảng, cột và kiểu.
            Assert.Equal(11, Scalar<int>(db, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"));
            foreach (var (table, column, type) in new[]
            {
                ("Users", "Username", "nvarchar"), ("Users", "PasswordHash", "nvarchar"), ("Users", "IsActive", "bit"),
                ("Users", "LockoutEndUtc", "datetime2"), ("Submissions", "Id", "bigint"), ("Submissions", "Code", "nvarchar"),
                ("Lessons", "IsPublished", "bit"), ("Lessons", "Version", "int"), ("Exercises", "IsArchived", "bit"),
                ("Assignments", "DueAtUtc", "datetime2"), ("SolutionViews", "FirstViewedUtc", "datetime2"),
            })
            {
                Assert.Equal(type, Scalar<string>(db, $"SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'"));
            }

            // 5) Ràng buộc bị DB từ chối (vai trò sai, trùng tên khác hoa/thường, bài nộp sai số).
            Assert.ThrowsAny<SqlException>(() => Exec(db, "INSERT INTO Users (Username, DisplayName, PasswordHash, Role, SecurityStamp, CreatedUtc) VALUES (N'quantri', N'x', N'x', N'Admin', N's', SYSUTCDATETIME())"));
            Assert.ThrowsAny<SqlException>(() => Exec(db, "INSERT INTO Users (Username, DisplayName, PasswordHash, Role, SecurityStamp, CreatedUtc) VALUES (N'HS01', N'x', N'x', N'Student', N's', SYSUTCDATETIME())"));
            Assert.ThrowsAny<SqlException>(() => Exec(db, "INSERT INTO Classes (Name, CreatedUtc) VALUES (N'12a1', SYSUTCDATETIME())"));
            var hs = Scalar<int>(db, "SELECT Id FROM Users WHERE Username = N'hs01'");
            var asg = Scalar<int>(db, "SELECT TOP 1 Id FROM Assignments WHERE ExerciseId = N'bai-01-tong-hai-so'");
            Assert.ThrowsAny<SqlException>(() => Exec(db, $"INSERT INTO Submissions (AssignmentId, ExerciseId, StudentId, Code, PassedTests, TotalTests, IsPassed, IsLate, SubmittedUtc) VALUES ({asg}, N'bai-01-tong-hai-so', {hs}, N'x', 6, 5, 0, 0, SYSUTCDATETIME())"));
            Exec(db, $"INSERT INTO Submissions (AssignmentId, ExerciseId, StudentId, Code, PassedTests, TotalTests, IsPassed, IsLate, SubmittedUtc) VALUES ({asg}, N'bai-01-tong-hai-so', {hs}, N'x', 5, 5, 1, 0, SYSUTCDATETIME())");
            Assert.ThrowsAny<SqlException>(() => Exec(db, "DELETE FROM Exercises WHERE Id = N'bai-01-tong-hai-so'"));
            Assert.ThrowsAny<SqlException>(() => Exec(db, "DELETE FROM Lessons WHERE Id = N'bai-01-nhap-xuat'"));
            Exec(db, "DELETE FROM Lessons WHERE Id = N'bai-99-mau'"); // bài chưa giao: xóa dây chuyền như v2

            // 6) Mở khóa bằng script.
            Exec(db, "UPDATE Users SET FailedLoginCount = 4, LockoutEndUtc = DATEADD(MINUTE, 10, SYSUTCDATETIME()) WHERE Username = N'hs01'");
            Exec(db, Script("scripts/mo-khoa-tai-khoan.sql").Replace("N'giaovien'", "N'HS01'"));
            Assert.Equal(0, Scalar<int>(db, "SELECT COUNT(*) FROM Users WHERE Username = N'hs01' AND (FailedLoginCount <> 0 OR LockoutEndUtc IS NOT NULL)"));

            // 7) schema.sql idempotent chạy được trên DB đã migrate và trên DB trống.
            foreach (var batch in SplitBatches(Script("sqlserver/schema.sql")))
            {
                Exec(db, batch);
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            Exec(master, $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
        }
    }

    // Script do EF sinh có lệnh GO (không phải T-SQL) nên chạy từng lô riêng.
    private static IEnumerable<string> SplitBatches(string script) =>
        System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b + "\n");
}

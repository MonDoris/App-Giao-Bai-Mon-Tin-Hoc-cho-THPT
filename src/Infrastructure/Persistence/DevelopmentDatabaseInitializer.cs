using Microsoft.EntityFrameworkCore;

namespace TinHoc12.Infrastructure.Persistence;

/// <summary>Khởi tạo DB cho môi trường Development: SQL Server (migration + seed T-SQL); nhánh SQLite chỉ phục vụ kiểm thử.</summary>
public static class DevelopmentDatabaseInitializer
{
    /// <summary>
    /// SQL Server: áp migration (tạo DB nếu chưa có, không xóa dữ liệu đã có), rồi nạp seed T-SQL idempotent nếu bảng còn trống.
    /// </summary>
    public static async Task InitializeSqlServerAsync(AppDbContext db, string? seedLessonsPath, string? seedUsersPath)
    {
        await db.Database.MigrateAsync();

        await RunIfFileExistsAsync(db, seedLessonsPath, async () => !await db.Lessons.AnyAsync());
        await RunIfFileExistsAsync(db, seedUsersPath, async () => !await db.Users.AnyAsync());
    }

    /// <summary>
    /// CHỈ PHỤC VỤ KIỂM THỬ (SQLite in-memory), không dùng khi chạy ứng dụng. Tạo DB từ model; nạp bài học mẫu nếu chưa có bài nào; nạp tài khoản/lớp/giao bài mẫu nếu chưa có tài khoản nào.
    /// Hai script đều idempotent nên chạy lại không gây lỗi.
    /// </summary>
    public static async Task InitializeAsync(AppDbContext db, string? seedLessonsPath, string? seedUsersPath = null)
    {
        await db.Database.EnsureCreatedAsync();

        await RunIfFileExistsAsync(db, seedLessonsPath, async () => !await db.Lessons.AnyAsync());
        await RunIfFileExistsAsync(db, seedUsersPath, async () => !await db.Users.AnyAsync());
    }

    private static async Task RunIfFileExistsAsync(AppDbContext db, string? path, Func<Task<bool>> shouldRun)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !await shouldRun())
        {
            return;
        }

        var script = await File.ReadAllTextAsync(path);
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }

    }
}

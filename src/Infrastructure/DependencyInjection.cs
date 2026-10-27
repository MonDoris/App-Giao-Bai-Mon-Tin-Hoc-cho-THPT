using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinHoc12.Application.Abstractions;
using TinHoc12.Infrastructure.Persistence;
using TinHoc12.Infrastructure.Repositories;
using TinHoc12.Infrastructure.Security;

namespace TinHoc12.Infrastructure;

public static class DependencyInjection
{
    public const string SqlServerProvider = "SqlServer";
    public const string SqliteProvider = "Sqlite";

    /// <summary>
    /// Đăng ký DbContext + repository. Chuỗi kết nối lấy từ ConnectionStrings:DefaultConnection.
    /// Database:Provider = SqlServer (mặc định, cách chạy duy nhất của ứng dụng). Giá trị Sqlite CHỈ PHỤC VỤ KIỂM THỬ (tests/ dùng SQLite in-memory), không dùng khi chạy ứng dụng.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? SqlServerProvider;
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:DefaultConnection.");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, SqliteProvider, StringComparison.OrdinalIgnoreCase))
            {
                // Chỉ phục vụ kiểm thử (tests/ dùng SQLite in-memory), không dùng khi chạy ứng dụng. Gỡ khi test chuyển sang SQL Server.
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ILessonAuthoringRepository, LessonAuthoringRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<IProgressRepository, ProgressRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }

    /// <summary>
    /// Môi trường Development: với SQL Server thì tự áp migration (tạo DB nếu chưa có, giữ nguyên dữ liệu đã có) rồi nạp
    /// database/seed-sample.sql (khi chưa có bài học) và database/seed-users.sql (khi chưa có tài khoản).
    /// Nhánh Sqlite CHỈ PHỤC VỤ KIỂM THỬ (đọc Database:SeedScript/SeedUsersScript), không dùng khi chạy ứng dụng.
    /// </summary>
    public static async Task InitializeDevelopmentDatabaseAsync(this IServiceProvider services, IConfiguration configuration, string contentRootPath)
    {
        var provider = configuration["Database:Provider"] ?? SqlServerProvider;
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (string.Equals(provider, SqliteProvider, StringComparison.OrdinalIgnoreCase))
        {
            await DevelopmentDatabaseInitializer.InitializeAsync(
                db, ResolvePath(configuration["Database:SeedScript"], contentRootPath), ResolvePath(configuration["Database:SeedUsersScript"], contentRootPath));
            return;
        }

        await DevelopmentDatabaseInitializer.InitializeSqlServerAsync(
            db, FindDatabaseScript("seed-sample.sql", contentRootPath), FindDatabaseScript("seed-users.sql", contentRootPath));
    }

    private static string? ResolvePath(string? path, string contentRootPath) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path, contentRootPath);

    /// <summary>Tìm database/&lt;tên tệp&gt; bằng cách đi ngược từ thư mục gốc nội dung của ứng dụng.</summary>
    private static string? FindDatabaseScript(string fileName, string contentRootPath)
    {
        for (var dir = new DirectoryInfo(contentRootPath); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "database", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

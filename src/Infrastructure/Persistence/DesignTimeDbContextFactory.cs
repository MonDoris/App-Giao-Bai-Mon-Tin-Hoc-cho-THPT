using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TinHoc12.Infrastructure.Persistence;

/// <summary>
/// Dùng cho công cụ dotnet ef (migrations, script). Luôn dùng SQL Server, độc lập với cấu hình chạy ứng dụng.
/// Chuỗi kết nối: biến môi trường ConnectionStrings__DefaultConnection; nếu không có thì đọc
/// ConnectionStrings:DefaultConnection trong src/Web/appsettings.json.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = ReadFromAppSettings()
                ?? throw new InvalidOperationException(
                    "Không tìm thấy ConnectionStrings:DefaultConnection (src/Web/appsettings.json) hoặc biến môi trường ConnectionStrings__DefaultConnection.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static string? ReadFromAppSettings()
    {
        // Tìm src/Web/appsettings.json từ thư mục hiện tại và thư mục chạy, đi ngược lên các thư mục cha.
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                foreach (var candidate in new[]
                {
                    Path.Combine(dir.FullName, "appsettings.json"),
                    Path.Combine(dir.FullName, "src", "Web", "appsettings.json"),
                    Path.Combine(dir.FullName, "Web", "appsettings.json"),
                })
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    using var doc = JsonDocument.Parse(File.ReadAllText(candidate), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                        && cs.TryGetProperty("DefaultConnection", out var value)
                        && value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString();
                    }
                }
            }
        }

        return null;
    }
}

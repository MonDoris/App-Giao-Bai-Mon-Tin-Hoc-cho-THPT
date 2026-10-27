using System.Diagnostics;
using Microsoft.Extensions.FileProviders;

namespace TinHoc12.Web;

/// <summary>
/// Chế độ "một lệnh chạy cả hai" cho dev: khi Frontend:ServeFromBackend = true, backend phục vụ luôn thư mục frontend/
/// tại gốc origin (cùng origin với API nên không cần CORS). Mặc định tắt: Production vẫn là API-only (QD-87);
/// cách chạy frontend tách riêng ở cổng 5173 vẫn dùng được song song.
/// </summary>
public static class FrontendHosting
{
    public const string ServeKey = "Frontend:ServeFromBackend";
    public const string PathKey = "Frontend:Path";
    public const string OpenBrowserKey = "Frontend:OpenBrowser";

    /// <summary>Phục vụ tệp tĩnh từ thư mục frontend nếu được bật; thư mục thiếu hoặc không có index.html thì báo lỗi rõ ràng khi khởi động.</summary>
    public static WebApplication UseFrontendFromBackend(this WebApplication app)
    {
        if (!app.Configuration.GetValue(ServeKey, false))
        {
            return app;
        }

        var root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration[PathKey] ?? "../../frontend"));
        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            throw new InvalidOperationException(
                $"Cấu hình {PathKey} không hợp lệ: không thấy \"{Path.Combine(root, "index.html")}\". " +
                $"Hãy trỏ {PathKey} tới thư mục frontend/ hoặc đặt {ServeKey} = false.");
        }

        var files = new PhysicalFileProvider(root);
        // /api/** luôn đi tới API, không bao giờ bị tệp tĩnh che.
        app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
        {
            branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            branch.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = files,
                // Như http-server -c-1: sửa tệp frontend rồi tải lại trang là thấy ngay.
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
            });
        });

        if (app.Configuration.GetValue(OpenBrowserKey, false))
        {
            app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(app));
        }

        return app;
    }

    private static void OpenBrowser(WebApplication app)
    {
        var url = app.Urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? app.Urls.FirstOrDefault();
        if (url is null)
        {
            return;
        }

        url = url.Replace("://0.0.0.0", "://localhost").Replace("://[::]", "://localhost").Replace("://+", "://localhost").Replace("://*", "://localhost");
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            app.Logger.LogInformation("Giao diện DK Edu: {Url}", url);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Không tự mở được trình duyệt; hãy mở {Url} bằng tay.", url);
        }
    }
}

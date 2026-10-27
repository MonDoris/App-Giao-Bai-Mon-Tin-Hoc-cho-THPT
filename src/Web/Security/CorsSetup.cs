using System.Text.RegularExpressions;

namespace TinHoc12.Web.Security;

/// <summary>
/// CORS cho frontend tách riêng (QD-89, AC-490 đến AC-497): chỉ các origin liệt kê trong Cors:AllowedOrigins được gọi API kèm cookie.
/// Không wildcard, không phản chiếu origin tùy ý; cấu hình sai làm ứng dụng báo lỗi rõ ràng khi khởi động.
/// </summary>
public static partial class CorsSetup
{
    public const string ConfigKey = "Cors:AllowedOrigins";
    public const string PolicyName = "frontend";

    // scheme://host[:port], không đường dẫn, không dấu "/" cuối, không query/fragment, không thông tin đăng nhập.
    [GeneratedRegex(@"^https?://[A-Za-z0-9]([A-Za-z0-9.\-]*[A-Za-z0-9])?(:[0-9]{1,5})?$|^https?://\[[0-9A-Fa-f:.]+\](:[0-9]{1,5})?$")]
    private static partial Regex OriginPattern();

    /// <summary>Đọc và kiểm tra danh sách origin; giá trị sai ném InvalidOperationException có chứa "Cors:AllowedOrigins".</summary>
    public static string[] ReadAllowedOrigins(IConfiguration configuration)
    {
        var origins = configuration.GetSection(ConfigKey).GetChildren().Select(c => c.Value ?? string.Empty).ToArray();
        foreach (var origin in origins)
        {
            if (!OriginPattern().IsMatch(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    $"Cấu hình {ConfigKey} không hợp lệ: \"{origin}\". Mỗi giá trị phải có dạng http(s)://tên-miền[:cổng], " +
                    "không dùng dấu *, không có đường dẫn, không có dấu / ở cuối.");
            }
        }

        return origins.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, string[] allowedOrigins)
    {
        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "X-CSRF-TOKEN")
            .WithExposedHeaders("Retry-After") // api.js đọc Retry-After khi bị giới hạn đăng nhập (429)
            .SetPreflightMaxAge(TimeSpan.FromSeconds(600))));
        return services;
    }
}

using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TinHoc12.Application.Security;
using TinHoc12.Web.Middleware;

namespace TinHoc12.Web.Security;

/// <summary>Giới hạn tần suất đăng nhập theo IP (QD-22, AC-87). Ngưỡng cao vì cả lớp có thể dùng chung một IP.</summary>
public static class RateLimiting
{
    public const string LoginPolicy = "login";

    public static IServiceCollection AddLoginRateLimiter(this IServiceCollection services, AuthOptions auth)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = auth.LoginRatePerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? ((int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture)
                    : "60";
                await http.Response.WriteAsJsonAsync(
                    new ApiError("TOO_MANY_REQUESTS", "Bạn thử quá nhiều lần, vui lòng đợi ít phút."), cancellationToken);
            };
        });
        return services;
    }
}

/// <summary>Làm sạch chuỗi do người dùng nhập trước khi ghi log (chống log injection, AC-158).</summary>
public static class LogSanitizer
{
    public static string Clean(string? value, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var chars = value.Where(c => !char.IsControl(c)).Take(maxLength).ToArray();
        return new string(chars);
    }
}

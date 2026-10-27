using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using TinHoc12.Web.Security;

namespace TinHoc12.Web.Middleware;

/// <summary>Header bảo mật cho mọi phản hồi (AC-99); mọi /api/** không được cache.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "same-origin";
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers.CacheControl = "no-store";
        }

        return _next(context);
    }
}

/// <summary>Giới hạn kích thước thân yêu cầu cho /api: đăng nhập 4 KB, nộp bài và nhập danh sách 100 KB, còn lại 1 MB.</summary>
public sealed class RequestLimitsMiddleware
{
    private const long Default = 1_048_576;
    private readonly RequestDelegate _next;

    public RequestLimitsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method)))
        {
            var limit = LimitFor(context.Request.Path.Value ?? string.Empty);
            if (context.Request.ContentLength > limit)
            {
                await ApiErrors.PayloadTooLarge().ExecuteAsync(context);
                return;
            }

            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = limit;
            }
        }

        await _next(context);
    }

    private static long LimitFor(string path)
    {
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return 4 * 1024;
        }

        if (path.EndsWith("/submissions", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/teacher/students/import", StringComparison.OrdinalIgnoreCase))
        {
            return 100 * 1024;
        }

        return Default;
    }
}

/// <summary>Chống CSRF (QD-18, AC-89): mọi POST/PUT/PATCH/DELETE tới /api cần token đúng trong header X-CSRF-TOKEN.</summary>
public sealed class CsrfMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;

    public CsrfMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api") && IsUnsafe(context.Request.Method)
            && !await _antiforgery.IsRequestValidAsync(context))
        {
            await ApiErrors.CsrfInvalid().ExecuteAsync(context);
            return;
        }

        await _next(context);
    }

    private static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}

/// <summary>Khi tài khoản phải đổi mật khẩu (QD-21), chỉ cho dùng me/change-password/logout/csrf (AC-94).</summary>
public sealed class PasswordChangeGateMiddleware
{
    private static readonly string[] Allowed =
    {
        "/api/auth/me", "/api/auth/change-password", "/api/auth/logout", "/api/auth/csrf",
    };

    private readonly RequestDelegate _next;

    public PasswordChangeGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api") && context.GetSessionUser() is { MustChangePassword: true }
            && !Allowed.Any(a => path.Equals(a, StringComparison.OrdinalIgnoreCase)))
        {
            await ApiErrors.PasswordChangeRequired().ExecuteAsync(context);
            return;
        }

        await _next(context);
    }
}

using System.Data.Common;
using TinHoc12.Application.Exceptions;

namespace TinHoc12.Web.Middleware;

/// <summary>
/// Chuyển ngoại lệ thành JSON lỗi chung cho /api: NotFound 404, Forbidden 403, InvalidCredentials 401, ValidationFailed 422,
/// Conflict 409, TooManyRequests 429, DataInvalid 422, lỗi kết nối DB 503, còn lại 500. Không lộ chi tiết nội bộ;
/// chi tiết chỉ ghi vào log (không chứa mật khẩu/hash/cookie).
/// </summary>
public sealed class ApiExceptionMiddleware
{
    private static readonly HashSet<string> PreservedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Vary", "Retry-After", "X-Frame-Options", "Referrer-Policy",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isApi = context.Request.Path.StartsWithSegments("/api");
        if (isApi)
        {
            // ID rỗng như /api/lessons/ hoặc /api/exercises//solution -> 404 (AC-59).
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.Length > "/api/".Length && (path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal)))
            {
                await ApiErrors.NotFound("Không tìm thấy tài nguyên.").ExecuteAsync(context);
                return;
            }
        }

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client đã hủy yêu cầu; không cần phản hồi.
        }
        catch (Exception ex) when (isApi && !context.Response.HasStarted)
        {
            var result = Map(context, ex);
            // Giữ header CORS và header bảo mật đã đặt trước đó: nếu mất, frontend khác origin không đọc được mã lỗi (QD-89, AC-495).
            var kept = context.Response.Headers
                .Where(h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase) || PreservedHeaders.Contains(h.Key))
                .ToList();
            context.Response.Clear();
            foreach (var header in kept)
            {
                context.Response.Headers[header.Key] = header.Value;
            }

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await result.ExecuteAsync(context);
        }
    }

    private IResult Map(HttpContext context, Exception ex)
    {
        switch (ex)
        {
            case NotFoundException nf:
                return ApiErrors.NotFound(nf.Message);
            case InvalidCredentialsException:
                return ApiErrors.InvalidCredentials();
            case ForbiddenException fb:
                _logger.LogInformation("Từ chối quyền truy cập vào {Method} {Path}.", context.Request.Method, context.Request.Path);
                return ApiErrors.Forbidden(fb.Message);
            case ValidationFailedException vf:
                return ApiErrors.ValidationFailed(vf);
            case ConflictException cf:
                return ApiErrors.Conflict(cf.Code, cf.Message);
            case TooManyRequestsException tm:
                context.Response.Headers.RetryAfter = tm.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ApiErrors.TooManyRequests(tm.Message);
            case DataInvalidException di:
                _logger.LogWarning("Dữ liệu bài học bị lỗi ở trường {Field}: {Message}", di.Field, di.Message);
                return ApiErrors.DataInvalid(di.Field, di.Message);
            case BadHttpRequestException bad:
                return bad.StatusCode switch
                {
                    StatusCodes.Status413PayloadTooLarge => ApiErrors.PayloadTooLarge(),
                    StatusCodes.Status415UnsupportedMediaType => ApiErrors.UnsupportedMediaType(),
                    _ => ApiErrors.BadInput(),
                };
        }

        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is DbException)
            {
                _logger.LogError(ex, "Lỗi truy cập cơ sở dữ liệu.");
                return ApiErrors.Unavailable();
            }
        }

        _logger.LogError(ex, "Lỗi không lường trước khi xử lý yêu cầu.");
        return ApiErrors.Internal();
    }
}

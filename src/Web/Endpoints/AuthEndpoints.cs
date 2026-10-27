using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.UseCases.Auth;
using TinHoc12.Web.Middleware;
using TinHoc12.Web.Security;

namespace TinHoc12.Web.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapGet("/csrf", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { token = tokens.RequestToken, headerName = SessionSupport.CsrfHeaderName });
        }).AllowAnonymous();

        auth.MapPost("/login", async (
            LoginRequest request, HttpContext http, LoginUseCase useCase, ILoggerFactory loggers, CancellationToken ct) =>
        {
            try
            {
                var ticket = await useCase.ExecuteAsync(request, ct);
                await SessionSupport.SignInAsync(http, ticket);
                return Results.Ok(new LoginResultDto(ticket.User, ticket.RedirectUrl));
            }
            catch (InvalidCredentialsException)
            {
                // Ghi log không chứa mật khẩu; tên đăng nhập được làm sạch để chống log injection (AC-158).
                loggers.CreateLogger("DKEdu.Auth").LogWarning(
                    "Đăng nhập thất bại lúc {Time:o} từ IP {Ip} với tên đăng nhập '{Username}'.",
                    DateTime.UtcNow, http.Connection.RemoteIpAddress, LogSanitizer.Clean(request.Username));
                throw;
            }
        }).AllowAnonymous().RequireRateLimiting(RateLimiting.LoginPolicy);

        auth.MapPost("/logout", async (HttpContext http, SessionUseCase useCase, CancellationToken ct) =>
        {
            if (http.User.Identity?.IsAuthenticated == true)
            {
                await useCase.LogoutAsync(http.GetCurrentUser().Id, ct);
            }

            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        auth.MapGet("/me", async (HttpContext http, SessionUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.GetMeAsync(http.GetCurrentUser().Id, ct))).RequireAuthorization();

        auth.MapPost("/change-password", async (
            ChangePasswordRequest request, HttpContext http, ChangePasswordUseCase useCase, CancellationToken ct) =>
        {
            var ticket = await useCase.ExecuteAsync(http.GetCurrentUser(), request, ct);
            // Cấp lại cookie cho phiên hiện tại (SecurityStamp mới); các phiên khác bị 401.
            await SessionSupport.SignInAsync(http, ticket);
            return Results.NoContent();
        }).RequireAuthorization();

        // Không có đăng ký tự do: mọi đường dẫn auth khác -> 404 (AC-103).
        auth.MapFallback("{**rest}", () => ApiErrors.NotFound("Không tìm thấy tài nguyên.")).AllowAnonymous();

        return app;
    }
}

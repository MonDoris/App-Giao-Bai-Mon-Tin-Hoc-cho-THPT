using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Auth;
using TinHoc12.Web.Middleware;

namespace TinHoc12.Web.Security;

/// <summary>Các hằng số và hàm tiện ích cho phiên đăng nhập bằng cookie (QD-17, QD-23).</summary>
public static class SessionSupport
{
    public const string CookieName = "dkedu.auth";
    public const string StampClaim = "dkedu:stamp";
    public const string UserItemKey = "dkedu.session.user";
    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    public static ClaimsPrincipal CreatePrincipal(SessionTicket ticket)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ticket.User.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, ticket.User.Username),
                new Claim(ClaimTypes.Role, ticket.User.Role),
                new Claim(StampClaim, ticket.SecurityStamp),
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    public static Task SignInAsync(HttpContext context, SessionTicket ticket) =>
        context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(ticket),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

    /// <summary>Danh tính người gọi lấy từ phiên đã được kiểm tra ở máy chủ (không bao giờ từ nội dung yêu cầu).</summary>
    public static CurrentUser GetCurrentUser(this HttpContext context)
    {
        var idText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(idText, out var id) || !Roles.IsValid(role))
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
        }

        return new CurrentUser(id, role!);
    }

    /// <summary>Người dùng của phiên đã kiểm tra ở request hiện tại (null nếu chưa đăng nhập).</summary>
    public static UserDto? GetSessionUser(this HttpContext context) =>
        context.Items.TryGetValue(UserItemKey, out var user) ? user as UserDto : null;

    /// <summary>Kiểm tra mỗi request: tài khoản còn hoạt động và SecurityStamp khớp (AC-92); phiên quá 10 giờ bị hủy (AC-91).</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var options = services.GetRequiredService<AuthOptions>();
        var clock = services.GetRequiredService<IClock>();

        var issued = context.Properties.IssuedUtc;
        if (issued is null || issued.Value.UtcDateTime.AddHours(options.AbsoluteHours) < clock.UtcNow)
        {
            await RejectAsync(context);
            return;
        }

        var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue(StampClaim);
        var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(idText, out var id))
        {
            await RejectAsync(context);
            return;
        }

        var user = await services.GetRequiredService<SessionUseCase>().ValidateAsync(id, stamp, context.HttpContext.RequestAborted);
        if (user is null || user.Role != role)
        {
            await RejectAsync(context);
            return;
        }

        context.HttpContext.Items[UserItemKey] = user;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static Task WriteJsonAsync(HttpContext context, int statusCode, ApiError error)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(error);
    }
}

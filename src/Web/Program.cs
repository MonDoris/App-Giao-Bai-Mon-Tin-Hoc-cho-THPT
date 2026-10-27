using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Security;
using TinHoc12.Application.UseCases.Assignments;
using TinHoc12.Application.UseCases.Auth;
using TinHoc12.Application.UseCases.Authoring;
using TinHoc12.Application.UseCases.Lessons;
using TinHoc12.Application.UseCases.Progress;
using TinHoc12.Application.UseCases.Student;
using TinHoc12.Application.UseCases.Students;
using TinHoc12.Infrastructure;
using TinHoc12.Web.Endpoints;
using TinHoc12.Web.Middleware;
using TinHoc12.Web.Security;
using TinHoc12.Web;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình xác thực ngoài mã nguồn (AC-159): thiếu thì dùng mặc định, giá trị <= 0 làm ứng dụng báo lỗi rõ ràng khi khởi động.
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
authOptions.Validate();
builder.Services.AddSingleton(authOptions);

// CORS cho frontend tách riêng (QD-89): chỉ các origin trong Cors:AllowedOrigins; cấu hình sai thì báo lỗi rõ ràng khi khởi động (AC-496).
builder.Services.AddFrontendCors(CorsSetup.ReadAllowedOrigins(builder.Configuration));

// Composition root: đăng ký hạ tầng (EF Core) và các use case của Application.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<SubmissionThrottle>();
foreach (var useCase in new[]
{
    typeof(GetLessonsUseCase), typeof(GetLessonDetailUseCase), typeof(GetSolutionUseCase),
    typeof(LoginUseCase), typeof(SessionUseCase), typeof(ChangePasswordUseCase), typeof(DetectDefaultPasswordsUseCase),
    typeof(GetMyAssignmentsUseCase), typeof(SubmitSolutionUseCase), typeof(GetMySubmissionsUseCase),
    typeof(LessonAuthoringUseCase), typeof(ExerciseAuthoringUseCase), typeof(AssignmentUseCase),
    typeof(StudentManagementUseCase), typeof(ClassManagementUseCase), typeof(ProgressUseCase),
})
{
    builder.Services.AddScoped(useCase);
}

// Minimal API ném BadHttpRequestException khi JSON sai để ApiExceptionMiddleware trả JSON lỗi thống nhất.
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

// Khóa Data Protection (ký cookie/antiforgery): có thể đặt thư mục bền bằng Auth:KeyDirectory (AC-156).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("DKEdu");
if (!string.IsNullOrWhiteSpace(authOptions.KeyDirectory))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(authOptions.KeyDirectory));
}

// Cổng HTTPS để chuyển hướng HTTP -> HTTPS ở Production (cấu hình Https:Port, mặc định 443).
builder.Services.AddHttpsRedirection(o => o.HttpsPort = builder.Configuration.GetValue("Https:Port", 443));

var secureCookies = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = SessionSupport.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.Cookie.SecurePolicy = secureCookies;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(authOptions.IdleMinutes);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = SessionSupport.ValidatePrincipalAsync,
            OnRedirectToLogin = ctx => SessionSupport.WriteJsonAsync(
                ctx.HttpContext, StatusCodes.Status401Unauthorized,
                new ApiError("UNAUTHENTICATED", "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.")),
            OnRedirectToAccessDenied = ctx => SessionSupport.WriteJsonAsync(
                ctx.HttpContext, StatusCodes.Status403Forbidden,
                new ApiError("FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.")),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.TeacherOnly, p => p.RequireAuthenticatedUser().RequireRole(Roles.Teacher))
    .AddPolicy(AuthPolicies.StudentOnly, p => p.RequireAuthenticatedUser().RequireRole(Roles.Student));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = SessionSupport.CsrfHeaderName;
    options.Cookie.Name = "dkedu.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = secureCookies;
    options.SuppressXFrameOptionsHeader = true;
});

builder.Services.AddLoginRateLimiter(authOptions);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Development: tự áp migration SQL Server và nạp seed mẫu nếu DB còn trống (Production không tự đụng tới DB).
    await app.Services.InitializeDevelopmentDatabaseAsync(app.Configuration, app.Environment.ContentRootPath);
}

if (app.Environment.IsProduction())
{
    // HTTPS bắt buộc ở môi trường thật (cookie Secure, mật khẩu không đi dạng rõ).
    app.UseHsts();
    app.UseHttpsRedirection();
    // Nếu vẫn tới bằng HTTP (không chuyển hướng được): trả 400 rõ ràng thay vì lỗi 500 do cookie Secure.
    app.Use(async (context, next) =>
    {
        if (context.Request.IsHttps)
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ApiError("HTTPS_REQUIRED", "Hãy truy cập DK Edu bằng HTTPS (địa chỉ bắt đầu bằng https://)."));
    });
    await WarnAboutDefaultPasswordsAsync(app);
}

// Backend mặc định chỉ là API (QD-87); trang giao diện nằm ở thư mục frontend/ độc lập.
// Riêng Development bật Frontend:ServeFromBackend để `dotnet run` phục vụ luôn frontend/ tại http://localhost:5000/.
// UseCors đứng trước xác thực, CSRF, giới hạn tần suất để preflight và các lỗi 401/403/429 đều có header CORS (AC-497).
app.UseMiddleware<SecurityHeadersMiddleware>();
app.Use((context, next) =>
{
    // Phản hồi thay đổi theo Origin (có hay không có header CORS) nên luôn báo cho bộ nhớ đệm trung gian (AC-491);
    // ASP.NET chỉ tự thêm Vary: Origin khi có từ 2 origin trở lên.
    context.Response.Headers.Append("Vary", "Origin");
    return next();
});
app.UseCors(CorsSetup.PolicyName);
app.UseFrontendFromBackend();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<RequestLimitsMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<CsrfMiddleware>();
app.UseMiddleware<PasswordChangeGateMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapApiEndpoints();

app.Run();

static async Task WarnAboutDefaultPasswordsAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var accounts = await scope.ServiceProvider.GetRequiredService<DetectDefaultPasswordsUseCase>().ExecuteAsync();
        if (accounts.Count > 0)
        {
            app.Logger.LogWarning(
                "Tài khoản mẫu vẫn dùng mật khẩu mặc định: {Accounts}. Hãy đổi mật khẩu hoặc xóa tài khoản mẫu trước khi triển khai thật.",
                string.Join(", ", accounts));
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Không kiểm tra được mật khẩu mặc định của tài khoản mẫu khi khởi động.");
    }
}

/// <summary>Để project kiểm thử tích hợp (WebApplicationFactory) tham chiếu được điểm vào của ứng dụng.</summary>
public partial class Program
{
}

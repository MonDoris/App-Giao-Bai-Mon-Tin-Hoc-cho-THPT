using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace TinHoc12.Web.Tests;

/// <summary>Dùng chung một ứng dụng đã cấu hình CORS cho http://localhost:5173 (AC-491).</summary>
public sealed class CorsFixture : IDisposable
{
    public const string Origin = "http://localhost:5173";

    public AppFactory Factory { get; } = AppFactory.With(("Cors:AllowedOrigins:0", Origin));

    public void Dispose() => Factory.Dispose();
}

/// <summary>CORS cho frontend tách riêng (QD-89, AC-490 đến AC-497).</summary>
public class CorsTests : IClassFixture<CorsFixture>
{
    private const string Allowed = CorsFixture.Origin;
    private readonly AppFactory _factory;

    public CorsTests(CorsFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? origin, string? preflightMethod = null, string? preflightHeaders = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        if (preflightMethod is not null)
        {
            request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", preflightMethod);
        }

        if (preflightHeaders is not null)
        {
            request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", preflightHeaders);
        }

        return request;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    [Fact]
    public async Task Allowed_origin_gets_exact_origin_credentials_and_vary()
    {
        // AC-491
        var response = await _factory.NewBrowser().Http.SendAsync(Request(HttpMethod.Get, "/api/auth/csrf", Allowed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Allowed, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
        Assert.Contains("Origin", Header(response, "Vary")!);
    }

    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("http://localhost:5174")]
    [InlineData("https://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("null")]
    public async Task Unlisted_origins_get_no_cors_headers_on_requests_or_preflight(string origin)
    {
        // AC-492
        var browser = _factory.NewBrowser();
        var simple = await browser.Http.SendAsync(Request(HttpMethod.Get, "/api/auth/csrf", origin));
        var preflight = await browser.Http.SendAsync(Request(HttpMethod.Options, "/api/teacher/lessons", origin, "PUT", "content-type,x-csrf-token"));

        foreach (var response in new[] { simple, preflight })
        {
            Assert.Null(Header(response, "Access-Control-Allow-Origin"));
            Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
        }
    }

    [Fact]
    public async Task Preflight_from_allowed_origin_lists_methods_headers_and_max_age_only_for_whitelisted_headers()
    {
        // AC-493
        var browser = _factory.NewBrowser();
        var response = await browser.Http.SendAsync(Request(HttpMethod.Options, "/api/teacher/lessons", Allowed, "PUT", "content-type,x-csrf-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Allowed, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
        var methods = Header(response, "Access-Control-Allow-Methods")!;
        foreach (var method in new[] { "GET", "POST", "PUT", "PATCH", "DELETE" })
        {
            Assert.Contains(method, methods);
        }

        var headers = Header(response, "Access-Control-Allow-Headers")!;
        Assert.Contains("Content-Type", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-CSRF-TOKEN", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("600", Header(response, "Access-Control-Max-Age"));

        var evil = await browser.Http.SendAsync(Request(HttpMethod.Options, "/api/teacher/lessons", Allowed, "PUT", "x-evil"));
        Assert.DoesNotContain("x-evil", Header(evil, "Access-Control-Allow-Headers") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_needs_no_login_and_no_csrf_token()
    {
        // AC-494 (phần xác thực/CSRF)
        var response = await _factory.NewBrowser().Http.SendAsync(Request(HttpMethod.Options, "/api/teacher/lessons", Allowed, "POST", "content-type,x-csrf-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Preflight_requests_do_not_count_towards_the_login_rate_limit()
    {
        // AC-494 (phần giới hạn đăng nhập)
        using var factory = AppFactory.With(("Auth:LoginRatePerMinute", "3"), ("Cors:AllowedOrigins:0", Allowed));
        var browser = factory.NewBrowser();
        for (var i = 0; i < 10; i++)
        {
            var preflight = await browser.Http.SendAsync(Request(HttpMethod.Options, "/api/auth/login", Allowed, "POST", "content-type,x-csrf-token"));
            Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        }

        var login = await browser.LoginAsync("hs01", Accounts.StudentPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Error_responses_from_allowed_origin_still_carry_cors_headers()
    {
        // AC-495: 401, 403, 400 (CSRF), và 404/422 do ngoại lệ (đi qua ApiExceptionMiddleware, nơi Response.Clear() chạy)
        var anonymous = _factory.NewBrowser();
        var unauthorized = await anonymous.Http.SendAsync(Request(HttpMethod.Get, "/api/auth/me", Allowed));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(Allowed, Header(unauthorized, "Access-Control-Allow-Origin"));

        var noCsrf = await anonymous.Http.SendAsync(Request(HttpMethod.Post, "/api/auth/logout", Allowed));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        Assert.Equal("CSRF_INVALID", (await TestBrowser.JsonAsync(noCsrf))["error"].Str());
        Assert.Equal(Allowed, Header(noCsrf, "Access-Control-Allow-Origin"));

        var student = _factory.NewBrowser();
        await student.LoginOkAsync("hs01", Accounts.StudentPassword);
        var forbidden = await student.Http.SendAsync(Request(HttpMethod.Get, "/api/teacher/lessons", Allowed));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(Allowed, Header(forbidden, "Access-Control-Allow-Origin"));

        var missing = await student.Http.SendAsync(Request(HttpMethod.Get, "/api/lessons/khong-co", Allowed));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(Allowed, Header(missing, "Access-Control-Allow-Origin"));
        Assert.Equal("DENY", Header(missing, "X-Frame-Options"));

        var teacher = _factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var invalid = await teacher.PostAsync("/api/teacher/lessons", new { title = "x" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var invalidWithOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/lessons")
        {
            Content = JsonContent.Create(new { title = "x" }),
        };
        invalidWithOrigin.Headers.TryAddWithoutValidation("Origin", Allowed);
        invalidWithOrigin.Headers.Add("X-CSRF-TOKEN", await teacher.CsrfAsync());
        var withHeaders = await teacher.Http.SendAsync(invalidWithOrigin);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withHeaders.StatusCode);
        Assert.Equal(Allowed, Header(withHeaders, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(withHeaders, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Rate_limited_login_exposes_retry_after_to_the_allowed_origin()
    {
        // AC-495 (429)
        using var factory = AppFactory.With(("Auth:LoginRatePerMinute", "3"), ("Cors:AllowedOrigins:0", Allowed));
        var browser = factory.NewBrowser();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            var token = await browser.CsrfAsync(refresh: true);
            var request = Request(HttpMethod.Post, "/api/auth/login", Allowed);
            request.Content = JsonContent.Create(new { username = "hs01", password = "SaiMatKhau" + i });
            request.Headers.Add("X-CSRF-TOKEN", token);
            last = await browser.Http.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Equal(Allowed, Header(last, "Access-Control-Allow-Origin"));
        Assert.Contains("Retry-After", Header(last, "Access-Control-Expose-Headers")!);
        Assert.NotNull(Header(last, "Retry-After"));
    }

    [Fact]
    public async Task Empty_allowed_origins_means_no_cross_origin_request_gets_cors_headers()
    {
        // AC-490: appsettings.json đặt mảng rỗng; môi trường Staging không nạp appsettings.Development.json
        using var factory = AppFactory.With(("environment", "Staging"));
        var response = await factory.NewBrowser().Http.SendAsync(Request(HttpMethod.Get, "/api/auth/csrf", Allowed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
        Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void Config_files_declare_the_cors_section()
    {
        // AC-490
        var settings = File.ReadAllText(Path.Combine(RepoRoot.Path, "src", "Web", "appsettings.json"));
        var development = File.ReadAllText(Path.Combine(RepoRoot.Path, "src", "Web", "appsettings.Development.json"));
        Assert.Matches(@"""Cors""\s*:\s*\{\s*""AllowedOrigins""\s*:\s*\[\s*\]", settings);
        Assert.Matches(@"""Cors""\s*:\s*\{\s*""AllowedOrigins""\s*:\s*\[\s*""http://localhost:5173""\s*\]", development);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("localhost:5173")]
    [InlineData("ftp://x.example")]
    [InlineData("http://localhost:5173/")]
    [InlineData("http://localhost:5173/app")]
    [InlineData("http://localhost:5173?x=1")]
    public void Invalid_origin_configuration_stops_startup_with_a_clear_message(string origin)
    {
        // AC-496
        using var factory = AppFactory.With(("Cors:AllowedOrigins:0", origin));
        var ex = Assert.ThrowsAny<Exception>(() => factory.NewBrowser());
        Assert.Contains("Cors:AllowedOrigins", (ex.InnerException ?? ex).Message);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com:8443")]
    public async Task Valid_origin_configuration_starts_and_is_honoured(string origin)
    {
        // AC-496
        using var factory = AppFactory.With(("Cors:AllowedOrigins:0", origin));
        var response = await factory.NewBrowser().Http.SendAsync(Request(HttpMethod.Get, "/api/auth/csrf", origin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(origin, Header(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Backend_code_has_no_wildcard_origin_and_use_cors_precedes_auth_csrf_gate_and_rate_limiter()
    {
        // AC-497
        var web = Path.Combine(RepoRoot.Path, "src", "Web");
        foreach (var file in Directory.EnumerateFiles(web, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("AllowAnyOrigin", text);
            Assert.DoesNotContain("WithOrigins(\"*\")", text);
            Assert.DoesNotMatch(@"SetIsOriginAllowed\(\s*\w*\s*=>\s*true", text);
        }

        var program = File.ReadAllText(Path.Combine(web, "Program.cs"));
        var cors = program.IndexOf("app.UseCors(", StringComparison.Ordinal);
        Assert.True(cors > 0);
        foreach (var later in new[]
        {
            "app.UseAuthentication()", "app.UseMiddleware<CsrfMiddleware>()", "app.UseMiddleware<PasswordChangeGateMiddleware>()",
            "app.UseAuthorization()", "app.UseRateLimiter()", "app.UseMiddleware<RequestLimitsMiddleware>()",
        })
        {
            var index = program.IndexOf(later, StringComparison.Ordinal);
            Assert.True(index > cors, $"{later} phải đứng sau UseCors");
        }
    }

    [Fact]
    public void Cookies_stay_samesite_strict_and_httponly()
    {
        // AC-503
        var web = Path.Combine(RepoRoot.Path, "src", "Web");
        foreach (var file in Directory.EnumerateFiles(web, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("SameSiteMode.None", text);
            Assert.DoesNotContain("SameSiteMode.Lax", text);
            Assert.DoesNotContain("SameSiteMode.Unspecified", text);
        }

        var program = File.ReadAllText(Path.Combine(web, "Program.cs"));
        Assert.Equal(2, Regex.Matches(program, @"SameSite = SameSiteMode\.Strict").Count);
        Assert.Equal(2, Regex.Matches(program, @"HttpOnly = true").Count);
        Assert.Equal(2, Regex.Matches(program, @"SecurePolicy = secureCookies").Count);
    }
}

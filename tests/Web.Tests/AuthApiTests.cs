using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;

namespace TinHoc12.Web.Tests;

/// <summary>Đăng nhập, cookie phiên, CSRF, khóa tạm, đăng xuất, đổi mật khẩu (AC-81..94, 96, 98, 99, 103).</summary>
public class AuthApiTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public AuthApiTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Csrf_endpoint_is_public_and_returns_token_and_header_name()
    {
        var browser = _factory.NewBrowser();
        var response = await browser.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await TestBrowser.JsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(json["token"].Str()));
        Assert.Equal("X-CSRF-TOKEN", json["headerName"].Str());
    }

    [Theory]
    [InlineData("hs01", "HocSinh@2026", "Student", "/dashboard.html")]
    [InlineData("giaovien", "GiaoVien@2026", "Teacher", "/teacher/dashboard.html")]
    [InlineData("  HS01 ", "HocSinh@2026", "Student", "/dashboard.html")]
    public async Task Login_succeeds_with_role_redirect_and_no_secrets(string user, string password, string role, string redirect)
    {
        var browser = _factory.NewBrowser();
        await _factory.NewBrowser().LoginAsync("hs01", "khoi-dong-nong-123"); // làm nóng (JIT, khởi tạo DB) rồi mới đo
        var sw = Stopwatch.StartNew();
        var response = await browser.LoginAsync(user, password);
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"Đăng nhập mất {sw.Elapsed}");
        var text = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(text)!;
        Assert.Equal(role, json["user"]!["role"].Str());
        Assert.Equal(redirect, json["redirectUrl"].Str());
        Assert.False(json["user"]!["mustChangePassword"]!.GetValue<bool>());
        Assert.Equal(new[] { "displayName", "id", "mustChangePassword", "role", "username" }, json["user"]!.AsObject().Select(p => p.Key).OrderBy(k => k).ToArray());
        Assert.DoesNotContain("PBKDF2", text);
        Assert.DoesNotContain("securityStamp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, text);
    }

    [Fact]
    public async Task Session_cookie_is_httponly_samesite_strict_with_fixed_name()
    {
        var browser = _factory.NewBrowser();
        var response = await browser.LoginAsync("hs01", Accounts.StudentPassword);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("dkedu.auth=", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Return_url_is_honoured_only_when_safe_and_allowed_for_the_role()
    {
        var student = _factory.NewBrowser();
        var ok = await TestBrowser.JsonAsync(await student.LoginAsync("hs01", Accounts.StudentPassword, "/problem.html?lesson=a&problem=b"));
        Assert.Equal("/problem.html?lesson=a&problem=b", ok["redirectUrl"].Str());

        foreach (var bad in new[] { "//evil.com", "https://evil.com", "/\\evil.com", "javascript:alert(1)", "/teacher/index.html", "data:text/html,x" })
        {
            var json = await TestBrowser.JsonAsync(await _factory.NewBrowser().LoginAsync("hs01", Accounts.StudentPassword, bad));
            Assert.Equal("/dashboard.html", json["redirectUrl"].Str());
        }

        var teacher = await TestBrowser.JsonAsync(await _factory.NewBrowser().LoginAsync("giaovien", Accounts.TeacherPassword, "/teacher/progress.html"));
        Assert.Equal("/teacher/progress.html", teacher["redirectUrl"].Str());
    }

    [Fact]
    public async Task All_failure_causes_return_the_same_401_body_and_headers()
    {
        var teacher = _factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var create = await TestStudents.CreateAsync(teacher, "khoa-" + Guid.NewGuid().ToString("N")[..6], "Em Bi Khoa", "Matkhau2026");
        await teacher.PatchAsync($"/api/teacher/students/{create.Id}", new { isActive = false });

        var results = new List<(HttpStatusCode Status, string Body, string Headers)>();
        foreach (var (user, pass) in new[] { ("khong-ton-tai", "Abcdef12"), ("hs02", "SaiMatKhau1"), (create.Username, "Matkhau2026") })
        {
            var response = await _factory.NewBrowser().LoginAsync(user, pass);
            var headers = string.Join("|", response.Headers.Where(h => h.Key != "Date" && !h.Key.StartsWith("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(h => h.Key + "=" + string.Join(",", h.Value)));
            results.Add((response.StatusCode, await response.Content.ReadAsStringAsync(), headers));
        }

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Unauthorized, r.Status));
        Assert.Single(results.Select(r => r.Body).Distinct());
        Assert.Single(results.Select(r => r.Headers).Distinct());
        var body = JsonNode.Parse(results[0].Body)!;
        Assert.Equal("INVALID_CREDENTIALS", body["error"].Str());
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.", body["message"].Str());
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        var browser = _factory.NewBrowser();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.LoginAsync("hs03", "Sai" + i + "xxxxxx")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.LoginAsync("hs03", Accounts.StudentPassword)).StatusCode);
        _factory.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.OK, (await browser.LoginAsync("hs03", Accounts.StudentPassword)).StatusCode);
    }

    [Theory]
    [InlineData("", "abc12345")]
    [InlineData("   ", "abc12345")]
    [InlineData("hs01", "")]
    [InlineData("hs01", "    ")]
    public async Task Empty_input_gives_422_with_field_errors(string user, string pass)
    {
        var response = await _factory.NewBrowser().LoginAsync(user, pass);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await TestBrowser.JsonAsync(response);
        Assert.Equal("VALIDATION_FAILED", json["error"].Str());
        Assert.NotEmpty(json["errors"]!.AsArray());
    }

    [Fact]
    public async Task Overlong_input_is_422_oversized_body_is_413_and_sql_text_is_just_401()
    {
        var browser = _factory.NewBrowser();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await browser.LoginAsync(new string('a', 51), "abc12345")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await browser.LoginAsync("hs01", new string('a', 129))).StatusCode);
        var big = await browser.PostAsync("/api/auth/login", "{\"username\":\"hs01\",\"password\":\"" + new string('a', 6000) + "\"}");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, big.StatusCode);
        Assert.Equal("PAYLOAD_TOO_LARGE", (await TestBrowser.JsonAsync(big))["error"].Str());
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.LoginAsync("' OR 1=1--", "' OR '1'='1")).StatusCode);
        var malformed = await browser.PostAsync("/api/auth/login", "{ not json");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, malformed.StatusCode);
        var wrongType = await browser.PostAsync("/api/auth/login", "{\"username\":123,\"password\":true}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongType.StatusCode);
    }

    [Fact]
    public async Task Writes_without_or_with_wrong_csrf_token_are_rejected_without_side_effects()
    {
        var browser = _factory.NewBrowser();
        var missing = await browser.PostAsync("/api/auth/login", new { username = "hs01", password = Accounts.StudentPassword }, csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("CSRF_INVALID", (await TestBrowser.JsonAsync(missing))["error"].Str());

        var wrong = await browser.SendAsync(HttpMethod.Post, "/api/auth/login", new { username = "hs01", password = Accounts.StudentPassword }, token: "token-sai");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);

        var foreign = _factory.NewBrowser();
        var token = await browser.CsrfAsync(refresh: true);
        var noCookie = await foreign.SendAsync(HttpMethod.Post, "/api/auth/login", new { username = "hs01", password = Accounts.StudentPassword }, token: token);
        Assert.Equal(HttpStatusCode.BadRequest, noCookie.StatusCode);
    }

    [Fact]
    public async Task Every_write_method_needs_csrf_and_a_token_from_another_identity_is_rejected()
    {
        var teacher = _factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            var response = await teacher.SendAsync(method, "/api/teacher/classes/1", new { name = "x" }, csrf: false);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var anonymous = _factory.NewBrowser();
        var anonymousToken = await anonymous.CsrfAsync();
        var bound = await teacher.SendAsync(HttpMethod.Post, "/api/teacher/classes", new { name = "Lop-" + Guid.NewGuid().ToString("N")[..5] }, token: anonymousToken);
        Assert.Equal(HttpStatusCode.BadRequest, bound.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await teacher.PostAsync("/api/teacher/classes", new { name = "Lop-" + Guid.NewGuid().ToString("N")[..5] })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync("/api/teacher/classes")).StatusCode);
    }

    [Fact]
    public async Task Me_requires_login_and_is_never_cached()
    {
        var browser = _factory.NewBrowser();
        var unauthenticated = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("UNAUTHENTICATED", (await TestBrowser.JsonAsync(unauthenticated))["error"].Str());

        await browser.LoginOkAsync("hs01", Accounts.StudentPassword);
        var me = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("no-store", me.Headers.CacheControl!.ToString());
        var json = await TestBrowser.JsonAsync(me);
        Assert.Equal("hs01", json["username"].Str());
        Assert.Equal("Student", json["role"].Str());
    }

    [Fact]
    public async Task Logout_revokes_the_session_even_for_a_copied_cookie_and_is_idempotent()
    {
        var browser = _factory.NewBrowser();
        var login = await browser.LoginAsync("hs02", Accounts.StudentPassword);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("dkedu.auth=", StringComparison.Ordinal)).Split(';')[0];

        var logout = await browser.PostAsync("/api/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("dkedu.auth=;", StringComparison.Ordinal) || c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        browser.ForgetCsrf();
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);

        var replay = _factory.NewBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.Http.SendAsync(request)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/auth/logout")).StatusCode);
        await browser.LoginOkAsync("hs02", Accounts.StudentPassword);
    }

    [Fact]
    public async Task Logging_out_in_one_tab_signs_out_every_tab_of_the_same_account()
    {
        var tabA = _factory.NewBrowser();
        var tabB = _factory.NewBrowser();
        await tabA.LoginOkAsync("hs03", Accounts.StudentPassword);
        await tabB.LoginOkAsync("hs03", Accounts.StudentPassword);
        // Đăng nhập lần hai KHÔNG đổi SecurityStamp nên cả hai phiên cùng tồn tại.
        Assert.Equal(HttpStatusCode.OK, (await tabB.GetAsync("/api/auth/me")).StatusCode);

        await tabA.LogoutAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await tabB.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tabB.GetAsync("/api/lessons")).StatusCode);
    }

    [Fact]
    public async Task Sessions_expire_after_the_absolute_lifetime_of_10_hours()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginOkAsync("hs02", Accounts.StudentPassword);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);

        _factory.Clock.Advance(TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(1)));

        var expired = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("UNAUTHENTICATED", (await TestBrowser.JsonAsync(expired))["error"].Str());
        _factory.Clock.Advance(TimeSpan.FromHours(-10).Add(TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public async Task There_is_no_self_registration()
    {
        var browser = _factory.NewBrowser();
        foreach (var path in new[] { "/api/auth/register", "/api/auth/signup", "/api/users", "/api/register" })
        {
            var response = await browser.PostAsync(path, new { username = "hacker", password = "Abcdef12", role = "Teacher" });
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized, $"{path} -> {response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/auth/register", new { username = "hacker" })).StatusCode);
        // Backend chỉ còn là API (AC-499): không có trang đăng ký nào ở thư mục frontend độc lập (AC-527).
        Assert.False(File.Exists(Path.Combine(RepoRoot.Path, "frontend", "register.html")));
    }

    [Fact]
    public async Task Security_headers_are_present_on_api_and_error_responses()
    {
        var browser = _factory.NewBrowser();
        foreach (var url in new[] { "/api/auth/csrf", "/api/auth/me", "/api/lessons", "/api/khong-co", "/khong-co" })
        {
            var response = await browser.GetAsync(url);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
            if (url.StartsWith("/api", StringComparison.Ordinal))
            {
                Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            }
        }
    }

    [Fact]
    public async Task Errors_are_always_json_with_error_and_message()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginOkAsync("hs01", Accounts.StudentPassword);
        foreach (var (url, status) in new[]
        {
            ("/api/lessons/khong-co", HttpStatusCode.NotFound),
            ("/api/khong-co", HttpStatusCode.NotFound),
            ("/api/lessons/", HttpStatusCode.NotFound),
            ("/api/teacher/lessons", HttpStatusCode.Forbidden),
            ("/api/teacher/khong-co", HttpStatusCode.Forbidden),
        })
        {
            var response = await browser.GetAsync(url);
            Assert.Equal(status, response.StatusCode);
            Assert.StartsWith("application/json", response.Content.Headers.ContentType!.MediaType);
            var json = await TestBrowser.JsonAsync(response);
            Assert.False(string.IsNullOrWhiteSpace(json["error"].Str()));
            Assert.False(string.IsNullOrWhiteSpace(json["message"].Str()));
            var text = json.ToJsonString();
            Assert.DoesNotContain("StackTrace", text);
            Assert.DoesNotContain("Ser" + "ver=", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}

internal static class TestStudents
{
    public sealed record Created(int Id, string Username, string? TemporaryPassword);

    public static async Task<Created> CreateAsync(TestBrowser teacher, string username, string name, string? password = null, int[]? classIds = null)
    {
        var response = await teacher.PostAsync("/api/teacher/students", new { username, displayName = name, classIds, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await TestBrowser.JsonAsync(response);
        return new Created(json["id"].Int(), json["username"].Str(), json["temporaryPassword"]?.GetValue<string>());
    }
}

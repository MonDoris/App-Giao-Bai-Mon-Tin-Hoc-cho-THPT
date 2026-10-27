using System.Net;
using System.Text.Json.Nodes;

namespace TinHoc12.Web.Tests;

/// <summary>Ma trận endpoint x {chưa đăng nhập, Student, Teacher} (AC-96, 97, mục 5.3).</summary>
public class AuthorizationMatrixTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public AuthorizationMatrixTests(AppFactory factory)
    {
        _factory = factory;
    }

    private static readonly (string Method, string Url, object? Body, int Student, int Teacher)[] Matrix =
    {
        ("GET", "/api/lessons", null, 200, 200),
        ("GET", "/api/lessons/bai-01-nhap-xuat", null, 200, 200),
        ("GET", "/api/lessons/khong-co", null, 404, 404),
        ("GET", "/api/exercises/bai-01-tong-hai-so/solution", null, 200, 200),
        ("GET", "/api/exercises/bai-03-dem-uoc/solution", null, 200, 200),
        ("GET", "/api/me/assignments", null, 200, 403),
        ("GET", "/api/me/exercises/bai-01-tong-hai-so/submissions", null, 200, 403),
        ("GET", "/api/me/submissions/999999", null, 404, 403),
        ("GET", "/api/me/khong-co", null, 404, 403),
        ("POST", "/api/exercises/bai-01-tong-hai-so/submissions", new { code = "print(1)", passedTests = 0, totalTests = 1 }, 409, 403),
        ("GET", "/api/teacher/lessons", null, 403, 200),
        ("GET", "/api/teacher/lessons/bai-01-nhap-xuat", null, 403, 200),
        ("GET", "/api/teacher/exercises/bai-01-tong-hai-so", null, 403, 200),
        ("GET", "/api/teacher/assignments", null, 403, 200),
        ("GET", "/api/teacher/progress?lessonId=bai-01-nhap-xuat", null, 403, 200),
        ("GET", "/api/teacher/progress/students/1/exercises/bai-01-tong-hai-so/submissions", null, 403, 404),
        ("GET", "/api/teacher/students", null, 403, 200),
        ("GET", "/api/teacher/classes", null, 403, 200),
        ("POST", "/api/teacher/students", new { username = "ma-tran-01", displayName = "Ma Trận" }, 403, 201),
        ("POST", "/api/teacher/students/import", new { rows = new[] { new { username = "ma-tran-02", displayName = "MT" } } }, 403, 200),
        ("POST", "/api/teacher/classes", new { name = "MaTran" }, 403, 201),
        ("POST", "/api/teacher/lessons", new { title = "x" }, 403, 422),
        ("POST", "/api/teacher/lessons/reorder", new { ids = new[] { "a" } }, 403, 422),
        ("POST", "/api/teacher/assignments", new { }, 403, 422),
        ("PATCH", "/api/teacher/students/1", new { isActive = false }, 403, 403),
        ("PATCH", "/api/teacher/students/987654", new { isActive = false }, 403, 404),
        ("DELETE", "/api/teacher/students/1", null, 403, 404),
        ("DELETE", "/api/teacher/lessons/khong-co", null, 403, 404),
        ("GET", "/api/teacher/khong-co", null, 403, 404),
        ("GET", "/api/khong-co", null, 404, 404),
    };

    public static IEnumerable<object[]> Rows => Matrix.Select((m, i) => new object[] { i });

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Endpoint_matrix(int index)
    {
        var (method, url, body, student, teacher) = Matrix[index];
        var http = new HttpMethod(method);

        var anonymous = _factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(http, url, body)).StatusCode);

        var s = _factory.NewBrowser();
        await s.LoginOkAsync("hs01", Accounts.StudentPassword);
        var studentResponse = await s.SendAsync(http, url, body);
        Assert.Equal((HttpStatusCode)student, studentResponse.StatusCode);

        var t = _factory.NewBrowser();
        await t.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var teacherResponse = await t.SendAsync(http, url, body);
        Assert.Equal((HttpStatusCode)teacher, teacherResponse.StatusCode);

        if (studentResponse.StatusCode == HttpStatusCode.Forbidden)
        {
            var json = await TestBrowser.JsonAsync(studentResponse);
            Assert.Equal("FORBIDDEN", json["error"].Str());
            Assert.Equal(2, json.AsObject().Count);
        }
    }

    [Fact]
    public async Task Role_cannot_be_chosen_by_the_client_when_creating_students()
    {
        var teacher = _factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var response = await teacher.PostAsync("/api/teacher/students", new { username = "vai-tro-01", displayName = "Em", role = "Teacher", isActive = false });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await TestBrowser.JsonAsync(response);
        var list = await TestBrowser.JsonAsync(await teacher.GetAsync("/api/teacher/students?search=vai-tro-01"));
        Assert.Single(list["students"]!.AsArray());
        Assert.True(list["students"]![0]!["isActive"]!.GetValue<bool>());
        Assert.Equal("vai-tro-01", created["username"].Str());

        var asStudent = _factory.NewBrowser();
        var login = await TestBrowser.JsonAsync(await asStudent.LoginAsync("vai-tro-01", created["temporaryPassword"].Str()));
        Assert.Equal("Student", login["user"]!["role"].Str());
        Assert.True(login["user"]!["mustChangePassword"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Teachers_cannot_modify_other_teachers_through_the_student_api()
    {
        var teacher = _factory.NewBrowser();
        await teacher.LoginOkAsync(Accounts.Teacher, Accounts.TeacherPassword);
        var me = await TestBrowser.JsonAsync(await teacher.GetAsync("/api/auth/me"));
        var id = me["id"].Int();

        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PatchAsync($"/api/teacher/students/{id}", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/teacher/students/{id}/reset-password")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/teacher/students/{id}/unlock")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.PostAsync("/api/teacher/students/987654/reset-password")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync("/api/auth/me")).StatusCode);
    }
}

public class RateLimitAndConfigTests
{
    [Fact]
    public async Task Login_rate_limit_returns_429_with_retry_after()
    {
        using var factory = AppFactory.With(("Auth:LoginRatePerMinute", "3"));
        var browser = factory.NewBrowser();

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await browser.LoginAsync("hs01", "SaiMatKhau" + i);
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
        Assert.True(last!.Headers.Contains("Retry-After"));
        var json = await TestBrowser.JsonAsync(last);
        Assert.Equal("TOO_MANY_REQUESTS", json["error"].Str());
    }

    [Fact]
    public async Task Production_over_plain_http_redirects_to_https_and_never_returns_500()
    {
        using var factory = AppFactory.With(("environment", "Production"), ("Https:Port", "8443"));
        var browser = factory.NewBrowser();

        var csrf = await browser.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, csrf.StatusCode);
        Assert.StartsWith("https://", csrf.Headers.Location!.OriginalString);
        Assert.Contains(":8443", csrf.Headers.Location.OriginalString);
        var page = await browser.GetAsync("/api/lessons");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, page.StatusCode);
        Assert.False(csrf.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Development_over_http_works_and_sends_no_hsts()
    {
        using var factory = new AppFactory();
        var response = await factory.NewBrowser().GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData("Auth:IdleMinutes", "0")]
    [InlineData("Auth:MaxFailedAttempts", "-1")]
    [InlineData("Auth:LockoutMinutes", "0")]
    [InlineData("Auth:AbsoluteHours", "0")]
    [InlineData("Auth:LoginRatePerMinute", "0")]
    [InlineData("Auth:SubmissionMinIntervalSeconds", "0")]
    public void Invalid_auth_configuration_stops_startup_with_a_clear_message(string key, string value)
    {
        using var factory = AppFactory.With((key, value));
        var ex = Assert.ThrowsAny<Exception>(() => factory.NewBrowser());
        var message = (ex.InnerException ?? ex).Message;
        Assert.Contains(key, message);
    }

    [Fact]
    public async Task Configured_lockout_threshold_is_honoured()
    {
        using var factory = AppFactory.With(("Auth:MaxFailedAttempts", "2"));
        var browser = factory.NewBrowser();
        await browser.LoginAsync("hs01", "Sai1xxxxxx");
        await browser.LoginAsync("hs01", "Sai2xxxxxx");
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.LoginAsync("hs01", Accounts.StudentPassword)).StatusCode);
    }
}

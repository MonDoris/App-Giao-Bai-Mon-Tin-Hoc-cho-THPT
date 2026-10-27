using System.Net;
using System.Text.RegularExpressions;

namespace TinHoc12.Web.Tests;

/// <summary>Backend chỉ còn là API (QD-87, QD-95, AC-498 đến AC-502).</summary>
public class ApiOnlyTests : IClassFixture<AppFactory>
{
    private static readonly string[] StaticUrls =
    {
        "/", "/index.html", "/login.html", "/lessons.html", "/dashboard.html", "/teacher/index.html",
        "/css/style.css", "/js/api.js", "/fonts/OFL.txt", "/img/logo-dk-edu.png",
    };

    private readonly AppFactory _factory;

    public ApiOnlyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Html_and_static_files_are_404_for_anonymous_and_signed_in_users_with_security_headers()
    {
        // AC-499
        var student = _factory.NewBrowser();
        await student.LoginOkAsync("hs01", Accounts.StudentPassword);
        foreach (var browser in new[] { _factory.NewBrowser(), student })
        {
            foreach (var url in StaticUrls)
            {
                var response = await browser.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} -> {response.StatusCode}");
                Assert.Null(response.Headers.Location);
                Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
                Assert.DoesNotContain("<html", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
                Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
                Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
                Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
            }
        }
    }

    [Fact]
    public void Backend_code_no_longer_serves_the_ui_and_page_guard_is_gone()
    {
        // AC-498, AC-530
        var web = Path.Combine(RepoRoot.Path, "src", "Web");
        Assert.False(Directory.Exists(Path.Combine(web, "wwwroot")));
        Assert.False(File.Exists(Path.Combine(web, "Middleware", "PageGuard" + "Middleware.cs")));
        var folders = new[] { Path.Combine(RepoRoot.Path, "src"), Path.Combine(RepoRoot.Path, "tests") };
        foreach (var file in folders.SelectMany(f => Directory.EnumerateFiles(f, "*.cs", SearchOption.AllDirectories))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                              && !f.EndsWith("ApiOnlyTests.cs", StringComparison.Ordinal)
                              // Nơi duy nhất được phục vụ tệp tĩnh: chế độ dev Frontend:ServeFromBackend (mặc định tắt).
                              && !f.EndsWith($"{Path.DirectorySeparatorChar}FrontendHosting.cs", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            foreach (var banned in new[] { "UseDefault" + "Files", "UseStatic" + "Files", "StaticFile" + "Options", "FileExtensionContent" + "TypeProvider", "DefaultFiles" + "Options", "PageGuard" + "Middleware" })
            {
                Assert.False(text.Contains(banned, StringComparison.Ordinal), $"{Path.GetFileName(file)} còn nhắc {banned}");
            }
        }
    }

    [Fact]
    public void Endpoint_count_is_unchanged_at_43()
    {
        // AC-500: Auth 5, Content 7, Teacher 31
        var endpoints = Path.Combine(RepoRoot.Path, "src", "Web", "Endpoints");
        int Count(string file) => Regex.Matches(File.ReadAllText(Path.Combine(endpoints, file)), @"\.Map(Get|Post|Put|Patch|Delete)\(").Count;
        Assert.Equal(5, Count("AuthEndpoints.cs"));
        Assert.Equal(7, Count("ContentEndpoints.cs"));
        Assert.Equal(31, Count("TeacherEndpoints.cs"));
    }

    [Fact]
    public async Task Api_responses_are_never_cached()
    {
        // AC-500
        var response = await _factory.NewBrowser().GetAsync("/api/auth/csrf");
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Login_redirect_url_is_always_a_relative_path()
    {
        // AC-502
        foreach (var (user, returnUrl) in new[] { ("hs01", (string?)null), ("hs01", "https://evil.com"), ("giaovien", "/teacher/progress.html"), ("hs01", "//evil.com") })
        {
            var json = await TestBrowser.JsonAsync(await _factory.NewBrowser().LoginAsync(user, user == "hs01" ? Accounts.StudentPassword : Accounts.TeacherPassword, returnUrl));
            var redirect = json["redirectUrl"].Str();
            Assert.Matches(@"^/[^/\\]", redirect);
            Assert.DoesNotContain("://", redirect);
        }
    }
}

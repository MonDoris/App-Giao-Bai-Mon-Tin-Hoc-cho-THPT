using System.Net;

namespace TinHoc12.Web.Tests;

/// <summary>Dùng chung một ứng dụng bật Frontend:ServeFromBackend (chế độ `dotnet run` ở Development).</summary>
public sealed class FrontendHostingFixture : IDisposable
{
    public AppFactory Factory { get; } = AppFactory.With(("Frontend:ServeFromBackend", "true"));

    public void Dispose() => Factory.Dispose();
}

/// <summary>Backend phục vụ luôn thư mục frontend/ khi bật cờ; /api không đổi.</summary>
public class FrontendHostingTests : IClassFixture<FrontendHostingFixture>
{
    private readonly AppFactory _factory;

    public FrontendHostingTests(FrontendHostingFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Theory]
    [InlineData("/", "text/html", "frontend/index.html")]
    [InlineData("/login.html", "text/html", "frontend/login.html")]
    [InlineData("/teacher/", "text/html", "frontend/teacher/index.html")]
    [InlineData("/js/core/api.js", "text/javascript", "frontend/js/core/api.js")]
    [InlineData("/css/style.css", "text/css", "frontend/css/style.css")]
    public async Task Frontend_files_are_served_with_correct_type_security_headers_and_no_cache(string url, string mediaType, string file)
    {
        var response = await _factory.NewBrowser().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(RepoRoot.Path, file)), await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Theory]
    [InlineData("/khong-co.html")]
    [InlineData("/js/")]
    [InlineData("/../src/Web/appsettings.json")]
    [InlineData("/%2e%2e/src/Web/appsettings.json")]
    public async Task Missing_files_directories_and_paths_outside_frontend_are_404(string url)
    {
        var response = await _factory.NewBrowser().GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Api_still_works_and_is_not_cached()
    {
        var anonymous = await _factory.NewBrowser().GetAsync("/api/lessons");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var student = _factory.NewBrowser();
        await student.LoginOkAsync("hs01", Accounts.StudentPassword);
        var response = await student.GetAsync("/api/lessons");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public void Missing_frontend_folder_stops_startup_with_a_clear_message()
    {
        using var factory = AppFactory.With(("Frontend:ServeFromBackend", "true"), ("Frontend:Path", "../../khong-co-thu-muc"));
        var ex = Assert.ThrowsAny<Exception>(() => factory.NewBrowser());
        Assert.Contains("Frontend:Path", ex.ToString());
    }
}

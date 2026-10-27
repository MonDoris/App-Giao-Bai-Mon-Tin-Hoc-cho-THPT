using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinHoc12.Application.Abstractions;

namespace TinHoc12.Web.Tests;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;

    public void Advance(TimeSpan span) => UtcNow += span;
}

internal static class RepoRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "src", "TinHoc12.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo.");
    }
}

/// <summary>Ứng dụng thật chạy trên SQLite in-memory, nạp seed mẫu bằng chính script seed (không cần SQL Server).</summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keeper;
    private readonly Dictionary<string, string?> _settings = new();

    public FakeClock Clock { get; } = new();

    private static string ConnectionFor(string name) =>
        new SqliteConnectionStringBuilder { DataSource = name, Mode = SqliteOpenMode.Memory, Cache = SqliteCacheMode.Shared }.ToString();

    public AppFactory() : this(Array.Empty<(string, string)>())
    {
    }

    /// <summary>Tạo ứng dụng với một số cấu hình ghi đè (ví dụ Auth:LoginRatePerMinute).</summary>
    public static AppFactory With(params (string Key, string Value)[] settings) => new(settings);

    private AppFactory(IEnumerable<(string Key, string Value)> settings)
    {
        var name = "webtest_" + Guid.NewGuid().ToString("N");
        _keeper = new SqliteConnection(ConnectionFor(name));
        _keeper.Open();
        var database = System.IO.Path.Combine(RepoRoot.Path, "database");
        _settings["Database:Provider"] = "Sqlite";
        _settings["ConnectionStrings:DefaultConnection"] = ConnectionFor(name);
        _settings["Database:SeedScript"] = System.IO.Path.Combine(database, "sqlite/seed-sample.sqlite.sql");
        _settings["Database:SeedUsersScript"] = System.IO.Path.Combine(database, "sqlite/seed-users.sqlite.sql");
        _settings["Auth:LoginRatePerMinute"] = "100000";
        // Test mặc định kiểm tra backend API-only; chế độ phục vụ frontend được bật riêng trong FrontendHostingTests.
        _settings["Frontend:ServeFromBackend"] = "false";
        _settings["Frontend:OpenBrowser"] = "false";
        foreach (var (key, value) in settings)
        {
            _settings[key] = value;
        }
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment(_settings.GetValueOrDefault("environment") ?? "Development");
        foreach (var (key, value) in _settings.Where(p => p.Key != "environment"))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keeper.Dispose();
        }
    }

    public TestBrowser NewBrowser() => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }), Clock);

    public T Resolve<T>(Func<IServiceProvider, T> use)
    {
        using var scope = Services.CreateScope();
        return use(scope.ServiceProvider);
    }
}

/// <summary>Một trình duyệt giả: giữ cookie, tự lấy và gửi token CSRF cho các yêu cầu ghi.</summary>
public sealed class TestBrowser
{
    private string? _csrf;

    public HttpClient Http { get; }
    public FakeClock Clock { get; }

    public TestBrowser(HttpClient http, FakeClock clock)
    {
        Http = http;
        Clock = clock;
    }

    public async Task<string> CsrfAsync(bool refresh = false)
    {
        if (_csrf is null || refresh)
        {
            var response = await Http.GetAsync("/api/auth/csrf");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonObject>();
            _csrf = json!["token"]!.GetValue<string>();
        }

        return _csrf;
    }

    public void ForgetCsrf() => _csrf = null;

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool csrf = true, string? token = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = body is string raw
                ? new StringContent(raw, System.Text.Encoding.UTF8, "application/json")
                : JsonContent.Create(body);
        }

        if (csrf)
        {
            request.Headers.Add("X-CSRF-TOKEN", token ?? await CsrfAsync());
        }

        return await Http.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool csrf = true) => SendAsync(HttpMethod.Post, url, body, csrf);

    public Task<HttpResponseMessage> PutAsync(string url, object? body) => SendAsync(HttpMethod.Put, url, body);

    public Task<HttpResponseMessage> PatchAsync(string url, object? body) => SendAsync(HttpMethod.Patch, url, body);

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url);

    public async Task<HttpResponseMessage> LoginAsync(string username, string password, string? returnUrl = null)
    {
        var response = await PostAsync("/api/auth/login", new { username, password, returnUrl });
        ForgetCsrf();
        return response;
    }

    public async Task<TestBrowser> LoginOkAsync(string username, string password)
    {
        var response = await LoginAsync(username, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return this;
    }

    public async Task LogoutAsync()
    {
        var response = await PostAsync("/api/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        ForgetCsrf();
    }

    public static async Task<JsonNode> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text) ?? throw new InvalidOperationException("Phản hồi không phải JSON: " + text);
    }
}

internal static class Json
{
    public static JsonSerializerOptions Web { get; } = new(JsonSerializerDefaults.Web);

    public static string Str(this JsonNode? node) => node!.GetValue<string>();

    public static int Int(this JsonNode? node) => node!.GetValue<int>();
}

internal static class Accounts
{
    public const string Teacher = "giaovien";
    public const string TeacherPassword = "GiaoVien@2026";
    public const string StudentPassword = "HocSinh@2026";
}

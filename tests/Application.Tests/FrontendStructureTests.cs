using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TinHoc12.Application.Tests;

/// <summary>
/// Quét tĩnh cấu trúc frontend độc lập (v6): đổi tên trang, tài nguyên, cấu hình API, cổng gọi API duy nhất,
/// guard phía trình duyệt, cờ đăng nhập và manifest (AC-477 đến AC-489, AC-509 đến AC-516, AC-482).
/// </summary>
public class FrontendStructureTests
{
    private static string Frontend => RepoPaths.Frontend;

    private static string[] HtmlFiles => Directory.GetFiles(Frontend, "*.html", SearchOption.AllDirectories);

    private static string[] JsFiles => Directory.GetFiles(Frontend, "*.js", SearchOption.AllDirectories);

    private static string Rel(string path) => Path.GetRelativePath(Frontend, path).Replace('\\', '/');

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Frontend, relative));

    /// <summary>Mã JS bỏ chú thích khối và các dòng chú thích bắt đầu bằng //.</summary>
    private static string Code(string file)
    {
        var text = Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join('\n', text.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static string CodeOf(string relative) => Code(Path.Combine(Frontend, relative));

    private static readonly string[] GuardedPages =
    {
        "lessons.html", "dashboard.html", "assignments.html", "change-password.html", "lesson.html", "problem.html",
        "teacher/index.html", "teacher/dashboard.html", "teacher/lesson-edit.html", "teacher/exercise-edit.html",
        "teacher/assignments.html", "teacher/progress.html", "teacher/students.html",
    };

    // ------------------------------------------------------------------ AC-477, AC-478, AC-481: cấu trúc
    [Fact]
    public void Frontend_folder_holds_every_asset_and_the_old_web_root_is_gone()
    {
        Assert.True(Directory.Exists(Frontend));
        Assert.False(Directory.Exists(Path.Combine(RepoPaths.Src, "Web", "wwwroot")));
        Assert.Equal(15, HtmlFiles.Length);
        Assert.Equal(42, JsFiles.Length);
        // js/ chia theo vai trò: core (nền tảng), components (khung + widget), pages (từng trang), code (soạn/chạy Python), teacher
        Assert.Empty(Directory.GetFiles(Path.Combine(Frontend, "js"), "*.js"));
        Assert.Equal(new[] { "code", "components", "core", "pages", "teacher" }, Directory.GetDirectories(Path.Combine(Frontend, "js")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(13, Directory.GetFiles(Path.Combine(Frontend, "js", "core"), "*.js").Length);
        Assert.Equal(5, Directory.GetFiles(Path.Combine(Frontend, "js", "components"), "*.js").Length);
        Assert.Equal(9, Directory.GetFiles(Path.Combine(Frontend, "js", "pages"), "*.js").Length);
        Assert.Equal(4, Directory.GetFiles(Path.Combine(Frontend, "js", "code"), "*.js").Length);
        Assert.Equal(11, Directory.GetFiles(Path.Combine(Frontend, "js", "teacher"), "*.js").Length);
        Assert.Single(Directory.GetFiles(Path.Combine(Frontend, "css")), f => Path.GetFileName(f) == "style.css");
        Assert.Equal(13, Directory.GetFiles(Path.Combine(Frontend, "fonts")).Length);
        Assert.Equal(5, Directory.GetFiles(Path.Combine(Frontend, "img"), "*.png").Length);
        Assert.Single(RepoPaths.Files(RepoPaths.Root, "style.css"));
        Assert.Single(RepoPaths.Files(RepoPaths.Root, "api.js"), f => f.StartsWith(Frontend, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Renamed_pages_have_the_expected_titles_and_scripts()
    {
        Assert.False(File.Exists(Path.Combine(Frontend, "landing.html")));
        var landing = Read("index.html");
        Assert.Contains("<title>Học Tin học 12 với Python – DK Edu</title>", landing);
        Assert.Single(Regex.Matches(landing, "<h1[ >]"));
        Assert.Contains("src=\"/js/pages/landing.js\"", landing);

        var lessons = Read("lessons.html");
        Assert.Contains("<title>Danh sách bài học – DK Edu</title>", lessons);
        Assert.Contains("src=\"/js/pages/lessons.js\"", lessons);
        Assert.Contains("<title>Bài học của tôi – DK Edu</title>", Read("teacher/index.html"));
    }

    [Fact]
    public void There_are_no_build_tools_package_files_or_stray_scripts()
    {
        // AC-481
        Assert.Empty(Directory.GetFiles(Frontend, "*.json", SearchOption.AllDirectories));
        foreach (var name in new[] { "package.json", "package-lock.json", "tsconfig.json", ".babelrc" })
        {
            Assert.False(File.Exists(Path.Combine(Frontend, name)), name);
            Assert.False(File.Exists(Path.Combine(RepoPaths.Root, name)), name);
        }

        foreach (var pattern in new[] { "vite.config.*", "webpack.config.*", "rollup.config.*" })
        {
            Assert.Empty(Directory.GetFiles(Frontend, pattern, SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(RepoPaths.Root, pattern));
        }

        Assert.False(Directory.Exists(Path.Combine(Frontend, "node_modules")));
        Assert.False(Directory.Exists(Path.Combine(RepoPaths.Root, "node_modules")));
        Assert.False(Directory.Exists(Path.Combine(Frontend, "data")));
        Assert.False(Directory.Exists(Path.Combine(Frontend, "dist")));
        Assert.Empty(Directory.GetFiles(Frontend, "*.mjs", SearchOption.AllDirectories));
        var modules = RepoPaths.Files(RepoPaths.Root, "*.mjs").Select(f => Path.GetRelativePath(RepoPaths.Root, f).Replace('\\', '/')).ToArray();
        Assert.Equal(new[] { "tests/frontend/guard.test.mjs" }, modules);
    }

    // ------------------------------------------------------------------ AC-479: liên kết nội bộ
    [Fact]
    public void Internal_links_follow_the_renamed_pages()
    {
        foreach (var file in JsFiles)
        {
            var code = Code(file);
            Assert.DoesNotContain("landing.html", code);
            Assert.DoesNotMatch(@"(?<![A-Za-z0-9_-])/index\.html", code);
        }

        foreach (var file in HtmlFiles)
        {
            var text = Regex.Replace(File.ReadAllText(file), "<!--.*?-->", string.Empty, RegexOptions.Singleline);
            Assert.DoesNotContain("landing.html", text);
            Assert.DoesNotMatch(@"(?<![A-Za-z0-9_-])/index\.html", text);
        }

        Assert.Equal(2, Regex.Matches(CodeOf("js/components/header.js"), "'/lessons.html'").Count);
        foreach (var file in new[] { "js/pages/assignments.js", "js/pages/dashboard.js", "js/pages/problem.js", "js/pages/lesson.js" })
        {
            Assert.Contains("/lessons.html", CodeOf(file));
        }

        Assert.DoesNotContain("landing.html", Regex.Replace(Read("css/style.css"), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline));
        Assert.DoesNotContain("landing.html", Read("css/style.css"));
    }

    // ------------------------------------------------------------------ AC-480: mọi tài nguyên tham chiếu đều có thật
    [Fact]
    public void Every_referenced_css_js_img_and_font_file_exists()
    {
        var missing = new List<string>();
        foreach (var file in JsFiles.Concat(HtmlFiles).Concat(Directory.GetFiles(Frontend, "*.css", SearchOption.AllDirectories)))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"[""'(\s]((/(css|js|img|fonts)/)[A-Za-z0-9_./@-]+)"))
            {
                var target = m.Groups[1].Value.TrimEnd('.', '/');
                if (!File.Exists(Path.Combine(Frontend, target.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))))
                {
                    missing.Add($"{Rel(file)} -> {target}");
                }
            }
        }

        Assert.Empty(missing);
    }

    // ------------------------------------------------------------------ AC-482: manifest bảo toàn nội dung
    [Fact]
    public void Manifest_matches_every_unmodified_file_and_covers_all_74_original_files()
    {
        var manifest = File.ReadAllLines(Path.Combine(RepoPaths.Root, "docs", "frontend-manifest.sha256")).Where(l => l.Trim().Length > 0).ToArray();
        Assert.Equal(74, manifest.Length);
        var mismatched = new List<string>();
        foreach (var line in manifest)
        {
            var match = Regex.Match(line, @"^([0-9a-f]{64})  (\S+)  ->  (\S+)(  MODIFIED)?$");
            Assert.True(match.Success, line);
            var path = Path.Combine(Frontend, match.Groups[3].Value.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), match.Groups[3].Value);
            if (!match.Groups[4].Success && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() != match.Groups[1].Value)
            {
                mismatched.Add(match.Groups[3].Value);
            }
        }

        Assert.Empty(mismatched);
        // Tệp ngoài manifest chỉ là hai tệp mới (AC-477)
        var listed = manifest.Select(l => Regex.Match(l, @"->  (\S+)").Groups[1].Value).ToHashSet();
        var extra = Directory.GetFiles(Frontend, "*", SearchOption.AllDirectories).Select(Rel).Where(f => !listed.Contains(f)).OrderBy(f => f).ToArray();
        Assert.Equal(new[] { "img/hero-classroom.jpg", "js/core/config.js", "js/core/guard.js" }, extra);
    }

    // ------------------------------------------------------------------ AC-484 đến AC-488: cấu hình và cổng gọi API
    [Fact]
    public void Config_module_exports_only_api_base_with_no_logic()
    {
        var code = CodeOf("js/core/config.js");
        Assert.Matches(@"^\s*export const API_BASE = 'http://localhost:5000';\s*$", code);
        Assert.DoesNotMatch(@"location|localStorage|sessionStorage|document|window|import", code);
        var raw = Read("js/core/config.js");
        Assert.Contains("Cors:AllowedOrigins", raw);
        Assert.Contains("''", raw);
    }

    [Fact]
    public void Api_module_is_the_only_gateway_and_always_uses_api_base_with_credentials_include()
    {
        var api = CodeOf("js/core/api.js");
        Assert.Contains("import { API_BASE } from '/js/core/config.js';", api);
        Assert.Contains("function apiUrl(path)", api);
        Assert.Equal(3, Regex.Matches(api, @"\bfetch\(").Count);
        Assert.Equal(3, Regex.Matches(api, @"\bfetch\(apiUrl\(").Count);
        Assert.Equal(3, Regex.Matches(api, @"credentials: 'include'").Count);
        Assert.Equal(3, Regex.Matches(api, @"cache: 'no-store'").Count);
        Assert.Contains("'X-CSRF-TOKEN'", api);
        Assert.Equal(2, Regex.Matches(api, @"\bAPI_BASE\b").Count); // import + apiUrl

        foreach (var file in JsFiles)
        {
            var code = Code(file);
            Assert.DoesNotMatch(@"credentials:\s*'(same-origin|omit)'", code);
            Assert.DoesNotMatch(@"XMLHttpRequest|navigator\.sendBeacon|EventSource|WebSocket", code);
            if (Rel(file) == "js/core/api.js")
            {
                continue;
            }

            Assert.DoesNotMatch(@"\bfetch\(", code);
            Assert.DoesNotMatch(@"[""'`]/api/", code);
            if (Rel(file) is not "js/core/config.js")
            {
                Assert.DoesNotMatch(@"\bAPI_BASE\b", code);
            }
        }
    }

    [Fact]
    public void Network_failures_are_reported_and_never_leave_a_blank_page()
    {
        // AC-489
        var api = CodeOf("js/core/api.js");
        Assert.Contains("new ApiError({ kind: 'network', message: 'Không kết nối được máy chủ' })", api);
        var auth = CodeOf("js/core/auth.js");
        var fatal = auth[auth.IndexOf("function renderFatal", StringComparison.Ordinal)..];
        Assert.Contains("removeAttribute('data-guard')", fatal[..fatal.IndexOf("main.append(box)", StringComparison.Ordinal)]);
        Assert.Contains("Không kết nối được máy chủ, hãy kiểm tra kết nối rồi thử lại.", CodeOf("js/pages/login.js"));
    }

    // ------------------------------------------------------------------ AC-507, AC-508: guard.js
    [Fact]
    public void Guard_module_is_dependency_free_and_exports_the_shared_rules()
    {
        var guard = CodeOf("js/core/guard.js");
        Assert.DoesNotMatch(@"^\s*import\s", guard.Replace("\r", string.Empty));
        Assert.DoesNotContain("import ", guard);
        foreach (var name in new[] { "sanitizeReturnUrl", "homeFor", "redirectIfSignedIn", "onRestoreFromCache", "isSafeRedirect", "joinApiUrl" })
        {
            Assert.Contains($"export function {name}(", guard.Replace("export async function", "export function"));
        }

        Assert.Contains("export { homeFor };", CodeOf("js/core/api.js"));
        Assert.DoesNotContain("function homeFor", CodeOf("js/core/api.js"));
        Assert.Contains("pageshow", guard);
        Assert.Contains("e.persisted", guard);
    }

    [Fact]
    public void Role_declarations_match_the_guard_table()
    {
        // AC-508: 7 trang giáo viên khai báo Teacher; dashboard và assignments của học sinh khai báo Student
        foreach (var file in Directory.GetFiles(Path.Combine(Frontend, "js", "teacher"), "*.js"))
        {
            var code = Code(file);
            if (code.Contains("initPage(", StringComparison.Ordinal))
            {
                Assert.Contains("role: 'Teacher'", code);
            }
        }

        Assert.Contains("role: 'Student'", CodeOf("js/pages/dashboard.js"));
        Assert.Contains("role: 'Student'", CodeOf("js/pages/assignments.js"));
        var auth = CodeOf("js/core/auth.js");
        Assert.Contains("location.replace('/change-password.html')", auth);
        Assert.Contains("location.replace('/dashboard.html?notice=forbidden')", auth);
        Assert.Contains("location.replace('/teacher/dashboard.html')", auth);
    }

    // ------------------------------------------------------------------ AC-509, AC-510, AC-511: chống nhá nội dung
    [Fact]
    public void Exactly_the_13_protected_pages_carry_data_guard_and_a_noscript_fallback()
    {
        var guarded = HtmlFiles.Where(f => Regex.IsMatch(File.ReadAllText(f), @"<html lang=""vi"" data-guard>")).Select(Rel).OrderBy(x => x).ToArray();
        Assert.Equal(GuardedPages.OrderBy(x => x).ToArray(), guarded);
        Assert.Equal(13, guarded.Length);
        foreach (var page in new[] { "index.html", "login.html" })
        {
            Assert.DoesNotContain("data-guard", Read(page));
        }

        foreach (var page in GuardedPages)
        {
            var text = Read(page);
            Assert.Contains("<noscript><style>html[data-guard] body{visibility:visible}</style></noscript>", text);
            Assert.Single(Regex.Matches(text, "<noscript>"));
            Assert.False(Regex.IsMatch(text, @"<script(?![^>]*src=)[^>]*>"), page);
        }

        var css = Read("css/style.css");
        var rule = Regex.Match(css, @"[^\n]*\n\s*html\[data-guard\] body \{ visibility: hidden; \}");
        Assert.True(rule.Success);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(rule.Value) <= 300);
        Assert.DoesNotContain("!important", rule.Value);
        Assert.True(css.IndexOf("html[data-guard] body", StringComparison.Ordinal) < css.IndexOf("*, *::before, *::after", StringComparison.Ordinal));
    }

    [Fact]
    public void Init_page_reveals_the_page_only_after_success_and_each_protected_page_calls_it_once()
    {
        var auth = CodeOf("js/core/auth.js");
        var init = auth[auth.IndexOf("export async function initPage", StringComparison.Ordinal)..auth.IndexOf("function renderFatal", StringComparison.Ordinal)];
        var reveal = init.IndexOf("removeAttribute('data-guard')", StringComparison.Ordinal);
        Assert.True(reveal > 0);
        Assert.Single(Regex.Matches(init, "removeAttribute\\('data-guard'\\)"));
        Assert.True(reveal > init.LastIndexOf("location.replace(", StringComparison.Ordinal), "không được bỏ ẩn trước các nhánh chuyển hướng");
        Assert.True(reveal < init.IndexOf("renderHeader(", StringComparison.Ordinal));
        Assert.Contains("onRestoreFromCache();", init);

        foreach (var page in GuardedPages)
        {
            var module = Regex.Match(Read(page), @"<script type=""module"" src=""(/js/[^""]+)""").Groups[1].Value;
            var code = CodeOf(module.TrimStart('/'));
            Assert.Single(Regex.Matches(code, @"\binitPage\("));
            Assert.Matches(@"await initPage\(", code);
        }
    }

    // ------------------------------------------------------------------ AC-512: cờ th12.signedin
    [Fact]
    public void Signed_in_hint_lives_only_in_storage_js_and_holds_no_identity()
    {
        var storage = CodeOf("js/core/storage.js");
        foreach (var name in new[] { "markSignedIn", "clearSignedIn", "hasSignedInHint" })
        {
            Assert.Contains($"export function {name}()", storage);
        }

        Assert.Contains("'th12.signedin'", storage);
        Assert.Contains("setItem(SIGNEDIN_KEY, '1')", storage);
        var keys = Regex.Matches(storage + CodeOf("js/core/theme-init.js"), @"['""`]th12\.([a-z]+)['""`]").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "sidebar", "signedin", "theme" }, keys);

        var api = CodeOf("js/core/api.js");
        Assert.Contains("markSignedIn();", api);
        Assert.Equal(3, Regex.Matches(api, @"\bclearSignedIn\(\);").Count); // goToLogin, mọi 401, logout (kể cả khi API lỗi)
        Assert.Contains("markSignedIn();", CodeOf("js/core/auth.js"));
    }

    // ------------------------------------------------------------------ AC-513 đến AC-516: login, landing, 401
    [Fact]
    public void Login_and_landing_only_ask_the_server_when_the_hint_is_set_and_never_use_api_base()
    {
        var landing = CodeOf("js/pages/landing.js");
        var login = CodeOf("js/pages/login.js");
        foreach (var code in new[] { landing, login })
        {
            Assert.Contains("redirectIfSignedIn({ me: api.me, hasHint: hasSignedInHint, clearHint: clearSignedIn", code);
            Assert.Contains("onRestoreFromCache();", code);
            Assert.DoesNotContain("API_BASE", code);
            Assert.DoesNotContain("/js/core/auth.js", code);
            Assert.DoesNotContain("/js/components/header.js", code);
        }

        Assert.All(Regex.Matches(landing, @"(?<![/\w])api\.\w+").Select(m => m.Value), v => Assert.Equal("api.me", v));
        Assert.Contains("isSafeRedirect(result.redirectUrl) ? result.redirectUrl : homeFor(result.user)", login);
        Assert.DoesNotContain("returnUrl: returnUrl()", landing);
    }

    [Fact]
    public void Go_to_login_skips_return_url_on_public_pages_and_marks_expiry_only_after_a_real_session()
    {
        var api = CodeOf("js/core/api.js");
        var go = api[api.IndexOf("export function goToLogin", StringComparison.Ordinal)..api.IndexOf("async function readBody", StringComparison.Ordinal)];
        Assert.Contains("const wasSignedIn = hasSignedInHint();", go);
        Assert.Contains("clearSignedIn();", go);
        Assert.Contains("expired && wasSignedIn", go);
        Assert.Contains("if (redirecting) return;", go);
        Assert.Contains("startsWith('/login.html')", go);
    }
}

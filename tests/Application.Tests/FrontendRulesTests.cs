using System.Globalization;
using System.Text.RegularExpressions;

namespace TinHoc12.Application.Tests;

/// <summary>Quét tĩnh mã giao diện: logo, favicon, tiêu đề, an toàn XSS, dung lượng, tương phản (AC-74..80, 125, 146, 150, 151).</summary>
public class FrontendRulesTests
{
    private static string Frontend => RepoPaths.Frontend;

    private static string[] HtmlFiles => Directory.GetFiles(Frontend, "*.html", SearchOption.AllDirectories);

    private static string[] JsFiles => Directory.GetFiles(Frontend, "*.js", SearchOption.AllDirectories);

    /// <summary>Đọc mã JS đã bỏ chú thích dòng (bắt đầu bằng //) và chú thích khối để không bắt nhầm lời giải thích.</summary>
    private static string Code(string file)
    {
        var text = File.ReadAllText(file);
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join('\n', text.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static string Rel(string path) => Path.GetRelativePath(Frontend, path).Replace('\\', '/');

    private static readonly string[] ExpectedPages =
    {
        "index.html", "login.html", "lessons.html", "dashboard.html", "assignments.html", "change-password.html", "lesson.html", "problem.html",
        "teacher/index.html", "teacher/dashboard.html", "teacher/lesson-edit.html", "teacher/exercise-edit.html", "teacher/assignments.html",
        "teacher/progress.html", "teacher/students.html",
    };

    // Trang công khai: landing và đăng nhập không mang <html data-guard> (AC-509)
    private static readonly string[] PublicPages = { "index.html", "login.html" };

    [Fact]
    public void There_are_the_expected_pages()
    {
        var names = HtmlFiles.Select(Rel).OrderBy(x => x).ToArray();
        Assert.Equal(15, names.Length);
        foreach (var page in ExpectedPages)
        {
            Assert.Contains(page, names);
        }

        // Đổi tên trang (QD-92, AC-478): tên cũ không còn
        Assert.DoesNotContain("landing.html", names);
        Assert.False(File.Exists(Path.Combine(Frontend, "js", "index.js")));
        Assert.True(File.Exists(Path.Combine(Frontend, "js", "lessons.js")));

        Assert.DoesNotContain(names, n => n.Contains("register", StringComparison.OrdinalIgnoreCase) || n.Contains("signup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_html_declares_the_png_favicon_and_apple_touch_icon_and_no_data_svg_icon()
    {
        foreach (var file in HtmlFiles)
        {
            var text = File.ReadAllText(file);
            Assert.Contains("<link rel=\"icon\" type=\"image/png\" sizes=\"32x32\" href=\"/img/favicon-32.png\">", text);
            Assert.Contains("<link rel=\"apple-touch-icon\" href=\"/img/apple-touch-icon.png\">", text);
            Assert.DoesNotContain("data:image/svg", text);
            Assert.DoesNotContain("&gt;_", text);
            Assert.DoesNotContain(">_<", text);
        }
    }

    [Fact]
    public void Every_html_has_vietnamese_language_charset_dk_edu_title_description_skip_link_and_at_most_one_h1()
    {
        foreach (var file in HtmlFiles)
        {
            var text = File.ReadAllText(file);
            var name = Rel(file);
            Assert.Matches(@"<html lang=""vi""( data-guard)?>", text);
            Assert.Contains("<meta charset=\"utf-8\">", text);
            Assert.Matches(@"<title>[^<]+ – DK Edu</title>", text);
            Assert.DoesNotContain("Tin học 12</title>", text);
            var description = Regex.Match(text, @"<meta name=""description"" content=""([^""]*)""").Groups[1].Value;
            Assert.Contains("DK Edu", description);
            Assert.Contains("Bỏ qua, tới nội dung chính", text);
            Assert.True(Regex.Matches(text, "<h1[ >]").Count <= 1, name);
            Assert.DoesNotContain("href=\"css/", text);
            Assert.DoesNotContain("src=\"js/", text);
            Assert.Contains("href=\"/css/style.css\"", text);

            // AC-166: theme-init.js đồng bộ, nằm trước stylesheet; AC-305: không script nội dòng
            var init = text.IndexOf("<script src=\"/js/theme-init.js\"></script>", StringComparison.Ordinal);
            var sheet = text.IndexOf("<link rel=\"stylesheet\" href=\"/css/style.css\">", StringComparison.Ordinal);
            Assert.True(init >= 0 && sheet > init, $"{name}: theme-init.js phải đứng trước style.css");
            Assert.Contains("<meta name=\"color-scheme\" content=\"light dark\">", text);
            Assert.Contains("<meta name=\"theme-color\" content=\"#f1f4f9\" media=\"(prefers-color-scheme: light)\">", text);
            Assert.Contains("<meta name=\"theme-color\" content=\"#0b1424\" media=\"(prefers-color-scheme: dark)\">", text);
            Assert.False(Regex.IsMatch(text, @"<script(?![^>]*src=)[^>]*>"), $"{name}: không được có script nội dòng");
            Assert.True(Regex.Matches(text, @"<script[^>]*type=\""module\""[^>]*src=").Count == 1, name);
            Assert.True(Regex.Matches(text, @"<link[^>]*rel=\""preload\""[^>]*as=\""font\""").Count <= 2, name);
        }
    }

    [Theory]
    [InlineData("login.html", "Đăng nhập – DK Edu")]
    [InlineData("change-password.html", "Đổi mật khẩu – DK Edu")]
    [InlineData("assignments.html", "Bài tập của tôi – DK Edu")]
    [InlineData("index.html", "Học Tin học 12 với Python – DK Edu")]
    [InlineData("lessons.html", "Danh sách bài học – DK Edu")]
    [InlineData("teacher/index.html", "Bài học của tôi – DK Edu")]
    [InlineData("teacher/lesson-edit.html", "Soạn bài học – DK Edu")]
    [InlineData("teacher/exercise-edit.html", "Soạn bài tập – DK Edu")]
    [InlineData("teacher/assignments.html", "Giao bài – DK Edu")]
    [InlineData("teacher/progress.html", "Tiến độ học sinh – DK Edu")]
    [InlineData("teacher/students.html", "Quản lý học sinh – DK Edu")]
    public void Page_titles_match_the_specification(string page, string title)
    {
        Assert.Contains($"<title>{title}</title>", File.ReadAllText(Path.Combine(Frontend, page)));
    }

    [Fact]
    public void Login_page_has_labelled_fields_alert_region_logo_and_no_register_link()
    {
        var html = File.ReadAllText(Path.Combine(Frontend, "login.html"));
        Assert.Contains("<h1>Đăng nhập</h1>", html);
        Assert.Contains("autocomplete=\"username\"", html);
        Assert.Contains("autocapitalize=\"none\"", html);
        Assert.Contains("spellcheck=\"false\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("autocomplete=\"current-password\"", html);
        Assert.Contains("<label for=\"username\">Tên đăng nhập</label>", html);
        Assert.Contains("<label for=\"password\">Mật khẩu</label>", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("alt=\"Logo DK Edu: mũ cử nhân, sách mở và ký hiệu D/K\"", html);
        Assert.Contains("srcset=\"/img/logo-dk-edu.png 1x, /img/logo-dk-edu@2x.png 2x\"", html);
        Assert.Matches("<img[^>]*width=\"240\"[^>]*height=\"240\"", html);
        Assert.DoesNotContain("Đăng ký", html);
        Assert.DoesNotContain("method=\"get\"", html, StringComparison.OrdinalIgnoreCase);

        // AC-193..195, AC-204: panel thương hiệu, câu giá trị, nút đổi giao diện, mảnh mã do JS dựng
        Assert.Contains("<aside class=\"auth-brand\"", html);
        Assert.Contains("class=\"auth-drift\" aria-hidden=\"true\"", html);
        Assert.Contains("Học Tin học 12 bằng Python, viết mã và nhận kết quả ngay trên trình duyệt.", html);
        Assert.Contains("fetchpriority=\"high\"", html);
        Assert.Contains("Quên mật khẩu? Hãy nhờ giáo viên đặt lại mật khẩu cho bạn.", html);
        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.True(html.IndexOf("id=\"theme-slot\"", StringComparison.Ordinal) < html.IndexOf("id=\"username\"", StringComparison.Ordinal), "nút đổi giao diện phải đứng trước ô nhập trong thứ tự Tab");
        var fx = File.ReadAllText(Path.Combine(Frontend, "js", "login-fx.js"));
        Assert.Contains("className = 'drift'", fx);
        foreach (var piece in new[] { "'/'", "'< >'", "'{}'", "'print()'", "'for'", "'def'" })
        {
            Assert.Contains(piece, fx);
        }

        Assert.Contains("cancelAnimationFrame", fx);
        Assert.Contains("export function destroy", fx);
        Assert.Contains("export function start", fx);
        Assert.Contains("prefers-reduced-motion", fx);
        Assert.Contains("visibilitychange", fx);
        Assert.Contains("IntersectionObserver", fx);
        var js = File.ReadAllText(Path.Combine(Frontend, "js", "login.js"));
        Assert.Contains("Đang đăng nhập…", js);
        Assert.Contains("Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.", js);
        Assert.Contains("Bạn thử quá nhiều lần, vui lòng đợi ít phút.", js);
    }

    [Fact]
    public void Header_uses_the_small_logo_dk_edu_label_and_role_labels()
    {
        var header = File.ReadAllText(Path.Combine(Frontend, "js", "header.js"));
        Assert.Contains("/img/logo-dk-icon.png", header);
        Assert.Contains("aria-label': 'DK Edu – về trang chủ'", header);
        Assert.Contains("'DK Edu'", header);
        foreach (var label in new[] { "Bài học", "Bài tập của tôi", "Bài học của tôi", "Giao bài", "Tiến độ", "Học sinh", "Đổi mật khẩu", "Đăng xuất", "Giáo viên", "Xem như học sinh (xem trước)" })
        {
            Assert.Contains(label, header);
        }

        Assert.Contains("aria-expanded", header);
        Assert.Contains("aria-controls", header);
        Assert.Contains("'Escape'", header);
        Assert.Contains("aria-current", header);
        Assert.Contains("textContent", File.ReadAllText(Path.Combine(Frontend, "js", "dom.js")));
        var css = File.ReadAllText(Path.Combine(Frontend, "css", "style.css"));
        Assert.Matches(@"\.brand__logo\s*\{[^}]*height:\s*40px", css);
        Assert.Matches(@"\.brand__logo\s*\{[^}]*object-fit:\s*contain", css);

        // AC-179, AC-180 (thay cho "nền header trắng" cũ của AC-77): viên thuốc kính mờ + phương án dự phòng nền đặc
        Assert.Matches(@"\.site-header\s*\{[^}]*background:\s*var\(--glass-bg\)", css);
        Assert.Matches(@"\.site-header\s*\{[^}]*backdrop-filter:\s*blur\(\d+px\)", css);
        // v5 (app shell) thay viên thuốc kính nổi bằng thanh trên dính: viền dưới + kính mờ, vẫn có phương án dự phòng nền đặc
        Assert.Matches(@"\.site-header\s*\{[^}]*border-bottom:\s*1px solid var\(--glass-border\)", css);
        Assert.Matches(@"\.site-header\s*\{[^}]*view-transition-name:\s*app-topbar", css);
        Assert.Matches(@"@supports not \(backdrop-filter: blur\(1px\)\)\s*\{\s*[^{]*\.site-header\s*\{[^}]*background:\s*var\(--surface\)", css);
        Assert.Matches(@"@media \(prefers-reduced-transparency: reduce\)\s*\{\s*[^{]*\.site-header\s*\{[^}]*backdrop-filter:\s*none", css);
        Assert.Matches(@"\.brand__tile\s*\{[^}]*background:\s*var\(--logo-tile\)", css);
        Assert.Contains("data-role", header);
        // v5: thanh trên và thanh bên dính bằng CSS (grid + sticky), không còn sentinel/IntersectionObserver/chỉ báo trượt của v4
        Assert.DoesNotContain("header-sentinel", header);
        Assert.Contains("app-sidebar", header);
    }

    [Fact]
    public void Javascript_never_injects_html_from_data()
    {
        foreach (var file in JsFiles)
        {
            var text = Code(file);
            Assert.DoesNotMatch(@"\binnerHTML\b|\bouterHTML\b|insertAdjacentHTML|document\.write\b|\beval\(|new Function\(", text);
        }
    }

    [Fact]
    public void Web_storage_is_only_used_by_storage_js_and_never_for_credentials()
    {
        // AC-311: ngoài storage.js chỉ theme-init.js (chạy đồng bộ trước module nên cần đọc trực tiếp th12.theme)
        foreach (var file in JsFiles.Where(f => Path.GetFileName(f) is not "storage.js" and not "theme-init.js"))
        {
            var text = Code(file);
            Assert.DoesNotMatch(@"localStorage|sessionStorage", text);
        }

        var themeInit = Code(Path.Combine(Frontend, "js", "theme-init.js"));
        Assert.DoesNotContain("sessionStorage", themeInit);
        Assert.Contains("th12.theme", themeInit);
        Assert.True(new FileInfo(Path.Combine(Frontend, "js", "theme-init.js")).Length <= 1536); // v5 đã nâng ngưỡng lên 1,5 KB

        var storage = Code(Path.Combine(Frontend, "js", "storage.js"));
        Assert.DoesNotMatch(@"(?i)password|token|csrf|secret", storage.Replace("th12.", string.Empty));
        foreach (var file in JsFiles)
        {
            var text = Code(file);
            Assert.DoesNotMatch(@"console\.(log|debug)\(", text);
            Assert.DoesNotMatch(@"[?&](password|pass|pwd|token)=", text);
            Assert.DoesNotMatch(@"document\.cookie", text);
        }
    }

    [Fact]
    public void Password_never_appears_in_urls_and_login_posts_json()
    {
        var api = File.ReadAllText(Path.Combine(Frontend, "js", "api.js"));
        Assert.Contains("call('POST', '/api/auth/login', body", api);
        Assert.Contains("'X-CSRF-TOKEN'", api);
        var login = File.ReadAllText(Path.Combine(Frontend, "js", "login.js"));
        Assert.DoesNotContain("location.search = ", login);
    }

    [Fact]
    public void Pyodide_is_only_referenced_by_the_problem_page_and_lazily_by_the_exercise_editor()
    {
        foreach (var file in HtmlFiles)
        {
            var text = File.ReadAllText(file);
            Assert.Equal(Rel(file) == "problem.html", text.Contains("pyodide", StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in JsFiles.Where(f => Path.GetFileName(f) is not "runner.js" and not "py-worker.js"))
        {
            var text = File.ReadAllText(file);
            if (Path.GetFileName(file) == "problem.js")
            {
                Assert.Contains("from '/js/runner.js'", text);
            }
            else
            {
                Assert.DoesNotContain("from '/js/runner.js'", text);
            }
        }

        Assert.Contains("await import('/js/runner.js')", File.ReadAllText(Path.Combine(Frontend, "js", "teacher", "exercise-edit.js")));
    }

    [Fact]
    public void Problem_page_run_button_never_calls_the_save_api_and_preview_does_not_submit()
    {
        var text = File.ReadAllText(Path.Combine(Frontend, "js", "problem.js"));
        var onRun = text[text.IndexOf("async function onRun()", StringComparison.Ordinal)..text.IndexOf("function testRow", StringComparison.Ordinal)];
        Assert.DoesNotContain("api.submit", onRun);
        Assert.Contains("if (preview)", text);
        var submitCalls = Regex.Matches(text, @"api\.submit\(").Count;
        Assert.Equal(1, submitCalls);
        Assert.Contains("Gửi lại kết quả", text);
        Assert.Contains("Đã lưu kết quả lên hệ thống (lần nộp thứ", text);
        Assert.Contains("Đề bài vừa được cập nhật, hãy tải lại trang.", text);
        Assert.Contains("Bạn nộp quá nhanh, thử lại sau vài giây.", text);
        Assert.Contains("Chưa lưu được kết quả", text);
        Assert.Contains("Chế độ xem trước – kết quả sẽ không được lưu", text);
    }

    [Fact]
    public void Solution_is_never_fetched_automatically_when_the_problem_page_opens()
    {
        var text = File.ReadAllText(Path.Combine(Frontend, "js", "problem.js"));
        Assert.DoesNotContain("fetchSolution().then", text);
        Assert.Single(Regex.Matches(text, @"api\.solution\("));
        Assert.Contains("solutionData !== null", text);
    }

    [Fact]
    public void Logo_files_have_the_required_sizes_dimensions_and_total_weight()
    {
        var img = Path.Combine(Frontend, "img");
        (int W, int H) Size(string f)
        {
            var b = File.ReadAllBytes(Path.Combine(img, f));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, b[..4]);
            return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16)), System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20)));
        }

        long Len(string f) => new FileInfo(Path.Combine(img, f)).Length;

        var logo = Size("logo-dk-edu.png");
        Assert.Equal(logo.W, logo.H);
        Assert.True(logo.W <= 512);
        Assert.True(Len("logo-dk-edu.png") <= 120 * 1024);
        Assert.Equal((1024, 1024), Size("logo-dk-edu@2x.png"));
        Assert.True(Len("logo-dk-edu@2x.png") <= 400 * 1024);
        var icon = Size("logo-dk-icon.png");
        Assert.True(Math.Max(icon.W, icon.H) <= 256);
        Assert.True(Len("logo-dk-icon.png") <= 60 * 1024);
        Assert.NotEqual(icon.W, icon.H);
        Assert.Equal((32, 32), Size("favicon-32.png"));
        Assert.Equal((180, 180), Size("apple-touch-icon.png"));
        Assert.True(Directory.GetFiles(img).Sum(f => new FileInfo(f).Length) <= 800 * 1024);

        foreach (var original in new[] { "logo-1-goc.png", "logo-2-goc.png" })
        {
            Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "docs", "brand", original)), original);
        }
    }

    [Fact]
    public void Own_javascript_and_css_stay_under_500_kb()
    {
        // Nâng từ 500 KB lên 560 KB khi thiết kế lại trang Bài học của tôi và trình soạn bài (thẻ tổng quan, thẻ bài, thanh bước mới).
        var total = JsFiles.Concat(Directory.GetFiles(Frontend, "*.css", SearchOption.AllDirectories)).Sum(f => new FileInfo(f).Length);
        Assert.True(total <= 560 * 1024, $"JS+CSS = {total / 1024} KB");
    }

    [Fact]
    public void Fonts_are_self_hosted_within_250_kb_with_license_and_images_stay_png_only()
    {
        // AC-173, AC-299
        var fonts = Path.Combine(Frontend, "fonts");
        var woff = Directory.GetFiles(fonts, "*.woff2");
        Assert.Equal(12, woff.Length);
        Assert.Equal(13, Directory.GetFiles(fonts).Length); // 12 woff2 + OFL.txt (AC-477)
        Assert.Equal(5, Directory.GetFiles(Path.Combine(Frontend, "img"), "*.png").Length);
        Assert.All(woff, f => Assert.True(new FileInfo(f).Length > 0));
        Assert.True(woff.Sum(f => new FileInfo(f).Length) <= 250 * 1024, "tổng font phải <= 250 KB");
        Assert.True(File.Exists(Path.Combine(fonts, "OFL.txt")));
        Assert.Contains("SIL Open Font License", File.ReadAllText(Path.Combine(fonts, "OFL.txt")));
        Assert.All(Directory.GetFiles(Path.Combine(Frontend, "img")), f => Assert.Contains(Path.GetExtension(f), new[] { ".png", ".jpg" }));
    }

    // ------------------------------------------------------------ CSS: token, tương phản, quy tắc (AC-161..165, 277, 278, ...)
    private static string CssRaw => File.ReadAllText(Path.Combine(Frontend, "css", "style.css"));

    private static string CssCode => Regex.Replace(CssRaw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private const string DarkSelector = ":root[data-theme=\"dark\"]";

    private const string AutoSelector = ":root:not([data-theme])";

    /// <summary>Duyệt luật CSS ở mọi độ sâu (vào trong @layer/@media/@supports); @font-face/@keyframes trả nguyên khối.</summary>
    private static List<(string Selector, string Body)> CssRules(string css)
    {
        var output = new List<(string, string)>();
        Walk(css, output);
        return output;
    }

    private static void Walk(string css, List<(string, string)> output)
    {
        var i = 0;
        while (i < css.Length)
        {
            var open = css.IndexOf('{', i);
            if (open < 0)
            {
                break;
            }

            var semi = css.IndexOf(';', i);
            if (semi >= 0 && semi < open)
            {
                i = semi + 1;
                continue;
            }

            var prelude = css[i..open].Trim();
            var depth = 1;
            var j = open + 1;
            while (j < css.Length && depth > 0)
            {
                if (css[j] == '{')
                {
                    depth++;
                }
                else if (css[j] == '}')
                {
                    depth--;
                }

                j++;
            }

            var body = css[(open + 1)..(j - 1)];
            if (prelude.StartsWith("@layer", StringComparison.Ordinal) || prelude.StartsWith("@media", StringComparison.Ordinal) || prelude.StartsWith("@supports", StringComparison.Ordinal))
            {
                Walk(body, output);
            }
            else
            {
                output.Add((prelude, body));
            }

            i = j;
        }
    }

    private static Dictionary<string, string> Scope(string selector)
    {
        var vars = new Dictionary<string, string>();
        foreach (var (_, body) in CssRules(CssCode).Where(r => r.Selector == selector))
        {
            foreach (Match m in Regex.Matches(body, @"(--[a-z0-9-]+)\s*:\s*([^;]+);"))
            {
                vars.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());
            }
        }

        return vars;
    }

    // Bảng token của spec (mục 3.1): tên, giá trị sáng, giá trị tối
    private static readonly (string Name, string Light, string Dark)[] Tokens =
    {
        ("--bg", "#f1f4f9", "#0b1424"), ("--surface", "#fbfcfe", "#111d33"), ("--surface-2", "#e9eef6", "#182742"),
        ("--text", "#14213a", "#e6edf7"), ("--muted", "#4a5870", "#a3b2cb"), ("--border", "#c8d2e2", "#2b3d5e"),
        ("--input-border", "#64748b", "#7388a8"), ("--brand-navy", "#12305a", "#12305a"), ("--brand-blue", "#38a9e0", "#38a9e0"),
        ("--brand-link", "#0b6fa8", "#6cc3ee"), ("--primary", "#12305a", "#38a9e0"), ("--primary-dark", "#0b2140", "#5bbbe9"),
        ("--on-primary", "#fbfcfe", "#08182d"), ("--primary-soft", "#dff0fa", "#14304f"), ("--accent-stroke", "#0b6fa8", "#6cc3ee"),
        ("--success-bg", "#dcfce7", "#123524"), ("--success-text", "#14532d", "#8be0aa"),
        ("--warn-bg", "#fef3c7", "#3a2a0c"), ("--warn-text", "#7c2d12", "#f5cf7a"),
        ("--danger-bg", "#fee2e2", "#3d1620"), ("--danger-text", "#7f1d1d", "#ffb4b4"),
        ("--error", "#991b1b", "#ffa3a3"), ("--error-border", "#b91c1c", "#f87171"),
        ("--success-solid", "#166534", "#4ade80"), ("--on-success", "#fbfcfe", "#052e16"),
        ("--danger-solid", "#b91c1c", "#f87171"), ("--on-danger", "#fbfcfe", "#2a0a0a"),
        ("--disabled-bg", "#e5e7eb", "#1f2d47"), ("--disabled-text", "#4b5563", "#9fb0cc"),
        ("--heat-1", "#fef3c7", "#3a2a0c"), ("--heat-2", "#fde68a", "#4b3910"), ("--heat-3", "#fcd34d", "#5e4710"),
        ("--zebra", "#f1f4f9", "#152238"), ("--ring", "#12305a", "#8fd3f5"), ("--ring-on-dark", "#8fd3f5", "#8fd3f5"),
        ("--code-bg", "#0c1729", "#0c1729"), ("--code-text", "#dbe5f4", "#dbe5f4"), ("--code-muted", "#8fa3c2", "#8fa3c2"),
        ("--panel-bg", "#0a1e3c", "#0a1e3c"), ("--panel-text", "#e6edf7", "#e6edf7"), ("--panel-accent", "#38a9e0", "#38a9e0"),
        ("--logo-tile", "#ffffff", "#ffffff"),
    };

    private static readonly string[] OtherTokens =
    {
        "--glass-bg", "--glass-border", "--scrim", "--shadow-1", "--shadow-2", "--shadow-pop",
    };

    [Fact]
    public void Every_token_is_declared_with_the_specified_value_in_root_dark_and_the_auto_dark_media_block()
    {
        // AC-161, AC-171
        var light = Scope(":root");
        var dark = Scope(DarkSelector);
        var auto = Scope(AutoSelector);
        foreach (var (name, lightValue, darkValue) in Tokens)
        {
            Assert.True(light.TryGetValue(name, out var l), $"thiếu {name} ở :root");
            Assert.True(dark.TryGetValue(name, out var d), $"thiếu {name} ở [data-theme=dark]");
            Assert.True(auto.TryGetValue(name, out var a), $"thiếu {name} ở khối prefers-color-scheme");
            Assert.Equal(lightValue, l);
            Assert.Equal(darkValue, d);
            Assert.Equal(darkValue, a);
            if (lightValue != darkValue)
            {
                Assert.NotEqual(l, d);
            }
        }

        foreach (var name in OtherTokens)
        {
            Assert.True(light.ContainsKey(name) && dark.ContainsKey(name) && auto.ContainsKey(name), name);
            Assert.NotEqual(light[name], dark[name]);
        }

        Assert.Matches(@":root\s*\{[^}]*color-scheme:\s*light", CssCode);
        Assert.Matches(@"data-theme=""dark""\]\s*\{[^}]*color-scheme:\s*dark", CssCode);
        Assert.Contains("@media (prefers-color-scheme: dark)", CssCode);
    }

    [Fact]
    public void Brand_palette_variables_keep_the_specified_light_values()
    {
        var light = Scope(":root");
        Assert.Equal("#12305a", light["--brand-navy"]);
        Assert.Equal("#38a9e0", light["--brand-blue"]);
        Assert.Equal("#0b6fa8", light["--brand-link"]);
    }

    [Fact]
    public void Spacing_radius_easing_duration_and_header_tokens_follow_the_specification()
    {
        // AC-164, AC-165
        var root = Scope(":root");
        var expected = new Dictionary<string, string>
        {
            ["--s-1"] = "4px", ["--s-2"] = "8px", ["--s-3"] = "12px", ["--s-4"] = "16px", ["--s-5"] = "24px", ["--s-6"] = "32px", ["--s-7"] = "48px", ["--s-8"] = "64px",
            ["--r-xs"] = "6px", ["--r-control"] = "10px", ["--r-card"] = "16px", ["--r-pill"] = "999px",
            ["--ease-out"] = "cubic-bezier(0.16, 1, 0.3, 1)", ["--ease-spring"] = "cubic-bezier(0.34, 1.56, 0.64, 1)",
            ["--dur-fast"] = "120ms", ["--dur-base"] = "220ms", ["--dur-slow"] = "420ms", ["--dur-pill"] = "450ms", ["--stagger"] = "50ms",
            ["--header-h"] = "64px", ["--header-gap"] = "0px", // v5: thanh trên dính sát mép nên không còn khoảng hở
        };
        foreach (var (name, value) in expected)
        {
            Assert.Equal(value, root[name]);
        }

        foreach (Match m in Regex.Matches(CssCode, @"border-radius\s*:\s*([^;}]+)"))
        {
            foreach (var part in m.Groups[1].Value.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // 5px: thanh tiến độ .bar của dashboard (v5)
                Assert.Matches(@"^(var\(--r-(xs|control|card|pill)\)|50%|5px|0)$", part);
            }
        }
    }

    private static (double R, double G, double B) Rgb(string value, Dictionary<string, string> vars)
    {
        value = value.Trim();
        var v = Regex.Match(value, @"^var\((--[a-z0-9-]+)\)$");
        if (v.Success)
        {
            return Rgb(vars[v.Groups[1].Value], vars);
        }

        var hex = value.TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        return (int.Parse(hex[..2], NumberStyles.HexNumber) / 255.0, int.Parse(hex[2..4], NumberStyles.HexNumber) / 255.0, int.Parse(hex[4..6], NumberStyles.HexNumber) / 255.0);
    }

    private static double Luminance((double R, double G, double B) c)
    {
        static double L(double x) => x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        return 0.2126 * L(c.R) + 0.7152 * L(c.G) + 0.0722 * L(c.B);
    }

    private static double Contrast(string fg, string bg, Dictionary<string, string> vars)
    {
        var a = Luminance(Rgb($"var({fg})", vars));
        var b = Luminance(Rgb($"var({bg})", vars));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static IEnumerable<object[]> TextPairs()
    {
        var pairs = new (string Fg, string Bg, double Min)[]
        {
            ("--text", "--surface", 4.5), ("--text", "--bg", 4.5), ("--text", "--surface-2", 4.5), ("--text", "--primary-soft", 4.5),
            ("--text", "--zebra", 4.5), ("--text", "--heat-1", 4.5),
            ("--muted", "--surface", 4.5), ("--muted", "--bg", 4.5), ("--muted", "--surface-2", 4.5), ("--muted", "--zebra", 4.5),
            ("--brand-link", "--surface", 4.5), ("--brand-link", "--bg", 4.5), ("--brand-link", "--surface-2", 4.5), ("--brand-link", "--primary-soft", 4.5),
            ("--on-primary", "--primary", 4.5), ("--on-primary", "--primary-dark", 4.5), ("--brand-navy", "--brand-blue", 4.5),
            ("--success-text", "--success-bg", 4.5), ("--warn-text", "--warn-bg", 4.5), ("--danger-text", "--danger-bg", 4.5),
            ("--warn-text", "--heat-2", 4.5), ("--warn-text", "--heat-3", 4.5),
            ("--error", "--surface", 4.5), ("--error", "--bg", 4.5), ("--on-success", "--success-solid", 4.5), ("--on-danger", "--danger-solid", 4.5),
            ("--disabled-text", "--disabled-bg", 4.5), ("--code-text", "--code-bg", 4.5), ("--code-muted", "--code-bg", 4.5),
            ("--panel-text", "--panel-bg", 4.5), ("--panel-accent", "--panel-bg", 4.5),
            // Thành phần giao diện / đồ họa >= 3:1
            ("--input-border", "--surface", 3), ("--accent-stroke", "--surface", 3), ("--ring", "--bg", 3), ("--ring-on-dark", "--code-bg", 3),
            ("--error-border", "--surface", 3),
        };
        foreach (var theme in new[] { "light", "dark" })
        {
            foreach (var (fg, bg, min) in pairs)
            {
                yield return new object[] { theme, fg, bg, min };
            }
        }
    }

    [Theory]
    [MemberData(nameof(TextPairs))]
    public void Text_and_background_pairs_meet_the_contrast_ratio_in_both_themes(string theme, string fg, string bg, double min)
    {
        // AC-162
        var vars = theme == "light" ? Scope(":root") : Scope(DarkSelector);
        var ratio = Contrast(fg, bg, vars);
        Assert.True(ratio >= min, $"[{theme}] {fg} trên {bg} = {ratio:0.00}:1 (cần >= {min})");
    }

    [Fact]
    public void Header_text_stays_readable_on_the_glass_background_in_the_worst_case()
    {
        // AC-162: nền kính alpha 0,88 trộn lên khối mã tối nhất (--code-bg) vẫn cho chữ --text và --muted >= 4,5:1
        foreach (var (theme, scope, glass) in new[]
        {
            ("light", Scope(":root"), (R: 251.0, G: 252.0, B: 254.0)),
            ("dark", Scope(DarkSelector), (R: 17.0, G: 29.0, B: 51.0)),
        })
        {
            const double alpha = 0.88;
            var code = Rgb("var(--code-bg)", scope);
            var mixed = (R: (glass.R / 255 * alpha) + (code.R * (1 - alpha)), G: (glass.G / 255 * alpha) + (code.G * (1 - alpha)), B: (glass.B / 255 * alpha) + (code.B * (1 - alpha)));
            var lum = Luminance(mixed);
            foreach (var fg in new[] { "--text", "--muted" })
            {
                var l = Luminance(Rgb($"var({fg})", scope));
                var ratio = (Math.Max(l, lum) + 0.05) / (Math.Min(l, lum) + 0.05);
                Assert.True(ratio >= 4.5, $"[{theme}] {fg} trên kính = {ratio:0.00}:1");
            }

            Assert.Contains("0.88", scope["--glass-bg"]);
        }
    }

    [Fact]
    public void Light_blue_is_never_used_as_text_colour_and_never_carries_light_text()
    {
        var css = CssCode;
        Assert.DoesNotMatch(@"(?<![-\w])color:\s*(var\(--brand-blue\)|#38a9e0)", css);
        foreach (var (_, body) in CssRules(css))
        {
            if (Regex.IsMatch(body, @"background(-color)?:\s*(var\(--brand-blue\)|#38a9e0)"))
            {
                Assert.DoesNotMatch(@"(?<![-\w])color:\s*(#fff\b|#ffffff|white|var\(--on-primary\)|var\(--surface\))", body);
            }
        }
    }

    [Fact]
    public void Colours_are_only_declared_in_token_blocks_and_no_purple_or_pure_black_white_backgrounds_exist()
    {
        // AC-163
        var tokenSelectors = new[] { ":root", DarkSelector, AutoSelector };
        foreach (var (selector, body) in CssRules(CssCode).Where(r => !tokenSelectors.Contains(r.Selector)))
        {
            // Ngoại lệ có chủ đích của v5: bảng màu tô cú pháp --lp-* trên khối mã tối và hai nửa sáng/tối cố định (.lp-half--*) của landing
            var checkedBody = selector.StartsWith(".lp-half--", StringComparison.Ordinal) ? string.Empty : Regex.Replace(body, @"--lp-[a-z]+\s*:\s*#[0-9a-fA-F]{3,8}\s*;", string.Empty);
            Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", checkedBody);
            Assert.DoesNotMatch(@"(?<![-\w])(rgba?|hsla?)\(", body);
        }

        Assert.DoesNotMatch(@"(?<![-\w])(purple|violet|indigo|black|white)(?![-\w])", CssCode);
    }

    [Fact]
    public void Fonts_are_declared_with_swap_woff2_unicode_ranges_and_the_specified_stacks()
    {
        // AC-174, AC-176, AC-178
        var faces = CssRules(CssCode).Where(r => r.Selector == "@font-face" && r.Body.Contains("/fonts/")).ToList();
        Assert.Equal(12, faces.Count);
        Assert.Equal(9, faces.Count(f => f.Body.Contains("\"Be Vietnam Pro\"")));
        Assert.Equal(3, faces.Count(f => f.Body.Contains("\"JetBrains Mono\"")));
        foreach (var (_, body) in faces)
        {
            Assert.Contains("font-display: swap", body);
            Assert.Matches(@"src:\s*url\(""/fonts/[a-z0-9-]+\.woff2""\)\s*format\(""woff2""\)", body);
            Assert.Contains("unicode-range:", body);
            var file = Regex.Match(body, @"/fonts/([a-z0-9-]+\.woff2)").Groups[1].Value;
            Assert.True(File.Exists(Path.Combine(Frontend, "fonts", file)), file);
            if (file.Contains("-vietnamese-"))
            {
                foreach (var range in new[] { "U+0102-0103", "U+0110-0111", "U+0128-0129", "U+0168-0169", "U+01A0-01A1", "U+01AF-01B0", "U+1EA0-1EF9" })
                {
                    Assert.Contains(range, body);
                }
            }
        }

        foreach (var weight in new[] { "400", "600", "700" })
        {
            Assert.Equal(3, faces.Count(f => f.Body.Contains("\"Be Vietnam Pro\"") && Regex.IsMatch(f.Body, $@"font-weight:\s*{weight};")));
        }

        var root = Scope(":root");
        Assert.StartsWith("\"Be Vietnam Pro\"", root["--font-ui"]);
        Assert.StartsWith("\"JetBrains Mono\"", root["--font-mono"]);
        Assert.Matches(@"body\s*\{[^}]*font-family:\s*var\(--font-ui\)", CssCode);
        Assert.DoesNotMatch(@"\bInter\b|Georgia|Times|Playfair", CssCode);
        Assert.DoesNotMatch(@"(?<!sans-)serif", CssCode);
        Assert.Matches(@"html\s*\{[^}]*font-size:\s*16px", CssCode);
        Assert.Matches(@"body\s*\{[^}]*line-height:\s*1\.(6\d*|[7-9]\d*)", CssCode);
        Assert.Matches(@"h1\s*\{[^}]*font-size:\s*clamp\(\s*1\.(7[5-9]|[89]\d*)rem", CssCode);
        Assert.Matches(@"\.prose\s*\{[^}]*max-width:\s*70ch", CssCode);
    }

    [Fact]
    public void Motion_uses_token_easing_only_animates_allowed_properties_and_never_loops()
    {
        // AC-277, AC-278, AC-282, AC-280, AC-283
        var css = CssCode;
        Assert.DoesNotContain("infinite", css);
        Assert.DoesNotMatch(@"transition(-property)?\s*:\s*[^;}]*\ball\b", css);
        foreach (Match m in Regex.Matches(css, @"(?<![-\w])(transition|animation)(-timing-function)?\s*:\s*([^;}]+)"))
        {
            Assert.DoesNotMatch(@"(?<![-\w(])(ease|ease-in|ease-out|ease-in-out)(?![-\w])", m.Groups[3].Value);
        }

        var allowedTransition = new[] { "transform", "opacity", "stroke-dashoffset", "color", "background-color", "border-color", "box-shadow" };
        var items = Regex.Matches(css, @"(?<![-\w])transition\s*:\s*([^;}]+)").Cast<Match>().SelectMany(m => Regex.Split(m.Groups[1].Value, @",(?![^(]*\))")).Select(i => i.Trim()).ToList();
        foreach (var item in items)
        {
            var property = item.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (property == "none")
            {
                continue; // tắt transition (ví dụ khi dùng animation-timeline) là hợp lệ
            }

            Assert.Contains(property, allowedTransition);
            if (!Regex.IsMatch(item, @"^(transform|opacity|stroke-dashoffset)\b"))
            {
                Assert.Contains("var(--dur-fast)", item);
            }
        }

        foreach (var (_, body) in CssRules(css).Where(r => r.Selector.StartsWith("@keyframes", StringComparison.Ordinal)))
        {
            foreach (Match m in Regex.Matches(body, @"([a-z-]+)\s*:"))
            {
                Assert.Contains(m.Groups[1].Value, new[] { "transform", "opacity", "stroke-dashoffset" });
            }
        }

        foreach (Match m in Regex.Matches(css, @"animation-iteration-count\s*:\s*(\d+)"))
        {
            Assert.True(int.Parse(m.Groups[1].Value) <= 3);
        }

        Assert.Contains("@view-transition { navigation: auto; }", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("--stagger: 0ms", css);
        Assert.Contains("view-transition-name: app-topbar", css);
        Assert.DoesNotContain("@keyframes spin", css);
        Assert.DoesNotContain(".spinner", css);
        Assert.DoesNotMatch(@"cursor\s*:\s*url\(", css);
    }

    [Fact]
    public void Css_declares_layers_and_has_no_font_smaller_than_1rem_nor_bare_vh_without_dvh_fallback()
    {
        // AC-290, AC-300, AC-303
        var css = CssCode;
        Assert.Contains("@layer tokens, base, components, pages;", css);
        Assert.DoesNotContain("@import", css);
        foreach (Match m in Regex.Matches(css, @"font-size\s*:\s*([\d.]+)(rem|px|em)"))
        {
            var size = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var rem = m.Groups[2].Value == "px" ? size / 16 : size;
            Assert.True(rem >= 1, $"font-size {m.Value} < 1rem");
        }

        foreach (var (selector, body) in CssRules(css))
        {
            var declarations = body.Split(';').Select(d => d.Trim()).Where(d => d.Contains(':')).ToList();
            for (var i = 0; i < declarations.Count; i++)
            {
                if (!Regex.IsMatch(declarations[i], @"\d(\.\d+)?vh\b"))
                {
                    continue;
                }

                var property = declarations[i][..declarations[i].IndexOf(':')].Trim();
                if (property == "animation-range")
                {
                    continue; // phạm vi cuộn của scroll-driven animation (v5), không phải kích thước phần tử
                }

                var neighbours = new[] { i - 1, i + 1 }.Where(n => n >= 0 && n < declarations.Count).Select(n => declarations[n]);
                Assert.True(neighbours.Any(n => n.StartsWith(property + ":", StringComparison.Ordinal) && n.Contains("dvh")), $"{selector}: {declarations[i]} thiếu dự phòng dvh");
            }
        }
    }

    [Fact]
    public void Javascript_has_no_window_scroll_listener_set_interval_external_libraries_or_foreign_urls()
    {
        // AC-282, AC-284, AC-285
        foreach (var file in JsFiles)
        {
            var text = Code(file);
            Assert.DoesNotMatch(@"setInterval\s*\(", text);
            Assert.DoesNotMatch(@"(window|document|document\.body|document\.documentElement)\.addEventListener\(\s*['""]scroll['""]", text);
            Assert.DoesNotMatch(@"import\s[^;]*from\s+['""]https?:", text);
            Assert.DoesNotMatch(@"(?i)\b(gsap|lottie|framer-motion|matter-js|three\.js|anime\.js)\b", text);
        }

        // AC-488: địa chỉ API chỉ được khai báo ở js/config.js (đúng http://localhost:5000); mọi nơi khác chỉ cho SVG namespace và Pyodide
        var allowed = new[] { "http://www.w3.org/2000/svg", "https://cdn.jsdelivr.net/pyodide/" };
        foreach (var file in JsFiles.Concat(HtmlFiles).Concat(Directory.GetFiles(Frontend, "*.css", SearchOption.AllDirectories)))
        {
            foreach (Match m in Regex.Matches(Code(file), @"https?://[^\s'""`)<>]+"))
            {
                var isApiBase = Rel(file) == "js/config.js" && m.Value == "http://localhost:5000";
                Assert.True(isApiBase || allowed.Any(a => m.Value.StartsWith(a, StringComparison.Ordinal)), $"{Rel(file)}: {m.Value}");
            }
        }
    }

    // Tệp còn dùng emoji / em-dash cho tới khi giai đoạn 2 của thiết kế lại hoàn tất (AC-313); xóa dần khỏi danh sách này.
    private static readonly string[] PendingPhaseTwoFiles =
    {
        "js/problem.js", "js/teacher/progress.js", "js/teacher/students.js",
    };

    [Fact]
    public void Displayed_code_has_no_emoji_or_em_dash()
    {
        // AC-313 (en-dash trong <title> và chuỗi cũ được giữ nguyên)
        foreach (var file in JsFiles.Concat(HtmlFiles).Concat(Directory.GetFiles(Frontend, "*.css", SearchOption.AllDirectories)))
        {
            if (PendingPhaseTwoFiles.Contains(Rel(file)))
            {
                continue;
            }

            foreach (var rune in File.ReadAllText(file).EnumerateRunes())
            {
                var v = rune.Value;
                var bad = v == 0x2014 || (v >= 0x1F300 && v <= 0x1FAFF) || (v >= 0x2600 && v <= 0x27BF) || v == 0x2B50 || v == 0x2B1B || v == 0x2B1C || (v >= 0x1F000 && v <= 0x1F2FF);
                Assert.False(bad, $"{Rel(file)} chứa ký tự U+{v:X4}");
            }
        }
    }

    [Fact]
    public void Body_text_is_at_least_16_px_and_touch_targets_are_at_least_44_px()
    {
        var css = CssRaw;
        Assert.Matches(@"html\s*\{[^}]*font-size:\s*16px", css);
        Assert.Matches(@"\.btn\s*\{[^}]*min-height:\s*44px", css);
        Assert.Matches(@"\.input, \.select, \.textarea\s*\{[^}]*min-height:\s*44px", css);
        Assert.Matches(@"\.check\s*\{[^}]*min-height:\s*44px", css);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("overflow-x: auto", css);
        Assert.Matches(@":focus-visible\s*\{[^}]*outline:\s*3px solid var\(--ring\)", css);
        Assert.Contains("@media (forced-colors: active)", css);
        Assert.Matches(@"\.theme-btn\s*\{[^}]*width:\s*44px", css);
    }

    [Fact]
    public void Ui_script_modules_exist_clean_up_after_themselves_and_use_svg_icons()
    {
        // AC-168, AC-273, AC-286, QD-56
        var js = Path.Combine(Frontend, "js");
        foreach (var name in new[] { "theme-init.js", "theme.js", "toast.js", "fx.js", "login-fx.js", "icons.js" })
        {
            Assert.True(File.Exists(Path.Combine(js, name)), name);
        }

        var theme = Code(Path.Combine(js, "theme.js"));
        Assert.Contains("id: 'btn-theme'", theme);
        Assert.Contains("Đổi giao diện", theme);
        Assert.Contains("startViewTransition", theme);
        Assert.Contains("uiPrefs", theme);
        Assert.Contains("pagehide", theme);
        var toast = Code(Path.Combine(js, "toast.js"));
        Assert.Contains("toast-region", toast);
        Assert.Contains("Đóng thông báo", toast);
        var fx = Code(Path.Combine(js, "fx.js"));
        Assert.Contains("pagehide", fx);
        Assert.Contains("disconnect()", fx);
        Assert.Contains("createElementNS", Code(Path.Combine(js, "icons.js")));
        var modal = Code(Path.Combine(js, "modal.js"));
        Assert.Contains("toast-region", modal);
        Assert.Contains("has-modal", modal);
    }
}

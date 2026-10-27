using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TinHoc12.Application.Tests;

/// <summary>Kiểm thử ranh giới Clean Architecture và các quy tắc mã nguồn (AC-39, 52, 53, 54, 63, 65, 73).</summary>
public class ArchitectureTests
{
    private static readonly string[] Projects = { "Domain", "Application", "Infrastructure", "Web" };

    private static XDocument Csproj(string name)
        => XDocument.Load(Path.Combine(RepoPaths.Src, name, name + ".csproj"));

    private static string[] ProjectRefs(string name)
        => Csproj(name).Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension((string)e.Attribute("Include")!))
            .OrderBy(x => x).ToArray();

    private static string[] PackageRefs(string name)
        => Csproj(name).Descendants("PackageReference")
            .Select(e => (string)e.Attribute("Include")!).ToArray();

    [Fact]
    public void Solution_contains_exactly_the_four_projects_and_three_test_projects()
    {
        var sln = File.ReadAllText(Path.Combine(RepoPaths.Src, "TinHoc12.sln"));
        var included = Regex.Matches(sln, @"""([^""]+\.csproj)""").Select(m => m.Groups[1].Value.Replace('\\', '/')).ToArray();
        Assert.Equal(7, included.Length);
        foreach (var p in Projects)
        {
            Assert.Contains($"{p}/{p}.csproj", included);
        }

        Assert.Contains("../tests/Application.Tests/Application.Tests.csproj", included);
        Assert.Contains("../tests/Infrastructure.Tests/Infrastructure.Tests.csproj", included);
        Assert.Contains("../tests/Web.Tests/Web.Tests.csproj", included);
        Assert.Equal(4, Directory.GetDirectories(RepoPaths.Src).Count(d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj"))));
    }

    [Fact]
    public void Domain_has_no_project_or_package_references()
    {
        Assert.Empty(ProjectRefs("Domain"));
        Assert.Empty(PackageRefs("Domain"));
    }

    [Fact]
    public void Application_references_only_Domain_and_no_framework_packages()
    {
        Assert.Equal(new[] { "Domain" }, ProjectRefs("Application"));
        Assert.DoesNotContain(PackageRefs("Application"), p => p.Contains("EntityFramework") || p.Contains("AspNetCore"));
        Assert.Equal("Microsoft.NET.Sdk", (string)Csproj("Application").Root!.Attribute("Sdk")!);
    }

    [Fact]
    public void Infrastructure_references_Application_and_uses_EF_Core_SqlServer()
    {
        Assert.Equal(new[] { "Application" }, ProjectRefs("Infrastructure"));
        Assert.Contains("Microsoft.EntityFrameworkCore.SqlServer", PackageRefs("Infrastructure"));
    }

    [Fact]
    public void Web_references_Application_and_Infrastructure_but_not_Domain()
    {
        Assert.Equal(new[] { "Application", "Infrastructure" }, ProjectRefs("Web"));
    }

    [Fact]
    public void No_project_references_Web_and_there_is_no_cycle()
    {
        var all = Projects.ToDictionary(p => p, ProjectRefs);
        Assert.DoesNotContain(all.Values, refs => refs.Contains("Web"));

        var visiting = new HashSet<string>();
        var done = new HashSet<string>();
        void Visit(string n)
        {
            if (done.Contains(n)) return;
            Assert.True(visiting.Add(n), $"Vòng tham chiếu tại {n}");
            foreach (var r in all[n]) Visit(r);
            visiting.Remove(n);
            done.Add(n);
        }

        foreach (var p in Projects) Visit(p);
    }

    [Fact]
    public void Application_and_Domain_sources_do_not_use_EF_AspNet_or_Identity_namespaces()
    {
        var forbidden = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions.Identity", "Microsoft.Data." };
        foreach (var project in new[] { "Application", "Domain" })
        {
            foreach (var file in RepoPaths.Files(Path.Combine(RepoPaths.Src, project), "*.cs"))
            {
                var text = File.ReadAllText(file);
                Assert.DoesNotContain(forbidden, f => text.Contains(f, StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Domain_source_does_not_use_other_layers()
    {
        foreach (var file in RepoPaths.Files(Path.Combine(RepoPaths.Src, "Domain"), "*.cs"))
        {
            Assert.DoesNotMatch(@"using\s+TinHoc12\.(Application|Infrastructure|Web)", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Web_source_does_not_use_Domain_namespace()
    {
        var offenders = RepoPaths.Files(Path.Combine(RepoPaths.Src, "Web"), "*.cs")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"using\s+[\w\.]*Domain"))
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Entities_live_in_Domain_and_repository_interface_in_Application()
    {
        foreach (var e in new[] { "Lesson", "Exercise", "TestCase", "User", "SchoolClass", "ClassMember", "Assignment", "AssignmentClass", "Submission", "SolutionView" })
        {
            Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Domain", "Entities", e + ".cs")));
        }

        foreach (var i in new[] { "ILessonRepository", "ILessonAuthoringRepository", "IUserRepository", "IClassRepository", "IAssignmentRepository", "ISubmissionRepository", "IPasswordHasher", "IClock" })
        {
            Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Application", "Abstractions", i + ".cs")), i);
        }

        Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Application", "Security", "Roles.cs")));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Application", "Security", "CurrentUser.cs")));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Infrastructure", "Security", "Pbkdf2PasswordHasher.cs")));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Infrastructure", "Security", "SystemClock.cs")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(RepoPaths.Src, "Infrastructure", "Migrations"), "*_AddAuthAndAssignments.cs"));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Infrastructure", "Persistence", "AppDbContext.cs")));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Src, "Infrastructure", "Repositories", "LessonRepository.cs")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(RepoPaths.Src, "Infrastructure", "Migrations"), "*_InitialCreate.cs"));
    }

    [Fact]
    public void No_hard_coded_connection_strings_in_cs_or_js()
    {
        var needles = new[] { "Server" + "=", "Data Source" + "=" };
        var files = RepoPaths.Files(RepoPaths.Src, "*.cs").Concat(RepoPaths.Files(RepoPaths.Frontend, "*.js"))
            .Concat(RepoPaths.Files(RepoPaths.Tests, "*.cs").Where(f => !f.EndsWith("ArchitectureTests.cs", StringComparison.Ordinal)));
        needles = needles.Concat(new[] { "(local" + "db)" }).ToArray();
        var offenders = files.Where(f =>
        {
            var text = File.ReadAllText(f);
            return needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
        }).ToArray();
        Assert.Empty(offenders);
        Assert.Contains("DefaultConnection", File.ReadAllText(Path.Combine(RepoPaths.Src, "Web", "appsettings.json")));
    }

    [Fact]
    public void Sample_lesson_content_is_not_hard_coded_in_source()
    {
        var titles = new[] { "Tổng hai số", "Diện tích hình chữ nhật", "Xếp loại học lực", "Đếm ước của n", "Chẵn hay lẻ" };
        var files = RepoPaths.Files(RepoPaths.Src, "*.cs")
            .Concat(RepoPaths.Files(RepoPaths.Frontend, "*.js"))
            .Concat(RepoPaths.Files(RepoPaths.Frontend, "*.html"))
            .Concat(RepoPaths.Files(RepoPaths.Frontend, "*.css"));
        var offenders = files.Where(f =>
        {
            var text = File.ReadAllText(f);
            return titles.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
        }).ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Frontend_has_no_json_data_files_and_no_data_folders()
    {
        var frontend = RepoPaths.Frontend;
        Assert.Empty(Directory.GetFiles(frontend, "*.json", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(RepoPaths.Root, "data")));
        Assert.False(Directory.Exists(Path.Combine(RepoPaths.Root, "web")));
        Assert.False(Directory.Exists(Path.Combine(frontend, "data")));
        Assert.False(Directory.Exists(Path.Combine(frontend, "dist")));
        // Backend đã thành API-only: thư mục giao diện cũ trong src/Web không còn (AC-477, AC-526)
        Assert.False(Directory.Exists(Path.Combine(RepoPaths.Src, "Web", "wwwroot")));
    }

    [Fact]
    public void Frontend_never_fetches_json_files_or_touches_the_db()
    {
        foreach (var js in RepoPaths.Files(RepoPaths.Frontend, "*.js"))
        {
            var text = File.ReadAllText(js);
            Assert.DoesNotMatch(@"fetch\([^)]*\.json", text);
        }
    }

    [Fact]
    public void Infrastructure_does_not_read_json_files()
    {
        foreach (var cs in RepoPaths.Files(Path.Combine(RepoPaths.Src, "Infrastructure"), "*.cs"))
        {
            if (cs.Contains("Migrations")) continue;
            // Chỉ đọc chuỗi kết nối (cấu hình) từ appsettings cho công cụ dotnet ef, không đọc dữ liệu bài học.
            var text = File.ReadAllText(cs);
            if (Path.GetFileName(cs) == "DesignTimeDbContextFactory.cs")
            {
                // Ngoại lệ hẹp: chỉ được nhắc tới appsettings*.json; mọi tham chiếu .json khác (dữ liệu bài) đều bị bắt.
                text = System.Text.RegularExpressions.Regex.Replace(text, @"appsettings(\.[A-Za-z]+)?\.json|System\.Text\.Json", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Repository_contains_no_java_files()
    {
        var java = Directory.EnumerateFiles(RepoPaths.Root, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}"))
            .Where(f => f.EndsWith(".java", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".gradle", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(java);
    }
}

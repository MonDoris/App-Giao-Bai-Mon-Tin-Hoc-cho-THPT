namespace TinHoc12.Application.Tests;

/// <summary>Tìm thư mục gốc repo (chứa src\TinHoc12.sln) bằng cách đi ngược từ thư mục chạy test.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "TinHoc12.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Không tìm thấy thư mục gốc của repo (src/TinHoc12.sln).");
    }

    public static string Src => Path.Combine(Root, "src");
    public static string Tests => Path.Combine(Root, "tests");

    /// <summary>Thư mục giao diện độc lập (HTML/CSS/JS thuần) ở gốc repo (QD-87, AC-526).</summary>
    public static string Frontend => Path.Combine(Root, "frontend");

    public static IEnumerable<string> Files(string folder, string pattern)
        => Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}

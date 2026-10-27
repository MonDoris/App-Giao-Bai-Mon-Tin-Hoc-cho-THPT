using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.UseCases.Lessons;
using TinHoc12.Infrastructure.Repositories;

namespace TinHoc12.Infrastructure.Tests;

/// <summary>
/// Kiểm thử dữ liệu mẫu (AC-50, AC-51, AC-69) trên SQLite in-memory bằng bản SQLite của script seed.
/// Bản T-SQL (seed-sample.sql) được sinh cùng nguồn nội dung và đã chạy kiểm tra trên SQL Server LocalDB.
/// </summary>
public class SeedSampleTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "TinHoc12.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo.");
    }

    private static readonly TinHoc12.Application.Security.CurrentUser Teacher = new(1, TinHoc12.Application.Security.Roles.Teacher);

    private static GetLessonDetailUseCase Detail(TinHoc12.Infrastructure.Persistence.AppDbContext ctx) => new(
        new LessonRepository(ctx), new AssignmentRepository(ctx), new SubmissionRepository(ctx), new TinHoc12.Infrastructure.Security.SystemClock());

    private static string SeedScript => File.ReadAllText(Path.Combine(RepoRoot(), "database", "sqlite/seed-sample.sqlite.sql"));

    private static void RunScript(SqliteConnection connection, string script)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = script;
        cmd.ExecuteNonQuery();
    }

    private static TestDb SeededDb(int times = 1)
    {
        var db = new TestDb();
        for (var i = 0; i < times; i++)
        {
            RunScript(db.Connection, SeedScript);
        }

        return db;
    }

    [Fact]
    public async Task Seed_is_idempotent_and_loads_3_lessons_and_6_exercises()
    {
        using var db = SeededDb(times: 2);
        await using var ctx = db.CreateContext();

        Assert.Equal(3, await ctx.Lessons.CountAsync());
        Assert.Equal(6, await ctx.Exercises.CountAsync());
        Assert.Equal(new[]
        {
            "bai-01-nhap-xuat", "bai-02-re-nhanh", "bai-03-vong-lap",
        }, await ctx.Lessons.OrderBy(l => l.Order).Select(l => l.Id).ToListAsync());
    }

    [Fact]
    public async Task Every_sample_exercise_has_examples_tests_hidden_and_solution_and_maps_cleanly()
    {
        using var db = SeededDb();
        await using var ctx = db.CreateContext();
        var useCase = Detail(ctx);

        foreach (var id in new[] { "bai-01-nhap-xuat", "bai-02-re-nhanh", "bai-03-vong-lap" })
        {
            var lesson = await useCase.ExecuteAsync(Teacher, id);
            Assert.NotEmpty(lesson.Summary);
            Assert.InRange(lesson.Notes.Count, 3, 5);
            Assert.NotEmpty(lesson.Conclusion);
            Assert.Equal(2, lesson.Exercises.Count);
            foreach (var ex in lesson.Exercises)
            {
                Assert.False(string.IsNullOrWhiteSpace(ex.StarterCode));
                Assert.NotEmpty(ex.Examples);
                Assert.True(ex.TestCases.Count >= 4, ex.Id);
                Assert.Contains(ex.TestCases, t => t.IsHidden);
                Assert.True(ex.HasSolution);
            }
        }
    }

    private static string CollapseWhitespace(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

    /// <summary>AC-60 trên dữ liệu seed THẬT: chi tiết bài (trả ra client) không được chứa đáp án của bài tập nào.</summary>
    [Fact]
    public async Task Lesson_detail_of_real_seed_data_never_leaks_any_solution_code()
    {
        using var db = SeededDb();
        await using var ctx = db.CreateContext();
        var useCase = Detail(ctx);
        var solutions = await ctx.Exercises.Select(e => new { e.Id, e.SolutionCode }).ToListAsync();
        // Dòng nằm trong StarterCode vốn được công khai cho học sinh nên không tính là lộ đáp án.
        var starterLines = (await ctx.Exercises.Select(e => e.StarterCode).ToListAsync())
            .SelectMany(c => c.Split('\n')).Select(l => l.Trim()).ToHashSet();
        Assert.Equal(6, solutions.Count);

        var options = new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        foreach (var lessonId in await ctx.Lessons.Select(l => l.Id).ToListAsync())
        {
            var lesson = await useCase.ExecuteAsync(Teacher, lessonId);
            var json = System.Text.Json.JsonSerializer.Serialize(lesson, options);
            // Giải mã chuỗi JSON (xuống dòng, dấu nháy kép) để so khớp nội dung thật, rồi chuẩn hóa khoảng trắng.
            var haystack = CollapseWhitespace(json.Replace("\\n", "\n").Replace("\\\"", "\""));
            var haystackRaw = CollapseWhitespace(json);

            foreach (var s in solutions)
            {
                var code = s.SolutionCode!;
                var whole = CollapseWhitespace(code);
                Assert.DoesNotContain(whole, haystack);
                Assert.DoesNotContain(whole, haystackRaw);

                foreach (var line in code.Split('\n').Select(l => l.Trim()))
                {
                    if (line.Length < 15 || line.StartsWith('#') || starterLines.Contains(line)) continue;
                    Assert.False(haystack.Contains(line, StringComparison.Ordinal) || haystackRaw.Contains(line, StringComparison.Ordinal),
                        $"Bài {lessonId} lộ dòng đáp án của {s.Id}: {line}");
                }
            }
        }
    }

    private static string? FindPython()
    {
        foreach (var exe in new[] { "python", "python3", "py" })
        {
            try
            {
                var psi = new ProcessStartInfo(exe, "--version") { RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi);
                if (p is not null && p.WaitForExit(5000) && p.ExitCode == 0)
                {
                    return exe;
                }
            }
            catch
            {
                // thử tệp thực thi kế tiếp
            }
        }

        return null;
    }

    private static string RunPython(string python, string code, string stdin)
    {
        var psi = new ProcessStartInfo(python)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(code);
        using var p = Process.Start(psi)!;
        p.StandardInput.Write(stdin);
        p.StandardInput.Close();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10000);
        return output;
    }

    /// <summary>Cùng quy tắc với js/grader.js: bỏ khoảng trắng cuối dòng và cuối đầu ra, đổi CRLF thành LF.</summary>
    private static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd(' ', '\t', '\r', '\f', '\v'));
        return string.Join("\n", lines).TrimEnd();
    }

    [SkippableFact]
    public async Task Solutions_pass_all_tests_and_starter_code_fails_when_python_is_available()
    {
        var python = FindPython();
        Skip.If(python is null, "Bỏ qua: máy không có Python (python/python3/py) nên không chạy thật lời giải; trình duyệt dùng Pyodide để chấm.");

        using var db = SeededDb();
        await using var ctx = db.CreateContext();
        var exercises = await ctx.Exercises.Include(e => e.TestCases).ToListAsync();
        Assert.Equal(6, exercises.Count);

        foreach (var ex in exercises)
        {
            foreach (var t in ex.TestCases)
            {
                var actual = Normalize(RunPython(python, ex.SolutionCode!, t.Input));
                Assert.True(Normalize(t.ExpectedOutput) == actual, $"{ex.Id}: solution sai ở test {t.Order}");

                var starter = Normalize(RunPython(python, ex.StarterCode, t.Input));
                Assert.False(Normalize(t.ExpectedOutput) == starter, $"{ex.Id}: starterCode không được đạt test {t.Order}");
            }
        }
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Tests;

/// <summary>DB SQLite in-memory (giữ kết nối mở suốt vòng đời) dùng cho kiểm thử, không cần SQL Server.</summary>
internal sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        return new AppDbContext(options);
    }

    public SqliteConnection Connection => _connection;

    public void Dispose() => _connection.Dispose();

    public static Lesson Lesson(string id, int order, int exercises = 0)
    {
        var lesson = new Lesson
        {
            Id = id,
            Order = order,
            Title = "Bài " + order,
            Description = "Mô tả",
            SummaryJson = "[\"a\"]",
            NotesJson = "[\"b\"]",
            ConclusionJson = "[\"c\"]",
        };
        for (var i = 1; i <= exercises; i++)
        {
            lesson.Exercises.Add(Exercise(id + "-bt" + i, id, i));
        }

        return lesson;
    }

    public static Exercise Exercise(string id, string lessonId, int order)
    {
        var e = new Exercise
        {
            Id = id,
            LessonId = lessonId,
            Order = order,
            Title = "BT " + order,
            Difficulty = "Easy",
            Statement = "s",
            InputFormat = "i",
            OutputFormat = "o",
            ExamplesJson = "[{\"input\":\"1\",\"output\":\"1\"}]",
            StarterCode = "x",
            SolutionCode = "print(1)",
            SolutionExplanation = "gt",
        };
        e.TestCases.Add(new TestCase { Order = 3, Input = "3", ExpectedOutput = "3", IsHidden = true });
        e.TestCases.Add(new TestCase { Order = 1, Input = "1", ExpectedOutput = "1", IsHidden = false });
        e.TestCases.Add(new TestCase { Order = 2, Input = "2", ExpectedOutput = "2", IsHidden = false });
        return e;
    }
}

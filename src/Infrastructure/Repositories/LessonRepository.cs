using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

/// <summary>Cài đặt <see cref="ILessonRepository"/> bằng EF Core. Mọi truy vấn đều được tham số hóa.</summary>
public sealed class LessonRepository : ILessonRepository
{
    private readonly AppDbContext _db;

    public LessonRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LessonHeader>> GetLessonHeadersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Lessons
            .AsNoTracking()
            .OrderBy(l => l.Order)
            .Select(l => new LessonHeader(l.Id, l.Order, l.Title, l.Description, l.Exercises.Count(), l.IsPublished, l.IsArchived))
            .ToListAsync(cancellationToken);
    }

    public async Task<Lesson?> GetLessonWithExercisesAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.Lessons
            .AsNoTracking()
            .AsSplitQuery()
            .Include(l => l.Exercises.OrderBy(e => e.Order))
                .ThenInclude(e => e.TestCases.OrderBy(t => t.Order))
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<Exercise?> GetExerciseAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.Exercises
            .AsNoTracking()
            .Include(e => e.Lesson)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetTestCountsAsync(
        IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        var rows = await _db.TestCases
            .AsNoTracking()
            .Where(t => ids.Contains(t.ExerciseId))
            .GroupBy(t => t.ExerciseId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }
}

using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

public sealed class LessonAuthoringRepository : ILessonAuthoringRepository
{
    private readonly AppDbContext _db;

    public LessonAuthoringRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TeacherLessonRow>> ListLessonsAsync(bool includeArchived, CancellationToken cancellationToken = default) =>
        await _db.Lessons.AsNoTracking()
            .Where(l => includeArchived || !l.IsArchived)
            .OrderBy(l => l.Order)
            .Select(l => new TeacherLessonRow(
                l.Id,
                l.Order,
                l.Title,
                l.Description,
                l.IsPublished,
                l.IsArchived,
                l.Exercises.Count(),
                l.Exercises.Count(e => e.IsPublished),
                _db.Assignments.Count(a => a.Exercise!.LessonId == l.Id),
                l.Version,
                l.UpdatedUtc,
                l.CreatedBy != null ? l.CreatedBy.DisplayName : null))
            .ToListAsync(cancellationToken);

    public Task<Lesson?> GetLessonForEditAsync(string id, CancellationToken cancellationToken = default) =>
        _db.Lessons
            .AsSplitQuery()
            .Include(l => l.CreatedBy)
            .Include(l => l.Exercises).ThenInclude(e => e.TestCases)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<Exercise?> GetExerciseForEditAsync(string id, CancellationToken cancellationToken = default) =>
        _db.Exercises
            .AsSplitQuery()
            .Include(e => e.TestCases)
            .Include(e => e.CreatedBy)
            .Include(e => e.Lesson)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> LessonIdExistsAsync(string id, CancellationToken cancellationToken = default) =>
        _db.Lessons.AnyAsync(l => l.Id == id, cancellationToken);

    public Task<bool> ExerciseIdExistsAsync(string id, CancellationToken cancellationToken = default) =>
        _db.Exercises.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task<int> NextLessonOrderAsync(CancellationToken cancellationToken = default) =>
        (await _db.Lessons.MaxAsync(l => (int?)l.Order, cancellationToken) ?? 0) + 1;

    public async Task<int> NextExerciseOrderAsync(string lessonId, CancellationToken cancellationToken = default) =>
        (await _db.Exercises.Where(e => e.LessonId == lessonId).MaxAsync(e => (int?)e.Order, cancellationToken) ?? 0) + 1;

    public async Task<Dependencies> GetLessonDependenciesAsync(string lessonId, CancellationToken cancellationToken = default) =>
        new(
            await _db.Assignments.CountAsync(a => a.Exercise!.LessonId == lessonId, cancellationToken),
            await _db.Submissions.CountAsync(s => s.Exercise!.LessonId == lessonId, cancellationToken));

    public async Task<Dependencies> GetExerciseDependenciesAsync(string exerciseId, CancellationToken cancellationToken = default) =>
        new(
            await _db.Assignments.CountAsync(a => a.ExerciseId == exerciseId, cancellationToken),
            await _db.Submissions.CountAsync(s => s.ExerciseId == exerciseId, cancellationToken));

    public async Task<IReadOnlyList<Lesson>> GetAllLessonsAsync(CancellationToken cancellationToken = default) =>
        await _db.Lessons.OrderBy(l => l.Order).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Exercise>> GetExercisesOfLessonAsync(string lessonId, CancellationToken cancellationToken = default) =>
        await _db.Exercises.Where(e => e.LessonId == lessonId).OrderBy(e => e.Order).ToListAsync(cancellationToken);

    public void AddLesson(Lesson lesson) => _db.Lessons.Add(lesson);

    public void AddExercise(Exercise exercise) => _db.Exercises.Add(exercise);

    public void RemoveLesson(Lesson lesson) => _db.Lessons.Remove(lesson);

    public void RemoveExercise(Exercise exercise) => _db.Exercises.Remove(exercise);
}

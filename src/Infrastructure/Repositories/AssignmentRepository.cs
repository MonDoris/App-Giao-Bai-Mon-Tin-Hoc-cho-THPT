using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly AppDbContext _db;

    public AssignmentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StudentAssignmentRow>> GetVisibleForStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await _db.Assignments
            .AsNoTracking()
            .Where(a => a.CancelledUtc == null
                && a.Exercise!.IsPublished && !a.Exercise.IsArchived
                && a.Exercise.Lesson!.IsPublished && !a.Exercise.Lesson.IsArchived
                && (a.AssignedToAll
                    || a.Classes.Any(ac => _db.ClassMembers.Any(m => m.StudentId == studentId && m.ClassId == ac.ClassId))))
            .Select(a => new StudentAssignmentRow(
                a.Id,
                a.ExerciseId,
                a.Exercise!.Title,
                a.Exercise.Order,
                a.Exercise.Difficulty,
                a.Exercise.LessonId,
                a.Exercise.Lesson!.Title,
                a.Exercise.Lesson.Order,
                a.DueAtUtc,
                a.Note))
            .ToListAsync(cancellationToken);
    }

    public Task<Assignment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Assignments.Include(a => a.Classes).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Assignment>> GetActiveForExercisesAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.Distinct().ToList();
        return await _db.Assignments.Include(a => a.Classes)
            .Where(a => a.CancelledUtc == null && ids.Contains(a.ExerciseId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssignmentListRow>> ListAsync(
        string? lessonId, int? classId, bool includeCancelled, CancellationToken cancellationToken = default)
    {
        var query = _db.Assignments.AsNoTracking().Include(a => a.Classes).Include(a => a.Exercise).ThenInclude(e => e!.Lesson).AsQueryable();
        if (!includeCancelled)
        {
            query = query.Where(a => a.CancelledUtc == null);
        }

        if (!string.IsNullOrEmpty(lessonId))
        {
            query = query.Where(a => a.Exercise!.LessonId == lessonId);
        }

        if (classId is { } cid)
        {
            query = query.Where(a => a.AssignedToAll || a.Classes.Any(c => c.ClassId == cid));
        }

        var assignments = await query.OrderByDescending(a => a.CreatedUtc).ThenByDescending(a => a.Id).ToListAsync(cancellationToken);
        return await BuildRowsAsync(assignments, cancellationToken);
    }

    public async Task<IReadOnlyList<AssignmentListRow>> ListByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var list = ids.Distinct().ToList();
        var assignments = await _db.Assignments.AsNoTracking()
            .Include(a => a.Classes).Include(a => a.Exercise).ThenInclude(e => e!.Lesson)
            .Where(a => list.Contains(a.Id))
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
        return await BuildRowsAsync(assignments, cancellationToken);
    }

    public Task<bool> HasSubmissionsAsync(int assignmentId, CancellationToken cancellationToken = default) =>
        _db.Submissions.AnyAsync(s => s.AssignmentId == assignmentId, cancellationToken);

    public async Task<int> CountStudentsReachedAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        var targets = await _db.Assignments.AsNoTracking()
            .Where(a => a.CancelledUtc == null && ids.Contains(a.ExerciseId))
            .Select(a => new { a.AssignedToAll, ClassIds = a.Classes.Select(c => c.ClassId).ToList() })
            .ToListAsync(cancellationToken);
        if (targets.Count == 0)
        {
            return 0;
        }

        if (targets.Any(t => t.AssignedToAll))
        {
            return await _db.Users.CountAsync(u => u.Role == Roles.Student && u.IsActive, cancellationToken);
        }

        var classIds = targets.SelectMany(t => t.ClassIds).Distinct().ToList();
        return await _db.ClassMembers
            .Where(m => classIds.Contains(m.ClassId) && m.Student!.IsActive && m.Student.Role == Roles.Student)
            .Select(m => m.StudentId).Distinct().CountAsync(cancellationToken);
    }

    public void Add(Assignment assignment) => _db.Assignments.Add(assignment);

    public void Remove(Assignment assignment) => _db.Assignments.Remove(assignment);

    /// <summary>Tính số học sinh được giao/đã nộp/đã đạt cho một nhóm assignment bằng số truy vấn cố định.</summary>
    private async Task<IReadOnlyList<AssignmentListRow>> BuildRowsAsync(List<Assignment> assignments, CancellationToken cancellationToken)
    {
        if (assignments.Count == 0)
        {
            return new List<AssignmentListRow>();
        }

        var classIds = assignments.SelectMany(a => a.Classes.Select(c => c.ClassId)).Distinct().ToList();
        var classNames = await _db.Classes.AsNoTracking().Where(c => classIds.Contains(c.Id))
            .Select(c => new ClassRef(c.Id, c.Name)).ToListAsync(cancellationToken);

        var activeStudents = await _db.Users.AsNoTracking()
            .Where(u => u.Role == Roles.Student && u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken);
        var activeSet = activeStudents.ToHashSet();
        var members = await _db.ClassMembers.AsNoTracking()
            .Where(m => classIds.Contains(m.ClassId)).Select(m => new { m.ClassId, m.StudentId }).ToListAsync(cancellationToken);

        var exerciseIds = assignments.Select(a => a.ExerciseId).Distinct().ToList();
        var done = await _db.Submissions.AsNoTracking()
            .Where(s => exerciseIds.Contains(s.ExerciseId))
            .Select(s => new { s.ExerciseId, s.StudentId, s.IsPassed })
            .ToListAsync(cancellationToken);
        var byExercise = done
            .GroupBy(d => d.ExerciseId)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.Any(y => y.IsPassed)));

        var rows = new List<AssignmentListRow>();
        foreach (var a in assignments)
        {
            var targetIds = a.AssignedToAll
                ? activeSet
                : members.Where(m => a.Classes.Any(c => c.ClassId == m.ClassId) && activeSet.Contains(m.StudentId))
                    .Select(m => m.StudentId).ToHashSet();
            byExercise.TryGetValue(a.ExerciseId, out var perStudent);
            var submitted = perStudent is null ? 0 : targetIds.Count(perStudent.ContainsKey);
            var passed = perStudent is null ? 0 : targetIds.Count(id => perStudent.TryGetValue(id, out var p) && p);
            var classes = a.Classes.Select(c => classNames.FirstOrDefault(n => n.Id == c.ClassId)).Where(n => n is not null).Select(n => n!).OrderBy(n => n.Name).ToList();
            rows.Add(new AssignmentListRow(
                a,
                a.Exercise?.Title ?? a.ExerciseId,
                a.Exercise?.LessonId ?? string.Empty,
                a.Exercise?.Lesson?.Title ?? string.Empty,
                classes,
                targetIds.Count,
                submitted,
                passed));
        }

        return rows;
    }
}

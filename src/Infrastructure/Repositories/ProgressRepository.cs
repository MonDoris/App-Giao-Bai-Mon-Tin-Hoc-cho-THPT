using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Security;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

/// <summary>Dữ liệu bảng tiến độ bằng một số truy vấn cố định (không N+1), đủ nhanh cho 200 học sinh x 30 bài tập.</summary>
public sealed class ProgressRepository : IProgressRepository
{
    private readonly AppDbContext _db;

    public ProgressRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProgressSnapshot?> GetAsync(
        int? classId, string lessonId, string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await _db.Lessons.AnyAsync(l => l.Id == lessonId, cancellationToken))
        {
            return null;
        }

        var exercises = await _db.Exercises.AsNoTracking()
            .Where(e => e.LessonId == lessonId)
            .OrderBy(e => e.Order)
            .Select(e => new ProgressExercise(e.Id, e.Order, e.Title))
            .ToListAsync(cancellationToken);
        var exerciseIds = exercises.Select(e => e.Id).ToList();

        var query = _db.Users.AsNoTracking().Where(u => u.Role == Roles.Student);
        if (classId is { } cid)
        {
            query = query.Where(u => u.ClassMemberships.Any(m => m.ClassId == cid));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = SearchPattern.Contains(term);
            query = query.Where(u => EF.Functions.Like(u.Username, pattern, SearchPattern.Escape)
                || EF.Functions.Like(u.DisplayName, pattern, SearchPattern.Escape));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = query.OrderBy(u => u.DisplayName).ThenBy(u => u.Username);
        List<(int Id, string Username, string DisplayName)> pageUsers;
        if (sort == "passed")
        {
            // Sắp theo số bài đã đạt trên TOÀN BỘ học sinh của bộ lọc, rồi mới phân trang.
            var everyone = await ordered.Select(u => new { u.Id, u.Username, u.DisplayName }).ToListAsync(cancellationToken);
            var passedPairs = await _db.Submissions.AsNoTracking()
                .Where(s => s.IsPassed && exerciseIds.Contains(s.ExerciseId))
                .Select(s => new { s.StudentId, s.ExerciseId }).Distinct().ToListAsync(cancellationToken);
            var counts = passedPairs.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.Count());
            pageUsers = everyone
                .OrderByDescending(u => counts.GetValueOrDefault(u.Id))
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => (u.Id, u.Username, u.DisplayName)).ToList();
        }
        else
        {
            pageUsers = (await ordered.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new { u.Id, u.Username, u.DisplayName }).ToListAsync(cancellationToken))
                .Select(u => (u.Id, u.Username, u.DisplayName)).ToList();
        }

        var studentIds = pageUsers.Select(u => u.Id).ToList();

        var memberships = await _db.ClassMembers.AsNoTracking()
            .Where(m => studentIds.Contains(m.StudentId))
            .Select(m => new { m.StudentId, m.ClassId, m.SchoolClass!.Name })
            .ToListAsync(cancellationToken);
        var students = pageUsers.Select(u => new ProgressStudent(
            u.Id,
            u.Username,
            u.DisplayName,
            memberships.Where(m => m.StudentId == u.Id).OrderBy(m => m.Name).Select(m => new ClassRef(m.ClassId, m.Name)).ToList())).ToList();

        var assignments = (await _db.Assignments.AsNoTracking()
                .Where(a => a.CancelledUtc == null && exerciseIds.Contains(a.ExerciseId))
                .Select(a => new { a.ExerciseId, a.AssignedToAll, a.DueAtUtc, ClassIds = a.Classes.Select(c => c.ClassId).ToList() })
                .ToListAsync(cancellationToken))
            .Select(a => new ProgressAssignment(a.ExerciseId, a.AssignedToAll, a.ClassIds, a.DueAtUtc))
            .ToList();

        var rows = await _db.Submissions.AsNoTracking()
            .Where(s => studentIds.Contains(s.StudentId) && exerciseIds.Contains(s.ExerciseId))
            .Select(s => new SubmissionRepository.SubmissionRow(s.StudentId, s.ExerciseId, s.PassedTests, s.IsPassed, s.IsLate, s.SubmittedUtc))
            .ToListAsync(cancellationToken);
        var stats = SubmissionRepository.Aggregate(rows);

        var views = await _db.SolutionViews.AsNoTracking()
            .Where(v => studentIds.Contains(v.StudentId) && exerciseIds.Contains(v.ExerciseId))
            .Select(v => new { v.StudentId, v.ExerciseId })
            .ToListAsync(cancellationToken);

        var testCounts = (await _db.TestCases.AsNoTracking()
                .Where(t => exerciseIds.Contains(t.ExerciseId))
                .GroupBy(t => t.ExerciseId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Id, r => r.Count);

        return new ProgressSnapshot(
            exercises,
            students,
            total,
            assignments,
            stats,
            views.Select(v => (v.StudentId, v.ExerciseId)).ToHashSet(),
            testCounts);
    }
}

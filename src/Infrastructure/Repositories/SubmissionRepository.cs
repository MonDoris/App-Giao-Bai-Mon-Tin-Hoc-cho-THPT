using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

public sealed class SubmissionRepository : ISubmissionRepository
{
    private readonly AppDbContext _db;

    public SubmissionRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Add(Submission submission) => _db.Submissions.Add(submission);

    public async Task<IReadOnlyDictionary<string, ExerciseStats>> GetStatsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new SubmissionRow(s.StudentId, s.ExerciseId, s.PassedTests, s.IsPassed, s.IsLate, s.SubmittedUtc))
            .ToListAsync(cancellationToken);
        return Aggregate(rows).ToDictionary(p => p.Key.ExerciseId, p => p.Value);
    }

    public async Task<IReadOnlySet<string>> GetViewedSolutionExerciseIdsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var ids = await _db.SolutionViews.AsNoTracking().Where(v => v.StudentId == studentId).Select(v => v.ExerciseId).ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    public async Task RecordSolutionViewAsync(int studentId, string exerciseId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (await _db.SolutionViews.AnyAsync(v => v.StudentId == studentId && v.ExerciseId == exerciseId, cancellationToken))
        {
            return;
        }

        var view = new SolutionView { StudentId = studentId, ExerciseId = exerciseId, FirstViewedUtc = nowUtc };
        _db.SolutionViews.Add(view);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai yêu cầu cùng lúc: dòng đã được ghi bởi yêu cầu kia, bỏ qua (idempotent).
            _db.Entry(view).State = EntityState.Detached;
        }
    }

    public async Task<IReadOnlyList<Submission>> GetByStudentExerciseAsync(int studentId, string exerciseId, CancellationToken cancellationToken = default) =>
        await _db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.ExerciseId == exerciseId)
            .OrderBy(s => s.SubmittedUtc).ThenBy(s => s.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

    public Task<Submission?> GetOwnedAsync(long id, int studentId, CancellationToken cancellationToken = default) =>
        _db.Submissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.StudentId == studentId, cancellationToken);

    public Task<int> CountAttemptsAsync(int studentId, string exerciseId, CancellationToken cancellationToken = default) =>
        _db.Submissions.CountAsync(s => s.StudentId == studentId && s.ExerciseId == exerciseId, cancellationToken);

    internal sealed record SubmissionRow(int StudentId, string ExerciseId, int PassedTests, bool IsPassed, bool IsLate, DateTime SubmittedUtc);

    /// <summary>Gộp các lần nộp theo (học sinh, bài tập). Làm trong bộ nhớ trên các cột nhẹ (không có mã nguồn).</summary>
    internal static Dictionary<(int StudentId, string ExerciseId), ExerciseStats> Aggregate(IEnumerable<SubmissionRow> rows) =>
        rows.GroupBy(r => (r.StudentId, r.ExerciseId)).ToDictionary(
            g => g.Key,
            g => new ExerciseStats(
                g.Key.ExerciseId,
                g.Count(),
                g.Max(x => x.PassedTests),
                g.Any(x => x.IsPassed),
                g.Any(x => x.IsPassed && !x.IsLate),
                g.Max(x => x.SubmittedUtc)));
}

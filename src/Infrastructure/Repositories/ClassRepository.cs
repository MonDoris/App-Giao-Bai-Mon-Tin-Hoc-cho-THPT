using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

public sealed class ClassRepository : IClassRepository
{
    private readonly AppDbContext _db;

    public ClassRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ClassRow>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Classes.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ClassRow(c.Id, c.Name, c.Members.Count()))
            .ToListAsync(cancellationToken);

    public Task<SchoolClass?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Classes.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ClassRef>> GetExistingAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var list = ids.Distinct().ToList();
        return await _db.Classes.AsNoTracking()
            .Where(c => list.Contains(c.Id))
            .Select(c => new ClassRef(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken = default) =>
        _db.Classes.AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId), cancellationToken);

    public Task<bool> IsTargetOfActiveAssignmentAsync(int classId, CancellationToken cancellationToken = default) =>
        _db.AssignmentClasses.AnyAsync(ac => ac.ClassId == classId && ac.Assignment!.CancelledUtc == null, cancellationToken);

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<ClassRef>>> GetClassesOfStudentsAsync(
        IEnumerable<int> studentIds, CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        var rows = await _db.ClassMembers.AsNoTracking()
            .Where(m => ids.Contains(m.StudentId))
            .Select(m => new { m.StudentId, m.ClassId, m.SchoolClass!.Name })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.StudentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ClassRef>)g.OrderBy(r => r.Name).Select(r => new ClassRef(r.ClassId, r.Name)).ToList());
    }

    public async Task ReplaceMembershipsAsync(int studentId, IReadOnlyCollection<int> classIds, CancellationToken cancellationToken = default)
    {
        var current = await _db.ClassMembers.Where(m => m.StudentId == studentId).ToListAsync(cancellationToken);
        var wanted = classIds.ToHashSet();
        _db.ClassMembers.RemoveRange(current.Where(m => !wanted.Contains(m.ClassId)));
        foreach (var classId in wanted.Where(id => current.All(m => m.ClassId != id)))
        {
            _db.ClassMembers.Add(new ClassMember { ClassId = classId, StudentId = studentId });
        }
    }

    public void Add(SchoolClass schoolClass) => _db.Classes.Add(schoolClass);

    public async Task RemoveAsync(SchoolClass schoolClass, CancellationToken cancellationToken = default)
    {
        // Lớp chỉ còn nằm trong các lượt giao đã hủy: gỡ liên kết để không vướng khóa ngoại (Restrict).
        var links = await _db.AssignmentClasses.Where(ac => ac.ClassId == schoolClass.Id).ToListAsync(cancellationToken);
        _db.AssignmentClasses.RemoveRange(links);
        _db.Classes.Remove(schoolClass);
    }
}

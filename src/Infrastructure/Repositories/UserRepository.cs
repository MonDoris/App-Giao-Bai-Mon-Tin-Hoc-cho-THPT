using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;
using TinHoc12.Infrastructure.Persistence;

namespace TinHoc12.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);

    public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(u => u.Username == normalizedUsername, cancellationToken);

    public async Task<IReadOnlySet<string>> GetExistingUsernamesAsync(
        IEnumerable<string> normalizedUsernames, CancellationToken cancellationToken = default)
    {
        var names = normalizedUsernames.Distinct().ToList();
        if (names.Count == 0)
        {
            return new HashSet<string>();
        }

        var found = await _db.Users.AsNoTracking().Where(u => names.Contains(u.Username)).Select(u => u.Username).ToListAsync(cancellationToken);
        return found.Select(n => n.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
    }

    public void Add(User user) => _db.Users.Add(user);

    public async Task<(IReadOnlyList<User> Items, int Total)> SearchStudentsAsync(
        string? search, int? classId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
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
        var items = await query
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Username)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountActiveStudentsAsync(CancellationToken cancellationToken = default) =>
        _db.Users.CountAsync(u => u.Role == Roles.Student && u.IsActive, cancellationToken);
}

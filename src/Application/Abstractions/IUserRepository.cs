using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Người dùng theo Id (được theo dõi để cập nhật); null nếu không có.</summary>
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Người dùng theo tên đăng nhập đã chuẩn hóa chữ thường (được theo dõi).</summary>
    Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetExistingUsernamesAsync(IEnumerable<string> normalizedUsernames, CancellationToken cancellationToken = default);

    void Add(User user);

    /// <summary>Tìm học sinh (role Student), sắp theo họ tên; search khớp một phần tên đăng nhập hoặc họ tên.</summary>
    Task<(IReadOnlyList<User> Items, int Total)> SearchStudentsAsync(
        string? search, int? classId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountActiveStudentsAsync(CancellationToken cancellationToken = default);
}

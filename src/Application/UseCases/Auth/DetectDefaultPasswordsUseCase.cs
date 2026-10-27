using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases.Auth;

/// <summary>
/// Tìm các tài khoản mẫu vẫn dùng mật khẩu mẫu (AC-102). Chỉ trả về tên đăng nhập, không bao giờ trả/ghi mật khẩu.
/// </summary>
public sealed class DetectDefaultPasswordsUseCase
{
    private static readonly (string Username, string Password)[] Samples =
    {
        ("giaovien", "GiaoVien@2026"),
        ("hs01", "HocSinh@2026"),
        ("hs02", "HocSinh@2026"),
        ("hs03", "HocSinh@2026"),
    };

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public DetectDefaultPasswordsUseCase(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var found = new List<string>();
        foreach (var (username, password) in Samples)
        {
            var user = await _users.GetByUsernameAsync(PasswordPolicy.NormalizeUsername(username), cancellationToken);
            if (user is not null && _hasher.Verify(user.PasswordHash, password) != PasswordVerification.Failed)
            {
                found.Add(username);
            }
        }

        return found;
    }
}

using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;

namespace TinHoc12.Application.UseCases.Auth;

/// <summary>Kiểm tra phiên mỗi yêu cầu (AC-92), đăng xuất (đổi SecurityStamp, AC-90) và lấy thông tin người dùng (AC-93).</summary>
public sealed class SessionUseCase
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public SessionUseCase(IUserRepository users, IUnitOfWork unitOfWork)
    {
        _users = users;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Trả về người dùng nếu tài khoản còn hoạt động và SecurityStamp khớp; ngược lại null (phiên bị thu hồi).</summary>
    public async Task<UserDto?> ValidateAsync(int userId, string? securityStamp, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || string.IsNullOrEmpty(securityStamp)
            || !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            return null;
        }

        return SessionMapper.ToDto(user);
    }

    /// <summary>Đổi SecurityStamp để vô hiệu mọi cookie cũ của tài khoản. Không có tài khoản thì bỏ qua (idempotent).</summary>
    public async Task LogoutAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.RotateSecurityStamp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto> GetMeAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        return SessionMapper.ToDto(user);
    }
}

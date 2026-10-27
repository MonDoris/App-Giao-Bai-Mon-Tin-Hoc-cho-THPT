using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Rules;
using TinHoc12.Application.Security;

namespace TinHoc12.Application.UseCases.Auth;

/// <summary>Đổi mật khẩu cá nhân (AC-94). Cả hai role. Thành công đổi SecurityStamp; Web cấp lại cookie cho phiên hiện tại.</summary>
public sealed class ChangePasswordUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly AuthOptions _options;

    public ChangePasswordUseCase(IUserRepository users, IPasswordHasher hasher, IUnitOfWork unitOfWork, IClock clock, AuthOptions options)
    {
        _users = users;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options;
    }

    /// <returns>Phiên mới (SecurityStamp mới) để Web cấp lại cookie.</returns>
    public async Task<SessionTicket> ExecuteAsync(CurrentUser current, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(current.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        var errors = new FieldErrors();
        var currentPassword = request.CurrentPassword ?? string.Empty;
        var newPassword = request.NewPassword ?? string.Empty;

        if (string.IsNullOrEmpty(currentPassword))
        {
            errors.Add("currentPassword", "Vui lòng nhập mật khẩu hiện tại.");
        }
        else if (currentPassword.Length > PasswordPolicy.MaxLength)
        {
            errors.Add("currentPassword", "Mật khẩu hiện tại không đúng.");
        }

        errors.ThrowIfAny();

        if (_hasher.Verify(user.PasswordHash, currentPassword) == PasswordVerification.Failed)
        {
            // Sai mật khẩu hiện tại cũng tính vào bộ đếm sai như đăng nhập.
            user.RegisterFailedLogin(_clock.UtcNow, _options.MaxFailedAttempts, _options.LockoutDuration);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ValidationFailedException("currentPassword", "Mật khẩu hiện tại không đúng.");
        }

        var policyError = PasswordPolicy.Validate(newPassword, user.Username);
        if (policyError is not null)
        {
            throw new ValidationFailedException("newPassword", policyError);
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            throw new ValidationFailedException("newPassword", "Mật khẩu mới không được trùng mật khẩu cũ.");
        }

        user.PasswordHash = _hasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.ClearLockout();
        user.RotateSecurityStamp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return SessionMapper.ToTicket(user, null);
    }
}

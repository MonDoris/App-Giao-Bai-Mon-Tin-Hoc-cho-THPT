using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Dtos;
using TinHoc12.Application.Exceptions;
using TinHoc12.Application.Mapping;
using TinHoc12.Application.Security;
using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.UseCases.Auth;

/// <summary>Đăng nhập (AC-82, 85, 86, 88): một thông báo chung cho mọi lỗi, khóa tạm sau nhiều lần sai.</summary>
public sealed class LoginUseCase
{
    private static readonly object DummyLock = new();
    private static string? _dummyHash;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly AuthOptions _options;

    public LoginUseCase(IUserRepository users, IPasswordHasher hasher, IUnitOfWork unitOfWork, IClock clock, AuthOptions options)
    {
        _users = users;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options;
    }

    /// <exception cref="ValidationFailedException">Thiếu hoặc quá dài (kiểm tra trước khi băm).</exception>
    /// <exception cref="InvalidCredentialsException">Sai thông tin, tài khoản bị khóa hoặc bị vô hiệu hóa.</exception>
    public async Task<SessionTicket> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new FieldErrors();
        var rawUsername = request.Username ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawUsername))
        {
            errors.Add("username", "Vui lòng nhập tên đăng nhập.");
        }
        else if (rawUsername.Trim().Length > PasswordPolicy.UsernameMaxLength)
        {
            errors.Add("username", $"Tên đăng nhập tối đa {PasswordPolicy.UsernameMaxLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("password", "Vui lòng nhập mật khẩu.");
        }
        else if (password.Length > PasswordPolicy.MaxLength)
        {
            errors.Add("password", $"Mật khẩu tối đa {PasswordPolicy.MaxLength} ký tự.");
        }

        errors.ThrowIfAny();

        var username = PasswordPolicy.NormalizeUsername(rawUsername);
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            // Vẫn tốn một lần băm để thời gian phản hồi xấp xỉ trường hợp sai mật khẩu (chống dò tài khoản).
            _hasher.Verify(GetDummyHash(), password);
            throw new InvalidCredentialsException();
        }

        var now = _clock.UtcNow;
        var verification = _hasher.Verify(user.PasswordHash, password);

        if (!user.IsActive || user.IsLockedOut(now))
        {
            throw new InvalidCredentialsException();
        }

        if (verification == PasswordVerification.Failed)
        {
            user.RegisterFailedLogin(now, _options.MaxFailedAttempts, _options.LockoutDuration);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        user.RegisterSuccessfulLogin(now);
        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.Hash(password);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return SessionMapper.ToTicket(user, request.ReturnUrl);
    }

    private string GetDummyHash()
    {
        if (_dummyHash is null)
        {
            lock (DummyLock)
            {
                _dummyHash ??= _hasher.Hash(Guid.NewGuid().ToString("N"));
            }
        }

        return _dummyHash;
    }
}

internal static class SessionMapper
{
    public static UserDto ToDto(User user) =>
        new(user.Id, user.Username, user.DisplayName, user.Role, user.MustChangePassword);

    public static SessionTicket ToTicket(User user, string? returnUrl)
    {
        var redirect = user.MustChangePassword
            ? ReturnUrlPolicy.ChangePasswordPage
            : ReturnUrlPolicy.Sanitize(returnUrl, user.Role) ?? ReturnUrlPolicy.HomeFor(user.Role);
        return new SessionTicket(ToDto(user), user.SecurityStamp, redirect);
    }
}

namespace TinHoc12.Application.Dtos;

public sealed record LoginRequest(string? Username, string? Password, string? ReturnUrl = null);

public sealed record UserDto(int Id, string Username, string DisplayName, string Role, bool MustChangePassword);

/// <summary>Phản hồi đăng nhập gửi cho trình duyệt (không có hash/securityStamp).</summary>
public sealed record LoginResultDto(UserDto User, string RedirectUrl);

/// <summary>Kết quả nội bộ của use case: Web dùng SecurityStamp để tạo cookie phiên, không gửi ra ngoài.</summary>
public sealed record SessionTicket(UserDto User, string SecurityStamp, string RedirectUrl);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>Người dùng của một phiên hợp lệ (đã kiểm tra IsActive và SecurityStamp).</summary>
public sealed record SessionUser(UserDto User);

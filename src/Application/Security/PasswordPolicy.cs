namespace TinHoc12.Application.Security;

/// <summary>Chính sách mật khẩu và tên đăng nhập (QD-20).</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;

    public const string Hint = "Mật khẩu dài 8–128 ký tự, có ít nhất 1 chữ cái và 1 chữ số, không trùng tên đăng nhập.";

    /// <summary>Trả về thông báo lỗi tiếng Việt, hoặc null nếu hợp lệ.</summary>
    public static string? Validate(string? password, string? username)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Vui lòng nhập mật khẩu.";
        }

        if (password.Length < MinLength || password.Length > MaxLength)
        {
            return $"Mật khẩu phải dài từ {MinLength} đến {MaxLength} ký tự.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "Mật khẩu phải có ít nhất 1 chữ cái và 1 chữ số.";
        }

        if (!string.IsNullOrEmpty(username) && string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
        {
            return "Mật khẩu không được trùng tên đăng nhập.";
        }

        return null;
    }

    /// <summary>Chuẩn hóa tên đăng nhập: cắt khoảng trắng đầu/cuối, chữ thường.</summary>
    public static string NormalizeUsername(string? username) => (username ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidUsername(string username)
    {
        if (username.Length < UsernameMinLength || username.Length > UsernameMaxLength)
        {
            return false;
        }

        return username.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-');
    }
}

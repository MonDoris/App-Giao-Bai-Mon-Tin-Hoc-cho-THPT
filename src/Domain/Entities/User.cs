namespace TinHoc12.Domain.Entities;

/// <summary>Tài khoản đăng nhập (Teacher hoặc Student).</summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Tên đăng nhập, luôn lưu chữ thường.</summary>
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Teacher | Student.</summary>
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>Đổi khi đăng xuất/đổi mật khẩu/khóa tài khoản để vô hiệu mọi cookie cũ.</summary>
    public string SecurityStamp { get; set; } = NewStamp();

    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedUtc { get; set; }

    public ICollection<ClassMember> ClassMemberships { get; set; } = new List<ClassMember>();

    public static string NewStamp() => Guid.NewGuid().ToString("N");

    public void RotateSecurityStamp() => SecurityStamp = NewStamp();

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc is { } end && end > nowUtc;

    /// <summary>Ghi nhận một lần sai mật khẩu; đủ ngưỡng thì khóa tạm và đặt lại bộ đếm.</summary>
    public void RegisterFailedLogin(DateTime nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEndUtc = nowUtc + lockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
    }

    public void ClearLockout()
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
    }
}

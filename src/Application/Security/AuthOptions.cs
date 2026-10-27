namespace TinHoc12.Application.Security;

/// <summary>Tham số xác thực (AC-159). Giá trị lấy từ cấu hình ngoài mã nguồn, thiếu thì dùng mặc định.</summary>
public sealed class AuthOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int IdleMinutes { get; set; } = 120;
    public int AbsoluteHours { get; set; } = 10;
    public int LoginRatePerMinute { get; set; } = 60;
    public int SubmissionMinIntervalSeconds { get; set; } = 3;

    /// <summary>Thư mục lưu khóa Data Protection (tùy chọn). Để trống: dùng nơi lưu mặc định của ASP.NET Core.</summary>
    public string? KeyDirectory { get; set; }

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);

    /// <summary>Báo lỗi cấu hình rõ ràng khi có giá trị không hợp lệ (&lt;= 0).</summary>
    public void Validate()
    {
        Check(nameof(MaxFailedAttempts), MaxFailedAttempts);
        Check(nameof(LockoutMinutes), LockoutMinutes);
        Check(nameof(IdleMinutes), IdleMinutes);
        Check(nameof(AbsoluteHours), AbsoluteHours);
        Check(nameof(LoginRatePerMinute), LoginRatePerMinute);
        Check(nameof(SubmissionMinIntervalSeconds), SubmissionMinIntervalSeconds);
    }

    private static void Check(string name, int value)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"Cấu hình Auth:{name} không hợp lệ (phải là số nguyên dương, hiện là {value}).");
        }
    }
}

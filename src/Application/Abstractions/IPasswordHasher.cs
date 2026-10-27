namespace TinHoc12.Application.Abstractions;

public enum PasswordVerification
{
    Failed,
    Success,

    /// <summary>Đúng mật khẩu nhưng hash dùng tham số yếu hơn cấu hình hiện tại: nên băm lại.</summary>
    SuccessRehashNeeded,
}

/// <summary>Băm và kiểm tra mật khẩu (QD-19). Cài đặt nằm ở Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string hash, string password);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TinHoc12.Application.Abstractions;

namespace TinHoc12.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-HMAC-SHA512 (QD-19, AC-84). Định dạng tự mô tả để nâng cấp về sau:
/// <c>PBKDF2-SHA512$&lt;số vòng&gt;$&lt;salt base64&gt;$&lt;hash base64&gt;</c>. Salt ngẫu nhiên 128 bit cho mỗi mật khẩu.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string Algorithm = "PBKDF2-SHA512";
    public const int CurrentIterations = 120_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 64;
    private const int MaxIterationsAccepted = 5_000_000;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher() : this(CurrentIterations)
    {
    }

    /// <summary>Cho phép kiểm thử dùng số vòng khác (ví dụ kiểm tra băm lại hash cũ yếu hơn).</summary>
    public Pbkdf2PasswordHasher(int iterations)
    {
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, _iterations, HashBytes);
        return string.Create(CultureInfo.InvariantCulture, $"{Algorithm}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public PasswordVerification Verify(string hash, string password)
    {
        if (!TryParse(hash, out var iterations, out var salt, out var expected))
        {
            return PasswordVerification.Failed;
        }

        var actual = Derive(password, salt, iterations, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordVerification.Failed;
        }

        return iterations < _iterations || salt.Length < SaltBytes || expected.Length < HashBytes
            ? PasswordVerification.SuccessRehashNeeded
            : PasswordVerification.Success;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, length);

    private static bool TryParse(string? hash, out int iterations, out byte[] salt, out byte[] expected)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        expected = Array.Empty<byte>();
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations < 1 || iterations > MaxIterationsAccepted)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && expected.Length > 0;
    }
}

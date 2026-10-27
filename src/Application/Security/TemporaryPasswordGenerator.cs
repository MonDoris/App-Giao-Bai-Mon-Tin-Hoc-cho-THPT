using System.Security.Cryptography;

namespace TinHoc12.Application.Security;

/// <summary>Sinh mật khẩu tạm bằng CSPRNG (QD-21): 10 ký tự, loại ký tự dễ nhầm (0/O/1/l/I), luôn có chữ cái và chữ số.</summary>
public static class TemporaryPasswordGenerator
{
    private const string Letters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    public const int Length = 10;

    public static string Generate()
    {
        var chars = new char[Length];
        var all = Letters + Digits;
        for (var i = 0; i < Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Đảm bảo thỏa chính sách: ít nhất 1 chữ cái và 1 chữ số ở các vị trí ngẫu nhiên khác nhau.
        var letterAt = RandomNumberGenerator.GetInt32(Length);
        int digitAt;
        do
        {
            digitAt = RandomNumberGenerator.GetInt32(Length);
        }
        while (digitAt == letterAt);

        chars[letterAt] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        chars[digitAt] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        return new string(chars);
    }
}

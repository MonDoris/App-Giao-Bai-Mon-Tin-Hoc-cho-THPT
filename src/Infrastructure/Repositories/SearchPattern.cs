namespace TinHoc12.Infrastructure.Repositories;

/// <summary>Tạo mẫu LIKE an toàn (không cho người dùng chèn ký tự đại diện) dùng với EF.Functions.Like(..., Escape).</summary>
internal static class SearchPattern
{
    public const string Escape = @"\";

    public static string Contains(string term)
    {
        var escaped = term
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
        return "%" + escaped + "%";
    }
}

using System.Globalization;
using System.Text;

namespace TinHoc12.Application.Rules;

/// <summary>Sinh mã (slug) từ tiêu đề tiếng Việt (QD-36): bỏ dấu, chữ thường, ký tự lạ thành "-", gộp "-".</summary>
public static class SlugGenerator
{
    public static string Slugify(string? text, int maxLength = IdRules.MaxLength, string fallback = "muc")
    {
        var normalized = (text ?? string.Empty).Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var lastDash = true;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var c = char.ToLowerInvariant(ch);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        var slug = Cut(sb.ToString().Trim('-'), maxLength);
        return slug.Length == 0 ? fallback : slug;
    }

    /// <summary>Trả về mã chưa tồn tại: thêm hậu tố -2, -3... khi trùng, luôn giữ độ dài &lt;= maxLength.</summary>
    public static async Task<string> UniqueAsync(string baseSlug, Func<string, Task<bool>> exists, int maxLength = IdRules.MaxLength)
    {
        var candidate = Cut(baseSlug, maxLength);
        if (!await exists(candidate))
        {
            return candidate;
        }

        for (var n = 2; ; n++)
        {
            var suffix = "-" + n.ToString(CultureInfo.InvariantCulture);
            var next = Cut(baseSlug, maxLength - suffix.Length) + suffix;
            if (!await exists(next))
            {
                return next;
            }
        }
    }

    private static string Cut(string slug, int maxLength)
    {
        if (slug.Length <= maxLength)
        {
            return slug;
        }

        return slug[..maxLength].TrimEnd('-');
    }
}

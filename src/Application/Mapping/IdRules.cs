namespace TinHoc12.Application.Mapping;

/// <summary>Quy ước ID dạng slug [a-z0-9-], tối đa 100 ký tự.</summary>
public static class IdRules
{
    public const int MaxLength = 100;

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in id)
        {
            var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}

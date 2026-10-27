using TinHoc12.Application.Exceptions;

namespace TinHoc12.Application.Mapping;

/// <summary>Gom lỗi kiểm tra đầu vào theo từng trường rồi ném ValidationFailedException một lần (AC-113).</summary>
public sealed class FieldErrors
{
    private readonly List<FieldError> _errors = new();

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message) => _errors.Add(new FieldError(field, message));

    public void ThrowIfAny(string code = ValidationFailedException.DefaultCode)
    {
        if (_errors.Count > 0)
        {
            throw new ValidationFailedException(_errors.ToArray(), code);
        }
    }

    /// <summary>
    /// Kiểm tra một chuỗi văn bản. Trả về chuỗi đã chuẩn hóa (cắt khoảng trắng nếu trim) hoặc null nếu có lỗi.
    /// allowMultiline: cho phép xuống dòng/tab (mã, nội dung); ký tự điều khiển khác bị từ chối.
    /// </summary>
    public string? Text(string field, string label, string? value, int maxLength, bool required = true,
        bool trim = true, bool allowMultiline = false)
    {
        if (value is null)
        {
            if (required)
            {
                Add(field, $"{label} là bắt buộc.");
            }

            return null;
        }

        var text = trim ? value.Trim() : value;
        if (required && string.IsNullOrWhiteSpace(text))
        {
            Add(field, $"{label} không được để trống.");
            return null;
        }

        if (text.Length > maxLength)
        {
            Add(field, $"{label} tối đa {maxLength:N0} ký tự (hiện có {text.Length:N0}).");
            return null;
        }

        if (HasForbiddenControlChars(text, allowMultiline))
        {
            Add(field, $"{label} chứa ký tự điều khiển không hợp lệ.");
            return null;
        }

        return text;
    }

    /// <summary>Danh sách khối văn bản: min..max khối, mỗi khối không rỗng và không quá maxBlockLength.</summary>
    public List<string>? Blocks(string field, string label, IReadOnlyList<string>? blocks, int min, int max, int maxBlockLength)
    {
        if (blocks is null || blocks.Count < min)
        {
            Add(field, min <= 1 ? $"{label} cần ít nhất 1 khối nội dung." : $"{label} cần ít nhất {min} khối.");
            return null;
        }

        if (blocks.Count > max)
        {
            Add(field, $"{label} tối đa {max} khối.");
            return null;
        }

        var result = new List<string>(blocks.Count);
        var ok = true;
        for (var i = 0; i < blocks.Count; i++)
        {
            var text = Text($"{field}[{i}]", $"{label} (khối {i + 1})", blocks[i], maxBlockLength, allowMultiline: true);
            if (text is null)
            {
                ok = false;
            }
            else
            {
                result.Add(text);
            }
        }

        return ok ? result : null;
    }

    public static bool HasForbiddenControlChars(string text, bool allowMultiline)
    {
        foreach (var c in text)
        {
            if (c < ' ' || c == '\u007f')
            {
                var allowed = allowMultiline && (c == '\n' || c == '\r' || c == '\t');
                if (!allowed)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

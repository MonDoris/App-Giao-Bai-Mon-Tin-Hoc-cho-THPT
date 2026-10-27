namespace TinHoc12.Application.Exceptions;

/// <summary>Người gọi đã đăng nhập nhưng không có quyền (sai role, thao tác với tài khoản giáo viên...). -> 403.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.") : base(message) { }
}

/// <summary>Đăng nhập thất bại. Mọi nguyên nhân dùng một thông báo chung (AC-85). -> 401.</summary>
public sealed class InvalidCredentialsException : Exception
{
    public const string GenericMessage = "Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.";

    public InvalidCredentialsException() : base(GenericMessage) { }
}

public sealed record FieldError(string Field, string Message);

/// <summary>Dữ liệu đầu vào sai. -> 422 kèm danh sách lỗi theo từng trường.</summary>
public sealed class ValidationFailedException : Exception
{
    public const string DefaultCode = "VALIDATION_FAILED";

    public IReadOnlyList<FieldError> Errors { get; }
    public string Code { get; }

    public ValidationFailedException(IReadOnlyList<FieldError> errors, string code = DefaultCode, string? message = null)
        : base(message ?? (errors.Count == 1 ? errors[0].Message : "Dữ liệu không hợp lệ, hãy kiểm tra các trường được đánh dấu."))
    {
        Errors = errors;
        Code = code;
    }

    public ValidationFailedException(string field, string message, string code = DefaultCode)
        : this(new[] { new FieldError(field, message) }, code, message)
    {
    }
}

/// <summary>Xung đột với trạng thái hiện tại. Code: CONFLICT, HAS_DEPENDENCIES, DUPLICATE_ID, DUPLICATE_USERNAME, EXERCISE_CHANGED. -> 409.</summary>
public sealed class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>Gửi quá nhanh. -> 429.</summary>
public sealed class TooManyRequestsException : Exception
{
    public int RetryAfterSeconds { get; }

    public TooManyRequestsException(string message, int retryAfterSeconds = 3) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

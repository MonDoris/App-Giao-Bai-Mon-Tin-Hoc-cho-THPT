using System.Text.Json.Serialization;
using TinHoc12.Application.Exceptions;

namespace TinHoc12.Web.Middleware;

/// <summary>Lược đồ lỗi JSON chung của API: { error, message, field?, errors? }.</summary>
public sealed record ApiError(
    string Error,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Field = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FieldError>? Errors = null);

public static class ApiErrors
{
    public static IResult Unauthenticated() =>
        Results.Json(
            new ApiError("UNAUTHENTICATED", "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn."),
            statusCode: StatusCodes.Status401Unauthorized);

    public static IResult InvalidCredentials() =>
        Results.Json(new ApiError("INVALID_CREDENTIALS", InvalidCredentialsException.GenericMessage), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.") =>
        Results.Json(new ApiError("FORBIDDEN", message), statusCode: StatusCodes.Status403Forbidden);

    public static IResult PasswordChangeRequired() =>
        Results.Json(
            new ApiError("PASSWORD_CHANGE_REQUIRED", "Bạn cần đổi mật khẩu trước khi sử dụng các chức năng khác."),
            statusCode: StatusCodes.Status403Forbidden);

    public static IResult CsrfInvalid() =>
        Results.Json(
            new ApiError("CSRF_INVALID", "Yêu cầu không hợp lệ (thiếu hoặc sai mã bảo vệ). Hãy tải lại trang rồi thử lại."),
            statusCode: StatusCodes.Status400BadRequest);

    public static IResult NotFound(string message) =>
        Results.Json(new ApiError("NOT_FOUND", message), statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status409Conflict);

    public static IResult ValidationFailed(ValidationFailedException ex) =>
        Results.Json(
            new ApiError(
                ex.Code,
                ex.Message,
                ex.Errors.Count == 1 ? ex.Errors[0].Field : null,
                ex.Errors),
            statusCode: StatusCodes.Status422UnprocessableEntity);

    public static IResult BadInput(string message = "Dữ liệu gửi lên không hợp lệ (sai định dạng hoặc sai kiểu dữ liệu).") =>
        Results.Json(new ApiError("VALIDATION_FAILED", message), statusCode: StatusCodes.Status422UnprocessableEntity);

    public static IResult DataInvalid(string field, string message) =>
        Results.Json(new ApiError("DATA_INVALID", message, field), statusCode: StatusCodes.Status422UnprocessableEntity);

    public static IResult TooManyRequests(string message) =>
        Results.Json(new ApiError("TOO_MANY_REQUESTS", message), statusCode: StatusCodes.Status429TooManyRequests);

    public static IResult PayloadTooLarge() =>
        Results.Json(
            new ApiError("PAYLOAD_TOO_LARGE", "Dữ liệu gửi lên quá lớn."),
            statusCode: StatusCodes.Status413PayloadTooLarge);

    public static IResult UnsupportedMediaType() =>
        Results.Json(
            new ApiError("UNSUPPORTED_MEDIA_TYPE", "Dữ liệu gửi lên phải ở dạng JSON."),
            statusCode: StatusCodes.Status415UnsupportedMediaType);

    public static IResult Unavailable() =>
        Results.Json(
            new ApiError("UNAVAILABLE", "Máy chủ dữ liệu đang bận, vui lòng thử lại sau."),
            statusCode: StatusCodes.Status503ServiceUnavailable);

    public static IResult Internal() =>
        Results.Json(
            new ApiError("INTERNAL_ERROR", "Đã xảy ra lỗi trên máy chủ, vui lòng thử lại sau."),
            statusCode: StatusCodes.Status500InternalServerError);
}

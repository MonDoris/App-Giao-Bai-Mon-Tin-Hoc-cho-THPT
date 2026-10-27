namespace TinHoc12.Application.Security;

/// <summary>Chống chuyển hướng mở và chọn trang đích sau đăng nhập (QD-24, AC-98).</summary>
public static class ReturnUrlPolicy
{
    public const string ChangePasswordPage = "/change-password.html";
    public const string TeacherHome = "/teacher/dashboard.html";
    public const string StudentHome = "/dashboard.html";
    public const int MaxLength = 300;

    public static string HomeFor(string role) => role == Roles.Teacher ? TeacherHome : StudentHome;

    /// <summary>Trả về returnUrl nếu hợp lệ và role được phép vào; ngược lại trả về null.</summary>
    public static string? Sanitize(string? returnUrl, string role)
    {
        if (string.IsNullOrEmpty(returnUrl) || returnUrl.Length > MaxLength)
        {
            return null;
        }

        if (returnUrl[0] != '/' || (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')))
        {
            return null;
        }

        if (returnUrl.Any(c => char.IsControl(c) || c == '\\'))
        {
            return null;
        }

        var path = returnUrl;
        var cut = path.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0)
        {
            path = path[..cut];
        }

        // "/" và "/index.html" là landing mới (QD-92); "/landing.html" là tên cũ đã bỏ nên cũng không dẫn tới (tránh 404).
        if (path == "/" || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login.html", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/landing.html", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var isTeacherArea = path.StartsWith("/teacher/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/teacher", StringComparison.OrdinalIgnoreCase);
        if (isTeacherArea && role != Roles.Teacher)
        {
            return null;
        }

        if (role == Roles.Teacher
            && (string.Equals(path, "/assignments.html", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, StudentHome, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return returnUrl;
    }
}

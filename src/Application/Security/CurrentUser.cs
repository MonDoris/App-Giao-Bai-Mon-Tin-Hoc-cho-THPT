using TinHoc12.Application.Exceptions;

namespace TinHoc12.Application.Security;

/// <summary>Danh tính người đang gọi (lấy từ phiên đăng nhập, không bao giờ lấy từ nội dung yêu cầu).</summary>
public sealed record CurrentUser(int Id, string Role)
{
    public bool IsTeacher => Role == Roles.Teacher;
    public bool IsStudent => Role == Roles.Student;

    public void RequireTeacher()
    {
        if (!IsTeacher)
        {
            throw new ForbiddenException();
        }
    }

    public void RequireStudent()
    {
        if (!IsStudent)
        {
            throw new ForbiddenException();
        }
    }
}

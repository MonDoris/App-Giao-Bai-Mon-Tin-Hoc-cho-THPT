using TinHoc12.Application.Security;

namespace TinHoc12.Application.Tests;

/// <summary>
/// Bảng véc-tơ chuẩn của ReturnUrlPolicy.Sanitize (AC-501, AC-528, mục 4.1 của đặc tả v6).
/// Cùng bảng với tests/frontend/guard.test.mjs để máy chủ và client luôn khớp (AC-507, AC-517).
/// </summary>
public class ReturnUrlPolicyTests
{
    private static readonly string[] BothRolesNull =
    {
        "/", "/index.html", "/index.html?x=1", "/index.html#a", "/INDEX.HTML", "/landing.html", "/landing.html?x=1",
        "/login.html", "/login.html?returnUrl=%2F", "/api/lessons", "/API/me",
        "//evil.com", "/\\evil.com", "https://evil.com", "javascript:alert(1)", "data:text/html,x", "/a\r\nb", "/a\u007fb", "",
    };

    public static IEnumerable<object[]> RejectedForBoth() =>
        BothRolesNull.SelectMany(url => new[] { new object[] { url, Roles.Student }, new object[] { url, Roles.Teacher } });

    [Theory]
    [MemberData(nameof(RejectedForBoth))]
    public void Urls_that_are_never_valid_return_targets_give_null(string url, string role)
    {
        Assert.Null(ReturnUrlPolicy.Sanitize(url, role));
    }

    [Theory]
    [InlineData(Roles.Student)]
    [InlineData(Roles.Teacher)]
    public void Null_and_too_long_inputs_give_null(string role)
    {
        Assert.Null(ReturnUrlPolicy.Sanitize(null, role));
        Assert.Null(ReturnUrlPolicy.Sanitize("/" + new string('a', 400), role));
        Assert.Equal("/" + new string('a', 299), ReturnUrlPolicy.Sanitize("/" + new string('a', 299), role));
    }

    [Theory]
    [InlineData("/lessons.html")]
    [InlineData("/lesson.html?id=x")]
    [InlineData("/problem.html?lesson=a&problem=b")]
    [InlineData("/change-password.html")]
    public void Shared_pages_are_valid_for_both_roles(string url)
    {
        Assert.Equal(url, ReturnUrlPolicy.Sanitize(url, Roles.Student));
        Assert.Equal(url, ReturnUrlPolicy.Sanitize(url, Roles.Teacher));
    }

    [Theory]
    [InlineData("/dashboard.html")]
    [InlineData("/dashboard.html?x=1")]
    [InlineData("/assignments.html")]
    public void Student_pages_are_valid_for_students_and_rejected_for_teachers(string url)
    {
        Assert.Equal(url, ReturnUrlPolicy.Sanitize(url, Roles.Student));
        Assert.Null(ReturnUrlPolicy.Sanitize(url, Roles.Teacher));
    }

    [Theory]
    [InlineData("/teacher")]
    [InlineData("/teacher/")]
    [InlineData("/teacher/index.html")]
    [InlineData("/TEACHER/dashboard.html")]
    public void Teacher_area_is_rejected_for_students(string url)
    {
        Assert.Null(ReturnUrlPolicy.Sanitize(url, Roles.Student));
    }

    [Theory]
    [InlineData("/teacher/index.html")]
    [InlineData("/teacher/")]
    [InlineData("/teacher/dashboard.html")]
    public void Teacher_area_is_valid_for_teachers_and_not_mistaken_for_the_landing_page(string url)
    {
        Assert.Equal(url, ReturnUrlPolicy.Sanitize(url, Roles.Teacher));
    }

    [Fact]
    public void Home_pages_are_unchanged()
    {
        Assert.Equal("/dashboard.html", ReturnUrlPolicy.StudentHome);
        Assert.Equal("/teacher/dashboard.html", ReturnUrlPolicy.TeacherHome);
        Assert.Equal("/dashboard.html", ReturnUrlPolicy.HomeFor(Roles.Student));
        Assert.Equal("/teacher/dashboard.html", ReturnUrlPolicy.HomeFor(Roles.Teacher));
    }
}

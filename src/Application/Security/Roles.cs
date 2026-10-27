namespace TinHoc12.Application.Security;

/// <summary>Hai role cố định của hệ thống (QD-15).</summary>
public static class Roles
{
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static bool IsValid(string? role) => role == Teacher || role == Student;
}

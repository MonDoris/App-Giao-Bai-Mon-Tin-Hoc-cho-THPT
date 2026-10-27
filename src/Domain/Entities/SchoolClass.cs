namespace TinHoc12.Domain.Entities;

/// <summary>Một lớp học (ví dụ 12A1). Giáo viên gán học sinh vào lớp.</summary>
public class SchoolClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }

    public ICollection<ClassMember> Members { get; set; } = new List<ClassMember>();
}

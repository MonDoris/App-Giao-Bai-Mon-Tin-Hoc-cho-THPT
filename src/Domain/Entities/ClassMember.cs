namespace TinHoc12.Domain.Entities;

/// <summary>Học sinh thuộc một lớp.</summary>
public class ClassMember
{
    public int ClassId { get; set; }
    public int StudentId { get; set; }

    public SchoolClass? SchoolClass { get; set; }
    public User? Student { get; set; }
}

namespace TinHoc12.Domain.Entities;

/// <summary>Lớp là đối tượng nhận của một assignment.</summary>
public class AssignmentClass
{
    public int AssignmentId { get; set; }
    public int ClassId { get; set; }

    public Assignment? Assignment { get; set; }
    public SchoolClass? SchoolClass { get; set; }
}

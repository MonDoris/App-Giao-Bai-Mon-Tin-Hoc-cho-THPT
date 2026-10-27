namespace TinHoc12.Domain.Entities;

/// <summary>Một lần giao một bài tập cho "tất cả học sinh" hoặc cho một số lớp.</summary>
public class Assignment
{
    public int Id { get; set; }
    public string ExerciseId { get; set; } = string.Empty;
    public bool AssignedToAll { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string? Note { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }

    /// <summary>Khác null: đã hủy mềm (học sinh mất quyền truy cập, bài nộp giữ nguyên).</summary>
    public DateTime? CancelledUtc { get; set; }

    public Exercise? Exercise { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<AssignmentClass> Classes { get; set; } = new List<AssignmentClass>();

    public bool IsActive => CancelledUtc is null;
}

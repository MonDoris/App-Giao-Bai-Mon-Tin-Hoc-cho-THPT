namespace TinHoc12.Domain.Entities;

/// <summary>Một bài học. Các trường mảng (Summary, Notes, Conclusion) lưu dạng văn bản JSON trong DB.</summary>
public class Lesson
{
    public string Id { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Mảng JSON các chuỗi (cột Summary).</summary>
    public string SummaryJson { get; set; } = "[]";

    /// <summary>Mảng JSON các chuỗi (cột Notes).</summary>
    public string NotesJson { get; set; } = "[]";

    /// <summary>Mảng JSON các chuỗi (cột Conclusion).</summary>
    public string ConclusionJson { get; set; } = "[]";

    /// <summary>Bản đã xuất bản (học sinh chỉ thấy bản này). Dữ liệu tạo bằng SQL cũ mặc định là true.</summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>Đã lưu trữ: ẩn với học sinh, vẫn giữ dữ liệu.</summary>
    public bool IsArchived { get; set; }

    public int? CreatedByUserId { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>Token đồng thời: tăng 1 mỗi lần cập nhật.</summary>
    public int Version { get; set; } = 1;

    public User? CreatedBy { get; set; }
    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
}

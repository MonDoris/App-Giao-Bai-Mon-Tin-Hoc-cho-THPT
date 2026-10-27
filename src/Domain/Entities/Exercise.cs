namespace TinHoc12.Domain.Entities;

/// <summary>Một bài tập lập trình thuộc một bài học.</summary>
public class Exercise
{
    public string Id { get; set; } = string.Empty;
    public string LessonId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Easy | Medium | Hard.</summary>
    public string Difficulty { get; set; } = string.Empty;

    public string Statement { get; set; } = string.Empty;
    public string InputFormat { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = string.Empty;
    public string? Constraints { get; set; }

    /// <summary>Mảng JSON {input, output, explanation?} (cột Examples).</summary>
    public string ExamplesJson { get; set; } = "[]";

    public string StarterCode { get; set; } = string.Empty;

    /// <summary>Null hoặc rỗng nghĩa là bài tập không có đáp án.</summary>
    public string? SolutionCode { get; set; }

    public string? SolutionExplanation { get; set; }
    public bool ConfirmEveryTime { get; set; }
    public int TimeLimitMs { get; set; } = 5000;

    /// <summary>Bản đã xuất bản (dữ liệu tạo bằng SQL cũ mặc định là true).</summary>
    public bool IsPublished { get; set; } = true;

    public bool IsArchived { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>Token đồng thời: tăng 1 mỗi lần cập nhật.</summary>
    public int Version { get; set; } = 1;

    public User? CreatedBy { get; set; }
    public Lesson? Lesson { get; set; }
    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();

    public bool HasSolution => !string.IsNullOrWhiteSpace(SolutionCode);
}

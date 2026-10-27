namespace TinHoc12.Domain.Entities;

/// <summary>Một lần nộp bài của học sinh (chấm tại trình duyệt, server lưu lại để theo dõi).</summary>
public class Submission
{
    public long Id { get; set; }
    public int AssignmentId { get; set; }
    public string ExerciseId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int PassedTests { get; set; }
    public int TotalTests { get; set; }
    public bool IsPassed { get; set; }
    public bool IsLate { get; set; }

    /// <summary>Mảng JSON trạng thái từng test (tùy chọn).</summary>
    public string? ResultsJson { get; set; }

    public DateTime SubmittedUtc { get; set; }

    public Assignment? Assignment { get; set; }
    public Exercise? Exercise { get; set; }
    public User? Student { get; set; }
}

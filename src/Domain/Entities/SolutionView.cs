namespace TinHoc12.Domain.Entities;

/// <summary>Lần đầu một học sinh xem đáp án của một bài tập.</summary>
public class SolutionView
{
    public int StudentId { get; set; }
    public string ExerciseId { get; set; } = string.Empty;
    public DateTime FirstViewedUtc { get; set; }
}

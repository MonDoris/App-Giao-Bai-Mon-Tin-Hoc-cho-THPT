namespace TinHoc12.Domain.Entities;

/// <summary>Một test case dùng để chấm bài tập.</summary>
public class TestCase
{
    public int Id { get; set; }
    public string ExerciseId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>true: test ẩn, giao diện không được lộ input/đầu ra mong đợi.</summary>
    public bool IsHidden { get; set; }

    public Exercise? Exercise { get; set; }
}

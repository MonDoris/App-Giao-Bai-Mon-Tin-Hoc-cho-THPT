namespace TinHoc12.Application.Abstractions;

public sealed record ProgressExercise(string Id, int Order, string Title);

public sealed record ProgressStudent(int Id, string Username, string DisplayName, IReadOnlyList<ClassRef> Classes);

/// <summary>Assignment chưa hủy rút gọn để quyết định học sinh có được giao hay không.</summary>
public sealed record ProgressAssignment(string ExerciseId, bool AssignedToAll, IReadOnlyList<int> ClassIds, DateTime? DueAtUtc);

public sealed record ProgressSnapshot(
    IReadOnlyList<ProgressExercise> Exercises,
    IReadOnlyList<ProgressStudent> Students,
    int TotalStudents,
    IReadOnlyList<ProgressAssignment> Assignments,
    IReadOnlyDictionary<(int StudentId, string ExerciseId), ExerciseStats> Stats,
    IReadOnlySet<(int StudentId, string ExerciseId)> ViewedSolutions,
    IReadOnlyDictionary<string, int> TestCounts);

public interface IProgressRepository
{
    /// <summary>Dữ liệu cho bảng tiến độ (sort = "name" hoặc "passed": số bài đã đạt giảm dần, sắp trên toàn bộ học sinh của bộ lọc rồi mới phân trang): số truy vấn cố định, không N+1 (AC-143). Null nếu bài học không tồn tại.</summary>
    Task<ProgressSnapshot?> GetAsync(
        int? classId, string lessonId, string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default);
}

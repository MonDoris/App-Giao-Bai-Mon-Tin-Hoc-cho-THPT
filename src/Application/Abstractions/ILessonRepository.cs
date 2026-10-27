using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Abstractions;

/// <summary>Thông tin tối thiểu của một bài học dùng cho trang danh sách (mọi bài, kể cả bản nháp/lưu trữ).</summary>
public sealed record LessonHeader(
    string Id,
    int Order,
    string Title,
    string Description,
    int ExerciseCount,
    bool IsPublished = true,
    bool IsArchived = false);

public interface ILessonRepository
{
    /// <summary>Danh sách bài học sắp theo Order tăng dần (không tải nội dung chi tiết). Việc lọc theo role do use case làm.</summary>
    Task<IReadOnlyList<LessonHeader>> GetLessonHeadersAsync(CancellationToken cancellationToken = default);

    /// <summary>Bài học kèm bài tập (sắp theo Order) và test case (sắp theo Order); null nếu không có.</summary>
    Task<Lesson?> GetLessonWithExercisesAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Một bài tập theo Id (duy nhất toàn hệ thống), kèm bài học chứa nó; null nếu không có.</summary>
    Task<Exercise?> GetExerciseAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Số test case hiện có của từng bài tập.</summary>
    Task<IReadOnlyDictionary<string, int>> GetTestCountsAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default);
}

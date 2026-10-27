using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Abstractions;

public sealed record TeacherLessonRow(
    string Id,
    int Order,
    string Title,
    string Description,
    bool IsPublished,
    bool IsArchived,
    int ExerciseCount,
    int PublishedExerciseCount,
    int AssignmentCount,
    int Version,
    DateTime? UpdatedUtc,
    string? CreatedByName);

public sealed record Dependencies(int Assignments, int Submissions)
{
    public bool Any => Assignments > 0 || Submissions > 0;
}

/// <summary>Truy cập dữ liệu cho việc soạn bài (các thực thể được theo dõi để sửa).</summary>
public interface ILessonAuthoringRepository
{
    Task<IReadOnlyList<TeacherLessonRow>> ListLessonsAsync(bool includeArchived, CancellationToken cancellationToken = default);

    /// <summary>Bài học kèm bài tập, test case và người tạo (được theo dõi); null nếu không có.</summary>
    Task<Lesson?> GetLessonForEditAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Bài tập kèm test case, bài học chứa nó và người tạo (được theo dõi); null nếu không có.</summary>
    Task<Exercise?> GetExerciseForEditAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> LessonIdExistsAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> ExerciseIdExistsAsync(string id, CancellationToken cancellationToken = default);

    Task<int> NextLessonOrderAsync(CancellationToken cancellationToken = default);

    Task<int> NextExerciseOrderAsync(string lessonId, CancellationToken cancellationToken = default);

    Task<Dependencies> GetLessonDependenciesAsync(string lessonId, CancellationToken cancellationToken = default);

    Task<Dependencies> GetExerciseDependenciesAsync(string exerciseId, CancellationToken cancellationToken = default);

    /// <summary>Mọi bài học (kể cả lưu trữ) để sắp xếp lại, được theo dõi.</summary>
    Task<IReadOnlyList<Lesson>> GetAllLessonsAsync(CancellationToken cancellationToken = default);

    /// <summary>Mọi bài tập (kể cả lưu trữ) của một bài học để sắp xếp lại, được theo dõi.</summary>
    Task<IReadOnlyList<Exercise>> GetExercisesOfLessonAsync(string lessonId, CancellationToken cancellationToken = default);

    void AddLesson(Lesson lesson);

    void AddExercise(Exercise exercise);

    void RemoveLesson(Lesson lesson);

    void RemoveExercise(Exercise exercise);
}

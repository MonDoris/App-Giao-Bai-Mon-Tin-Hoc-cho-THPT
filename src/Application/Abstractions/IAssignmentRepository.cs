using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Abstractions;

/// <summary>Một assignment đang hiệu lực mà một học sinh được nhận, kèm thông tin hiển thị của bài tập.</summary>
public sealed record StudentAssignmentRow(
    int AssignmentId,
    string ExerciseId,
    string ExerciseTitle,
    int ExerciseOrder,
    string Difficulty,
    string LessonId,
    string LessonTitle,
    int LessonOrder,
    DateTime? DueAtUtc,
    string? Note);

public sealed record AssignmentListRow(
    Assignment Assignment,
    string ExerciseTitle,
    string LessonId,
    string LessonTitle,
    IReadOnlyList<ClassRef> Classes,
    int AssignedStudents,
    int SubmittedStudents,
    int PassedStudents);

public interface IAssignmentRepository
{
    /// <summary>
    /// Assignment chưa hủy mà học sinh được nhận (giao cho tất cả hoặc cho lớp của em), chỉ gồm bài tập và bài học
    /// đã xuất bản, chưa lưu trữ (QD-26), và đã tới ngày bắt đầu (StartAtUtc &lt;= nowUtc hoặc không đặt).
    /// Chưa chọn assignment hiệu lực: use case dùng AssignmentRules.
    /// </summary>
    Task<IReadOnlyList<StudentAssignmentRow>> GetVisibleForStudentAsync(int studentId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Assignment theo Id (được theo dõi, kèm Classes); null nếu không có.</summary>
    Task<Assignment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Assignment chưa hủy của các bài tập (được theo dõi, kèm Classes).</summary>
    Task<IReadOnlyList<Assignment>> GetActiveForExercisesAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentListRow>> ListAsync(string? lessonId, int? classId, bool includeCancelled, CancellationToken cancellationToken = default);

    /// <summary>Như ListAsync nhưng chỉ cho các assignment có Id trong danh sách (kể cả đã hủy).</summary>
    Task<IReadOnlyList<AssignmentListRow>> ListByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    Task<bool> HasSubmissionsAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Số học sinh đang hoạt động nhận được ít nhất một assignment chưa hủy của các bài tập này.</summary>
    Task<int> CountStudentsReachedAsync(IEnumerable<string> exerciseIds, CancellationToken cancellationToken = default);

    void Add(Assignment assignment);

    void Remove(Assignment assignment);
}

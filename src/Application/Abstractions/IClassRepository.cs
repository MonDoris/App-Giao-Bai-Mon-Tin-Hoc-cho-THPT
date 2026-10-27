using TinHoc12.Domain.Entities;

namespace TinHoc12.Application.Abstractions;

public sealed record ClassRow(int Id, string Name, int MemberCount);

public sealed record ClassRef(int Id, string Name);

public interface IClassRepository
{
    Task<IReadOnlyList<ClassRow>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Lớp theo Id (được theo dõi); null nếu không có.</summary>
    Task<SchoolClass?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Các lớp tồn tại trong danh sách Id (không theo dõi).</summary>
    Task<IReadOnlyList<ClassRef>> GetExistingAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken = default);

    Task<bool> IsTargetOfActiveAssignmentAsync(int classId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, IReadOnlyList<ClassRef>>> GetClassesOfStudentsAsync(
        IEnumerable<int> studentIds, CancellationToken cancellationToken = default);

    /// <summary>Thay toàn bộ lớp của một học sinh bằng tập classIds (lưu khi gọi IUnitOfWork.SaveChangesAsync).</summary>
    Task ReplaceMembershipsAsync(int studentId, IReadOnlyCollection<int> classIds, CancellationToken cancellationToken = default);

    void Add(SchoolClass schoolClass);

    /// <summary>Xóa lớp; các thành viên bị gỡ khỏi lớp (học sinh không bị xóa) và gỡ lớp khỏi các lượt giao đã hủy.</summary>
    Task RemoveAsync(SchoolClass schoolClass, CancellationToken cancellationToken = default);
}

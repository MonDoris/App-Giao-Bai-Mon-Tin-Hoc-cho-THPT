namespace TinHoc12.Application.Abstractions;

/// <summary>Lưu thay đổi của các repository. Xung đột đồng thời/ràng buộc DB được chuyển thành ConflictException.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Chạy nhiều bước lưu trong một giao dịch; lỗi thì hoàn tác toàn bộ.</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

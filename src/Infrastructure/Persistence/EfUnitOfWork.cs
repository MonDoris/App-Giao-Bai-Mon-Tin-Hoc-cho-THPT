using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TinHoc12.Application.Abstractions;
using TinHoc12.Application.Exceptions;

namespace TinHoc12.Infrastructure.Persistence;

/// <summary>Lưu thay đổi bằng EF Core; xung đột đồng thời và vi phạm ràng buộc được chuyển thành ConflictException.</summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("CONFLICT", "Dữ liệu vừa được người khác thay đổi, hãy tải lại.");
        }
        catch (DbUpdateException ex) when (IsConstraintViolation(ex))
        {
            throw new ConflictException("CONFLICT", "Dữ liệu bị trùng hoặc đang được sử dụng, không thể lưu thay đổi.");
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            await action();
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await action();
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsConstraintViolation(DbUpdateException ex)
    {
        for (Exception? e = ex.InnerException; e is not null; e = e.InnerException)
        {
            if (e is SqlException sql && (sql.Number is 2601 or 2627 or 547))
            {
                return true;
            }

            // Chỉ phục vụ kiểm thử (SQLite in-memory), không dùng khi chạy ứng dụng; gỡ khi test chuyển sang SQL Server.
            if (e is SqliteException sqlite && sqlite.SqliteErrorCode == 19)
            {
                return true;
            }
        }

        return false;
    }
}

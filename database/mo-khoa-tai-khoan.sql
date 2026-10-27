-- =====================================================================
-- mo-khoa-tai-khoan.sql : mở khóa tạm do đăng nhập sai (SQL Server / T-SQL)
-- Dùng khi tài khoản (nhất là tài khoản giáo viên) bị khóa 15 phút do nhập sai mật khẩu nhiều lần.
-- Sửa @Username rồi chạy: sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i mo-khoa-tai-khoan.sql
-- Script chỉ xóa bộ đếm sai và thời điểm hết khóa; không đổi mật khẩu, không đổi trạng thái hoạt động.
-- =====================================================================
SET NOCOUNT ON;
DECLARE @Username nvarchar(50) = N'giaovien';

UPDATE [Users]
SET [FailedLoginCount] = 0, [LockoutEndUtc] = NULL
WHERE [Username] = LOWER(@Username);

IF @@ROWCOUNT = 0
    PRINT N'Không tìm thấy tên đăng nhập này.';
ELSE
    PRINT N'Đã mở khóa tài khoản.';

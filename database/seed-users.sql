-- =====================================================================
-- seed-users.sql : tài khoản mẫu, lớp mẫu và giao bài mẫu (SQL Server / T-SQL)
--   * 1 giáo viên: giaovien ; 3 học sinh: hs01, hs02, hs03 (mật khẩu: xem database/HUONG-DAN-TAI-KHOAN.md)
--   * Lớp 12A1 (hs01, hs02), lớp 12A2 (hs03)
--   * Giao bài: 4 bài tập của Bài 1-2 cho TẤT CẢ học sinh; 2 bài tập của Bài 3 chỉ cho lớp 12A1
--     (hs01/hs02 thấy 6 bài tập, hs03 thấy 4 bài tập), không hạn nộp.
-- Mật khẩu được lưu dạng BĂM (PBKDF2-HMAC-SHA512, 120000 vòng, salt riêng cho từng tài khoản); script KHÔNG chứa mật khẩu thô.
-- Idempotent: chạy nhiều lần không tạo dữ liệu trùng và không đổi mật khẩu/trạng thái của tài khoản đã có.
-- Yêu cầu: đã có schema và đã chạy seed-sample.sql (nếu chưa có bài tập thì phần giao bài tự bỏ qua; chạy lại sau là đủ).
-- CẢNH BÁO: đổi mật khẩu mẫu (hoặc xóa tài khoản mẫu) TRƯỚC KHI triển khai thật.
-- Chạy: sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i seed-users.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

-- 1) Tài khoản -----------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Username] = N'giaovien')
    INSERT INTO [Users] ([Username], [DisplayName], [PasswordHash], [Role], [IsActive], [MustChangePassword], [FailedLoginCount], [SecurityStamp], [CreatedUtc])
    VALUES (N'giaovien', N'Giáo viên Mẫu', N'PBKDF2-SHA512$120000$cqIjYq/HxefiV9yAjd6nCA==$jUZvALFAHKk1g9pdVopk7T6U7BF/0HEQsiRztUGWSIWMIVCKUNpnrN3tHN6QMxbK/YSZ1KoSre8P33AXlRYuhQ==', N'Teacher', 1, 0, 0, N'a1b2c3d4e5f60718293a4b5c6d7e8f01', SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Username] = N'hs01')
    INSERT INTO [Users] ([Username], [DisplayName], [PasswordHash], [Role], [IsActive], [MustChangePassword], [FailedLoginCount], [SecurityStamp], [CreatedUtc])
    VALUES (N'hs01', N'Học sinh 01', N'PBKDF2-SHA512$120000$7edhFjPD65CNvO7DsSekeA==$M4S9k/eaFD1jBEmJbt4EEu8sbRW56HRgUsysFR5yh7m8aveBpXza0ZO9K/hIZxS8YdVhIYPnuzMRwt9QBKaOtA==', N'Student', 1, 0, 0, N'b2c3d4e5f60718293a4b5c6d7e8f0112', SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Username] = N'hs02')
    INSERT INTO [Users] ([Username], [DisplayName], [PasswordHash], [Role], [IsActive], [MustChangePassword], [FailedLoginCount], [SecurityStamp], [CreatedUtc])
    VALUES (N'hs02', N'Học sinh 02', N'PBKDF2-SHA512$120000$kVaGuOpmbY+E3Unt2ABY4Q==$C9RdjprmklFL4n3xA6F8Hq1o9Hg+BaofIGDcGWiL3POpiRz0Woy3L9QTvyFFOgWyeLNKfsOjR63dOmt2wYM3ZQ==', N'Student', 1, 0, 0, N'c3d4e5f60718293a4b5c6d7e8f011223', SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Username] = N'hs03')
    INSERT INTO [Users] ([Username], [DisplayName], [PasswordHash], [Role], [IsActive], [MustChangePassword], [FailedLoginCount], [SecurityStamp], [CreatedUtc])
    VALUES (N'hs03', N'Học sinh 03', N'PBKDF2-SHA512$120000$ZBNo0/xRAYHcBmsZGkjXGg==$BvXdFIkfQUgIGsFKX9AJZDzaEG2u8cCsqz9lNBfYyUmhMYnfygY3ABcYqXoEjbpPVMgLT0lCl0bLYEryQD2sqA==', N'Student', 1, 0, 0, N'd4e5f60718293a4b5c6d7e8f01122334', SYSUTCDATETIME());

-- 2) Lớp ------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [Classes] WHERE [Name] = N'12A1')
    INSERT INTO [Classes] ([Name], [CreatedUtc]) VALUES (N'12A1', SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [Classes] WHERE [Name] = N'12A2')
    INSERT INTO [Classes] ([Name], [CreatedUtc]) VALUES (N'12A2', SYSUTCDATETIME());

-- 3) Thành viên lớp ------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [ClassMembers] WHERE [ClassId] = (SELECT [Id] FROM [Classes] WHERE [Name] = N'12A1') AND [StudentId] = (SELECT [Id] FROM [Users] WHERE [Username] = N'hs01'))
    INSERT INTO [ClassMembers] ([ClassId], [StudentId])
    VALUES ((SELECT [Id] FROM [Classes] WHERE [Name] = N'12A1'), (SELECT [Id] FROM [Users] WHERE [Username] = N'hs01'));
IF NOT EXISTS (SELECT 1 FROM [ClassMembers] WHERE [ClassId] = (SELECT [Id] FROM [Classes] WHERE [Name] = N'12A1') AND [StudentId] = (SELECT [Id] FROM [Users] WHERE [Username] = N'hs02'))
    INSERT INTO [ClassMembers] ([ClassId], [StudentId])
    VALUES ((SELECT [Id] FROM [Classes] WHERE [Name] = N'12A1'), (SELECT [Id] FROM [Users] WHERE [Username] = N'hs02'));
IF NOT EXISTS (SELECT 1 FROM [ClassMembers] WHERE [ClassId] = (SELECT [Id] FROM [Classes] WHERE [Name] = N'12A2') AND [StudentId] = (SELECT [Id] FROM [Users] WHERE [Username] = N'hs03'))
    INSERT INTO [ClassMembers] ([ClassId], [StudentId])
    VALUES ((SELECT [Id] FROM [Classes] WHERE [Name] = N'12A2'), (SELECT [Id] FROM [Users] WHERE [Username] = N'hs03'));

-- 4) Giao bài -------------------------------------------------------------
DECLARE @teacherId int = (SELECT [Id] FROM [Users] WHERE [Username] = N'giaovien');
DECLARE @class12A1 int = (SELECT [Id] FROM [Classes] WHERE [Name] = N'12A1');
DECLARE @newId int;

-- 4a) Bài 1-2: giao cho TẤT CẢ học sinh
IF EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-01-tong-hai-so')
   AND NOT EXISTS (SELECT 1 FROM [Assignments] WHERE [ExerciseId] = N'bai-01-tong-hai-so' AND [AssignedToAll] = 1 AND [CancelledUtc] IS NULL)
    INSERT INTO [Assignments] ([ExerciseId], [AssignedToAll], [DueAtUtc], [Note], [CreatedByUserId], [CreatedUtc])
    VALUES (N'bai-01-tong-hai-so', 1, NULL, NULL, @teacherId, SYSUTCDATETIME());
IF EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-01-dien-tich-hcn')
   AND NOT EXISTS (SELECT 1 FROM [Assignments] WHERE [ExerciseId] = N'bai-01-dien-tich-hcn' AND [AssignedToAll] = 1 AND [CancelledUtc] IS NULL)
    INSERT INTO [Assignments] ([ExerciseId], [AssignedToAll], [DueAtUtc], [Note], [CreatedByUserId], [CreatedUtc])
    VALUES (N'bai-01-dien-tich-hcn', 1, NULL, NULL, @teacherId, SYSUTCDATETIME());
IF EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-02-chan-le')
   AND NOT EXISTS (SELECT 1 FROM [Assignments] WHERE [ExerciseId] = N'bai-02-chan-le' AND [AssignedToAll] = 1 AND [CancelledUtc] IS NULL)
    INSERT INTO [Assignments] ([ExerciseId], [AssignedToAll], [DueAtUtc], [Note], [CreatedByUserId], [CreatedUtc])
    VALUES (N'bai-02-chan-le', 1, NULL, NULL, @teacherId, SYSUTCDATETIME());
IF EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-02-xep-loai')
   AND NOT EXISTS (SELECT 1 FROM [Assignments] WHERE [ExerciseId] = N'bai-02-xep-loai' AND [AssignedToAll] = 1 AND [CancelledUtc] IS NULL)
    INSERT INTO [Assignments] ([ExerciseId], [AssignedToAll], [DueAtUtc], [Note], [CreatedByUserId], [CreatedUtc])
    VALUES (N'bai-02-xep-loai', 1, NULL, NULL, @teacherId, SYSUTCDATETIME());

-- 4b) Bài 3: chỉ giao cho lớp 12A1
IF EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-03-tong-1-den-n')
   AND NOT EXISTS (SELECT 1 FROM [Assignments] a JOIN [AssignmentClasses] ac ON ac.[AssignmentId] = a.[Id]
                   WHERE a.[ExerciseId] = N'bai-03-tong-1-den-n' AND a.[AssignedToAll] = 0 AND a.[CancelledUtc] IS NULL AND ac.[ClassId] = @class12A1)
BEGIN
    INSERT INTO [Assignments] ([ExerciseId], [AssignedToAll], [DueAtUtc], [Note], [CreatedByUserId], [CreatedUtc])
    VALUES (N'bai-03-tong-1-den-n', 0, NULL, NULL, @teacherId, SYSUTCDATETIME());
    SET @newId = SCOPE_IDENTITY();
    INSERT INTO [AssignmentClasses] ([AssignmentId], [ClassId]) VALUES (@newId, @class12A1);
END
IF EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-03-dem-uoc')
   AND NOT EXISTS (SELECT 1 FROM [Assignments] a JOIN [AssignmentClasses] ac ON ac.[AssignmentId] = a.[Id]
                   WHERE a.[ExerciseId] = N'bai-03-dem-uoc' AND a.[AssignedToAll] = 0 AND a.[CancelledUtc] IS NULL AND ac.[ClassId] = @class12A1)
BEGIN
    INSERT INTO [Assignments] ([ExerciseId], [AssignedToAll], [DueAtUtc], [Note], [CreatedByUserId], [CreatedUtc])
    VALUES (N'bai-03-dem-uoc', 0, NULL, NULL, @teacherId, SYSUTCDATETIME());
    SET @newId = SCOPE_IDENTITY();
    INSERT INTO [AssignmentClasses] ([AssignmentId], [ClassId]) VALUES (@newId, @class12A1);
END

COMMIT TRANSACTION;
PRINT N'seed-users.sql: hoàn tất.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

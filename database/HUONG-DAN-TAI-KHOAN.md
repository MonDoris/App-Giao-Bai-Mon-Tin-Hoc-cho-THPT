# Hướng dẫn tài khoản, đăng nhập và giao bài (DK Edu)

## 1. Tài khoản mẫu
Chạy `database/seed-users.sql` (SQL Server; ở môi trường Development ứng dụng tự nạp khi bảng tài khoản còn trống). Script chạy lại nhiều lần không bị trùng; mật khẩu lưu dạng **băm PBKDF2** (script không chứa mật khẩu thô).

| Vai trò | Tên đăng nhập | Mật khẩu mẫu | Lớp |
|---|---|---|---|
| Giáo viên | `giaovien` | `GiaoVien@2026` | – |
| Học sinh | `hs01`, `hs02` | `HocSinh@2026` | 12A1 |
| Học sinh | `hs03` | `HocSinh@2026` | 12A2 |

Giao bài mẫu: 4 bài tập của Bài 1–2 giao cho **tất cả** học sinh; 2 bài tập của Bài 3 chỉ giao cho **lớp 12A1** (hs01, hs02 thấy 6 bài tập, hs03 thấy 4). Cần chạy `seed-sample.sql` trước để có bài tập (nếu chưa, phần giao bài tự bỏ qua; chạy lại là đủ).

> **CẢNH BÁO: phải đổi mật khẩu mẫu (hoặc xóa tài khoản mẫu) trước khi triển khai thật.** Khi ứng dụng khởi động ở môi trường `Production` mà tài khoản mẫu còn dùng mật khẩu mặc định, nhật ký có cảnh báo "Tài khoản mẫu vẫn dùng mật khẩu mặc định" (không ghi mật khẩu).

Lệnh nạp (SQL Server Express): `sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i database/seed-users.sql`. Hoặc chỉ cần `dotnet run --project src/Web` (Development tự áp migration và nạp seed nếu DB còn trống).

## 2. Tạo giáo viên mới
Giao diện **không** có chức năng tạo giáo viên (và không có đăng ký tự do). Hai cách:
1. **Khuyến nghị:** đăng nhập `giaovien`, tạo tài khoản ở trang *Học sinh*, rồi nâng quyền bằng SQL: `UPDATE Users SET Role = N'Teacher' WHERE Username = N'<tên>'`. Người đó đăng nhập bằng mật khẩu tạm và được yêu cầu đổi mật khẩu.
2. Tự sinh hash: định dạng `PBKDF2-SHA512$<số vòng>$<salt base64>$<hash base64>` (salt 16 byte, hash 64 byte, số vòng ≥ 100000). Ví dụ Python: `python -c "import hashlib,os,base64;s=os.urandom(16);print('PBKDF2-SHA512$120000$'+base64.b64encode(s).decode()+'$'+base64.b64encode(hashlib.pbkdf2_hmac('sha512',b'MatKhauMoi1',s,120000,64)).decode())"`, rồi `INSERT INTO Users (Username, DisplayName, PasswordHash, Role, IsActive, MustChangePassword, FailedLoginCount, SecurityStamp, CreatedUtc) VALUES (N'cogiao2', N'Cô Giáo 2', N'<hash>', N'Teacher', 1, 1, 0, REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''), SYSUTCDATETIME())`.

Tên đăng nhập dài 3–50 ký tự `[a-z0-9._-]` (lưu chữ thường). Mật khẩu 8–128 ký tự, có chữ cái và chữ số. **Quên mật khẩu:** giáo viên đặt lại cho học sinh ở trang *Học sinh*; với giáo viên dùng cách tạo hash ở trên.

## 3. Mở khóa tài khoản
Nhập sai 5 lần liên tiếp thì bị khóa tạm 15 phút. Giáo viên mở khóa học sinh ở trang *Học sinh*; với tài khoản giáo viên hãy chờ hết hạn hoặc chạy `database/mo-khoa-tai-khoan.sql` (sửa `@Username`).

## 4. Giao bài tập cho bài thêm bằng script SQL
Bài/bài tập thêm bằng `them-bai-moi.sql` mặc định **đã xuất bản** nhưng học sinh **chỉ thấy bài tập sau khi được giao**. Có thể giao trên giao diện (*Giao bài*) hoặc bằng SQL (giao cho tất cả học sinh, không hạn):
```sql
INSERT INTO Assignments (ExerciseId, AssignedToAll, DueAtUtc, Note, CreatedByUserId, CreatedUtc)
SELECT e.Id, 1, NULL, NULL, (SELECT Id FROM Users WHERE Username = N'giaovien'), SYSUTCDATETIME()
FROM Exercises e
WHERE e.LessonId = N'bai-99-mau'
  AND NOT EXISTS (SELECT 1 FROM Assignments a WHERE a.ExerciseId = e.Id AND a.AssignedToAll = 1 AND a.CancelledUtc IS NULL);
```

## 5. Cấu hình và triển khai thật
- Cấu hình `Auth:*` trong `src/Web/appsettings.json` (ghi đè bằng biến môi trường, ví dụ `Auth__IdleMinutes=60`): `MaxFailedAttempts` (5), `LockoutMinutes` (15), `IdleMinutes` (120), `AbsoluteHours` (10), `LoginRatePerMinute` (60 lần/phút/IP), `SubmissionMinIntervalSeconds` (3). Giá trị ≤ 0 làm ứng dụng báo lỗi cấu hình khi khởi động.
- **Bắt buộc dùng HTTPS ở môi trường thật** (`ASPNETCORE_ENVIRONMENT=Production`: cookie `Secure`, bật HSTS và chuyển HTTP sang HTTPS). Cổng HTTPS để chuyển hướng đặt bằng `Https:Port` (mặc định 443); truy cập bằng HTTP mà không chuyển hướng được thì nhận phản hồi 400 `HTTPS_REQUIRED` (không phải lỗi 500). Nếu chạy sau reverse proxy, đặt `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` để giới hạn tần suất theo IP thật.
- Khóa Data Protection (ký cookie/CSRF) lưu ở thư mục đặt bằng `Auth:KeyDirectory` (nên là thư mục bền, ví dụ `D:\DKEduKeys`); nếu mất khóa, người dùng chỉ phải đăng nhập lại. Chạy nhiều máy chủ phải dùng chung thư mục này.
- Kết quả nộp bài do trình duyệt học sinh chấm nên **chưa được xác minh phía máy chủ**: chỉ dùng để theo dõi, không dùng làm điểm thi chính thức.

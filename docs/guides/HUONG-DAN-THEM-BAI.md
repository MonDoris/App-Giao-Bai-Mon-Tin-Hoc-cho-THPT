# Hướng dẫn thêm bài học mới (dành cho giáo viên)

> **Cập nhật (phiên bản 3):** giáo viên nay có thể **soạn bài, bài tập và giao bài ngay trên giao diện web** (đăng nhập `giaovien` rồi vào "Bài học của tôi"), không cần chạy script SQL. Script SQL trong tài liệu này **vẫn dùng được**. Lưu ý: bài thêm bằng SQL mặc định **đã xuất bản**, nhưng học sinh **chỉ thấy bài tập sau khi được giao** (xem `HUONG-DAN-TAI-KHOAN.md`, mục 4, có đoạn SQL giao bài mẫu).

Nội dung bài học nằm trong cơ sở dữ liệu SQL Server (3 bảng `Lessons`, `Exercises`, `TestCases`). Thêm bài bằng **một script SQL**, **không** cần sửa code, build lại hay khởi động lại ứng dụng.

## 1. Cài đặt lần đầu
1. Cần .NET SDK 10 và SQL Server Express. Chuỗi kết nối: `src/Web/appsettings.json`, khóa `ConnectionStrings:DefaultConnection` (mặc định `.SQLEXPRESS`, DB `DKEducation`).
2. Tạo DB (chọn một cách):
   - `dotnet ef database update --project src/Infrastructure --startup-project src/Web` (DB sẽ được tự tạo), hoặc
   - tạo DB rỗng rồi chạy `database/sqlserver/schema.sql` (có thể chạy nhiều lần).
3. Nạp 3 bài mẫu (dữ liệu mẫu nạp bằng script, không nạp trong code): `database/sqlserver/seed-sample.sql` (chạy lại không bị trùng).
   `sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i database/sqlserver/seed-sample.sql`
   Nếu DB đã nạp bản cũ, chạy lại tệp này vẫn cập nhật nội dung 3 bài mẫu (các câu `UPDATE` cuối tệp; chỉ ghi đè Summary/Notes/Conclusion của 3 bài mẫu, không đụng bài khác).
4. Chạy ứng dụng (hai tiến trình): API bằng `dotnet run --project src/Web` (cổng 5000), giao diện bằng `python -m http.server 5173 --directory frontend` (hoặc `npx http-server frontend -p 5173 -c-1`), rồi mở `http://localhost:5173/`. **Chỉ dùng SQL Server**; ở môi trường Development ứng dụng tự áp migration và nạp seed nếu DB còn trống, nên bài thêm bằng script SQL sẽ hiện ngay. Muốn đổi máy chủ, đặt biến môi trường `ConnectionStrings__DefaultConnection`.

## 2. Thêm bài mới
Sao chép `database/scripts/them-bai-moi.sql` thành tệp mới, sửa nội dung, rồi chạy (SSMS, Azure Data Studio hoặc `sqlcmd` như trên). Tải lại trang chủ là thấy bài mới. Script chạy trong một giao dịch: lỗi thì tự hoàn tác.

## 3. Ý nghĩa các cột
**Lessons**: `Id` (slug `[a-z0-9-]`, ≤ 100 ký tự, duy nhất), `Order` (thứ tự hiển thị, duy nhất), `Title`, `Description` (mô tả ngắn), `Summary`, `Notes` (≥ 1 ý), `Conclusion` (ba cột này là **mảng JSON** các chuỗi).
**Exercises**: `Id` (slug, duy nhất **toàn hệ thống**), `LessonId`, `Order`, `Title`, `Difficulty` (`Easy`/`Medium`/`Hard`), `Statement`, `InputFormat`, `OutputFormat`, `Constraints` (có thể NULL), `Examples` (mảng JSON `{"input","output","explanation"}`, ≥ 1), `StarterCode`, `SolutionCode` (NULL = không có nút Đáp án), `SolutionExplanation`, `ConfirmEveryTime` (0/1), `TimeLimitMs` (mặc định 5000).
**TestCases**: `ExerciseId`, `Order` (thứ tự chấm), `Input`, `ExpectedOutput`, `IsHidden` (1 = test ẩn, học sinh không thấy input/đầu ra).

## 4. Quy ước hiển thị nội dung
- Mỗi phần tử mảng `Summary`/`Conclusion` là một đoạn; phần tử bắt đầu bằng `- ` là gạch đầu dòng; phần tử bắt đầu bằng ba dấu huyền (```` ```python ... ``` ````) là khối mã.
- Dùng dấu huyền đơn `` `print()` `` để hiển thị mã trong dòng. Trong JSON: xuống dòng là `\n`, dấu `"` viết `\"`.
- Đầu ra được so khớp sau khi bỏ khoảng trắng thừa cuối dòng và cuối đầu ra (phân biệt hoa/thường).
- Bài sai dữ liệu (JSON hỏng, không có test case...) chỉ làm hỏng chính bài đó; trang sẽ báo "Dữ liệu bài học bị lỗi: <trường>".

## 5. Xuất bản, lưu trữ và giao bài (phiên bản 3)
- Cột mới (có mặc định, script cũ không cần sửa): `IsPublished` (1 = đã xuất bản, học sinh mới thấy), `IsArchived` (1 = lưu trữ), `CreatedByUserId`, `UpdatedUtc`, `Version`. Dữ liệu tạo bằng script cũ nhận `IsPublished = 1`, `IsArchived = 0`, `Version = 1`.
- Bài tạo trên giao diện mặc định là **bản nháp** cho tới khi giáo viên bấm "Xuất bản".
- Bài học/bài tập đã được giao hoặc đã có bài nộp **không xóa được** (lưu trữ thay vì xóa).

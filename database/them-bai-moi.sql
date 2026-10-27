-- =====================================================================
-- them-bai-moi.sql : SCRIPT MẪU thêm 1 bài học + 1 bài tập + 3 test case (SQL Server / T-SQL)
--
-- Cách dùng: sao chép tệp này thành tệp mới (ví dụ them-bai-04.sql), sửa các giá trị
-- (Id, Order, tiêu đề, nội dung...) rồi chạy trên DB đã có schema:
--   sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i them-bai-04.sql
-- Không cần sửa mã nguồn, không cần build lại hay khởi động lại ứng dụng.
--
-- Quy ước:
--  * Id bài học và Id bài tập là slug [a-z0-9-], tối đa 100 ký tự, DUY NHẤT toàn hệ thống.
--  * [Order] của bài học không được trùng bài khác; [Order] của bài tập duy nhất trong bài; [Order] test case duy nhất trong bài tập.
--  * Summary / Notes / Conclusion / Examples là MẢNG JSON. Ghi chú: trong chuỗi JSON, xuống dòng viết là \n, dấu " viết là \".
--  * Difficulty chỉ nhận: Easy, Medium, Hard.
--  * Dùng NCHAR(10) để xuống dòng trong các cột văn bản thường (Statement, StarterCode, Input...).
--  * Toàn bộ script chạy trong một giao dịch: có lỗi thì ROLLBACK, không để lại dữ liệu dở dang.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

-- 1) Bài học ------------------------------------------------------------
INSERT INTO [Lessons] ([Id], [Order], [Title], [Description], [Summary], [Notes], [Conclusion])
VALUES (
    N'bai-99-mau',
    99,
    N'Bài 99: Bài học mẫu (thay bằng nội dung của bạn)',
    N'Mô tả ngắn hiển thị trên thẻ bài học ở trang chủ.',
    N'["Đoạn tóm tắt nội dung chính của bài học.", "- Ý chính thứ nhất (dòng bắt đầu bằng \"- \" sẽ hiển thị dạng gạch đầu dòng).", "- Ý chính thứ hai với đoạn mã `print(1 + 2)`."]',
    N'["Điểm cần lưu ý thứ nhất.", "Điểm cần lưu ý thứ hai."]',
    N'["- Tổng kết thứ nhất.", "- Tổng kết thứ hai."]'
);

-- 2) Bài tập ------------------------------------------------------------
INSERT INTO [Exercises] (
    [Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat],
    [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
VALUES (
    N'bai-99-mau-tong-ba-so',
    N'bai-99-mau',
    1,
    N'Tổng ba số',
    N'Easy',
    N'Nhập ba số nguyên (mỗi số trên một dòng). In ra tổng của ba số đó.',
    N'Ba dòng, mỗi dòng một số nguyên.',
    N'Một số nguyên là tổng của ba số.',
    N'-1000 ≤ mỗi số ≤ 1000.',
    N'[{"input": "1\n2\n3", "output": "6", "explanation": "1 + 2 + 3 = 6."}]',
    N'# Viết chương trình của bạn ở dưới đây' + NCHAR(10) + N'a = int(input())' + NCHAR(10),
    N'a = int(input())' + NCHAR(10) + N'b = int(input())' + NCHAR(10) + N'c = int(input())' + NCHAR(10) + N'print(a + b + c)' + NCHAR(10),
    N'Đọc ba số bằng int(input()) rồi in tổng của chúng.',
    0,      -- ConfirmEveryTime: 0 = chỉ hỏi xác nhận lần đầu xem đáp án
    5000    -- TimeLimitMs
);

-- 3) Test case (IsHidden = 1: test ẩn, giao diện không hiện input/đầu ra mong đợi) ----
INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
VALUES
    (N'bai-99-mau-tong-ba-so', 1, N'1' + NCHAR(10) + N'2' + NCHAR(10) + N'3', N'6', 0),
    (N'bai-99-mau-tong-ba-so', 2, N'0' + NCHAR(10) + N'0' + NCHAR(10) + N'0', N'0', 0),
    (N'bai-99-mau-tong-ba-so', 3, N'-5' + NCHAR(10) + N'5' + NCHAR(10) + N'-7', N'-7', 1);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH

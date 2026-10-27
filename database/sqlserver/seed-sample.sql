-- =====================================================================
-- seed-sample.sql : nạp 3 bài mẫu x 2 bài tập (SQL Server / T-SQL)
-- Idempotent: chạy nhiều lần không tạo dữ liệu trùng (kiểm tra theo Id).
-- Yêu cầu: đã tạo schema (dotnet ef database update hoặc schema.sql).
-- Chạy: sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i seed-sample.sql
-- (Tệp lưu UTF-8 có BOM; hãy mở/chạy bằng UTF-8 để giữ đúng tiếng Việt.)
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [Lessons] WHERE [Id] = N'bai-01-nhap-xuat')
    INSERT INTO [Lessons] ([Id], [Order], [Title], [Description], [Summary], [Notes], [Conclusion])
    VALUES (N'bai-01-nhap-xuat', 1, N'Bài 1: Biến, kiểu dữ liệu, nhập và xuất', N'Làm quen với print(), input(), ép kiểu int()/float() và các phép toán số học trong Python.',
        N'["Chương trình Python thường làm ba việc: nhận dữ liệu vào, xử lý và đưa kết quả ra. Dữ liệu được lưu trong biến, mỗi biến có một kiểu dữ liệu như số nguyên (`int`), số thực (`float`) hay chuỗi (`str`).", "- Hàm `print()` đưa kết quả ra màn hình; có thể in nhiều giá trị cách nhau bằng dấu phẩy.", "- Hàm `input()` đọc một dòng dữ liệu người dùng nhập vào và luôn trả về chuỗi.", "- Hàm `int()` và `float()` chuyển chuỗi thành số nguyên hoặc số thực.", "- Các phép toán số học: `+`, `-`, `*`, `/` (chia ra số thực), `//` (chia lấy phần nguyên), `%` (chia lấy dư), `**` (lũy thừa).", "```python\nso = int(input())\nprint(\"Gấp đôi:\", so * 2)\n```"]',
        N'["`input()` luôn trả về chuỗi. Phải ép kiểu bằng `int()` hoặc `float()` trước khi tính toán, nếu không \"3\" + \"4\" sẽ cho \"34\" thay vì 7.", "Phân biệt `/` và `//`: `7 / 2` cho `3.5` còn `7 // 2` cho `3`.", "Tên biến không được bắt đầu bằng chữ số, không chứa dấu cách và phân biệt chữ hoa, chữ thường (`tong` khác `Tong`).", "Bài tập được chấm bằng cách so khớp đầu ra, vì vậy chỉ in đúng kết quả yêu cầu, không in thêm lời nhắc như \"Nhap a:\"."]',
        N'["- Nhớ quy trình: đọc dữ liệu bằng `input()`, ép kiểu, tính toán, rồi in kết quả bằng `print()`.", "- `int(input())` là cách viết gọn để đọc một số nguyên từ một dòng.", "- Chú ý sự khác nhau giữa `/` (chia thực) và `//` (chia nguyên).", "- Hãy luyện thêm với các phép toán `%` và `**` ở những bài sau."]');

IF NOT EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-01-tong-hai-so')
BEGIN
    INSERT INTO [Exercises] ([Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat], [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
    VALUES (N'bai-01-tong-hai-so', N'bai-01-nhap-xuat', 1, N'Tổng hai số', N'Easy',
        N'Nhập vào hai số nguyên a và b (mỗi số trên một dòng). Hãy in ra tổng của hai số đó.',
        N'Dòng 1: số nguyên a.' + NCHAR(10) + N'Dòng 2: số nguyên b.',
        N'Một số nguyên duy nhất là tổng a + b.',
        N'-1 000 000 ≤ a, b ≤ 1 000 000.',
        N'[{"input": "3\n4", "output": "7", "explanation": "3 + 4 = 7."}]',
        N'# Nhập hai số nguyên a và b (mỗi số một dòng), in ra tổng của chúng.' + NCHAR(10) + N'a = int(input())' + NCHAR(10) + N'# Viết tiếp phần còn lại ở dưới đây' + NCHAR(10) + N'',
        N'a = int(input())' + NCHAR(10) + N'b = int(input())' + NCHAR(10) + N'print(a + b)' + NCHAR(10) + N'',
        N'Hàm input() trả về chuỗi nên ta dùng int() để chuyển thành số nguyên trước khi cộng. Sau đó in kết quả a + b bằng print().',
        0, 5000);
    INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
    VALUES
        (N'bai-01-tong-hai-so', 1, N'3' + NCHAR(10) + N'4', N'7', 0),
        (N'bai-01-tong-hai-so', 2, N'10' + NCHAR(10) + N'20', N'30', 0),
        (N'bai-01-tong-hai-so', 3, N'-5' + NCHAR(10) + N'3', N'-2', 0),
        (N'bai-01-tong-hai-so', 4, N'0' + NCHAR(10) + N'0', N'0', 1),
        (N'bai-01-tong-hai-so', 5, N'-7' + NCHAR(10) + N'-8', N'-15', 1),
        (N'bai-01-tong-hai-so', 6, N'100000' + NCHAR(10) + N'200000', N'300000', 1);
END

IF NOT EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-01-dien-tich-hcn')
BEGIN
    INSERT INTO [Exercises] ([Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat], [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
    VALUES (N'bai-01-dien-tich-hcn', N'bai-01-nhap-xuat', 2, N'Diện tích hình chữ nhật', N'Easy',
        N'Nhập chiều dài và chiều rộng của một hình chữ nhật (đều là số nguyên, mỗi số trên một dòng). Hãy in ra diện tích của hình chữ nhật đó.',
        N'Dòng 1: chiều dài (số nguyên).' + NCHAR(10) + N'Dòng 2: chiều rộng (số nguyên).',
        N'Một số nguyên là diện tích hình chữ nhật.',
        N'0 ≤ chiều dài, chiều rộng ≤ 10 000.',
        N'[{"input": "5\n3", "output": "15", "explanation": "Diện tích = 5 × 3 = 15."}]',
        N'# Nhập chiều dài và chiều rộng (số nguyên), in ra diện tích.' + NCHAR(10) + N'dai = int(input())' + NCHAR(10) + N'# Viết tiếp phần còn lại ở dưới đây' + NCHAR(10) + N'',
        N'dai = int(input())' + NCHAR(10) + N'rong = int(input())' + NCHAR(10) + N'print(dai * rong)' + NCHAR(10) + N'',
        N'Diện tích hình chữ nhật bằng chiều dài nhân chiều rộng. Đọc hai số nguyên rồi in tích của chúng.',
        0, 5000);
    INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
    VALUES
        (N'bai-01-dien-tich-hcn', 1, N'5' + NCHAR(10) + N'3', N'15', 0),
        (N'bai-01-dien-tich-hcn', 2, N'10' + NCHAR(10) + N'10', N'100', 0),
        (N'bai-01-dien-tich-hcn', 3, N'1' + NCHAR(10) + N'1', N'1', 0),
        (N'bai-01-dien-tich-hcn', 4, N'0' + NCHAR(10) + N'7', N'0', 1),
        (N'bai-01-dien-tich-hcn', 5, N'123' + NCHAR(10) + N'456', N'56088', 1);
END

IF NOT EXISTS (SELECT 1 FROM [Lessons] WHERE [Id] = N'bai-02-re-nhanh')
    INSERT INTO [Lessons] ([Id], [Order], [Title], [Description], [Summary], [Notes], [Conclusion])
    VALUES (N'bai-02-re-nhanh', 2, N'Bài 2: Cấu trúc rẽ nhánh if', N'Dùng if, elif, else, các toán tử so sánh, toán tử % và toán tử logic để chương trình đưa ra quyết định.',
        N'["Cấu trúc rẽ nhánh cho phép chương trình chọn một trong nhiều hướng xử lý tùy theo điều kiện đúng hay sai.", "- `if điều_kiện:` thực hiện khối lệnh thụt lề bên dưới khi điều kiện đúng.", "- `elif điều_kiện:` kiểm tra thêm một điều kiện khác khi các điều kiện trước đó sai.", "- `else:` thực hiện khi tất cả điều kiện phía trên đều sai.", "- Toán tử so sánh: `==`, `!=`, `<`, `<=`, `>`, `>=`. Toán tử logic: `and`, `or`, `not`.", "- Phép `%` cho số dư, ví dụ `n % 2 == 0` kiểm tra n chẵn.", "```python\ntuoi = int(input())\nif tuoi >= 18:\n    print(\"Du tuoi\")\nelse:\n    print(\"Chua du tuoi\")\n```"]',
        N'["Các lệnh trong cùng một khối phải thụt lề đều nhau, thường là 4 khoảng trắng.", "Cuối dòng `if`, `elif`, `else` luôn có dấu hai chấm `:`.", "`==` dùng để so sánh còn `=` dùng để gán giá trị. Viết `if x = 5:` sẽ báo lỗi cú pháp.", "Thứ tự các nhánh `elif` rất quan trọng: nên kiểm tra điều kiện lớn nhất hoặc đặc biệt nhất trước.", "Với số thực, hãy kiểm tra các mốc biên (ví dụ 8, 6.5, 5) thật cẩn thận bằng `>=` hoặc `>`."]',
        N'["- `if/elif/else` giúp chương trình ra quyết định theo điều kiện.", "- Luôn nhớ dấu `:` và thụt lề đúng.", "- Dùng `%` để kiểm tra tính chia hết; dùng `and`, `or` để ghép điều kiện.", "- Khi làm bài, hãy thử các giá trị biên để chắc chắn chương trình đúng."]');

IF NOT EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-02-chan-le')
BEGIN
    INSERT INTO [Exercises] ([Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat], [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
    VALUES (N'bai-02-chan-le', N'bai-02-re-nhanh', 1, N'Chẵn hay lẻ', N'Easy',
        N'Nhập một số nguyên n. In ra `Chan` nếu n là số chẵn, in ra `Le` nếu n là số lẻ.',
        N'Một dòng chứa số nguyên n.',
        N'In `Chan` hoặc `Le` (viết đúng chữ hoa, chữ thường, không dấu).',
        N'-1 000 000 ≤ n ≤ 1 000 000.',
        N'[{"input": "4", "output": "Chan", "explanation": "4 chia hết cho 2 nên là số chẵn."}, {"input": "7", "output": "Le", "explanation": "7 chia 2 dư 1 nên là số lẻ."}]',
        N'# Nhập số nguyên n, in ra Chan nếu n chẵn, Le nếu n lẻ.' + NCHAR(10) + N'n = int(input())' + NCHAR(10) + N'# Viết tiếp phần còn lại ở dưới đây' + NCHAR(10) + N'',
        N'n = int(input())' + NCHAR(10) + N'if n % 2 == 0:' + NCHAR(10) + N'    print("Chan")' + NCHAR(10) + N'else:' + NCHAR(10) + N'    print("Le")' + NCHAR(10) + N'',
        N'Một số là chẵn khi chia cho 2 dư 0. Trong Python, phép % với số âm vẫn cho kết quả 0 hoặc 1 khi chia cho 2 nên cách kiểm tra n % 2 == 0 đúng cho cả số âm và số 0.',
        0, 5000);
    INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
    VALUES
        (N'bai-02-chan-le', 1, N'4', N'Chan', 0),
        (N'bai-02-chan-le', 2, N'7', N'Le', 0),
        (N'bai-02-chan-le', 3, N'0', N'Chan', 0),
        (N'bai-02-chan-le', 4, N'-3', N'Le', 0),
        (N'bai-02-chan-le', 5, N'-8', N'Chan', 1),
        (N'bai-02-chan-le', 6, N'1001', N'Le', 1);
END

IF NOT EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-02-xep-loai')
BEGIN
    INSERT INTO [Exercises] ([Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat], [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
    VALUES (N'bai-02-xep-loai', N'bai-02-re-nhanh', 2, N'Xếp loại học lực', N'Medium',
        N'Nhập điểm trung bình là một số thực từ 0 đến 10. Hãy xếp loại và in ra:' + NCHAR(10) + N'' + NCHAR(10) + N'- `Gioi` nếu điểm ≥ 8' + NCHAR(10) + N'- `Kha` nếu 6.5 ≤ điểm < 8' + NCHAR(10) + N'- `Trung binh` nếu 5 ≤ điểm < 6.5' + NCHAR(10) + N'- `Yeu` nếu điểm < 5',
        N'Một dòng chứa số thực (điểm trung bình), dùng dấu chấm làm dấu thập phân.',
        N'In một trong bốn xếp loại: `Gioi`, `Kha`, `Trung binh`, `Yeu`.',
        N'0 ≤ điểm ≤ 10.',
        N'[{"input": "9", "output": "Gioi", "explanation": "9 ≥ 8 nên xếp loại Giỏi."}, {"input": "6.5", "output": "Kha", "explanation": "6.5 nằm trong khoảng [6.5; 8) nên xếp loại Khá."}]',
        N'# Nhập điểm trung bình (số thực) và in ra xếp loại.' + NCHAR(10) + N'diem = float(input())' + NCHAR(10) + N'# Viết tiếp phần còn lại ở dưới đây' + NCHAR(10) + N'',
        N'diem = float(input())' + NCHAR(10) + N'if diem >= 8:' + NCHAR(10) + N'    print("Gioi")' + NCHAR(10) + N'elif diem >= 6.5:' + NCHAR(10) + N'    print("Kha")' + NCHAR(10) + N'elif diem >= 5:' + NCHAR(10) + N'    print("Trung binh")' + NCHAR(10) + N'else:' + NCHAR(10) + N'    print("Yeu")' + NCHAR(10) + N'',
        N'Kiểm tra các mốc theo thứ tự từ cao xuống thấp bằng if/elif/else. Nhánh nào đúng trước thì được thực hiện, nên không cần ghi thêm cận trên cho mỗi nhánh.',
        0, 5000);
    INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
    VALUES
        (N'bai-02-xep-loai', 1, N'9', N'Gioi', 0),
        (N'bai-02-xep-loai', 2, N'8', N'Gioi', 0),
        (N'bai-02-xep-loai', 3, N'7.5', N'Kha', 0),
        (N'bai-02-xep-loai', 4, N'6.5', N'Kha', 0),
        (N'bai-02-xep-loai', 5, N'6.4', N'Trung binh', 0),
        (N'bai-02-xep-loai', 6, N'5', N'Trung binh', 0),
        (N'bai-02-xep-loai', 7, N'4.99', N'Yeu', 1),
        (N'bai-02-xep-loai', 8, N'0', N'Yeu', 1),
        (N'bai-02-xep-loai', 9, N'10', N'Gioi', 1);
END

IF NOT EXISTS (SELECT 1 FROM [Lessons] WHERE [Id] = N'bai-03-vong-lap')
    INSERT INTO [Lessons] ([Id], [Order], [Title], [Description], [Summary], [Notes], [Conclusion])
    VALUES (N'bai-03-vong-lap', 3, N'Bài 3: Vòng lặp for và while', N'Lặp lại công việc bằng for ... in range(), while, tích lũy tổng và lệnh break.',
        N'["Vòng lặp giúp thực hiện lặp đi lặp lại một khối lệnh mà không phải viết lại nhiều lần.", "- `for i in range(n):` lặp với i lần lượt nhận các giá trị 0, 1, ..., n-1.", "- `range(a, b)` lặp từ a đến b-1; `range(a, b, k)` lặp với bước nhảy k.", "- `while điều_kiện:` lặp chừng nào điều kiện còn đúng.", "- `break` thoát khỏi vòng lặp ngay lập tức.", "- Tích lũy tổng: khởi tạo `tong = 0` rồi cộng dồn trong vòng lặp.", "```python\ntich = 1\nfor k in range(1, 6):\n    tich = tich * k\nprint(tich)\n```"]',
        N'["`range(n)` không bao gồm n. Muốn lặp đến n phải dùng `range(1, n + 1)`.", "Với `while`, phải có lệnh làm thay đổi điều kiện bên trong vòng lặp, nếu không chương trình sẽ lặp vô hạn.", "Phải khởi tạo biến tích lũy (ví dụ `tong = 0`, `dem = 0`) trước khi vào vòng lặp.", "Thụt lề quyết định lệnh nào nằm trong vòng lặp, lệnh nào nằm ngoài."]',
        N'["- Dùng `for` khi biết trước số lần lặp, dùng `while` khi lặp theo điều kiện.", "- Luôn kiểm tra cận của `range()` để không bị thiếu hoặc thừa một giá trị.", "- Khởi tạo biến tích lũy trước vòng lặp và nhớ cập nhật điều kiện trong `while`.", "- Thử với các giá trị nhỏ nhất và lớn nhất của đề bài để kiểm tra chương trình."]');

IF NOT EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-03-tong-1-den-n')
BEGIN
    INSERT INTO [Exercises] ([Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat], [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
    VALUES (N'bai-03-tong-1-den-n', N'bai-03-vong-lap', 1, N'Tổng từ 1 đến n', N'Easy',
        N'Nhập số nguyên dương n. Hãy tính và in ra tổng 1 + 2 + 3 + ... + n.',
        N'Một dòng chứa số nguyên n.',
        N'Một số nguyên là tổng 1 + 2 + ... + n.',
        N'1 ≤ n ≤ 10000.',
        N'[{"input": "5", "output": "15", "explanation": "1 + 2 + 3 + 4 + 5 = 15."}]',
        N'# Nhập n, in ra tổng 1 + 2 + ... + n.' + NCHAR(10) + N'n = int(input())' + NCHAR(10) + N'tong = 0' + NCHAR(10) + N'# Dùng vòng lặp để cộng dồn, rồi in kết quả ở dưới đây' + NCHAR(10) + N'',
        N'n = int(input())' + NCHAR(10) + N'tong = 0' + NCHAR(10) + N'for i in range(1, n + 1):' + NCHAR(10) + N'    tong = tong + i' + NCHAR(10) + N'print(tong)' + NCHAR(10) + N'',
        N'Khởi tạo tong = 0, dùng for i in range(1, n + 1) để cộng từng giá trị i vào tong. Nhớ rằng range(1, n + 1) mới bao gồm cả n.',
        0, 5000);
    INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
    VALUES
        (N'bai-03-tong-1-den-n', 1, N'1', N'1', 0),
        (N'bai-03-tong-1-den-n', 2, N'5', N'15', 0),
        (N'bai-03-tong-1-den-n', 3, N'10', N'55', 0),
        (N'bai-03-tong-1-den-n', 4, N'100', N'5050', 0),
        (N'bai-03-tong-1-den-n', 5, N'10000', N'50005000', 1);
END

IF NOT EXISTS (SELECT 1 FROM [Exercises] WHERE [Id] = N'bai-03-dem-uoc')
BEGIN
    INSERT INTO [Exercises] ([Id], [LessonId], [Order], [Title], [Difficulty], [Statement], [InputFormat], [OutputFormat], [Constraints], [Examples], [StarterCode], [SolutionCode], [SolutionExplanation], [ConfirmEveryTime], [TimeLimitMs])
    VALUES (N'bai-03-dem-uoc', N'bai-03-vong-lap', 2, N'Đếm ước của n', N'Medium',
        N'Nhập số nguyên dương n. Hãy đếm và in ra số lượng ước dương của n (kể cả 1 và n).',
        N'Một dòng chứa số nguyên n.',
        N'Một số nguyên là số lượng ước dương của n.',
        N'1 ≤ n ≤ 10000.',
        N'[{"input": "12", "output": "6", "explanation": "Các ước của 12 là 1, 2, 3, 4, 6, 12 nên có 6 ước."}]',
        N'# Nhập n, đếm và in ra số ước dương của n.' + NCHAR(10) + N'n = int(input())' + NCHAR(10) + N'dem = 0' + NCHAR(10) + N'# Duyệt các số từ 1 đến n, đếm số ước, rồi in kết quả ở dưới đây' + NCHAR(10) + N'',
        N'n = int(input())' + NCHAR(10) + N'dem = 0' + NCHAR(10) + N'for i in range(1, n + 1):' + NCHAR(10) + N'    if n % i == 0:' + NCHAR(10) + N'        dem = dem + 1' + NCHAR(10) + N'print(dem)' + NCHAR(10) + N'',
        N'Duyệt i từ 1 đến n. Nếu n % i == 0 thì i là ước của n nên tăng biến đếm lên 1. Cuối cùng in biến đếm.',
        0, 5000);
    INSERT INTO [TestCases] ([ExerciseId], [Order], [Input], [ExpectedOutput], [IsHidden])
    VALUES
        (N'bai-03-dem-uoc', 1, N'1', N'1', 0),
        (N'bai-03-dem-uoc', 2, N'12', N'6', 0),
        (N'bai-03-dem-uoc', 3, N'7', N'2', 0),
        (N'bai-03-dem-uoc', 4, N'13', N'2', 0),
        (N'bai-03-dem-uoc', 5, N'36', N'9', 0),
        (N'bai-03-dem-uoc', 6, N'100', N'9', 1),
        (N'bai-03-dem-uoc', 7, N'10000', N'25', 1);
END

-- Cập nhật nội dung 3 bài mẫu cho DB đã nạp từ phiên bản cũ (INSERT chỉ chạy khi chưa có bài). Idempotent.
-- LƯU Ý: ghi đè Summary/Notes/Conclusion của đúng 3 bài mẫu; bài do giáo viên thêm không bị ảnh hưởng.
UPDATE Lessons SET Summary = N'["Chương trình Python thường làm ba việc: nhận dữ liệu vào, xử lý và đưa kết quả ra. Dữ liệu được lưu trong biến, mỗi biến có một kiểu dữ liệu như số nguyên (`int`), số thực (`float`) hay chuỗi (`str`).", "- Hàm `print()` đưa kết quả ra màn hình; có thể in nhiều giá trị cách nhau bằng dấu phẩy.", "- Hàm `input()` đọc một dòng dữ liệu người dùng nhập vào và luôn trả về chuỗi.", "- Hàm `int()` và `float()` chuyển chuỗi thành số nguyên hoặc số thực.", "- Các phép toán số học: `+`, `-`, `*`, `/` (chia ra số thực), `//` (chia lấy phần nguyên), `%` (chia lấy dư), `**` (lũy thừa).", "```python\nso = int(input())\nprint(\"Gấp đôi:\", so * 2)\n```"]', Notes = N'["`input()` luôn trả về chuỗi. Phải ép kiểu bằng `int()` hoặc `float()` trước khi tính toán, nếu không \"3\" + \"4\" sẽ cho \"34\" thay vì 7.", "Phân biệt `/` và `//`: `7 / 2` cho `3.5` còn `7 // 2` cho `3`.", "Tên biến không được bắt đầu bằng chữ số, không chứa dấu cách và phân biệt chữ hoa, chữ thường (`tong` khác `Tong`).", "Bài tập được chấm bằng cách so khớp đầu ra, vì vậy chỉ in đúng kết quả yêu cầu, không in thêm lời nhắc như \"Nhap a:\"."]', Conclusion = N'["- Nhớ quy trình: đọc dữ liệu bằng `input()`, ép kiểu, tính toán, rồi in kết quả bằng `print()`.", "- `int(input())` là cách viết gọn để đọc một số nguyên từ một dòng.", "- Chú ý sự khác nhau giữa `/` (chia thực) và `//` (chia nguyên).", "- Hãy luyện thêm với các phép toán `%` và `**` ở những bài sau."]'
WHERE Id = N'bai-01-nhap-xuat';

UPDATE Lessons SET Summary = N'["Cấu trúc rẽ nhánh cho phép chương trình chọn một trong nhiều hướng xử lý tùy theo điều kiện đúng hay sai.", "- `if điều_kiện:` thực hiện khối lệnh thụt lề bên dưới khi điều kiện đúng.", "- `elif điều_kiện:` kiểm tra thêm một điều kiện khác khi các điều kiện trước đó sai.", "- `else:` thực hiện khi tất cả điều kiện phía trên đều sai.", "- Toán tử so sánh: `==`, `!=`, `<`, `<=`, `>`, `>=`. Toán tử logic: `and`, `or`, `not`.", "- Phép `%` cho số dư, ví dụ `n % 2 == 0` kiểm tra n chẵn.", "```python\ntuoi = int(input())\nif tuoi >= 18:\n    print(\"Du tuoi\")\nelse:\n    print(\"Chua du tuoi\")\n```"]', Notes = N'["Các lệnh trong cùng một khối phải thụt lề đều nhau, thường là 4 khoảng trắng.", "Cuối dòng `if`, `elif`, `else` luôn có dấu hai chấm `:`.", "`==` dùng để so sánh còn `=` dùng để gán giá trị. Viết `if x = 5:` sẽ báo lỗi cú pháp.", "Thứ tự các nhánh `elif` rất quan trọng: nên kiểm tra điều kiện lớn nhất hoặc đặc biệt nhất trước.", "Với số thực, hãy kiểm tra các mốc biên (ví dụ 8, 6.5, 5) thật cẩn thận bằng `>=` hoặc `>`."]', Conclusion = N'["- `if/elif/else` giúp chương trình ra quyết định theo điều kiện.", "- Luôn nhớ dấu `:` và thụt lề đúng.", "- Dùng `%` để kiểm tra tính chia hết; dùng `and`, `or` để ghép điều kiện.", "- Khi làm bài, hãy thử các giá trị biên để chắc chắn chương trình đúng."]'
WHERE Id = N'bai-02-re-nhanh';

UPDATE Lessons SET Summary = N'["Vòng lặp giúp thực hiện lặp đi lặp lại một khối lệnh mà không phải viết lại nhiều lần.", "- `for i in range(n):` lặp với i lần lượt nhận các giá trị 0, 1, ..., n-1.", "- `range(a, b)` lặp từ a đến b-1; `range(a, b, k)` lặp với bước nhảy k.", "- `while điều_kiện:` lặp chừng nào điều kiện còn đúng.", "- `break` thoát khỏi vòng lặp ngay lập tức.", "- Tích lũy tổng: khởi tạo `tong = 0` rồi cộng dồn trong vòng lặp.", "```python\ntich = 1\nfor k in range(1, 6):\n    tich = tich * k\nprint(tich)\n```"]', Notes = N'["`range(n)` không bao gồm n. Muốn lặp đến n phải dùng `range(1, n + 1)`.", "Với `while`, phải có lệnh làm thay đổi điều kiện bên trong vòng lặp, nếu không chương trình sẽ lặp vô hạn.", "Phải khởi tạo biến tích lũy (ví dụ `tong = 0`, `dem = 0`) trước khi vào vòng lặp.", "Thụt lề quyết định lệnh nào nằm trong vòng lặp, lệnh nào nằm ngoài."]', Conclusion = N'["- Dùng `for` khi biết trước số lần lặp, dùng `while` khi lặp theo điều kiện.", "- Luôn kiểm tra cận của `range()` để không bị thiếu hoặc thừa một giá trị.", "- Khởi tạo biến tích lũy trước vòng lặp và nhớ cập nhật điều kiện trong `while`.", "- Thử với các giá trị nhỏ nhất và lớn nhất của đề bài để kiểm tra chương trình."]'
WHERE Id = N'bai-03-vong-lap';

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH

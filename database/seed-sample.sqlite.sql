-- seed-sample.sqlite.sql : bản SQLite của seed-sample.sql, CHỈ PHỤC VỤ KIỂM THỬ (tests/ dùng SQLite
-- in-memory), KHÔNG dùng khi chạy ứng dụng. Gỡ khi test chuyển sang SQL Server. Cùng nội dung 3 bài x 2 bài tập, idempotent.
PRAGMA foreign_keys = ON;
BEGIN TRANSACTION;

INSERT OR IGNORE INTO "Lessons" ("Id", "Order", "Title", "Description", "Summary", "Notes", "Conclusion")
VALUES ('bai-01-nhap-xuat', 1, 'Bài 1: Biến, kiểu dữ liệu, nhập và xuất', 'Làm quen với print(), input(), ép kiểu int()/float() và các phép toán số học trong Python.',
    '["Chương trình Python thường làm ba việc: nhận dữ liệu vào, xử lý và đưa kết quả ra. Dữ liệu được lưu trong biến, mỗi biến có một kiểu dữ liệu như số nguyên (`int`), số thực (`float`) hay chuỗi (`str`).", "- Hàm `print()` đưa kết quả ra màn hình; có thể in nhiều giá trị cách nhau bằng dấu phẩy.", "- Hàm `input()` đọc một dòng dữ liệu người dùng nhập vào và luôn trả về chuỗi.", "- Hàm `int()` và `float()` chuyển chuỗi thành số nguyên hoặc số thực.", "- Các phép toán số học: `+`, `-`, `*`, `/` (chia ra số thực), `//` (chia lấy phần nguyên), `%` (chia lấy dư), `**` (lũy thừa).", "```python\nso = int(input())\nprint(\"Gấp đôi:\", so * 2)\n```"]',
    '["`input()` luôn trả về chuỗi. Phải ép kiểu bằng `int()` hoặc `float()` trước khi tính toán, nếu không \"3\" + \"4\" sẽ cho \"34\" thay vì 7.", "Phân biệt `/` và `//`: `7 / 2` cho `3.5` còn `7 // 2` cho `3`.", "Tên biến không được bắt đầu bằng chữ số, không chứa dấu cách và phân biệt chữ hoa, chữ thường (`tong` khác `Tong`).", "Bài tập được chấm bằng cách so khớp đầu ra, vì vậy chỉ in đúng kết quả yêu cầu, không in thêm lời nhắc như \"Nhap a:\"."]',
    '["- Nhớ quy trình: đọc dữ liệu bằng `input()`, ép kiểu, tính toán, rồi in kết quả bằng `print()`.", "- `int(input())` là cách viết gọn để đọc một số nguyên từ một dòng.", "- Chú ý sự khác nhau giữa `/` (chia thực) và `//` (chia nguyên).", "- Hãy luyện thêm với các phép toán `%` và `**` ở những bài sau."]');

INSERT OR IGNORE INTO "Exercises" ("Id", "LessonId", "Order", "Title", "Difficulty", "Statement", "InputFormat", "OutputFormat", "Constraints", "Examples", "StarterCode", "SolutionCode", "SolutionExplanation", "ConfirmEveryTime", "TimeLimitMs")
VALUES ('bai-01-tong-hai-so', 'bai-01-nhap-xuat', 1, 'Tổng hai số', 'Easy',
        'Nhập vào hai số nguyên a và b (mỗi số trên một dòng). Hãy in ra tổng của hai số đó.',
        'Dòng 1: số nguyên a.
Dòng 2: số nguyên b.',
        'Một số nguyên duy nhất là tổng a + b.',
        '-1 000 000 ≤ a, b ≤ 1 000 000.',
        '[{"input": "3\n4", "output": "7", "explanation": "3 + 4 = 7."}]',
        '# Nhập hai số nguyên a và b (mỗi số một dòng), in ra tổng của chúng.
a = int(input())
# Viết tiếp phần còn lại ở dưới đây
',
        'a = int(input())
b = int(input())
print(a + b)
',
        'Hàm input() trả về chuỗi nên ta dùng int() để chuyển thành số nguyên trước khi cộng. Sau đó in kết quả a + b bằng print().',
        0, 5000);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-tong-hai-so', 1, '3
4', '7', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-tong-hai-so', 2, '10
20', '30', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-tong-hai-so', 3, '-5
3', '-2', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-tong-hai-so', 4, '0
0', '0', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-tong-hai-so', 5, '-7
-8', '-15', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-tong-hai-so', 6, '100000
200000', '300000', 1);

INSERT OR IGNORE INTO "Exercises" ("Id", "LessonId", "Order", "Title", "Difficulty", "Statement", "InputFormat", "OutputFormat", "Constraints", "Examples", "StarterCode", "SolutionCode", "SolutionExplanation", "ConfirmEveryTime", "TimeLimitMs")
VALUES ('bai-01-dien-tich-hcn', 'bai-01-nhap-xuat', 2, 'Diện tích hình chữ nhật', 'Easy',
        'Nhập chiều dài và chiều rộng của một hình chữ nhật (đều là số nguyên, mỗi số trên một dòng). Hãy in ra diện tích của hình chữ nhật đó.',
        'Dòng 1: chiều dài (số nguyên).
Dòng 2: chiều rộng (số nguyên).',
        'Một số nguyên là diện tích hình chữ nhật.',
        '0 ≤ chiều dài, chiều rộng ≤ 10 000.',
        '[{"input": "5\n3", "output": "15", "explanation": "Diện tích = 5 × 3 = 15."}]',
        '# Nhập chiều dài và chiều rộng (số nguyên), in ra diện tích.
dai = int(input())
# Viết tiếp phần còn lại ở dưới đây
',
        'dai = int(input())
rong = int(input())
print(dai * rong)
',
        'Diện tích hình chữ nhật bằng chiều dài nhân chiều rộng. Đọc hai số nguyên rồi in tích của chúng.',
        0, 5000);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-dien-tich-hcn', 1, '5
3', '15', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-dien-tich-hcn', 2, '10
10', '100', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-dien-tich-hcn', 3, '1
1', '1', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-dien-tich-hcn', 4, '0
7', '0', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-01-dien-tich-hcn', 5, '123
456', '56088', 1);

INSERT OR IGNORE INTO "Lessons" ("Id", "Order", "Title", "Description", "Summary", "Notes", "Conclusion")
VALUES ('bai-02-re-nhanh', 2, 'Bài 2: Cấu trúc rẽ nhánh if', 'Dùng if, elif, else, các toán tử so sánh, toán tử % và toán tử logic để chương trình đưa ra quyết định.',
    '["Cấu trúc rẽ nhánh cho phép chương trình chọn một trong nhiều hướng xử lý tùy theo điều kiện đúng hay sai.", "- `if điều_kiện:` thực hiện khối lệnh thụt lề bên dưới khi điều kiện đúng.", "- `elif điều_kiện:` kiểm tra thêm một điều kiện khác khi các điều kiện trước đó sai.", "- `else:` thực hiện khi tất cả điều kiện phía trên đều sai.", "- Toán tử so sánh: `==`, `!=`, `<`, `<=`, `>`, `>=`. Toán tử logic: `and`, `or`, `not`.", "- Phép `%` cho số dư, ví dụ `n % 2 == 0` kiểm tra n chẵn.", "```python\ntuoi = int(input())\nif tuoi >= 18:\n    print(\"Du tuoi\")\nelse:\n    print(\"Chua du tuoi\")\n```"]',
    '["Các lệnh trong cùng một khối phải thụt lề đều nhau, thường là 4 khoảng trắng.", "Cuối dòng `if`, `elif`, `else` luôn có dấu hai chấm `:`.", "`==` dùng để so sánh còn `=` dùng để gán giá trị. Viết `if x = 5:` sẽ báo lỗi cú pháp.", "Thứ tự các nhánh `elif` rất quan trọng: nên kiểm tra điều kiện lớn nhất hoặc đặc biệt nhất trước.", "Với số thực, hãy kiểm tra các mốc biên (ví dụ 8, 6.5, 5) thật cẩn thận bằng `>=` hoặc `>`."]',
    '["- `if/elif/else` giúp chương trình ra quyết định theo điều kiện.", "- Luôn nhớ dấu `:` và thụt lề đúng.", "- Dùng `%` để kiểm tra tính chia hết; dùng `and`, `or` để ghép điều kiện.", "- Khi làm bài, hãy thử các giá trị biên để chắc chắn chương trình đúng."]');

INSERT OR IGNORE INTO "Exercises" ("Id", "LessonId", "Order", "Title", "Difficulty", "Statement", "InputFormat", "OutputFormat", "Constraints", "Examples", "StarterCode", "SolutionCode", "SolutionExplanation", "ConfirmEveryTime", "TimeLimitMs")
VALUES ('bai-02-chan-le', 'bai-02-re-nhanh', 1, 'Chẵn hay lẻ', 'Easy',
        'Nhập một số nguyên n. In ra `Chan` nếu n là số chẵn, in ra `Le` nếu n là số lẻ.',
        'Một dòng chứa số nguyên n.',
        'In `Chan` hoặc `Le` (viết đúng chữ hoa, chữ thường, không dấu).',
        '-1 000 000 ≤ n ≤ 1 000 000.',
        '[{"input": "4", "output": "Chan", "explanation": "4 chia hết cho 2 nên là số chẵn."}, {"input": "7", "output": "Le", "explanation": "7 chia 2 dư 1 nên là số lẻ."}]',
        '# Nhập số nguyên n, in ra Chan nếu n chẵn, Le nếu n lẻ.
n = int(input())
# Viết tiếp phần còn lại ở dưới đây
',
        'n = int(input())
if n % 2 == 0:
    print("Chan")
else:
    print("Le")
',
        'Một số là chẵn khi chia cho 2 dư 0. Trong Python, phép % với số âm vẫn cho kết quả 0 hoặc 1 khi chia cho 2 nên cách kiểm tra n % 2 == 0 đúng cho cả số âm và số 0.',
        0, 5000);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-chan-le', 1, '4', 'Chan', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-chan-le', 2, '7', 'Le', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-chan-le', 3, '0', 'Chan', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-chan-le', 4, '-3', 'Le', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-chan-le', 5, '-8', 'Chan', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-chan-le', 6, '1001', 'Le', 1);

INSERT OR IGNORE INTO "Exercises" ("Id", "LessonId", "Order", "Title", "Difficulty", "Statement", "InputFormat", "OutputFormat", "Constraints", "Examples", "StarterCode", "SolutionCode", "SolutionExplanation", "ConfirmEveryTime", "TimeLimitMs")
VALUES ('bai-02-xep-loai', 'bai-02-re-nhanh', 2, 'Xếp loại học lực', 'Medium',
        'Nhập điểm trung bình là một số thực từ 0 đến 10. Hãy xếp loại và in ra:

- `Gioi` nếu điểm ≥ 8
- `Kha` nếu 6.5 ≤ điểm < 8
- `Trung binh` nếu 5 ≤ điểm < 6.5
- `Yeu` nếu điểm < 5',
        'Một dòng chứa số thực (điểm trung bình), dùng dấu chấm làm dấu thập phân.',
        'In một trong bốn xếp loại: `Gioi`, `Kha`, `Trung binh`, `Yeu`.',
        '0 ≤ điểm ≤ 10.',
        '[{"input": "9", "output": "Gioi", "explanation": "9 ≥ 8 nên xếp loại Giỏi."}, {"input": "6.5", "output": "Kha", "explanation": "6.5 nằm trong khoảng [6.5; 8) nên xếp loại Khá."}]',
        '# Nhập điểm trung bình (số thực) và in ra xếp loại.
diem = float(input())
# Viết tiếp phần còn lại ở dưới đây
',
        'diem = float(input())
if diem >= 8:
    print("Gioi")
elif diem >= 6.5:
    print("Kha")
elif diem >= 5:
    print("Trung binh")
else:
    print("Yeu")
',
        'Kiểm tra các mốc theo thứ tự từ cao xuống thấp bằng if/elif/else. Nhánh nào đúng trước thì được thực hiện, nên không cần ghi thêm cận trên cho mỗi nhánh.',
        0, 5000);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 1, '9', 'Gioi', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 2, '8', 'Gioi', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 3, '7.5', 'Kha', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 4, '6.5', 'Kha', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 5, '6.4', 'Trung binh', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 6, '5', 'Trung binh', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 7, '4.99', 'Yeu', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 8, '0', 'Yeu', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-02-xep-loai', 9, '10', 'Gioi', 1);

INSERT OR IGNORE INTO "Lessons" ("Id", "Order", "Title", "Description", "Summary", "Notes", "Conclusion")
VALUES ('bai-03-vong-lap', 3, 'Bài 3: Vòng lặp for và while', 'Lặp lại công việc bằng for ... in range(), while, tích lũy tổng và lệnh break.',
    '["Vòng lặp giúp thực hiện lặp đi lặp lại một khối lệnh mà không phải viết lại nhiều lần.", "- `for i in range(n):` lặp với i lần lượt nhận các giá trị 0, 1, ..., n-1.", "- `range(a, b)` lặp từ a đến b-1; `range(a, b, k)` lặp với bước nhảy k.", "- `while điều_kiện:` lặp chừng nào điều kiện còn đúng.", "- `break` thoát khỏi vòng lặp ngay lập tức.", "- Tích lũy tổng: khởi tạo `tong = 0` rồi cộng dồn trong vòng lặp.", "```python\ntich = 1\nfor k in range(1, 6):\n    tich = tich * k\nprint(tich)\n```"]',
    '["`range(n)` không bao gồm n. Muốn lặp đến n phải dùng `range(1, n + 1)`.", "Với `while`, phải có lệnh làm thay đổi điều kiện bên trong vòng lặp, nếu không chương trình sẽ lặp vô hạn.", "Phải khởi tạo biến tích lũy (ví dụ `tong = 0`, `dem = 0`) trước khi vào vòng lặp.", "Thụt lề quyết định lệnh nào nằm trong vòng lặp, lệnh nào nằm ngoài."]',
    '["- Dùng `for` khi biết trước số lần lặp, dùng `while` khi lặp theo điều kiện.", "- Luôn kiểm tra cận của `range()` để không bị thiếu hoặc thừa một giá trị.", "- Khởi tạo biến tích lũy trước vòng lặp và nhớ cập nhật điều kiện trong `while`.", "- Thử với các giá trị nhỏ nhất và lớn nhất của đề bài để kiểm tra chương trình."]');

INSERT OR IGNORE INTO "Exercises" ("Id", "LessonId", "Order", "Title", "Difficulty", "Statement", "InputFormat", "OutputFormat", "Constraints", "Examples", "StarterCode", "SolutionCode", "SolutionExplanation", "ConfirmEveryTime", "TimeLimitMs")
VALUES ('bai-03-tong-1-den-n', 'bai-03-vong-lap', 1, 'Tổng từ 1 đến n', 'Easy',
        'Nhập số nguyên dương n. Hãy tính và in ra tổng 1 + 2 + 3 + ... + n.',
        'Một dòng chứa số nguyên n.',
        'Một số nguyên là tổng 1 + 2 + ... + n.',
        '1 ≤ n ≤ 10000.',
        '[{"input": "5", "output": "15", "explanation": "1 + 2 + 3 + 4 + 5 = 15."}]',
        '# Nhập n, in ra tổng 1 + 2 + ... + n.
n = int(input())
tong = 0
# Dùng vòng lặp để cộng dồn, rồi in kết quả ở dưới đây
',
        'n = int(input())
tong = 0
for i in range(1, n + 1):
    tong = tong + i
print(tong)
',
        'Khởi tạo tong = 0, dùng for i in range(1, n + 1) để cộng từng giá trị i vào tong. Nhớ rằng range(1, n + 1) mới bao gồm cả n.',
        0, 5000);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-tong-1-den-n', 1, '1', '1', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-tong-1-den-n', 2, '5', '15', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-tong-1-den-n', 3, '10', '55', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-tong-1-den-n', 4, '100', '5050', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-tong-1-den-n', 5, '10000', '50005000', 1);

INSERT OR IGNORE INTO "Exercises" ("Id", "LessonId", "Order", "Title", "Difficulty", "Statement", "InputFormat", "OutputFormat", "Constraints", "Examples", "StarterCode", "SolutionCode", "SolutionExplanation", "ConfirmEveryTime", "TimeLimitMs")
VALUES ('bai-03-dem-uoc', 'bai-03-vong-lap', 2, 'Đếm ước của n', 'Medium',
        'Nhập số nguyên dương n. Hãy đếm và in ra số lượng ước dương của n (kể cả 1 và n).',
        'Một dòng chứa số nguyên n.',
        'Một số nguyên là số lượng ước dương của n.',
        '1 ≤ n ≤ 10000.',
        '[{"input": "12", "output": "6", "explanation": "Các ước của 12 là 1, 2, 3, 4, 6, 12 nên có 6 ước."}]',
        '# Nhập n, đếm và in ra số ước dương của n.
n = int(input())
dem = 0
# Duyệt các số từ 1 đến n, đếm số ước, rồi in kết quả ở dưới đây
',
        'n = int(input())
dem = 0
for i in range(1, n + 1):
    if n % i == 0:
        dem = dem + 1
print(dem)
',
        'Duyệt i từ 1 đến n. Nếu n % i == 0 thì i là ước của n nên tăng biến đếm lên 1. Cuối cùng in biến đếm.',
        0, 5000);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 1, '1', '1', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 2, '12', '6', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 3, '7', '2', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 4, '13', '2', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 5, '36', '9', 0);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 6, '100', '9', 1);

INSERT OR IGNORE INTO "TestCases" ("ExerciseId", "Order", "Input", "ExpectedOutput", "IsHidden")
VALUES ('bai-03-dem-uoc', 7, '10000', '25', 1);

-- Cập nhật nội dung 3 bài mẫu cho DB đã nạp từ phiên bản cũ (INSERT chỉ chạy khi chưa có bài). Idempotent.
-- LƯU Ý: ghi đè Summary/Notes/Conclusion của đúng 3 bài mẫu; bài do giáo viên thêm không bị ảnh hưởng.
UPDATE "Lessons" SET "Summary" = '["Chương trình Python thường làm ba việc: nhận dữ liệu vào, xử lý và đưa kết quả ra. Dữ liệu được lưu trong biến, mỗi biến có một kiểu dữ liệu như số nguyên (`int`), số thực (`float`) hay chuỗi (`str`).", "- Hàm `print()` đưa kết quả ra màn hình; có thể in nhiều giá trị cách nhau bằng dấu phẩy.", "- Hàm `input()` đọc một dòng dữ liệu người dùng nhập vào và luôn trả về chuỗi.", "- Hàm `int()` và `float()` chuyển chuỗi thành số nguyên hoặc số thực.", "- Các phép toán số học: `+`, `-`, `*`, `/` (chia ra số thực), `//` (chia lấy phần nguyên), `%` (chia lấy dư), `**` (lũy thừa).", "```python\nso = int(input())\nprint(\"Gấp đôi:\", so * 2)\n```"]', "Notes" = '["`input()` luôn trả về chuỗi. Phải ép kiểu bằng `int()` hoặc `float()` trước khi tính toán, nếu không \"3\" + \"4\" sẽ cho \"34\" thay vì 7.", "Phân biệt `/` và `//`: `7 / 2` cho `3.5` còn `7 // 2` cho `3`.", "Tên biến không được bắt đầu bằng chữ số, không chứa dấu cách và phân biệt chữ hoa, chữ thường (`tong` khác `Tong`).", "Bài tập được chấm bằng cách so khớp đầu ra, vì vậy chỉ in đúng kết quả yêu cầu, không in thêm lời nhắc như \"Nhap a:\"."]', "Conclusion" = '["- Nhớ quy trình: đọc dữ liệu bằng `input()`, ép kiểu, tính toán, rồi in kết quả bằng `print()`.", "- `int(input())` là cách viết gọn để đọc một số nguyên từ một dòng.", "- Chú ý sự khác nhau giữa `/` (chia thực) và `//` (chia nguyên).", "- Hãy luyện thêm với các phép toán `%` và `**` ở những bài sau."]'
WHERE "Id" = 'bai-01-nhap-xuat';

UPDATE "Lessons" SET "Summary" = '["Cấu trúc rẽ nhánh cho phép chương trình chọn một trong nhiều hướng xử lý tùy theo điều kiện đúng hay sai.", "- `if điều_kiện:` thực hiện khối lệnh thụt lề bên dưới khi điều kiện đúng.", "- `elif điều_kiện:` kiểm tra thêm một điều kiện khác khi các điều kiện trước đó sai.", "- `else:` thực hiện khi tất cả điều kiện phía trên đều sai.", "- Toán tử so sánh: `==`, `!=`, `<`, `<=`, `>`, `>=`. Toán tử logic: `and`, `or`, `not`.", "- Phép `%` cho số dư, ví dụ `n % 2 == 0` kiểm tra n chẵn.", "```python\ntuoi = int(input())\nif tuoi >= 18:\n    print(\"Du tuoi\")\nelse:\n    print(\"Chua du tuoi\")\n```"]', "Notes" = '["Các lệnh trong cùng một khối phải thụt lề đều nhau, thường là 4 khoảng trắng.", "Cuối dòng `if`, `elif`, `else` luôn có dấu hai chấm `:`.", "`==` dùng để so sánh còn `=` dùng để gán giá trị. Viết `if x = 5:` sẽ báo lỗi cú pháp.", "Thứ tự các nhánh `elif` rất quan trọng: nên kiểm tra điều kiện lớn nhất hoặc đặc biệt nhất trước.", "Với số thực, hãy kiểm tra các mốc biên (ví dụ 8, 6.5, 5) thật cẩn thận bằng `>=` hoặc `>`."]', "Conclusion" = '["- `if/elif/else` giúp chương trình ra quyết định theo điều kiện.", "- Luôn nhớ dấu `:` và thụt lề đúng.", "- Dùng `%` để kiểm tra tính chia hết; dùng `and`, `or` để ghép điều kiện.", "- Khi làm bài, hãy thử các giá trị biên để chắc chắn chương trình đúng."]'
WHERE "Id" = 'bai-02-re-nhanh';

UPDATE "Lessons" SET "Summary" = '["Vòng lặp giúp thực hiện lặp đi lặp lại một khối lệnh mà không phải viết lại nhiều lần.", "- `for i in range(n):` lặp với i lần lượt nhận các giá trị 0, 1, ..., n-1.", "- `range(a, b)` lặp từ a đến b-1; `range(a, b, k)` lặp với bước nhảy k.", "- `while điều_kiện:` lặp chừng nào điều kiện còn đúng.", "- `break` thoát khỏi vòng lặp ngay lập tức.", "- Tích lũy tổng: khởi tạo `tong = 0` rồi cộng dồn trong vòng lặp.", "```python\ntich = 1\nfor k in range(1, 6):\n    tich = tich * k\nprint(tich)\n```"]', "Notes" = '["`range(n)` không bao gồm n. Muốn lặp đến n phải dùng `range(1, n + 1)`.", "Với `while`, phải có lệnh làm thay đổi điều kiện bên trong vòng lặp, nếu không chương trình sẽ lặp vô hạn.", "Phải khởi tạo biến tích lũy (ví dụ `tong = 0`, `dem = 0`) trước khi vào vòng lặp.", "Thụt lề quyết định lệnh nào nằm trong vòng lặp, lệnh nào nằm ngoài."]', "Conclusion" = '["- Dùng `for` khi biết trước số lần lặp, dùng `while` khi lặp theo điều kiện.", "- Luôn kiểm tra cận của `range()` để không bị thiếu hoặc thừa một giá trị.", "- Khởi tạo biến tích lũy trước vòng lặp và nhớ cập nhật điều kiện trong `while`.", "- Thử với các giá trị nhỏ nhất và lớn nhất của đề bài để kiểm tra chương trình."]'
WHERE "Id" = 'bai-03-vong-lap';

COMMIT;

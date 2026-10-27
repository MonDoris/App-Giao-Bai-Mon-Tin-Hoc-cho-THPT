# Đặc tả Tính năng: Trang web học Tin học lớp 12 (kiểu LeetCode, có bài học và bài tập Python)

> **CẬP NHẬT PHIÊN BẢN 3 (đọc trước):** tài liệu này được bổ sung bởi `docs/specs/dang-nhap-phan-quyen.md` (đăng nhập, 2 role Giáo viên/Học sinh, soạn bài & giao bài trên web, lưu kết quả nộp lên server, logo DK Edu; AC-74..AC-160). Khi mâu thuẫn, **tài liệu v3 thắng**. Các mục/AC của file này bị thay đổi hoặc vô hiệu: QD-01, QD-05, QD-10, QD-12, QD-13, mục 0.2, mục 1 (user story giáo viên), mục 2.2, mục 6; AC-01, 03, 08, 10, 15, 16, 29, 32, 35, 36, 38, 40, 44, 47, 48 (một phần vô hiệu), 49, 57, 58, 59, 60, 61, 62, 63, 64, 66, 67, 68, 72 — chi tiết tại mục 12 của file v3 và mục 9 cuối file này. AC còn lại giữ nguyên.

## 0. Bối cảnh, phạm vi & Quyết định mặc định

**Khách hàng:** Giáo viên Tin học lớp 12 (Việt Nam).
**Yêu cầu gốc:** "Làm một trang web giống LeetCode nhưng khác ở chỗ: (1) Khi học sinh bấm vào một bài học sẽ hiện các tiêu đề chính theo thứ tự: Tóm tắt nội dung chính của bài học, Những điểm cần lưu ý, Bài tập, và cuối cùng là Tổng kết bài học. (2) Trong phần Bài tập (giống LeetCode) có 2 màn hình: một màn hình hiển thị đề bài, một màn hình có chỗ để code. Có nút 'Đáp án' để học sinh xem khi không biết code."

**Thay đổi yêu cầu (phiên bản 2):** Khách hàng yêu cầu dự án theo **Clean Architecture**, công nghệ **HTML, CSS, JavaScript** phía client và **C# / ASP.NET Core (.NET)** phía server. **Không dùng Java.** Học sinh vẫn viết Python và chạy trong trình duyệt bằng Pyodide. Các [AC] chức năng cũ được giữ nguyên; chỉ sửa các chỗ mâu thuẫn với "web tĩnh không backend", `file://`, đường dẫn `web/` (xem mục 8 – Nhật ký thay đổi) và bổ sung mục 3.10 (AC-52 trở đi).

### 0.1 Quyết định mặc định (đã chốt, có thể điều chỉnh nếu Khách hàng phản hồi)
| Mã | Quyết định |
|---|---|
| QD-01 | ~~Web tĩnh không backend~~ → **Ứng dụng ASP.NET Core** (project `Web`) vừa cung cấp API JSON vừa phục vụ HTML/CSS/JS tĩnh trong `wwwroot`. Dữ liệu bài học lưu trong **SQL Server** (QD-05); không đăng nhập. Chạy bằng `dotnet run --project src/Web`. Mã nguồn giao diện (HTML/CSS/JS) nằm tại `D:\Education\src\Web\wwwroot`. |
| QD-02 | Toàn bộ giao diện và nội dung bằng **tiếng Việt** (thuộc tính `lang="vi"`, mã hóa UTF-8). |
| QD-03 | Ngôn ngữ lập trình cho bài tập: **Python**, chạy ngay trong trình duyệt bằng **Pyodide** nạp qua CDN. |
| QD-04 | Có nút **Chạy** (chạy thử với dữ liệu nhập tùy chọn) và nút **Nộp bài** (chấm theo bộ test case). |
| QD-05 | ~~File JSON~~ → Dữ liệu bài học/bài tập/test case lưu trong **cơ sở dữ liệu SQL Server**, truy cập bằng **Entity Framework Core** với **migrations**. Chuỗi kết nối cấu hình trong `appsettings.json` (khóa `ConnectionStrings:DefaultConnection`), không viết cứng trong code. Nội dung bài **tách khỏi mã nguồn** (HTML/JS/CSS/C#). Giáo viên/quản trị thêm bài bằng **script SQL** (hoặc seed) đặt trong `D:\Education\database`, **không sửa code giao diện, không build lại**. Frontend **không truy cập DB trực tiếp** mà lấy dữ liệu qua API. |
| QD-14 | Thư mục `D:\Education\database` chứa script SQL: `schema.sql` (sinh từ migration, idempotent), `seed-sample.sql` (3 bài mẫu x 2 bài tập, idempotent), `them-bai-moi.sql` (mẫu thêm bài). Kiểm thử Application dùng đối tượng giả (fake); kiểm thử Infrastructure dùng **SQLite in-memory hoặc EF Core InMemory** (không cần SQL Server thật). |
| QD-06 | Có **trang danh sách bài học** (trang chủ). |
| QD-07 | Có **3 bài mẫu** Python cơ bản phù hợp lớp 12 (mục 4). |
| QD-08 | Nút **Đáp án** có **hộp thoại xác nhận** trước khi hiện đáp án. |
| QD-09 | Bài tập bố cục **2 cột** (đề bài bên trái, trình soạn code bên phải); **responsive**, xếp dọc trên màn hình nhỏ. |
| QD-10 | Trạng thái học tập (bài đã nộp đạt, code đang soạn) lưu bằng `localStorage` của trình duyệt, chỉ phục vụ tiện ích cá nhân; không đồng bộ giữa các máy; server không lưu. |
| QD-11 | **Clean Architecture** với solution .NET tại `D:\Education\src` gồm 4 project: `Domain`, `Application`, `Infrastructure`, `Web` (chi tiết mục 5.1) và project kiểm thử xUnit tại `D:\Education\tests` (dành cho `Application`). Quy tắc phụ thuộc luôn hướng vào trong. **Không dùng Java.** |
| QD-12 | **Chấm bài và chạy code vẫn hoàn toàn ở trình duyệt** (Pyodide + Web Worker); server không chạy code học sinh, chỉ cung cấp dữ liệu qua API. |
| QD-13 | **Đáp án tách khỏi dữ liệu đề bài:** API đề bài không trả `solution`; đáp án lấy qua endpoint riêng, chỉ gọi sau khi học sinh bấm "Đáp án" và xác nhận. |

### 0.2 Ngoài phạm vi (phiên bản này)
Đăng nhập/tài khoản, bảng xếp hạng, lưu điểm lên máy chủ, giao diện giáo viên để soạn bài (giáo viên sửa file JSON trực tiếp), ngôn ngữ khác ngoài Python, chạy code Python phía server, giao diện quản trị bài học (thêm bài bằng script SQL), chạy offline hoàn toàn (cần Internet lần đầu để tải Pyodide từ CDN), triển khai lên hosting/Docker (chỉ yêu cầu chạy cục bộ bằng `dotnet run`).

### 0.3 Giả định cần Khách hàng xác nhận
1. Nội dung 3 bài mẫu do BA/dev soạn tạm, giáo viên sẽ thay bằng nội dung chính thức.
2. Bài tập có đầu vào qua `input()` và đầu ra qua `print()` (kiểu chấm so khớp đầu ra, như LeetCode/Codeforces dạng stdin/stdout).
3. Học sinh dùng trình duyệt hiện đại (Chrome/Edge/Firefox bản 2 năm gần nhất) và có kết nối Internet.
4. Máy phát triển/chạy server cài **.NET SDK phiên bản LTS mới nhất** (ví dụ .NET 8 hoặc mới hơn); dev chốt phiên bản và ghi vào `global.json`/`TargetFramework`. Có **SQL Server** (bản Express hoặc LocalDB đều được) và công cụ `dotnet-ef`; chuỗi kết nối mặc định cho môi trường phát triển dùng LocalDB, Khách hàng đổi trong `appsettings.json` nếu khác.
5. Vì chấm bài ở trình duyệt nên dữ liệu test case (kể cả test ẩn) vẫn được gửi xuống trình duyệt qua API (xem mục 6); chỉ `solution` được bảo vệ bằng endpoint riêng.

---

## 1. Mục tiêu & User Story

- Là một **học sinh lớp 12**, tôi muốn xem danh sách bài học và bấm vào một bài để đọc nội dung theo trình tự rõ ràng (Tóm tắt, Lưu ý, Bài tập, Tổng kết) để dễ nắm kiến thức.
- Là một **học sinh**, tôi muốn làm bài tập ngay trên trang với đề bài một bên và chỗ viết code một bên, bấm Chạy/Nộp bài để biết ngay kết quả, để tôi luyện tập lập trình như trên LeetCode mà không phải cài phần mềm.
- Là một **học sinh**, khi không biết code, tôi muốn bấm nút "Đáp án" để xem lời giải tham khảo để học từ đó.
- Là một **giáo viên Tin học**, tôi muốn thêm bài học/bài tập mới chỉ bằng cách thêm/sửa file dữ liệu để không phải biết lập trình web.

---

## 2. Luồng nghiệp vụ (Workflow)

### 2.1 Luồng học sinh
- Bước 1: Học sinh mở trang chủ `index.html` (địa chỉ gốc của ứng dụng, ví dụ `http://localhost:5000/`) → JavaScript gọi `GET /api/lessons` → thấy **danh sách bài học** (số thứ tự, tiêu đề, mô tả ngắn, số bài tập, trạng thái hoàn thành).
- Bước 2: Học sinh bấm vào một bài → chuyển tới trang bài học `lesson.html?id=<mã-bài>`; trang gọi `GET /api/lessons/{id}`.
- Bước 3: Trang bài học hiện **4 tiêu đề chính theo đúng thứ tự cố định**: (1) Tóm tắt nội dung chính của bài học → (2) Những điểm cần lưu ý → (3) Bài tập → (4) Tổng kết bài học.
- Bước 4: Trong mục **Bài tập**, học sinh thấy danh sách bài tập của bài; bấm một bài tập → mở **màn hình làm bài** `problem.html?lesson=<mã-bài>&problem=<mã-bt>`, gồm 2 cột: trái là **đề bài**, phải là **trình soạn code** (có mã khởi tạo).
- Bước 5: Học sinh viết code, có thể nhập dữ liệu thử rồi bấm **Chạy** để xem kết quả (stdout/lỗi).
- Bước 6: Học sinh bấm **Nộp bài** → hệ thống chạy code với từng test case, hiển thị kết quả từng test (Đạt/Sai/Lỗi/Quá thời gian) và kết luận chung.
- Bước 7: Nếu bí, học sinh bấm **Đáp án** → hộp thoại xác nhận → chọn "Xem đáp án" thì trình duyệt mới gọi `GET /api/exercises/{id}/solution` và hiện lời giải tham khảo kèm giải thích; chọn "Hủy" thì không gọi API, không hiện.
- Bước 8: Học sinh quay lại trang bài học, đọc **Tổng kết bài học** và/hoặc sang bài kế tiếp.

### 2.2 Luồng giáo viên (thêm bài mới)
- Bước 1: Sao chép `database/them-bai-moi.sql` thành script mới, điền dữ liệu bài (Lessons), các bài tập (Exercises) và test case (TestCases) theo schema ở mục 5.2.
- Bước 2: Chạy script trên SQL Server (SSMS, Azure Data Studio hoặc `sqlcmd`).
- Bước 3: Tải lại trang chủ → bài mới xuất hiện (API truy vấn DB mỗi lần, không cache lâu quá 5 giây), **không** sửa file HTML/JS/CSS/C#, **không** build lại, **không** khởi động lại server. Lần cài đặt ban đầu: tạo DB bằng `dotnet ef database update` (hoặc chạy `database/schema.sql`) rồi chạy `database/seed-sample.sql`.

### 2.3 Trường hợp biên (Edge Cases)
- Mã bài/bài tập trên URL không tồn tại hoặc thiếu → API trả 404, giao diện hiện thông báo "Không tìm thấy bài học/bài tập" kèm liên kết về trang chủ.
- Bản ghi bài học thiếu dữ liệu bắt buộc (ví dụ bài không có test case, cột JSON `Examples` sai cú pháp) → API trả lỗi có mô tả cho đúng bài đó (mục 5.3); giao diện hiện thông báo dễ hiểu, các bài khác vẫn dùng được.
- Không kết nối được SQL Server → API trả 503 với thông báo chung (không lộ chuỗi kết nối/chi tiết nội bộ); giao diện hiện "Máy chủ dữ liệu đang bận, thử lại sau".
- API không phản hồi (server tắt/mạng lỗi) → giao diện hiện "Không kết nối được máy chủ" kèm nút "Thử lại", không lỗi JavaScript chưa bắt.
- Không tải được Pyodide (mất mạng/CDN lỗi) → hiện thông báo và cho nút "Thử lại"; phần đọc bài vẫn dùng bình thường.
- Code học sinh bị vòng lặp vô hạn → bị dừng sau giới hạn thời gian, báo "Quá thời gian", trang không bị treo.
- Học sinh nộp bài khi trình soạn code trống → không chạy chấm, báo "Bạn chưa viết code".
- Học sinh bấm Đáp án rồi Hủy → trạng thái không đổi, không lộ đáp án.
- Bài học không có bài tập → mục "Bài tập" vẫn hiện tiêu đề kèm dòng "Bài học này chưa có bài tập".

---

## 3. Tiêu chí Nghiệm thu (Acceptance Criteria)

### 3.1 Trang danh sách bài học
- [AC-01]: Trang `index.html` hiển thị đủ số bài bằng số phần tử trả về từ `GET /api/lessons` (= số dòng bảng `Lessons`; với dữ liệu mẫu: đúng 3 bài), mỗi bài là một thẻ gồm: số thứ tự, tiêu đề, mô tả ngắn, số bài tập. Thứ tự hiển thị theo cột `Order` tăng dần.
- [AC-02]: Bấm vào thẻ bài học (hoặc phím Enter khi thẻ đang được focus) điều hướng tới `lesson.html?id=<mã-bài>` của đúng bài đó trong ≤ 1 lần bấm; không mở tab mới.
- [AC-03]: Thẻ bài học hiển thị nhãn "Đã hoàn thành" khi mọi bài tập của bài đó đã nộp đạt 100% test (đọc từ `localStorage`); khi `localStorage` bị chặn/không dùng được, trang vẫn hiển thị bình thường (không có nhãn, không báo lỗi trên console).

### 3.2 Trang bài học – cấu trúc 4 tiêu đề
- [AC-04]: Trang bài học có đúng 4 tiêu đề cấp 2 (`<h2>`) theo thứ tự từ trên xuống, với chính xác văn bản: "Tóm tắt nội dung chính của bài học", "Những điểm cần lưu ý", "Bài tập", "Tổng kết bài học". Kiểm thử tự động: lấy danh sách `h2` trong vùng nội dung, so khớp thứ tự và nội dung.
- [AC-05]: Thứ tự 4 tiêu đề cố định cho mọi bài học, không phụ thuộc dữ liệu/thứ tự cột trong DB.
- [AC-06]: Có mục lục cố định/đầu trang gồm 4 liên kết tương ứng; bấm một liên kết cuộn tới đúng tiêu đề trong ≤ 1 giây (có thể dùng neo `#id`).
- [AC-07]: Nội dung mỗi mục lấy từ API (`GET /api/lessons/{id}`, dữ liệu từ DB); mục "Tóm tắt" và "Tổng kết" hiển thị đoạn văn/danh sách gạch đầu dòng; mục "Những điểm cần lưu ý" hiển thị danh sách ≥ 1 mục (với bài mẫu: 3–5 mục); đoạn mã trong nội dung hiển thị bằng font đơn cách (monospace) trong khối `<pre><code>`.
- [AC-08]: Mục "Bài tập" liệt kê mọi bài tập của bài theo cột `Order`, mỗi dòng gồm: tên bài tập, độ khó (Dễ/Trung bình/Khó), trạng thái (Chưa làm/Đã đạt). Bấm vào dòng mở màn hình làm bài của đúng bài tập đó.
- [AC-09]: Trang bài học có liên kết "Về danh sách bài học" và "Bài trước/Bài sau" (ẩn nút Bài trước ở bài đầu, ẩn Bài sau ở bài cuối).
- [AC-10]: Mã bài `id` không hợp lệ hoặc không có trong bảng `Lessons` (API trả 404) → hiện thông báo "Không tìm thấy bài học" và liên kết về trang chủ; không có lỗi JavaScript chưa bắt (console không có `Uncaught`).

### 3.3 Màn hình làm bài – bố cục 2 cột
- [AC-11]: Ở viewport rộng ≥ 900 px, màn hình làm bài hiển thị **2 cột cạnh nhau**: cột trái "Đề bài", cột phải "Trình soạn code"; mỗi cột chiếm 40–60% chiều rộng khung chứa; hai cột cùng chiều cao và **cuộn độc lập** (cuộn đề bài không làm trượt trình soạn code).
- [AC-12]: Ở viewport < 900 px, hai cột chuyển thành **xếp dọc** (đề bài trên, code dưới) hoặc dạng hai tab "Đề bài | Code"; không xuất hiện thanh cuộn ngang của trang ở chiều rộng 360 px; mọi nút bấm có vùng chạm tối thiểu 44×44 px.
- [AC-13]: Cột Đề bài hiển thị đủ: tên bài tập, độ khó, mô tả đề, định dạng dữ liệu vào, định dạng dữ liệu ra, ràng buộc (nếu có) và ≥ 1 ví dụ mẫu (Input/Output) trình bày trong khối monospace; có thể kèm giải thích ví dụ.
- [AC-14]: Cột Code gồm: trình soạn code (vùng soạn thảo nhiều dòng, font monospace, hiển thị số dòng, hỗ trợ phím Tab chèn 4 khoảng trắng), ô "Dữ liệu vào (stdin)" để nhập thử, các nút **Chạy**, **Nộp bài**, **Đáp án**, **Đặt lại code**, và vùng "Kết quả" bên dưới.
- [AC-15]: Khi mở bài tập lần đầu, trình soạn code được điền sẵn mã khởi tạo (`starterCode`) từ API (cột `StarterCode`). Khi mở lại bài tập, code học sinh đã soạn gần nhất được khôi phục từ `localStorage` (tự lưu sau ≤ 1 giây kể từ lần gõ cuối). Nút "Đặt lại code" có xác nhận và đưa về `starterCode`.
- [AC-16]: Màn hình làm bài có liên kết quay về bài học chứa nó; tiêu đề tab trình duyệt có dạng "<Tên bài tập> – Tin học 12".

### 3.4 Chạy code (Pyodide)
- [AC-17]: Pyodide được nạp từ CDN bằng thẻ `<script>` với **phiên bản cố định** (ví dụ `v0.26.x`, không dùng `latest`); chỉ nạp khi vào màn hình làm bài, không nạp ở trang danh sách/bài học.
- [AC-18]: Trong lúc nạp Pyodide, hiển thị trạng thái "Đang tải trình chạy Python…" và các nút Chạy/Nộp bài ở trạng thái vô hiệu; sau khi sẵn sàng, các nút được bật. Mục tiêu: sẵn sàng trong ≤ 15 giây trên đường truyền 10 Mbps (lần tải đầu), ≤ 3 giây các lần sau nhờ cache trình duyệt.
- [AC-19]: Bấm **Chạy** với code `print("Xin chào")` và stdin rỗng → vùng Kết quả hiển thị đúng "Xin chào" trong ≤ 2 giây (sau khi Pyodide đã sẵn sàng).
- [AC-20]: Bấm **Chạy** với code dùng `input()` và có dữ liệu trong ô stdin → `input()` đọc lần lượt từng dòng của ô stdin; nếu `input()` được gọi nhiều hơn số dòng có sẵn, báo lỗi "Chương trình cần thêm dữ liệu vào" (không treo, không bật hộp thoại prompt của trình duyệt).
- [AC-21]: Code có lỗi cú pháp hoặc lỗi lúc chạy → vùng Kết quả hiển thị loại lỗi và **số dòng** gây lỗi (ví dụ "Lỗi ở dòng 3: NameError: name 'x' is not defined"), nền/ký hiệu báo lỗi phân biệt với kết quả thường; trang không bị đơ và học sinh có thể sửa và chạy lại ngay.
- [AC-22]: Mỗi lần chạy/chấm bị giới hạn **thời gian tối đa 5 giây** (mỗi test case). Code lặp vô hạn (`while True: pass`) bị dừng và báo "Quá thời gian (5 giây)"; trang vẫn phản hồi bấm nút sau đó (chạy trong Web Worker rồi dừng/khởi động lại worker).
- [AC-23]: Đầu ra quá 10 000 ký tự được cắt và thêm dòng "… (đã cắt bớt)"; không làm chậm trang (> 1 giây khi hiển thị).
- [AC-24]: Trong lúc một lần chạy đang diễn ra, nút Chạy/Nộp bài bị vô hiệu và nút Chạy đổi thành "Đang chạy…" để tránh chạy chồng; có nút "Dừng" để hủy lần chạy.

### 3.5 Nộp bài & chấm test case
- [AC-25]: Mỗi bài tập có ≥ 3 test case (bảng `TestCases`, sắp theo `Order`) (bài mẫu: 4–6 test, trong đó ≥ 1 test biên). Bấm **Nộp bài** chạy lần lượt toàn bộ test case với `input` tương ứng và so sánh đầu ra.
- [AC-26]: Quy tắc so khớp mặc định: bỏ khoảng trắng/dòng trống thừa ở **cuối mỗi dòng và cuối toàn bộ đầu ra**, đổi `\r\n` về `\n`, sau đó so sánh chuỗi **chính xác** với `expectedOutput`. Phân biệt chữ hoa/thường. Ví dụ: đầu ra `"5\n"` khớp `"5"`; `"5 "` khớp `"5"`; `"05"` không khớp `"5"`.
- [AC-27]: Sau khi chấm, hiển thị: (a) dòng kết luận "Chấp nhận – 5/5 test đạt" hoặc "Chưa đạt – 3/5 test đạt"; (b) danh sách từng test với trạng thái chỉ thuộc tập {Đạt, Sai kết quả, Lỗi chạy, Quá thời gian} kèm thời gian chạy (ms).
- [AC-28]: Với test **hiển thị** (`isHidden: false`), khi Sai kết quả cho xem Input, Đầu ra mong đợi và Đầu ra của bạn. Với test **ẩn** (`isHidden: true`), chỉ báo trạng thái, **không lộ** Input/đầu ra mong đợi trong giao diện.
- [AC-29]: Nộp bài đạt 100% test → đánh dấu bài tập "Đã đạt" trong `localStorage`, hiển thị thông báo chúc mừng, và cập nhật trạng thái ở trang bài học/trang chủ (AC-03, AC-08). Nộp bài chưa đạt không xóa trạng thái "Đã đạt" đã có trước đó.
- [AC-30]: Mỗi lần Nộp bài chạy các test **độc lập** (môi trường Python mới/biến toàn cục không rò rỉ giữa các test). Kiểm thử: code lưu biến toàn cục ở test 1 không ảnh hưởng test 2.
- [AC-31]: Nộp bài khi code trống (sau khi bỏ khoảng trắng) → không chạy chấm, hiển thị "Bạn chưa viết code" trong ≤ 0,5 giây.

### 3.6 Nút "Đáp án"
- [AC-32]: Nút "Đáp án" luôn hiển thị trong cột Code của mọi bài tập có đáp án (cờ `hasSolution: true` trong API đề bài; bài tập có `SolutionCode` khác rỗng trong DB); ban đầu **đáp án chưa có trong DOM** (không render) và **chưa được tải về** (không có request tới `/api/exercises/{id}/solution`).
- [AC-33]: Bấm "Đáp án" mở **hộp thoại xác nhận** (modal) với nội dung nêu rõ nên tự thử trước (ví dụ: "Bạn đã thử hết cách chưa? Xem đáp án sẽ không còn cơ hội tự suy nghĩ.") và 2 nút "Xem đáp án" và "Hủy". Hộp thoại đóng được bằng phím Esc, tương đương "Hủy".
- [AC-34]: Chọn "Hủy"/Esc/bấm nền ngoài → đóng hộp thoại, đáp án **không** hiện, nội dung trình soạn code **không đổi**.
- [AC-35]: Chọn "Xem đáp án" → gọi `GET /api/exercises/{id}/solution` rồi hiển thị khối "Đáp án tham khảo" gồm mã Python đầy đủ (monospace) và phần giải thích bằng lời (≥ 1 câu). Đáp án **không tự ghi đè** code của học sinh. Có nút "Chép vào trình soạn" (có xác nhận ghi đè nếu code hiện tại khác `starterCode`) và nút "Ẩn đáp án".
- [AC-36]: Sau khi xem đáp án một lần, trong cùng phiên vào lại bài tập đó đáp án vẫn hiện nhưng hộp thoại xác nhận không bắt buộc lại; (hoặc, nếu Khách hàng muốn, luôn xác nhận lại – cấu hình `confirmEveryTime`, mặc định `false`). Trạng thái "đã xem đáp án" được lưu `localStorage` và hiển thị huy hiệu "Đã xem đáp án" trong danh sách bài tập.
- [AC-37]: Hộp thoại xác nhận thân thiện bàn phím và trình đọc màn hình: có `role="dialog"`, `aria-modal="true"`, tiêu điểm vào nút "Hủy" khi mở và trả về nút "Đáp án" khi đóng; Tab quay vòng trong hộp thoại.
- [AC-38]: Bài tập không có đáp án (`SolutionCode` null/rỗng, `hasSolution: false`) → nút "Đáp án" bị ẩn (không hiện nút lỗi); gọi trực tiếp endpoint solution của bài đó trả 404.

### 3.7 Dữ liệu & khả năng mở rộng
- [AC-39]: Toàn bộ nội dung bài học, đề bài, test case, đáp án nằm trong **cơ sở dữ liệu SQL Server** (bảng `Lessons`, `Exercises`, `TestCases`; được nạp bởi script trong `D:\Education\database`); **không có** nội dung bài học nào được viết cứng trong file `.html/.js/.css/.cs`. Kiểm thử: tìm chuỗi tiêu đề bài mẫu trong `src/**/*.js`, `*.html`, `*.cs` (trừ `tests/`) → không có kết quả.
- [AC-40]: Thêm một bài mới đúng schema (mục 5.2) bằng **một script SQL** (INSERT vào `Lessons`, `Exercises`, `TestCases`) làm bài xuất hiện ở trang chủ và mở được đầy đủ 4 mục, **không sửa** file `.html/.js/.css/.cs`, **không build lại, không khởi động lại server**. Kiểm thử: chạy script thêm bài giả `bai-99` và xác nhận hiển thị.
- [AC-41]: Có tài liệu hướng dẫn ngắn `D:\Education\database\HUONG-DAN-THEM-BAI.md` (tiếng Việt, ≤ 1 trang) mô tả các cột dữ liệu, ví dụ script tối thiểu (`them-bai-moi.sql`), cách tạo DB (`dotnet ef database update` hoặc `schema.sql`), cách nạp mẫu và cách chạy ứng dụng (`dotnet run --project src/Web`).
- [AC-42]: Một bài có dữ liệu sai (không có test case, thiếu `Title`, cột JSON hỏng…) chỉ làm hỏng chính bài đó: API trả lỗi có mô tả, giao diện hiện "Dữ liệu bài học bị lỗi: <trường>" cho bài đó, các bài khác (và danh sách) hoạt động bình thường.

### 3.8 Giao diện, hiệu năng, tương thích & bảo mật
- [AC-43]: Mọi văn bản giao diện bằng tiếng Việt có dấu, hiển thị đúng (không lỗi mã hóa) trên Chrome, Edge, Firefox bản mới; `<html lang="vi">`; `<meta charset="utf-8">`.
- [AC-44]: Trang chủ và trang bài học (chưa tính Pyodide) tải xong và tương tác được trong ≤ 2 giây ở mạng 10 Mbps; tổng dung lượng JS/CSS tự viết (không tính Pyodide/CDN) ≤ 500 KB.
- [AC-45]: Responsive: các trang trang chủ, bài học, làm bài hiển thị đúng không tràn ngang tại các chiều rộng 360, 768, 1280 và 1920 px; cỡ chữ nội dung ≥ 16 px.
- [AC-46]: Tỉ lệ tương phản chữ/nền ≥ 4,5:1 cho văn bản thường; mọi nút/liên kết thao tác được hoàn toàn bằng bàn phím và có viền focus nhìn thấy.
- [AC-47]: Ứng dụng chạy được bằng `dotnet run --project src/Web` (sau khi có DB) và truy cập qua `http://localhost:<cổng>/`; ~~server tĩnh/`file://`~~ không còn là cách chạy được hỗ trợ. Giao diện chỉ gọi API cùng nguồn (same-origin, không cần CORS); phụ thuộc mạng ngoài chỉ gồm CDN của Pyodide (và thư viện soạn code nếu dùng, qua CDN phiên bản cố định).
- [AC-48]: Bảo mật: code học sinh chỉ chạy trong sandbox của trình duyệt (Pyodide/Web Worker), không gửi code hoặc kết quả ra máy chủ nào; nội dung từ API/DB được hiển thị an toàn (thoát HTML hoặc chỉ cho phép tập thẻ định dạng an toàn, không thực thi `<script>`). Kiểm thử: bài giả chứa `<script>alert(1)</script>` trong mô tả không bật hộp thoại alert.
- [AC-49]: Không có lỗi JavaScript (`console.error`/`Uncaught`) khi thao tác theo luồng chính ở cả 3 trang trên Chrome bản mới.

### 3.9 Bài mẫu
- [AC-50]: Sau khi chạy `database/seed-sample.sql` (hoặc migration seed) DB có đúng 3 bài mẫu như mục 4, mỗi bài có đủ 4 mục nội dung không rỗng, mỗi bài có ≥ 2 bài tập (tổng ≥ 6 bài tập), mỗi bài tập có `starterCode`, ≥ 1 ví dụ, ≥ 4 test case (≥ 1 test biên, ≥ 1 test ẩn) và đáp án.
- [AC-51]: Với mọi bài tập mẫu, chạy `solution` qua bộ chấm cho kết quả **100% test Đạt**, và `starterCode` (chưa sửa) cho kết quả **không đạt** (đảm bảo test có ý nghĩa). Kiểm thử tự động được.

### 3.10 Kiến trúc Clean Architecture & .NET (bổ sung, tiếp nối AC-51)
- [AC-52]: Có solution `D:\Education\src\TinHoc12.sln` gồm đúng 4 project: `Domain`, `Application`, `Infrastructure`, `Web`; và project kiểm thử xUnit trong `D:\Education\tests` (`Application.Tests`, `Infrastructure.Tests`). Tên project, thư mục nêu trên là bắt buộc; không có project Java hay mã Java nào trong repo.
- [AC-53]: Ranh giới phụ thuộc kiểm tra được qua file `.csproj`: (a) `Domain` **không có** `ProjectReference` và không có `PackageReference` nào (ngoài gói của SDK); (b) `Application` chỉ có `ProjectReference` tới `Domain`, không tham chiếu EF Core hay ASP.NET Core; (c) `Infrastructure` tham chiếu `Application` (và `Domain` qua Application) cùng gói EF Core/SQL Server; (d) `Web` tham chiếu `Application` và `Infrastructure` (chỉ để đăng ký DI), **không** có `ProjectReference` trực tiếp tới `Domain`; (e) không project nào tham chiếu `Web`; không có vòng tham chiếu. Kiểm thử tự động: phân tích XML các `.csproj` và so với danh sách cho phép.
- [AC-54]: Mã nguồn `Web` không dùng kiểu của `Domain` (không có `using ...Domain` trong `src/Web/**/*.cs`); endpoint chỉ gọi use case/interface của `Application` và trả DTO. Entity `Lesson`, `Exercise`, `TestCase` nằm trong `Domain`; use case, interface repository (ví dụ `ILessonRepository`) và DTO nằm trong `Application`; `DbContext` và cài đặt repository nằm trong `Infrastructure`.
- [AC-55]: `dotnet build D:\Education\src\TinHoc12.sln` thành công với **0 lỗi** (cảnh báo được ghi nhận nhưng không có cảnh báo nullable/analyzer mức lỗi).
- [AC-56]: `dotnet test` trên solution/thư mục `tests` **pass 100%**, gồm ≥ 1 test cho mỗi use case của `Application` (lấy danh sách bài, lấy chi tiết bài, lấy đáp án) với repository giả, bao phủ cả nhánh "không tìm thấy". Test không cần SQL Server thật, không cần mạng.
- [AC-57]: `GET /api/lessons` trả `200` và `Content-Type: application/json`, thân gồm mảng bài sắp theo `order` tăng dần; mỗi phần tử có đúng các trường `id`, `order`, `title`, `description`, `exerciseCount` (lược đồ mục 5.3). Không chứa nội dung chi tiết bài, test case hay đáp án.
- [AC-58]: `GET /api/lessons/{id}` với `id` hợp lệ trả `200` JSON theo lược đồ mục 5.3: `id`, `title`, `summary[]`, `notes[]`, `conclusion[]`, `exercises[]` (sắp theo `order`); mỗi exercise có `id`, `order`, `title`, `difficulty`, `statement`, `inputFormat`, `outputFormat`, `constraints`, `examples[]`, `starterCode`, `hasSolution`, `confirmEveryTime`, `timeLimitMs`, `testCases[]` (`input`, `expectedOutput`, `isHidden`).
- [AC-59]: ID không tồn tại hoặc sai định dạng (ví dụ `khong-co`, chuỗi rỗng, `' OR 1=1--`, chuỗi > 100 ký tự) → `GET /api/lessons/{id}` và `GET /api/exercises/{id}/solution` trả **404** với JSON lỗi `{ "error": "NOT_FOUND", "message": "..." }` bằng tiếng Việt; không trả 500, không lộ stack trace hay chuỗi kết nối.
- [AC-60]: **Đáp án không lộ trong đề bài:** phản hồi của `/api/lessons` và `/api/lessons/{id}` **không chứa** các trường `solution`, `solutionCode`, `solutionExplanation` hay nội dung đáp án (kiểm thử: tìm kiếm chuỗi `SolutionCode` mẫu trong thân phản hồi → không thấy).
- [AC-61]: `GET /api/exercises/{id}/solution` với exercise có đáp án trả `200` JSON `{ "exerciseId", "code", "explanation" }`; exercise không có đáp án hoặc ID sai → 404 (AC-59). Endpoint không có ở danh sách/đề bài và không được tiền tải.
- [AC-62]: Trên giao diện, đáp án chỉ được tải sau khi học sinh bấm "Đáp án" **và** chọn "Xem đáp án" (hoặc đã xem trước đó theo AC-36). Kiểm thử bằng theo dõi network: mở trang làm bài, bấm Đáp án rồi Hủy → **0** request tới `/api/exercises/*/solution`; chọn "Xem đáp án" → đúng **1** request.
- [AC-63]: Frontend (HTML/CSS/JS thuần, đặt trong `src/Web/wwwroot`) chỉ lấy dữ liệu qua `/api/...`; không `fetch` file `.json` dữ liệu và không có chuỗi kết nối DB hay logic truy cập DB trong JS. Kiểm thử: quét `wwwroot` không có file `.json` dữ liệu bài học và không có request tới đường dẫn `*.json` khi duyệt luồng chính.
- [AC-64]: Lỗi hạ tầng (không kết nối được DB, ngoại lệ không lường trước) → API trả 503/500 với JSON lỗi chung, không lộ chi tiết nội bộ; được ghi log phía server; giao diện hiển thị thông báo thân thiện (mục 2.3).

### 3.11 Cơ sở dữ liệu SQL Server, EF Core & migrations (bổ sung)
- [AC-65]: `Infrastructure` chứa `DbContext` (ví dụ `AppDbContext`), cấu hình entity (Fluent API) và cài đặt repository dùng EF Core với provider SQL Server. Chuỗi kết nối nằm ở `src/Web/appsettings.json` khóa `ConnectionStrings:DefaultConnection` (cho phép ghi đè bằng biến môi trường); **không** viết cứng chuỗi kết nối trong file `.cs`/`.js`. Kiểm thử: tìm `Server=` / `Data Source=` trong `*.cs`, `*.js` → không thấy.
- [AC-66]: Schema theo mục 5.2: ba bảng `Lessons`, `Exercises`, `TestCases` với khóa chính, khóa ngoại `Exercises.LessonId → Lessons.Id` và `TestCases.ExerciseId → Exercises.Id` (xóa dây chuyền), ràng buộc duy nhất `(LessonId, Order)` và `(ExerciseId, Order)`, `Lessons.Order` duy nhất, cột bắt buộc `NOT NULL` đúng như mục 5.2, `TestCases.IsHidden` kiểu `bit` mặc định 0. Kiểm thử: truy vấn `INFORMATION_SCHEMA` trên DB đã migrate khớp danh sách cột/kiểu/ràng buộc.
- [AC-67]: Migration EF Core nằm trong `src/Infrastructure/Migrations` (ít nhất migration khởi tạo). Trên SQL Server **trống**, `dotnet ef database update --project src/Infrastructure --startup-project src/Web` chạy thành công, tạo đủ 3 bảng và bảng `__EFMigrationsHistory`; chạy lần 2 không lỗi và không đổi gì. Model hiện tại không có thay đổi chưa tạo migration (`dotnet ef migrations has-pending-model-changes` báo không còn thay đổi).
- [AC-68]: `D:\Education\database\schema.sql` (sinh từ migration, dạng idempotent) chạy được trên DB trống và tạo schema tương đương AC-66; chạy lại lần 2 không lỗi.
- [AC-69]: `D:\Education\database\seed-sample.sql` (hoặc migration seed – dev chọn một, ghi rõ trong HUONG-DAN) nạp **đúng 3 bài mẫu x 2 bài tập** theo mục 4, mỗi bài tập ≥ 4 test case (≥ 1 biên, ≥ 1 ẩn `IsHidden = 1`) và đáp án. Idempotent: chạy 2 lần liên tiếp cho kết quả **3 dòng `Lessons`, 6 dòng `Exercises`**, không trùng, không lỗi; sau seed, AC-50 và AC-51 đạt.
- [AC-70]: `D:\Education\database\them-bai-moi.sql` là script mẫu thêm 1 bài (kèm 1 bài tập, ≥ 3 test) chạy được trong một giao dịch (`BEGIN TRAN … COMMIT`, lỗi thì rollback toàn bộ) và dùng `Id` bài/bài tập không trùng; script được kiểm thử bằng cách chạy rồi gọi API (AC-40).
- [AC-71]: Quy ước ID: `Lessons.Id` và `Exercises.Id` là chuỗi slug `[a-z0-9-]`, ≤ 100 ký tự, **duy nhất toàn hệ thống** (bài tập không trùng ID với bài tập ở bài khác, để endpoint `/api/exercises/{id}/solution` xác định được); ID mẫu: `bai-01-nhap-xuat`, `bai-01-tong-hai-so`…
- [AC-72]: Kiểm thử `Infrastructure.Tests` chạy trên **SQLite in-memory hoặc EF Core InMemory** (không cần SQL Server) và pass: repository trả bài sắp theo `Order`, trả test case sắp theo `Order` với đúng cờ `IsHidden`, trả `null` cho ID sai, xóa bài thì xóa dây chuyền bài tập và test case. Truy vấn dùng tham số hóa của EF Core (không ghép chuỗi SQL từ đầu vào người dùng).
- [AC-73]: Không còn nguồn dữ liệu JSON: không có thư mục `data/` hay `web/data/` chứa nội dung bài, và `Infrastructure` không đọc file JSON để lấy bài học (các cột có kiểu mảng như `Summary`, `Examples` lưu dạng văn bản JSON trong cột `nvarchar(max)` là ngoại lệ hợp lệ, xem mục 5.2).

---

## 4. Nội dung 3 bài mẫu (Python cơ bản, lớp 12)

Nội dung chi tiết do giáo viên hiệu chỉnh sau; dưới đây là khung tối thiểu để dev tạo dữ liệu mẫu.

**Bài 1 – Biến, kiểu dữ liệu, nhập và xuất (`bai-01-nhap-xuat`)**
- Kiến thức: `print()`, `input()`, `int()`, `float()`, phép toán số học.
- Lưu ý: `input()` luôn trả về chuỗi; ép kiểu trước khi tính; phân biệt `/` và `//`.
- BT1 (Dễ) "Tổng hai số": nhập 2 số nguyên (mỗi số một dòng), in tổng. Ví dụ: `3`,`4` → `7`. Test biên: số âm, số 0.
- BT2 (Dễ) "Diện tích hình chữ nhật": nhập chiều dài, chiều rộng (nguyên), in diện tích. Ví dụ: `5`,`3` → `15`.

**Bài 2 – Cấu trúc rẽ nhánh `if` (`bai-02-re-nhanh`)**
- Kiến thức: `if/elif/else`, toán tử so sánh, toán tử `%`, toán tử logic.
- Lưu ý: thụt lề 4 khoảng trắng; dấu `:`; `==` khác `=`.
- BT1 (Dễ) "Chẵn hay lẻ": nhập số nguyên n, in `Chan` nếu chẵn, `Le` nếu lẻ. Biên: 0, số âm.
- BT2 (Trung bình) "Xếp loại học lực": nhập điểm trung bình (số thực 0–10), in `Gioi` (≥ 8), `Kha` (≥ 6,5 và < 8), `Trung binh` (≥ 5 và < 6,5), `Yeu` (< 5). Biên: 8, 6.5, 5, 4.99.

**Bài 3 – Vòng lặp `for` và `while` (`bai-03-vong-lap`)**
- Kiến thức: `for ... in range()`, `while`, tích lũy tổng, `break`.
- Lưu ý: `range(n)` không gồm n; tránh vòng lặp vô hạn; khởi tạo biến tích lũy.
- BT1 (Dễ) "Tổng từ 1 đến n": nhập n (1 ≤ n ≤ 10000), in 1+2+…+n. Biên: n = 1, n = 10000.
- BT2 (Trung bình) "Đếm ước của n": nhập n (1 ≤ n ≤ 10000), in số lượng ước dương. Biên: n = 1, số nguyên tố, số chính phương.

---

## 5. Kiến trúc, lược đồ dữ liệu & API

### 5.1 Kiến trúc Clean Architecture (solution .NET tại `D:\Education\src`)

```
D:\Education  src    TinHoc12.sln
    Domain\          Entity Lesson, Exercise, TestCase. Không phụ thuộc gì.
    Application\     Use case, interface repository (ILessonRepository...), DTO. Chỉ phụ thuộc Domain.
    Infrastructure\  AppDbContext (EF Core, SQL Server), cấu hình entity, Migrations, cài đặt repository. Phụ thuộc Application.
    Web\             ASP.NET Core: API + phục vụ tĩnh wwwroot (index.html, lesson.html, problem.html, css/, js/). appsettings.json. Composition root (DI).
  tests    Application.Tests\     xUnit, repository giả
    Infrastructure.Tests\  xUnit, SQLite in-memory hoặc EF InMemory
  database\          schema.sql, seed-sample.sql, them-bai-moi.sql, HUONG-DAN-THEM-BAI.md
```

Quy tắc phụ thuộc (luôn hướng vào trong): `Web -> Infrastructure -> Application -> Domain`; `Web -> Application`. `Domain` không phụ thuộc gì; `Application` không biết EF Core/ASP.NET; `Web` không tham chiếu trực tiếp `Domain` (chỉ đăng ký DI và gọi use case). Kiểm thử bằng AC-53, AC-54.

### 5.2 Schema cơ sở dữ liệu (SQL Server, quản lý bằng EF Core migrations)

**Lessons**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | nvarchar(100) | PK, slug `[a-z0-9-]` |
| Order | int | NOT NULL, UNIQUE |
| Title | nvarchar(200) | NOT NULL |
| Description | nvarchar(500) | NOT NULL |
| Summary | nvarchar(max) | NOT NULL, mảng JSON các chuỗi (đoạn/ý tóm tắt) |
| Notes | nvarchar(max) | NOT NULL, mảng JSON chuỗi, >= 1 ý |
| Conclusion | nvarchar(max) | NOT NULL, mảng JSON chuỗi |

**Exercises**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | nvarchar(100) | PK, duy nhất toàn hệ thống |
| LessonId | nvarchar(100) | FK -> Lessons.Id, ON DELETE CASCADE |
| Order | int | NOT NULL, UNIQUE cùng LessonId |
| Title | nvarchar(200) | NOT NULL |
| Difficulty | nvarchar(10) | NOT NULL, `Easy` / `Medium` / `Hard` |
| Statement | nvarchar(max) | NOT NULL |
| InputFormat | nvarchar(max) | NOT NULL |
| OutputFormat | nvarchar(max) | NOT NULL |
| Constraints | nvarchar(max) | NULL |
| Examples | nvarchar(max) | NOT NULL, mảng JSON `{input, output, explanation?}`, >= 1 |
| StarterCode | nvarchar(max) | NOT NULL |
| SolutionCode | nvarchar(max) | NULL (NULL = không có đáp án) |
| SolutionExplanation | nvarchar(max) | NULL |
| ConfirmEveryTime | bit | NOT NULL, mặc định 0 |
| TimeLimitMs | int | NOT NULL, mặc định 5000 |

**TestCases**
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | int | PK, IDENTITY |
| ExerciseId | nvarchar(100) | FK -> Exercises.Id, ON DELETE CASCADE |
| Order | int | NOT NULL, UNIQUE cùng ExerciseId (thứ tự chấm) |
| Input | nvarchar(max) | NOT NULL (có thể rỗng) |
| ExpectedOutput | nvarchar(max) | NOT NULL |
| IsHidden | bit | NOT NULL, mặc định 0 (1 = test ẩn, AC-28) |

Ghi chú: các cột kiểu mảng (Summary, Notes, Conclusion, Examples) lưu JSON để đơn giản; dev có thể tách bảng riêng nếu muốn, miễn giữ nguyên lược đồ API ở 5.3 và các AC. Tên cột `Order` cần được đặt trong dấu `[ ]` ở script SQL.

### 5.3 Lược đồ API (JSON, camelCase, UTF-8)

- `GET /api/lessons` -> `200` `{ "lessons": [ { "id", "order", "title", "description", "exerciseCount" } ] }`
- `GET /api/lessons/{id}` -> `200` `{ "id", "title", "summary": [], "notes": [], "conclusion": [], "exercises": [ { "id", "order", "title", "difficulty": "easy|medium|hard", "statement", "inputFormat", "outputFormat", "constraints", "examples": [ {"input","output","explanation"} ], "starterCode", "hasSolution": true, "confirmEveryTime": false, "timeLimitMs": 5000, "testCases": [ {"input","expectedOutput","isHidden"} ] } ] }` — **không có** đáp án.
- `GET /api/exercises/{id}/solution` -> `200` `{ "exerciseId", "code", "explanation" }`; chỉ gọi sau khi học sinh xác nhận.
- Lỗi: `{ "error": "NOT_FOUND" | "DATA_INVALID" | "UNAVAILABLE", "message": "...", "field": "..."? }` với mã 404 / 422 (bài dữ liệu sai, AC-42) / 503.
- Ghi chú: trường `problems` trong đặc tả phiên bản 1 được đổi tên thành `exercises` cho khớp entity `Exercise`; `visible` đổi thành `isHidden` (đảo nghĩa).

Trang tĩnh: `src/Web/wwwroot/index.html`, `lesson.html`, `problem.html`, `css/`, `js/`; URL các trang giữ nguyên như mục 2.1.

---

## 6. Rủi ro & lưu ý cho đội phát triển
- Mở file bằng `file://` **không còn được hỗ trợ**; luôn truy cập qua ứng dụng ASP.NET Core (AC-47).
- Pyodide nặng (vài MB) -> nạp lười (AC-17), hiển thị tiến trình (AC-18).
- Chấm bài ở trình duyệt nên **test case (kể cả test ẩn) vẫn được gửi xuống trình duyệt** trong `/api/lessons/{id}` và học sinh am hiểu có thể xem qua công cụ dev; chấp nhận được cho mục đích tự học. Nếu sau này cần bảo mật hơn, tách chấm sang server (ngoài phạm vi, cần sandbox chạy Python phía server).
- Trạng thái "đã đạt" lưu `localStorage` có thể bị học sinh tự sửa; không dùng để tính điểm chính thức (QD-10).
- Đáp án giờ **không** nằm trong phản hồi đề bài và chỉ lấy qua endpoint riêng sau xác nhận; tuy nhiên endpoint này không yêu cầu đăng nhập nên học sinh biết kỹ thuật vẫn gọi trực tiếp được; chấp nhận được vì Đáp án là tính năng cung cấp có chủ đích (QD-13).
- Phụ thuộc SQL Server: dùng LocalDB/Express cho phát triển; cần ghi rõ phiên bản và cách tạo DB trong HUONG-DAN (AC-41). Test không phụ thuộc SQL Server thật (AC-56, AC-72). Seed bằng script SQL (không seed trong code) để giáo viên thêm bài không cần build.
- Cột JSON trong SQL có thể bị sửa sai cú pháp khi viết tay -> API phải bắt lỗi và trả `DATA_INVALID` cho đúng bài (AC-42).

---

## 7. Bàn giao
Đặc tả (phiên bản 2) hoàn tất, sẵn sàng bàn giao cho `@dev-engineer` triển khai solution tại `D:\Education\src`, `D:\Education	ests` và script tại `D:\Education\database`; sau đó `@qa` kiểm thử theo [AC-01] ... [AC-73].

---

## 8. Nhật ký thay đổi (phiên bản 2)
| Mục | Thay đổi |
|---|---|
| QD-01, 0.2, 0.3 | Bỏ "web tĩnh không backend"; thêm ASP.NET Core, .NET SDK, SQL Server |
| QD-05, QD-11..14 | Dữ liệu từ file JSON chuyển sang SQL Server + EF Core migrations; Clean Architecture; chấm bài vẫn ở trình duyệt; đáp án qua endpoint riêng; script trong `database/` |
| 2.1, 2.2, 2.3 | Frontend gọi API; giáo viên thêm bài bằng script SQL; thêm trường hợp mất kết nối API/DB |
| AC-01, 07, 10, 15, 25, 28, 32, 35, 38 | Nguồn dữ liệu đổi sang API/DB; `visible` -> `isHidden`; đáp án tải theo yêu cầu, 404 cho bài không có đáp án |
| AC-39..42, 47, 48, 50, 51 | Thay "file JSON/`web/data`/server tĩnh/`file://`" bằng DB, script SQL, `dotnet run` |
| AC-52..73 (mới) | Kiến trúc & ranh giới .csproj, build/test, API và 404, đáp án không lộ, schema, migration, seed, test Infrastructure |

---

## 9. Phiên bản 3 (tham chiếu)
Phiên bản 3 bổ sung đăng nhập/phân quyền/soạn bài/giao bài/logo DK Edu tại **`docs/specs/dang-nhap-phan-quyen.md`** (AC-74..AC-160, QD-15..QD-40). Tóm tắt phần bị ảnh hưởng của file này (chi tiết ở mục 12 của file v3):
- **Vô hiệu/thay thế:** QD-01 vế "không đăng nhập"; QD-10 (trạng thái chỉ ở `localStorage`) → server lưu kết quả; QD-12 vế "server không nhận kết quả"; mục 0.2 các mục đăng nhập/lưu điểm/giao diện giáo viên/quản trị bài học; AC-48 vế "không gửi code/kết quả ra máy chủ" (chỉ gửi khi Nộp bài).
- **Sửa diễn giải (giữ mã AC):** AC-01, 03, 08, 10, 15, 16, 29, 32, 35, 36, 38, 40, 44, 47, 49, 57–64, 66–68, 72 (API cần đăng nhập, học sinh chỉ thấy bài đã xuất bản/được giao, trạng thái lấy từ server, tiêu đề tab "– DK Edu", ...).
- **Không đổi:** `seed-sample.sql` (AC-69, AC-50, AC-51) và phần còn lại.


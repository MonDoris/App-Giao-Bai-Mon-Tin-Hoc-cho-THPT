# Báo cáo Nghiệm thu – Trang học Tin học lớp 12

Đặc tả: `docs/specs/trang-hoc-tin-hoc-lop-12.md` (AC-01..AC-73). QA kiểm thử độc lập, không sửa mã nguồn của Dev.

## Môi trường và lệnh đã chạy
- `dotnet build src/TinHoc12.sln`: 0 Warning, 0 Error.
- `dotnet test tests/Application.Tests`: 44/44 đạt.
- `dotnet test tests/Infrastructure.Tests`: 13/13 đạt.
- `dotnet run --project src/Web` (SQLite dev, cổng 5000): API kiểm bằng curl.
- SQL Server LocalDB, CSDL tạm `TinHoc12_QA` và `TinHoc12_QA2` (đã xóa sau khi kiểm): schema.sql chạy 2 lần, seed-sample.sql chạy 2 lần, them-bai-moi.sql, `dotnet ef database update` 2 lần, `has-pending-model-changes`; API SQL Server cổng 5001/5002/5003.
- Python harness (lấy nguyên văn từ `js/py-worker.js`) và `grader.js` chạy bằng CPython và Node.
- Không kiểm được trên trình duyệt: công cụ trình duyệt tích hợp không mở được `localhost` do Claude khởi chạy, nên mọi AC cần hiển thị/thời gian/sự kiện trình duyệt được ghi KHÔNG KIỂM ĐƯỢC.

## Bảng nghiệm thu

| Mã tiêu chí | Yêu cầu kiểm thử | Kết quả | Chi tiết / Nguyên nhân lỗi |
| :--- | :--- | :---: | :--- |
| [AC-01] | Trang chủ hiển thị đủ bài, thẻ có số thứ tự, tiêu đề, mô tả, số bài tập; sắp theo Order | [PASS] | API trả 3 bài, sắp đúng; `js/index.js` render đủ 4 trường (kiểm tra mã; chưa xem hiển thị thực) |
| [AC-02] | Bấm thẻ điều hướng `lesson.html?id=` đúng bài, không mở tab mới | [PASS] | Thẻ là `<a href="lesson.html?id=...">` không có target (mã nguồn) |
| [AC-03] | Nhãn "Đã hoàn thành" đọc từ localStorage; localStorage bị chặn vẫn chạy | [PASS] | `js/storage.js` có try/catch và bộ nhớ tạm; nhãn render từ `isLessonCompleted` (mã nguồn) |
| [AC-04] | Đúng 4 h2 theo thứ tự và văn bản cố định | [PASS] | `js/lesson.js` mảng SECTIONS khớp đúng 4 tiêu đề; mỗi mục render `<h2>` |
| [AC-05] | Thứ tự 4 mục cố định, không phụ thuộc dữ liệu | [PASS] | Thứ tự do mã JS quy định, không đọc từ DB |
| [AC-06] | Mục lục 4 liên kết neo, cuộn tới tiêu đề | [PASS] | `nav.toc` với 4 `href="#id"`; id tương ứng có trên h2; CSS `scroll-behavior: smooth` (mã nguồn) |
| [AC-07] | Nội dung từ API; tóm tắt/tổng kết là đoạn/gạch đầu dòng; lưu ý ≥1 mục; code trong `<pre><code>` | [PASS] | Dữ liệu mẫu: Notes 4–5 mục, Summary 6–7; `renderBlocks` tạo `<pre class="codeblock"><code>` |
| [AC-08] | Danh sách bài tập theo Order, có tên, độ khó, trạng thái; bấm mở màn hình làm bài | [PASS] | `renderExercises` dùng `difficultyInfo`, badge "Đã đạt/Chưa làm", href `problem.html?lesson=&problem=` |
| [AC-09] | Liên kết về danh sách; Bài trước/Bài sau, ẩn ở đầu/cuối | [PASS] | Có breadcrumb "Về danh sách bài học"; pager dùng `<span>` thay nút khi không có bài trước/sau |
| [AC-10] | ID sai (404) → thông báo "Không tìm thấy bài học" + liên kết; không Uncaught | [PASS] | API 404 xác nhận cho `khong-co`, `%20`, `' OR 1=1--`, chuỗi 101 ký tự; `notFound()` render thông báo và link. Console chưa đo trên trình duyệt |
| [AC-11] | ≥900px: 2 cột 40–60%, cùng chiều cao, cuộn độc lập | [KHÔNG KIỂM ĐƯỢC] | Lý do: cần hiển thị trình duyệt. CSS (`css/style.css` dòng 433–441) dùng grid `minmax(0,1fr)` x2, `height: calc(100vh...)`, `overflow-y: auto` cho `.pane__body` (đúng ý đồ, chưa đo) |
| [AC-12] | <900px xếp dọc; 360px không cuộn ngang; nút chạm ≥44px | [KHÔNG KIỂM ĐƯỢC] | Lý do: cần hiển thị 360px. CSS: `.workspace` 1 cột dưới 900px; `.btn` có `min-height: 44px` (mã nguồn) |
| [AC-13] | Cột đề bài đủ: tên, độ khó, đề, định dạng vào/ra, ràng buộc, ≥1 ví dụ trong khối monospace | [PASS] | `buildStatementPane` render đủ; ví dụ dùng `<pre>` |
| [AC-14] | Cột code: editor monospace có số dòng, Tab 4 khoảng trắng, ô stdin, nút Chạy/Nộp/Đáp án/Đặt lại, vùng Kết quả | [PASS] | `js/editor.js` (gutter, Tab = 4 spaces); `problem.js` tạo đủ 5 nút và `#results` |
| [AC-15] | Mở lần đầu = starterCode; mở lại khôi phục code đã lưu (≤1 s); Đặt lại có xác nhận | [PASS] | `saved ? saved : starterCode`; autosave 800 ms; `onReset` dùng `confirmDialog` |
| [AC-16] | Liên kết về bài học chứa bài; tiêu đề tab "<Tên bài tập> – Tin học 12" | [PASS] | `render()` đặt `document.title` và back-link `lesson.html?id=...#bai-tap` |
| [AC-17] | Pyodide CDN phiên bản cố định, chỉ nạp ở màn hình làm bài | [PASS] | `py-worker.js` `PYODIDE_VERSION='v0.26.4'`; URL cdn.jsdelivr.net (cố định, không latest); index/lesson không tham chiếu Pyodide; problem.html có preload |
| [AC-18] | Đang tải hiển thị trạng thái, nút vô hiệu; sẵn sàng ≤15 s | [KHÔNG KIỂM ĐƯỢC] | Lý do: thời gian tải và trạng thái nút cần trình duyệt. Mã nguồn có "Đang tải trình chạy Python…" và `refreshButtons` vô hiệu hóa khi chưa ready. CDN pyodide.js truy cập được (HTTP 200) |
| [AC-19] | Chạy `print("Xin chào")` ra đúng trong ≤2 s | [KHÔNG KIỂM ĐƯỢC] | Lý do: cần chạy Pyodide trong trình duyệt để đo ≤2 s. Harness chạy bằng CPython ra đúng chuỗi "Xin chào" (kiểm tra nội dung) |
| [AC-20] | input() đọc từng dòng stdin; thiếu dữ liệu báo "Chương trình cần thêm dữ liệu vào" | [PASS] | Harness CPython: `input()` đọc 2 dòng cho kết quả 7; thiếu dòng → EOFError, `errorText` ánh xạ đúng câu thông báo. Lưu ý: chưa chạy trên Pyodide thật |
| [AC-21] | Lỗi cú pháp/chạy báo loại lỗi và số dòng; trang không đơ | [PASS] | Harness: NameError dòng 2 "name 'x' is not defined"; SyntaxError dòng 1. `errorText` tạo "Lỗi ở dòng N: Type: msg". Phần "không đơ" và giao diện chưa kiểm trên trình duyệt |
| [AC-22] | Quá 5 s dừng, báo "Quá thời gian (5 giây)", worker khởi động lại | [KHÔNG KIỂM ĐƯỢC] | Lý do: cần đo thời gian thực trên Worker trong trình duyệt. Mã nguồn: `runner.js` đặt timer `limitMs`, terminate và start lại worker, báo `timedOut` |
| [AC-23] | Đầu ra >10 000 ký tự được cắt kèm "… (đã cắt bớt)"; hiển thị ≤1 s | [KHÔNG KIỂM ĐƯỢC] | Lý do: thời gian hiển thị cần trình duyệt. Logic cắt đã xác nhận: harness trả 10 001 ký tự với `truncated=True`, `runner.js` cắt còn 10 000 + "… (đã cắt bớt)" |
| [AC-24] | Khi đang chạy: Chạy/Nộp vô hiệu, Chạy đổi thành "Đang chạy…", có nút Dừng | [PASS] | `refreshButtons` và `btnStop` trong `problem.js`; `runner.stop()` hủy lần chạy (kiểm tra mã) |
| [AC-25] | ≥3 test theo Order; Nộp bài chạy lần lượt toàn bộ test | [PASS] | Dữ liệu seed: 5–9 test mỗi bài (theo SQL và SQLite); `onSubmit` lặp qua `ex.testCases` |
| [AC-26] | Quy tắc so khớp: bỏ khoảng trắng cuối dòng/cuối đầu ra, CRLF→LF, phân biệt hoa thường | [PASS] | `grader.js` chạy bằng Node: "5\n"≈"5", "5 "≈"5", "05"≠"5", "A"≠"a", "1\r\n2"≈"1\n2" đều đúng |
| [AC-27] | Kết luận "Chấp nhận – X/Y test đạt" hoặc "Chưa đạt – X/Y"; trạng thái thuộc tập chuẩn; có ms | [PASS] | `verdict` đúng câu; nhãn chuẩn "Đạt/Sai kết quả/Lỗi chạy/Quá thời gian" và `${res.ms} ms`. Ghi chú: "Đang chờ" chỉ là trạng thái tạm trước khi có kết quả |
| [AC-28] | Test hiển thị: sai thì xem Input/Mong đợi/Của bạn; test ẩn chỉ báo trạng thái | [PASS] | `fillRow` trả về sớm với `isHidden`; chỉ test hiển thị có khối chi tiết (mã nguồn) |
| [AC-29] | Đạt 100% → đánh dấu "Đã đạt", chúc mừng, cập nhật trang; chưa đạt không xóa "Đã đạt" | [PASS] | `progress.markPassed` chỉ thêm vào danh sách; có `alert--ok` "Chúc mừng"; trạng thái đọc lại khi tải trang bài học |
| [AC-30] | Mỗi test chạy môi trường mới, biến không rò rỉ | [PASS] | Harness: `env = {...}` mới mỗi lần. Chạy CPython: `x=5` không ảnh hưởng lần chạy sau (NameError) |
| [AC-31] | Code trống → "Bạn chưa viết code", không chấm | [PASS] | `onSubmit` kiểm tra `code.trim()` trước khi chạy, hiển thị ngay |
| [AC-32] | Nút Đáp án hiện khi hasSolution; ban đầu không render đáp án và không gọi API | [PASS] | `btnSolution` chỉ tạo khi `ex.hasSolution`; `solutionArea` rỗng; `fetchSolution` chỉ gọi khi bấm (trừ trường hợp AC-36) |
| [AC-33] | Modal xác nhận có nội dung yêu cầu, nút "Xem đáp án" và "Hủy", đóng bằng Esc | [PASS] | `confirmDialog` trong `js/modal.js`; nội dung đúng câu trong đặc tả; `keydown` Escape → close(false) |
| [AC-34] | Hủy/Esc/bấm nền: không hiện đáp án, code không đổi | [PASS] | `onSolution` return khi `!ok`, không gọi API, không sửa editor |
| [AC-35] | Xem đáp án: gọi API, hiện khối "Đáp án tham khảo" có code và giải thích; "Chép vào trình soạn" có xác nhận ghi đè; "Ẩn đáp án" | [PASS] | `renderSolution` đủ thành phần; `copySolution` xác nhận khi code khác starterCode; dữ liệu mẫu có giải thích cho mọi bài |
| [AC-36] | Đã xem thì lần sau không hỏi lại (trừ confirmEveryTime); huy hiệu "Đã xem đáp án" trong danh sách | [PASS] | `progress.hasViewedSolution` → bỏ xác nhận; `badge--seen` "Đã xem đáp án" trong `renderExercises` |
| [AC-37] | Hộp thoại: role=dialog, aria-modal, focus vào Hủy khi mở, trả focus khi đóng, Tab quay vòng | [PASS] | `modal.js`: `role="dialog" aria-modal="true"`, `cancelBtn.focus()`, `returnFocusTo.focus()`, Tab xử lý vòng |
| [AC-38] | Bài không có đáp án: ẩn nút; endpoint trả 404 | [PASS] | `ex.hasSolution ? ... : null`; `GetSolutionUseCase` ném NotFound khi `!HasSolution`; unit test `Exercise_without_solution_throws_not_found` đạt |
| [AC-39] | Nội dung bài nằm trong SQL; không viết cứng trong html/js/css/cs | [PASS] | Quét `src` không thấy tiêu đề bài mẫu; `ArchitectureTests.Sample_lesson_content_is_not_hard_coded_in_source` đạt |
| [AC-40] | Thêm bài bằng script SQL, xuất hiện không cần build/restart | [PASS] | Chạy script thêm bài `bai-98-mau` (từ mẫu `them-bai-moi.sql`) trên SQL Server; server 5001 đang chạy trả ngay bài mới trong danh sách và chi tiết 200 |
| [AC-41] | Có HUONG-DAN-THEM-BAI.md ≤1 trang: cột, script mẫu, cách tạo DB, nạp mẫu, chạy app | [PASS] | File 26 dòng, có đủ các mục theo đặc tả |
| [AC-42] | Bài lỗi chỉ làm hỏng bài đó: 422 DATA_INVALID có field; bài khác và danh sách vẫn chạy | [PASS] | SQL Server với dữ liệu hỏng: Notes sai JSON → 422 field "Notes"; Title rỗng → 422 field; không có test case → 422; danh sách 200. `describeError` hiển thị "Dữ liệu bài học bị lỗi: <field>" |
| [AC-43] | Tiếng Việt có dấu, lang="vi", meta charset utf-8 | [KHÔNG KIỂM ĐƯỢC] | Lý do: hiển thị đúng trên Chrome/Edge/Firefox cần trình duyệt. Markup đã kiểm: `<html lang="vi">`, `<meta charset="utf-8">` ở cả 3 trang; API trả `charset=utf-8`, tiếng Việt hiển thị đúng trong JSON |
| [AC-44] | Trang chủ và bài học tương tác ≤2 s ở 10 Mbps; JS/CSS tự viết ≤500 KB | [KHÔNG KIỂM ĐƯỢC] | Kích thước: JS+CSS khoảng 73 KB (đạt phần dung lượng). Thời gian ≤2 s ở 10 Mbps cần đo trên trình duyệt |
| [AC-45] | Không tràn ngang ở 360/768/1280/1920 px; cỡ chữ nội dung ≥16 px | [KHÔNG KIỂM ĐƯỢC] | Lý do: không đo được tràn ngang. Lưu ý từ CSS: nội dung chính 1rem (16 px), nhưng `.hint` 0.9rem, `.status` 0.9rem, `.example__label` 0.875rem (dưới 16 px); cần xác định có tính là "nội dung" không |
| [AC-46] | Tương phản ≥4,5:1; thao tác bằng bàn phím; viền focus rõ | [KHÔNG KIỂM ĐƯỢC] | Tương phản đã tính: chữ chính 14,4:1; chữ phụ 7,0:1; primary trên trắng 6,7:1. Thao tác bàn phím và focus thực tế chưa kiểm trên trình duyệt (CSS có `:focus-visible` 3px) |
| [AC-47] | Chạy bằng `dotnet run --project src/Web`, truy cập localhost; chỉ gọi API cùng nguồn | [PASS] | `dotnet run` chạy thành công; `http://localhost:5000/` trả 200; JS chỉ gọi `/api/...`; ngoài CDN chỉ Pyodide |
| [AC-48] | Code chạy trong Worker, không gửi ra server; nội dung API được thoát/không innerHTML | [PASS] | Không có `innerHTML`/`eval` trong wwwroot (grep); mọi nội dung dùng `textContent`/`createTextNode`; code học sinh chỉ đi tới Worker. Thử `<script>` trên trình duyệt chưa chạy |
| [AC-49] | Không có lỗi JavaScript (console error) ở luồng chính trên Chrome | [KHÔNG KIỂM ĐƯỢC] | Lý do: cần trình duyệt để đọc console |
| [AC-50] | Seed có đúng 3 bài, mỗi bài đủ 4 mục không rỗng, ≥2 bài tập, ≥4 test (có biên và ẩn), đáp án | [PASS] | SQL Server seed: 3 bài (Summary 6–7, Notes 4–5, Conclusion 4), 6 bài tập, test 5–9 mỗi bài, ẩn 1–3 mỗi bài, đều có đáp án và starterCode. Test "biên" xác nhận thủ công qua nội dung test |
| [AC-51] | Solution đạt 100%; starterCode không đạt | [PASS] | Script Python chạy trên API SQL Server (44 test) và SQLite (38 test): 0 lỗi; starterCode không đạt test nào. Lưu ý: `SeedSampleTests.Solutions_pass...` sẽ bỏ qua (pass vô điều kiện) nếu máy không có Python |
| [AC-52] | Solution có đúng 4 project + 2 test project; không có Java | [PASS] | `TinHoc12.sln` liệt kê đúng 6 project; không có file `.java`/`pom.xml`/`gradle`; test `Solution_contains_exactly_the_four_projects...` đạt |
| [AC-53] | Ranh giới phụ thuộc đúng qua .csproj | [PASS] | Domain: không ProjectReference/PackageReference; Application: chỉ Domain; Infrastructure: Application + EF SqlServer; Web: Application + Infrastructure, không Domain; không tham chiếu ngược. Đã xác nhận bằng đọc csproj và `ArchitectureTests` (13 test đạt) |
| [AC-54] | Web không dùng namespace Domain; entity ở Domain, interface ở Application, DbContext ở Infrastructure | [PASS] | grep `TinHoc12.Domain` trong `src/Web` không có kết quả; vị trí file đúng; `Web_source_does_not_use_Domain_namespace` đạt |
| [AC-55] | `dotnet build src/TinHoc12.sln` 0 lỗi | [PASS] | Build thành công: 0 Warning(s), 0 Error(s), exit 0 |
| [AC-56] | `dotnet test` pass 100%; có test cho use case gồm nhánh không tìm thấy; không cần SQL Server | [PASS] | Application.Tests 44/44 (có test NotFound cho cả 3 use case); Infrastructure.Tests 13/13 dùng SQLite; không cần SQL Server để chạy test |
| [AC-57] | `GET /api/lessons` 200 JSON, sắp theo order, đúng 5 trường, không có chi tiết | [PASS] | HTTP 200 `application/json; charset=utf-8`; khóa đúng: description, exerciseCount, id, order, title; thứ tự tăng dần |
| [AC-58] | `GET /api/lessons/{id}` có đủ khóa cấp bài, bài tập và test case theo lược đồ | [PASS] | Đã kiểm khóa: lesson (id, title, summary, notes, conclusion, exercises); exercise đủ 14 khóa; testCases (input, expectedOutput, isHidden); examples (input, output, explanation) |
| [AC-59] | ID sai/rỗng/SQL injection/>100 ký tự → 404 JSON NOT_FOUND tiếng Việt; không 500, không lộ chi tiết | [PASS] | curl: `khong-co`, `%20`, `' OR 1=1--`, chuỗi 101 ký tự, `/api/lessons/`, `/api/exercises//solution`, solution sai ID → đều 404 `{"error":"NOT_FOUND",...}` |
| [AC-60] | Phản hồi `/api/lessons` và `/api/lessons/{id}` không chứa đáp án | [PASS] | Không có khóa solution/solutionCode/solutionExplanation; không có chuỗi code đáp án trong JSON chi tiết (kiểm 3 bài). Chỉ có cờ `hasSolution` (cho phép theo đặc tả) |
| [AC-61] | `/api/exercises/{id}/solution` 200 `{exerciseId, code, explanation}`; không có đáp án hoặc sai ID → 404 | [PASS] | 200 với đúng 3 khóa; sai ID 404; bài không có đáp án 404 (unit test). Không có liên kết tiền tải |
| [AC-62] | Đáp án chỉ tải sau khi bấm và xác nhận; Hủy → 0 request; xem → 1 request | [PASS] | Mã: `revealSolution` chỉ gọi từ `onSolution` sau xác nhận. Ngoài lệ AC-36: tự tải khi đã xem trước đó. Đếm request trên trình duyệt chưa thực hiện |
| [AC-63] | Frontend chỉ lấy qua `/api`; không fetch file .json; không có logic DB ở JS | [PASS] | Quét wwwroot: không có `.json` dữ liệu, chỉ `fetch` tới `/api`; `ArchitectureTests.Frontend_never_fetches_json_files...` đạt |
| [AC-64] | Lỗi hạ tầng → 503/500 JSON chung; không lộ chi tiết; ghi log; giao diện thân thiện | [PASS] | Server với chuỗi kết nối sai: 503 `{"error":"UNAVAILABLE","message":"Máy chủ dữ liệu đang bận..."}`, không lộ chuỗi kết nối; mã ghi `LogError`; giao diện ánh xạ 503 sang thông báo thân thiện |
| [AC-65] | DbContext và Fluent API ở Infrastructure; chuỗi kết nối trong appsettings; không viết cứng | [PASS] | `AppDbContext`, `*Configuration.cs`; `ConnectionStrings:DefaultConnection` trong appsettings.json; grep `Server=`/`Data Source=` trong .cs/.js (trừ appsettings) không có kết quả |
| [AC-66] | Schema ba bảng đúng cột, kiểu, NOT NULL, khóa, FK cascade, unique (LessonId,Order), (ExerciseId,Order), Lessons.Order; IsHidden bit mặc định 0 | [PASS] | INFORMATION_SCHEMA SQL Server: đúng kiểu và nullability; default bit 0; FK `ON DELETE CASCADE` x2; CHECK Difficulty; unique index trong schema.sql; test `Duplicate_*_order_is_rejected` đạt |
| [AC-67] | Migration trong src/Infrastructure/Migrations; `dotnet ef database update` trên DB trống thành công; chạy lại không đổi; không có thay đổi model chưa migrate | [PASS] | DB trống: "Applying migration 20261019021148_InitialCreate. Done."; lần 2 "already up to date"; `has-pending-model-changes`: "No changes have been made to the model" |
| [AC-68] | schema.sql chạy trên DB trống, tạo schema tương đương, chạy lại không lỗi | [PASS] | Chạy trên DB trống 2 lần: không lỗi; cột/kiểu/khóa khớp AC-66 |
| [AC-69] | seed-sample.sql nạp đúng 3 bài x 2 bài tập; idempotent; mỗi bài tập ≥4 test (có biên và ẩn), có đáp án | [PASS] | Chạy 2 lần: 3 Lessons, 6 Exercises, 38 TestCases (13 ẩn), không trùng, không lỗi. AC-50/51 đạt |
| [AC-70] | them-bai-moi.sql chạy trong một giao dịch, rollback khi lỗi; ID không trùng; kiểm bằng gọi API | [PASS] | Chạy: 4/7/41 bản ghi; chạy lại → lỗi PK, `THROW`, số bản ghi không đổi; API thấy bài mới (xem AC-40) |
| [AC-71] | ID là slug [a-z0-9-] ≤100 ký tự, duy nhất toàn hệ thống | [PASS] | Tất cả ID mẫu đúng slug; khóa chính `Exercises.Id` và `Lessons.Id` đảm bảo duy nhất; `IdRules.IsValid` kiểm slug và độ dài |
| [AC-72] | Infrastructure.Tests trên SQLite/InMemory: sắp xếp, IsHidden, null khi sai ID, xóa dây chuyền; tham số hóa | [PASS] | 13/13 đạt, gồm `Deleting_lesson_cascades_to_exercises_and_test_cases`, `Unknown_ids_return_null`, `Headers_are_sorted_by_order...`; truy vấn LINQ (tham số hóa) |
| [AC-73] | Không còn nguồn JSON; không thư mục data/; Infrastructure không đọc JSON bài học | [PASS] | Không có thư mục `data/` hay `web/data/`; không có file .json dữ liệu; `ArchitectureTests.Infrastructure_does_not_read_json_files` và `Frontend_has_no_json_data_files...` đạt |

## Thống kê
- Tổng: 73 tiêu chí.
- [PASS]: 62.
- [FAIL]: 0.
- [KHÔNG KIỂM ĐƯỢC]: 11 (AC-11, AC-12, AC-18, AC-19, AC-22, AC-23, AC-43, AC-44, AC-45, AC-46, AC-49).

## Ghi chú và điểm cần lưu ý (không phải FAIL)
1. `SeedSampleTests.Solutions_pass_all_tests_and_starter_code_fails_when_python_is_available` trả về sớm (xem như pass) khi máy không có Python. Nên đổi thành Skip rõ ràng hoặc bắt buộc Python trong môi trường CI.
2. CSS dùng cỡ chữ 0,875–0,9 rem cho một số nhãn/gợi ý (`.hint`, `.status`, `.example__label`). Nếu Khách hàng coi đó là "nội dung" thì AC-45 chưa đạt; cần xác nhận.
3. Trạng thái "Đang chờ" hiển thị trước khi có kết quả từng test; đây là trạng thái tạm, không phải kết quả cuối.
4. File `src/Web/tinhoc12.dev.db` được tạo khi chạy chế độ SQLite dev. Nên đảm bảo nằm trong `.gitignore`.

## Các mục cần kiểm trên trình duyệt thật (để đóng 11 mục KHÔNG KIỂM ĐƯỢC)
- Mở `http://localhost:5000/` bằng Chrome/Edge/Firefox ở 360, 768, 1280, 1920 px: kiểm bố cục hai cột, không cuộn ngang, nút chạm.
- Mở một bài tập: đo thời gian Pyodide sẵn sàng (AC-18), chạy `print("Xin chào")` (AC-19), chạy `while True: pass` (AC-22), in >10 000 ký tự (AC-23).
- Mở Console và Network trong luồng chính (AC-49, AC-62 nếu cần), kiểm tỉ lệ tương phản và điều hướng bằng bàn phím (AC-46).

## TRẠNG THÁI
Không có tiêu chí [FAIL]. Tuy nhiên 11 tiêu chí [KHÔNG KIỂM ĐƯỢC] nên chưa đạt 100% [PASS]; theo quy tắc, **chưa thể kết luận ĐẠT CHUẨN**. Cần kiểm trên trình duyệt thật cho các mục nêu trên trước khi chốt. Không có lỗi nào cần chuyển ngược cho `@dev-engineer` ở thời điểm này, ngoài các ghi chú mục 1, 2 nêu trên để Dev/BA xem xét.

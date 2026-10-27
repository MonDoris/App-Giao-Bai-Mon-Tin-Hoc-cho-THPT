# Đặc tả Tính năng: Landing page công khai, App shell + Dashboard, và Trình soạn bài dễ hiểu cho giáo viên (Phiên bản 5)

> **Quan hệ với tài liệu cũ.** Đây là phần bổ sung (phiên bản 5) của `docs/specs/thiet-ke-lai-giao-dien.md` (v4, AC-161..AC-318, QD-41..QD-61), `docs/specs/dang-nhap-phan-quyen.md` (v3) và `docs/specs/trang-hoc-tin-hoc-lop-12.md` (v2). Đã grep `docs/`: mã AC lớn nhất là **AC-318** và QD lớn nhất là **QD-61**, nên tài liệu này dùng **AC-319 … AC-476** và **QD-62 … QD-86**. Khi mâu thuẫn, **v5 thắng v4**; các điểm mâu thuẫn được ghi ở **mục 11 (Nhật ký thay đổi)**. Giữ nguyên: ngôn ngữ thị giác "Sổ tay lập trình" (navy `#12305a` + xanh logo `#38a9e0`, Be Vietnam Pro + JetBrains Mono, token sáng/tối v4, hệ bo góc QD-61, QD-60 chữ ≥ 16 px), HTML/CSS/JS thuần, không thư viện ngoài, không `innerHTML`.

## 0. Bối cảnh, hiện trạng & Quyết định mặc định

**Khách hàng:** Giáo viên Tin học lớp 12.
**Phản hồi về v4 (đã code xong):** "Chỉnh lại trang web theo tiêu chuẩn có LANDING PAGE và DASHBOARD. Làm UI chuẩn hiện đại. Chỗ SOẠN BÀI của giáo viên chỉnh lại cho dễ hiểu, trình bày như hiện tại thì không hiểu tí nào."

### 0.1 Hiện trạng khảo sát mã nguồn

**Định tuyến và trang đích**
- `Program.cs`: thứ tự middleware `PageGuardMiddleware` rồi `UseDefaultFiles()` rồi `UseStaticFiles()`. `PageGuardMiddleware` coi `/` và mọi đường dẫn kết thúc `/` hoặc `.html` là trang HTML: chưa đăng nhập thì 302 tới `/login.html?returnUrl=...`. Hệ quả: **`/` hiện luôn bị đẩy sang đăng nhập, không có trang giới thiệu công khai**.
- `ReturnUrlPolicy`: `StudentHome = "/index.html"`, `TeacherHome = "/teacher/index.html"`; `Sanitize` loại `returnUrl` là `/`, `/api/*`, `/login.html*`. `PageGuard` đẩy giáo viên mở `/assignments.html` về `TeacherHome`, đẩy học sinh vào `/teacher/*` về `/index.html?notice=forbidden`. `auth.js` và `api.js (homeFor)` lặp lại các đích này ở phía trình duyệt.
- Test đang khóa hành vi này: `PageAndRoleTests` (`Root_redirects_to_plain_login…`, redirect theo role, các `Location` là `/index.html` và `/teacher/index.html`), `AuthApiTests` (`redirectUrl`), `AuthTests` (`RedirectUrl`), `FrontendRulesTests` (danh sách 12 HTML và `<title>`).

**Điều hướng**
- `header.js` dựng thanh viên thuốc nổi (`.site-header`): liên kết ngang + cụm người dùng; học sinh dùng menu dưới 768 px, giáo viên dưới 1360 px (QD-50) vì 5 nhãn không vừa một dòng. Không có tổng quan; trang đích sau đăng nhập là danh sách bài (học sinh) hoặc danh sách bài học (giáo viên).

**Dữ liệu sẵn có cho dashboard (không cần API mới)**
| Nguồn | Trường dùng được |
|---|---|
| `GET /api/lessons` (HS) | `id, order, title, description, exerciseCount, passedCount` |
| `GET /api/me/assignments` (HS) | `assignmentId, exerciseId, exerciseTitle, lessonId, lessonTitle, difficulty, dueAt, note, status (NotStarted/InProgress/Passed), isOverdue, bestPassedTests, totalTests, attempts, lastSubmittedAt, passedLate, hasViewedSolution` |
| `GET /api/teacher/lessons?includeArchived=` (GV) | `id, order, title, description, isPublished, isArchived, exerciseCount, publishedExerciseCount, assignmentCount, version, updatedAt, createdByName` |
| `GET /api/teacher/assignments` (GV) | `id, exerciseId, exerciseTitle, lessonId, lessonTitle, assignedToAll, classNames, dueAt, createdAt, cancelledAt, assignedStudents, submittedStudents, passedStudents` |
| `GET /api/teacher/students?pageSize=` (GV) | `total` (và danh sách) |
| `GET /api/teacher/progress` (GV) | ma trận học sinh x bài tập của MỘT bài học (nặng; không dùng cho dashboard) |
| **Không có** | luồng "bài nộp mới nhất" toàn lớp; lịch sử tiến độ theo ngày. Xem QD-70 |

**Trình soạn bài hiện tại: các điểm khó hiểu (đã đọc `teacher/lesson-edit.js`, `exercise-edit.js`, `blocks.js`, `index.js`)**

| # | Vị trí | Vấn đề cụ thể |
|---|---|---|
| 1 | `lesson-edit.js` | Một biểu mẫu dài trên cùng một trang, không có "bước", không nói "làm gì trước, làm gì sau". |
| 2 | `lesson-edit.js` | Hướng dẫn ký hiệu là một câu dài (`dòng bắt đầu bằng "- " là gạch đầu dòng; dùng \`mã\` (dấu huyền)...; khối bắt đầu bằng \`\`\` là khối mã`): giáo viên phải **tự gõ** `- `, dấu huyền, ba dấu huyền. |
| 3 | `lesson-edit.js` | Cùng câu hướng dẫn ấy lặp cho "Tóm tắt" và "Tổng kết" nhưng "Lưu ý" lại khác (mọi khối tự thành gạch đầu dòng, `forceList`); hai quy tắc khác nhau, không giải thích. |
| 4 | `blocks.js` | Ô nhập khối dùng **phông mã** (`textarea--code`) cho cả văn xuôi; thuật ngữ "Khối" không giải thích; chỉ có nút `↑ Lên`, `↓ Xuống`, `Xóa`, `+ Thêm khối` (ký hiệu mũi tên, dấu cộng thay cho biểu tượng + chữ); xóa khối **không hỏi, không hoàn tác**. |
| 5 | `lesson-edit.js` | Khối trống bị báo lỗi `Tóm tắt (khối 2) không được để trống.`: giáo viên phải tự đi xóa khối trống. |
| 6 | `lesson-edit.js` | "Mã bài học (tùy chọn)" với gợi ý `chữ thường không dấu, số và dấu gạch ngang; không đổi được sau khi tạo` nằm trong mục "Nâng cao"; thuật ngữ "mã" gây nhầm với mã lập trình. |
| 7 | `lesson-edit.js` | Phần "Xem trước nội dung" nằm **cuối trang**, dưới cả biểu mẫu, chỉ gồm 3 mục "Tóm tắt/Lưu ý/Tổng kết": **không giống** trang học sinh (thiếu mô tả, thiếu bài tập, tiêu đề mục khác). Nút "Xem trước (mở trang học sinh)" mở tab mới và chỉ thấy **bản đã lưu**, không phải bản đang gõ. |
| 8 | `lesson-edit.js` | Danh sách bài tập chỉ hiện sau khi lưu, nằm **dưới cùng** trang, không có lời giải thích vì sao bài mới chưa thêm được bài tập. Mỗi dòng có 8 nút ngang hàng (`Sửa`, `Xem trước`, `Xuất bản/Ẩn`, `Lưu trữ/Khôi phục`, `Xóa`, `↑ Lên`, `↓ Xuống`) và dòng thống kê `n test case (m ẩn) · n lượt giao · n bài nộp`. |
| 9 | `lesson-edit.js` | Nút "Xuất bản" và "Ẩn (về bản nháp)": "Ẩn" nghe như xóa hiển thị, "bản nháp" không được định nghĩa; **không có danh sách kiểm tra** việc đã đủ nội dung để xuất bản chưa; nút Xuất bản **không hiện** khi tạo mới. |
| 10 | `lesson-edit.js` | Nút "Lưu bài học" nằm trong cột phụ (cuối trang ở mobile), **không dính**; lỗi 422 chỉ có câu chung `Có trường chưa hợp lệ, hãy xem các ô được đánh dấu` ở vùng `#status` có thể đã cuộn khỏi màn hình; không có tổng hợp lỗi; không báo "đã lưu lúc nào"; không có Ctrl+S. |
| 11 | `lesson-edit.js` | Hộp thoại xung đột: `Bài học đã được người khác chỉnh sửa` với hai nút `Tải lại (bỏ thay đổi của tôi)` / `Giữ bản của tôi`; chọn "Giữ" thì **phải bấm Lưu lần nữa** nhưng hộp thoại không nói; thuật ngữ "phiên bản" không có. |
| 12 | `exercise-edit.js` | Thứ tự trường phản logic: Tên, Đề, Định dạng vào, Định dạng ra, Ràng buộc, Ví dụ, **Mã khởi tạo, Đáp án, Giải thích, rồi mới Bộ test**; "Độ khó", "Giới hạn thời gian", "Luôn hỏi xác nhận" nằm ở cột phụ tách khỏi nội dung. |
| 13 | `exercise-edit.js` | Thuật ngữ: `Input`, `Output`, `Bộ test case (3–50)`, `Test ẩn`, `Đầu ra mong đợi`, `Mã khởi tạo`, `Đáp án (mã Python) – tùy chọn`, `Giới hạn thời gian mỗi test (mili giây, 500–5000)`. Không giải thích "test ẩn" là gì ngoài một câu hint dài. |
| 14 | `exercise-edit.js` | Ví dụ mẫu và test case đều là các ô `textarea` 3 dòng phông mã xếp lưới; không phân biệt "ví dụ cho học sinh xem" và "bộ kiểm tra chấm bài"; không có cách thêm nhanh ví dụ thành test. |
| 15 | `exercise-edit.js` | Cảnh báo chất lượng test gộp thành **một đoạn dài** (`messages.join(' ')`); nút "Kiểm tra đáp án" ở cột phụ nhưng kết quả ở **cuối trang**; thông báo kết quả `n/m test đạt – chưa chặn việc lưu, nhưng nên...` dùng dấu gạch và thuật ngữ "test". |
| 16 | `exercise-edit.js` | Hint đề bài "Văn bản thuần. Có thể dùng \`mã\` và dòng bắt đầu bằng "- "..." là quy tắc **thứ ba** (`renderText`, cách đoạn bằng dòng trống) khác với bài học. |
| 17 | `teacher/index.js` | Mỗi bài học là một thẻ có **8 nút bằng nhau** (`Sửa, Xem trước, Giao bài, Xuất bản/Ẩn (về bản nháp), Lưu trữ/Khôi phục, Xóa, ↑ Lên, ↓ Xuống`); khó quét, dễ bấm nhầm hành động phá hủy. |
| 18 | `teacher/index.js` | Bộ lọc là `select` mặc định `Đang dùng (không gồm lưu trữ)` (thuật ngữ khó); dòng thông tin `n bài tập (m đã xuất bản) · n lượt giao · Tạo bởi ... · Cập nhật ...` quá dày; "Tạo bài học" là nút thường ở góc; trạng thái rỗng chỉ một câu `Bấm "Tạo bài học" để bắt đầu soạn bài`. |
| 19 | Chung | Ô nhập dùng placeholder/hint thay cho giải thích; không có ví dụ minh họa nào; không có "Điền mẫu"; không có chỉ báo hoàn thành từng phần. |

### 0.2 Phạm vi: tệp bị ảnh hưởng
| Nhóm | Tệp | Thay đổi |
|---|---|---|
| HTML mới | `wwwroot/landing.html`, `wwwroot/dashboard.html`, `wwwroot/teacher/dashboard.html` | Mới (15 trang HTML sau thay đổi) |
| HTML sửa | 12 trang cũ trừ `login.html`: thêm `data-shell` trên `<body>` và `<aside class="app-sidebar" id="site-menu">` rỗng để giữ chỗ (tránh CLS) | Sửa nhỏ |
| JS mới | `js/landing.js`, `js/dashboard.js`, `js/teacher/dashboard.js`, `js/charts.js` (hàm dựng vòng, thanh chồng, thanh ngang bằng SVG/CSS), `js/search.js` (tìm nhanh), `js/teacher/editor-kit.js` (thanh bước, thanh công cụ, thanh lưu dính, danh sách kiểm tra, bảng thuật ngữ), `js/teacher/samples.js` (văn bản mẫu) | Mới |
| JS sửa | `header.js` (viết lại thành app shell), `auth.js`, `api.js` (`homeFor`), `index.js` (bỏ khối dashboard, chuyển sang `dashboard.js`), `toast.js` (nút hành động "Hoàn tác"), `teacher/{lesson-edit,exercise-edit,blocks,index,common}.js`, `theme-init.js` (đọc thêm khóa thanh bên) | Sửa |
| CSS | `css/style.css` | Thêm lớp component/page cho landing, shell, dashboard, editor; giữ token v4 |
| Máy chủ | `Middleware/PageGuardMiddleware.cs`, `Application/Security/ReturnUrlPolicy.cs`, `Program.cs` (`UseDefaultFiles`) | Sửa (QD-62, QD-63) |
| Test | Xem mục 8 | Khách hàng/Dev tự cập nhật |

### 0.3 Bất biến (KHÔNG đổi)
- Toàn bộ **mục 0.3 của v4** (id, class, chuỗi cố định, tên trường biểu mẫu), **trừ** các điểm ghi ở mục 11. Cụ thể được giữ: nhãn "Bài học", "Bài tập của tôi", "Bài học của tôi", "Giao bài", "Tiến độ", "Học sinh", "Xem như học sinh (xem trước)", "Đổi mật khẩu", "Đăng xuất", "Giáo viên"; `aria-label="DK Edu – về trang chủ"`; id `site-header`, `site-menu`, `site-nav`, `btn-logout`, `status`, `content`, `page-title`, `list`, `btn-save`, `btn-publish`, `btn-check`, `title`, `description`, `lesson-id`, `summary`, `notes`, `conclusion`, `statement`, `inputFormat`, `outputFormat`, `constraints`, `starterCode`, `solutionCode`, `solutionExplanation`, `timeLimitMs`, `exercise-id`, `confirmEveryTime`, `examples`, `testCases`, `search`, `filter`; class `btn*`, `card`, `chip*`, `badge*`, `banner*`, `lesson-card*`, `lesson-grid`, `lesson-row*`, `block-list`, `block-item*`, `testcase-grid`, `testitem*`, `status--*`, `stateBox` (`state*`), `preview-banner`, `skip-link`, `sr-only`, `hidden`.
- **API, mô hình dữ liệu, quy tắc nghiệp vụ, thuật toán chấm, version/xung đột, nháp `localStorage` (`formDraft`, khóa `lesson:<id|new>` và `exercise:<id|new:<lessonId>>`), cảnh báo bài đã giao, `guardUnsaved`:** không đổi. **Parser hiển thị** (`renderBlocks`, `renderText`, `inlineRich` trong `dom.js`) **không đổi** (QD-71).
- Thiết kế mới chỉ **thêm cách nhập** (thanh công cụ, loại khối, mẫu); dữ liệu gửi lên máy chủ phải **giống hệt** định dạng cũ (AC-418).

### 0.4 Quyết định mặc định (BA đã chốt, Khách hàng có thể điều chỉnh)

| Mã | Quyết định |
|---|---|
| QD-62 | **Địa chỉ gốc `/` và `/landing.html`.** Tệp landing mới là `wwwroot/landing.html`. Chưa đăng nhập: `GET/HEAD /` trả **200** nội dung landing (không chuyển hướng). Đã đăng nhập: `/` và `/landing.html` đều **302** tới trang chủ theo role (đã `mustChangePassword` thì tới `/change-password.html`), cùng cách login.html xử lý hiện nay. **Quyết định:** người đã đăng nhập không xem landing (đơn giản, nhất quán; muốn xem lại landing phải đăng xuất). Cách sửa: (a) `Program.cs`: `UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = { "landing.html", "index.html" } })` sau khi `Clear()` danh sách mặc định, nên `/` ra `landing.html` còn `/teacher/` vẫn ra `teacher/index.html`; (b) `PageGuardMiddleware`: thêm nhánh cho path đúng bằng `/` hoặc `/landing.html`: chưa đăng nhập thì `_next` (kèm `Cache-Control: no-cache` và `Vary: Cookie`), đã đăng nhập thì 302 như trên; mọi đường dẫn HTML khác **giữ nguyên** hành vi bảo vệ. |
| QD-63 | **Trang đích sau đăng nhập.** `ReturnUrlPolicy.StudentHome = "/dashboard.html"`, `TeacherHome = "/teacher/dashboard.html"`. `/index.html` **vẫn là trang "Bài học"** (danh sách bài học của học sinh, và là đích "Xem như học sinh (xem trước)" của giáo viên); `/teacher/index.html` **vẫn là "Bài học của tôi"**. Giáo viên mở `/dashboard.html` bị 302 về `TeacherHome` (như `/assignments.html`); học sinh vào `/teacher/*` bị 302 tới `/dashboard.html?notice=forbidden`. `Sanitize` thêm: loại `/landing.html*`; với giáo viên loại thêm `/dashboard.html`. `auth.js` và `api.js` đổi theo. |
| QD-64 | **Hai dashboard là trang mới**, mục điều hướng đầu tiên của sidebar tên **"Tổng quan"**. Khối "bảng điều khiển" (lời chào, vòng tiến độ, "Tiếp tục học") của `index.js` (v4 QD-51) **chuyển** sang `dashboard.js`, giữ nguyên công thức và chuỗi (mục 2.8 v4). `index.html` chỉ còn tiêu đề, lời dẫn và lưới bài học (`#lessons`), giữ `<title>` "Danh sách bài học – DK Edu". |
| QD-65 | **App shell = sidebar trái + topbar** thay thanh viên thuốc nổi (QD-48, QD-49, QD-50 bị thay thế). Cấu trúc: `<aside id="site-menu" class="app-sidebar">` chứa logo và `<nav id="site-nav">` có nhóm mục; `<header id="site-header" class="site-header">` là topbar (dính trên); `<main id="main">`. Ngưỡng: **≥ 900 px** sidebar cố định (264 px) hoặc rail (72 px); **< 900 px** sidebar là ngăn kéo (drawer) mở bằng nút "Menu" ở topbar. **Không** làm thanh tab dưới (quyết định bỏ để tránh hai kiểu điều hướng song song). |
| QD-66 | **Trạng thái thu gọn sidebar** lưu ở `localStorage["th12.sidebar"]` = `collapsed` hoặc `expanded` (không gắn `userId`, không chứa dữ liệu nhạy cảm, mọi truy cập bọc `try/catch`); `theme-init.js` (vẫn tệp ngoài, nạp đồng bộ, **nâng trần từ 1 KB lên 1,5 KB**) đặt `data-sidebar` trên `<html>` trước khi vẽ. Chỉ `storage.js` và `theme-init.js` được đụng `localStorage` (AC-311 giữ). |
| QD-67 | **Tìm nhanh** (nút trên topbar + Ctrl+K) làm được **không cần API mới**: khi mở lần đầu mới gọi API sẵn có (`GET /api/lessons` cho học sinh, `GET /api/teacher/lessons` cho giáo viên) để lấy tên bài học; luôn có sẵn các đích điều hướng. Không gọi gì khi chưa mở. |
| QD-68 | **KPI chỉ tính từ API sẵn có**, công thức ở từng AC (AC-357 đến AC-362 và AC-368 đến AC-372). Quy ước: "hôm nay" là giờ máy khách; `dueAt` là ISO UTC, hiển thị bằng `formatDateTime` (`dd/MM/yyyy HH:mm`, JetBrains Mono, QD-59); "bài tập" tính **một lần theo `exerciseId`** (nếu có nhiều lượt giao cho cùng bài tập: coi là `Passed` nếu có lượt `Passed`, ngược lại lấy lượt có `dueAt` sớm nhất). |
| QD-69 | **Không thêm endpoint mới cho dashboard.** Học sinh: 2 yêu cầu `GET /api/lessons`, `GET /api/me/assignments`. Giáo viên: 3 yêu cầu `GET /api/teacher/lessons?includeArchived=false`, `GET /api/teacher/assignments`, `GET /api/teacher/students?page=1&pageSize=1` (chỉ đọc `total`; Dev kiểm tra máy chủ chấp nhận `pageSize=1`, nếu không thì dùng `pageSize` nhỏ nhất được chấp nhận). Thêm vào các yêu cầu hiện có của `initPage` (`GET /api/auth/me`). |
| QD-70 | **"Hoạt động gần đây" của giáo viên không có nguồn "bài nộp mới nhất"** (không API nào trả luồng bài nộp toàn lớp). Quyết định mặc định: hiển thị hai danh sách có thật từ dữ liệu sẵn có: **"Giao bài gần đây"** (assignments theo `createdAt` giảm dần, 5 mục) và **"Bài học sửa gần đây"** (theo `updatedAt`, 5 mục). **Endpoint tối thiểu đề xuất, CHƯA nằm trong phạm vi**, chỉ làm khi Khách hàng duyệt: `GET /api/teacher/activity?limit=10` trả `[{ submittedAt, studentName, className, exerciseId, exerciseTitle, lessonId, passedTests, totalTests, isPassed, isLate }]` (GV only, sắp theo `submittedAt` giảm dần). |
| QD-71 | **Không đổi parser.** Thanh công cụ chỉ **chèn đúng cú pháp hiện có**. Không có nút "In đậm" vì `inlineRich` không hỗ trợ (chỉ hỗ trợ \`mã\`). Loại khối trong giao diện **suy ra từ nội dung**: bắt đầu bằng ``` là **Đoạn mã**, bắt đầu bằng `- ` là **Mục danh sách**, còn lại là **Đoạn văn**. Trong ô nhập, các ký hiệu (`- `, hàng rào ```) **được ẩn** và hệ thống thêm lại khi lưu, nên dữ liệu gửi đi vẫn là chuỗi cũ (AC-394, AC-418). Một mục danh sách = một phần tử mảng (vì `renderBlocks` coi mỗi phần tử bắt đầu bằng `- ` là một `li`; một phần tử nhiều dòng `- ` sẽ thành MỘT gạch đầu dòng chứa chữ `- ` thừa). |
| QD-72 | **Thanh bước không phải trình hướng dẫn chặn.** Người dùng bấm bất kỳ bước nào bất kỳ lúc nào; chỉ có **một** nút Lưu và **một** yêu cầu `POST/PUT` như cũ. Chỉ báo hoàn thành là gợi ý, không khóa bước sau. |
| QD-73 | **Bố cục soạn bài:** ≥ 1100 px hai cột (biểu mẫu trái, xem trước phải, dính); < 1100 px một cột + tab "Soạn" / "Xem trước". Trình soạn **bài tập** (`exercise-edit.html`) vẫn là trang riêng. |
| QD-74 | **Bảng thuật ngữ thay đổi** (mục 2.10) là nguồn chuỗi duy nhất cho khu soạn bài; trạng thái bài giữ nhãn cũ `Bản nháp`, `Đã xuất bản`, `Lưu trữ` (chip hiện có). "Lưu tạm" (tự lưu `localStorage`) và "Lưu bài học" (lên máy chủ) là **hai khái niệm tách bạch** để không lẫn với "Bản nháp". |
| QD-75 | **"Điền mẫu"** chỉ điền vào ô trên màn hình (không gọi API, không lưu), có xác nhận nếu ô đang có chữ và có "Hoàn tác điền mẫu". Văn bản mẫu viết chung chung, **không chứa 5 tiêu đề bài mẫu** mà `ArchitectureTests.Sample_lesson_content_is_not_hard_coded_in_source` cấm (`Tổng hai số`, `Diện tích hình chữ nhật`, `Xếp loại học lực`, `Đếm ước của n`, `Chẵn hay lẻ`). |
| QD-76 | **Danh sách kiểm tra trước khi xuất bản không chặn.** Nghiệp vụ cũ không chặn nút xuất bản ở phía giao diện; giữ vậy. Nếu còn mục thiếu thì hiện hộp thoại xác nhận "Vẫn xuất bản?" (không phải chặn). Nếu máy chủ trả lỗi (422/409) khi xuất bản thì hiển thị đúng thông báo đó. Dev kiểm tra `LessonAuthoringUseCase.SetPublishedAsync` xem có điều kiện chặn nào và ghi lại (Dev báo, không tự thêm quy tắc). |
| QD-77 | **Phím tắt Ctrl+S** (Cmd+S trên macOS) trong trình soạn bài học và bài tập: gọi cùng hàm với nút Lưu, `preventDefault()` để không mở hộp thoại lưu trang. Không phím tắt nào khác ngoài Ctrl+K (tìm nhanh) và các phím trong thành phần (mũi tên trong tab, Esc đóng). |
| QD-78 | **Bài học mới chưa lưu thì chưa thêm bài tập được** (API cũ cần `lessonId` đã tồn tại). Bước 3 giải thích rõ và có nút "Lưu bài học và thêm bài tập" (lưu rồi tới bước 3 của trang sửa, tham số `step=3`). |
| QD-79 | **Danh sách bài học giáo viên:** các tab lọc `Tất cả` (gồm cả bài lưu trữ, bài lưu trữ làm mờ và có chip), `Bản nháp`, `Đã xuất bản`, `Lưu trữ`; mặc định mở tab `Tất cả`. (Khác mặc định cũ "không gồm lưu trữ": cần Khách hàng xác nhận, mục 9.) |
| QD-80 | **Biểu đồ:** chỉ SVG/CSS tự viết trong `js/charts.js` (QD-46 v4: chỉ animate `transform`, `opacity`, `stroke-dashoffset`); màu lấy từ token; thông tin luôn có **chữ và số** đi kèm, màu không là kênh duy nhất. |
| QD-81 | **Gõ chữ động trong hero** là ngoại lệ có kiểm soát của QD-47: một chuỗi `requestAnimationFrame` hữu hạn (≤ 4 giây, một lần), bắt đầu khi hero vào khung nhìn, hủy khi `prefers-reduced-motion`/`pagehide`/tab ẩn; không `setInterval`, không lặp vô hạn. Bản đầy đủ của mã luôn có trong DOM cho trình đọc màn hình. |
| QD-82 | **Nội dung landing chỉ nêu sự thật về sản phẩm** (mục 2.8): không bịa số lượng người dùng, đánh giá, logo trường/khách hàng, ảnh người thật, mốc thời gian. |
| QD-83 | **Landing: tab vai trò.** Mục "Dành cho học sinh / giáo viên" dùng tab (không dùng hai nửa song song) vì có 4 lợi ích mỗi bên và cần gọn ở mobile. Khi JS lỗi, cả hai bảng nội dung đều hiện (cải tiến dần). |
| QD-84 | **View Transition tới login:** dùng chung `view-transition-name: brand-mark` cho logo ở thanh điều hướng landing và logo ở `login.html`; trình duyệt không hỗ trợ thì điều hướng thường. |
| QD-85 | **SEO landing:** `title`, `meta description`, Open Graph (`og:title`, `og:description`, `og:type=website`, `og:locale=vi_VN`, `og:image` trỏ `/img/logo-dk-edu@2x.png` là PNG sẵn có); không `canonical`/`og:url` tuyệt đối vì chưa biết tên miền (Khách hàng cung cấp thì bổ sung). Chỉ một `h1`. |
| QD-86 | **Số dòng và ngưỡng 500 KB:** tổng `*.js`+`*.css` tự viết phải vẫn ≤ 500 KB (AC-299). Dev **đo trước khi code** (BA chưa đo kích thước hiện tại); nếu v4 đã gần trần thì phải lược bớt, ưu tiên bỏ tìm nhanh (AC-352) trước. |

### 0.5 Giả định cần Khách hàng xác nhận
Xem **mục 9**.

### 0.6 Ngoài phạm vi
Đăng ký tự do, quên mật khẩu tự phục vụ, thư viện biểu đồ, thanh tab dưới, bảng xếp hạng, thông báo đẩy, trình soạn WYSIWYG/Markdown đầy đủ, nút "In đậm" (parser không hỗ trợ), đếm ngược hạn nộp, luồng bài nộp toàn lớp (cần endpoint QD-70), nhiều ngôn ngữ, chia sẻ bài học giữa giáo viên.

---

## 1. Mục tiêu & User Story

- Là một **người chưa đăng nhập** (phụ huynh, học sinh mới, giáo viên khác), tôi muốn **mở địa chỉ gốc thấy ngay trang giới thiệu cho biết DK Edu làm được gì và đăng nhập ở đâu** để **hiểu sản phẩm mà không phải có tài khoản trước**.
- Là một **học sinh**, tôi muốn **một trang Tổng quan cho biết tiến độ, bài sắp đến hạn, bài quá hạn và bài nên học tiếp** để **biết ngay việc cần làm hôm nay**.
- Là một **giáo viên**, tôi muốn **một trang Tổng quan cho biết bao nhiêu bài còn là bản nháp, bài giao nào sắp hết hạn, lớp đang làm đến đâu** để **ra quyết định nhanh mà không phải mở từng trang**.
- Là một **giáo viên không rành công nghệ**, tôi muốn **soạn bài theo từng bước bằng lời thường, có nút chèn định dạng, có ví dụ mẫu và có khung xem trước giống hệt trang học sinh** để **soạn xong một bài mà không phải nhớ ký hiệu hay hỏi ai**.
- Là một **giáo viên**, tôi muốn **danh sách bài học dễ quét, nút chính rõ ràng, hành động nguy hiểm được gom vào menu** để **không bấm nhầm Xóa hay Lưu trữ**.
- Là **người dùng bàn phím, cận thị hoặc giảm chuyển động**, tôi muốn **mọi thành phần mới thao tác được bằng bàn phím, đủ tương phản, tắt được chuyển động** để **dùng thoải mái như mọi người**.

---

## 2. Luồng nghiệp vụ (Workflow)

### 2.1 Khách vãng lai mở `/`
- Bước 1: Mở `/` (chưa đăng nhập) thấy landing; thanh điều hướng dính với "Đăng nhập" là nút duy nhất kiểu CTA.
- Bước 2: Hero: tiêu đề, mô tả, "Đăng nhập" (chính), "Xem cách hoạt động" (phụ, cuộn mượt tới mục). Khung trình soạn mã bên phải gõ mã rồi hiện kết quả (một lần).
- Bước 3: Cuộn: dải điều đúng sự thật, tab vai trò, timeline 3 bước vẽ đường nối theo cuộn, bento tính năng, câu hỏi thường gặp, CTA cuối, footer.
- Bước 4: Bấm "Đăng nhập" tới `/login.html` (chuyển cảnh logo). Đã đăng nhập mà mở `/` bị đưa về Tổng quan theo role.

### 2.2 Sau đăng nhập
- Học sinh vào `/dashboard.html`, giáo viên vào `/teacher/dashboard.html`. Sidebar trái chia nhóm mục, topbar có tiêu đề/breadcrumb, tìm nhanh, đổi giao diện, menu người dùng.

### 2.3 Học sinh dùng Tổng quan
- Bước 1: Skeleton; Bước 2: 4 thẻ KPI + "Tiếp tục học" + "Hạn nộp sắp tới" + lưới bài học (tối đa 6) ; Bước 3: bấm thẻ vào bài học/bài tập.

### 2.4 Giáo viên dùng Tổng quan
- Bước 1: Skeleton; Bước 2: KPI, "Việc cần làm", biểu đồ tiến độ theo bài học, "Hoạt động gần đây", lối tắt "Tạo bài học" / "Giao bài" / "Xem tiến độ".

### 2.5 Giáo viên soạn bài học (từng bước)
- Bước 1 "Thông tin bài": tên, mô tả (và mã bài học tùy chọn ở mục nâng cao). Bước 2 "Nội dung bài": ba phần Tóm tắt, Lưu ý, Tổng kết, mỗi phần là danh sách khối có thanh công cụ. Bước 3 "Bài tập": danh sách bài tập (cần lưu bài học trước). Bước 4 "Xem lại và xuất bản": danh sách kiểm tra, nút xuất bản.
- Khung xem trước bên phải cập nhật khi gõ. Thanh lưu dính: trạng thái lưu, nút "Lưu bài học", Ctrl+S.

### 2.6 Giáo viên soạn bài tập (từng bước)
- Bước 1 Đề bài, Bước 2 Ví dụ, Bước 3 Bộ kiểm tra chấm bài (có "Chạy thử với đáp án"), Bước 4 Đáp án và mã khởi tạo, Bước 5 Nâng cao và xuất bản (cấu hình nâng cao thu gọn mặc định).

### 2.7 Giáo viên quản lý danh sách bài học
- Mở "Bài học của tôi": thấy tab lọc, nút "Tạo bài học" nổi bật; mỗi bài có nút chính "Soạn", "Xem trước", "Giao bài" và menu "Thêm". Danh sách rỗng hiện hướng dẫn 3 bước.

### 2.8 Nội dung landing (chuỗi do BA đề xuất, Khách hàng được sửa chữ; không em-dash, không emoji)

| Vị trí | Nội dung |
|---|---|
| `<title>` | `Học Tin học 12 với Python – DK Edu` |
| Meta description (110-160 ký tự) | `DK Edu giúp học sinh lớp 12 đọc bài, viết mã Python và nhận kết quả chấm ngay trên trình duyệt; giáo viên soạn bài, giao bài và theo dõi tiến độ cả lớp.` |
| Nav | Liên kết neo: `Dành cho ai` (#vai-tro), `Cách hoạt động` (#cach-hoat-dong), `Tính năng` (#tinh-nang), `Câu hỏi` (#hoi-dap); nút `Đăng nhập` |
| H1 (tối đa 2 dòng) | `Học Tin học 12 bằng Python, viết mã là chạy ngay` |
| Mô tả hero (18 từ) | `Đọc bài, viết mã Python và nhận kết quả chấm ngay trên trình duyệt, không cần cài đặt.` |
| CTA | Chính `Đăng nhập`; phụ `Xem cách hoạt động` |
| Mã mẫu trong khung hero (tên tệp `main.py`) | `n = int(input())` / `tong = 0` / `for i in range(1, n + 1):` / `    tong += i * i` / `print(tong)`; dữ liệu vào `5`; kết quả `55`; ba dòng kiểm tra `Bộ kiểm tra 1`, `Bộ kiểm tra 2`, `Bộ kiểm tra 3` đều `Đạt`. Có nhãn nhỏ `Ví dụ minh họa` |
| Dải điều đúng sự thật (4 ô, biểu tượng + chữ) | `Python chạy ngay trên trình duyệt` / `Chấm theo bộ kiểm tra của giáo viên` / `Không cần cài đặt phần mềm` / `Giao diện sáng và tối` |
| Tab học sinh (4 lợi ích) | Đọc bài theo 4 phần rõ ràng: tóm tắt, lưu ý, bài tập, tổng kết. Viết và chạy mã Python ngay, xem kết quả từng bộ kiểm tra. Xem hạn nộp và bài còn thiếu ở trang Tổng quan. Đã đạt bài nào, còn bài nào, thấy ngay trên thanh tiến độ. |
| Tab giáo viên (4 lợi ích) | Soạn bài từng bước, có khung xem trước giống hệt trang học sinh. Tạo bài tập với ví dụ và bộ kiểm tra, chạy thử bằng đáp án mẫu. Giao bài cho cả lớp hoặc từng lớp, đặt hạn nộp. Theo dõi ai đã làm, ai chưa làm, và xuất bảng tiến độ ra CSV. |
| Timeline 3 bước | `1. Giáo viên soạn và giao bài` / `2. Học sinh đọc, viết mã, chạy thử` / `3. Hệ thống chấm theo bộ kiểm tra, giáo viên theo dõi tiến độ` |
| Bento (6 ô) | `Soạn bài có xem trước` / `Giao bài và hạn nộp` / `Chấm tự động theo bộ kiểm tra` / `Theo dõi tiến độ cả lớp` / `Xem trước như học sinh` / `Chế độ sáng và tối` (mỗi ô 1 đến 2 câu mô tả cụ thể) |
| FAQ (>= 6 câu, trả lời thật) | (1) `Tôi cần cài gì để chạy mã Python?` Không cần cài; Python chạy trong trình duyệt, lần đầu tải trình chạy có thể mất vài giây và cần kết nối Internet. (2) `Làm sao có tài khoản?` Tài khoản do giáo viên cấp, hệ thống không có đăng ký tự do. (3) `Quên mật khẩu thì sao?` Báo giáo viên để đặt lại; lần đăng nhập sau phải đổi mật khẩu. (4) `Kết quả chấm có dùng làm điểm thi không?` Việc chấm diễn ra trên trình duyệt của học sinh nên chỉ để theo dõi, không thay điểm chính thức. (5) `Xem đáp án có bị ghi lại không?` Có, giáo viên thấy học sinh nào đã xem đáp án. (6) `Dùng trên điện thoại được không?` Giao diện co giãn theo màn hình, nhưng viết mã dài nên dùng máy tính. |
| CTA cuối | h2 `Sẵn sàng vào lớp?` + nút `Đăng nhập` |
| Footer | `DK Edu`, `Tin học 12`, các liên kết neo, `© {năm hiện tại}` |

### 2.9 Wireframe chữ

**Landing (>= 900 px)**
```
[Logo DK Edu] Dành cho ai | Cách hoạt động | Tính năng | Câu hỏi        [Sáng/Tối] [Đăng nhập]
+---------------------------------------------------------------------------------------+
| Học Tin học 12 bằng Python,        |  +-- main.py ---------------------------+        |
| viết mã là chạy ngay               |  | 1 n = int(input())                   |        |
| Đọc bài, viết mã Python...         |  | 2 tong = 0 ...                       |        |
| [Đăng nhập] [Xem cách hoạt động]   |  +-- Kết quả: 55   Bộ kiểm tra 1..3 Đạt |        |
+---------------------------------------------------------------------------------------+
| [Python chạy trên trình duyệt] [Chấm theo bộ kiểm tra] [Không cần cài] [Sáng và tối]    |
| Dành cho ai:  ( Học sinh | Giáo viên )  -> 4 lợi ích + minh họa nhỏ                      |
| Cách hoạt động: (1)------(2)------(3)   đường nối vẽ khi cuộn                          |
| Tính năng (bento):  [ ô lớn: Soạn bài ][ Giao bài ][ Chấm tự động ]                     |
|                     [ Tiến độ ][ Xem trước (rộng) ][ Sáng/Tối ]                         |
| Câu hỏi thường gặp: > câu 1  > câu 2 ...                                               |
| [ Sẵn sàng vào lớp?  [Đăng nhập] ]                                                     |
| Footer: DK Edu | Tin học 12 | liên kết neo | © năm                                     |
```

**App shell + Dashboard học sinh**
```
+-----------+---------------------------------------------------------------+
| [Logo]    | Tổng quan                    [Tìm nhanh Ctrl+K][Sáng/Tối][Tên v]|
| Tổng quan |---------------------------------------------------------------|
| HỌC TẬP   | Xin chào, {tên}                                               |
|  Bài học  | [Tiến độ 62%][Đã đạt 8][Sắp đến hạn 2][Quá hạn 1]             |
|  Bài tập  | [Tiếp tục học: ...]    | Hạn nộp sắp tới: 1) ... dd/MM HH:mm  |
| [<< Thu gọn] | Bài học của bạn: [thẻ][thẻ][thẻ] ... Xem tất cả bài học     |
+-----------+---------------------------------------------------------------+
```

**Dashboard giáo viên**
```
Tổng quan                               [Tạo bài học] [Giao bài] [Xem tiến độ]
[Bài học 6: 4 xuất bản, 2 nháp][Bài tập 18][Học sinh 32][Nộp 71% | Đạt 48%]
Việc cần làm                 | Tiến độ theo bài học (thanh chồng có chữ)
 - 2 bài nháp chưa xuất bản  |  Bài 1 ####====.... Đạt 12, Đã nộp 8, Chưa nộp 10
 - 1 bài giao sắp hết hạn    |
 - 14 lượt giao chưa có bài  | Hoạt động gần đây: Giao bài gần đây / Bài học sửa gần đây
```

**Soạn bài học (>= 1100 px)**
```
Soạn bài học: {tên}                                  Đã tự lưu tạm trên máy lúc 14:05
[1 Thông tin bài ✓][2 Nội dung bài !][3 Bài tập][4 Xem lại và xuất bản]   (thanh bước dính)
+--------------------------------------------+-------------------------------+
| Bước 2: Nội dung bài                       |  XEM TRƯỚC (như học sinh thấy)|
| Mục này để làm gì: ...   Ví dụ: ...        |  Tên bài + mô tả              |
| Tóm tắt  [Điền mẫu]                         |  Tóm tắt ... Lưu ý ...        |
|  Khối 1 [Đoạn văn] [Gạch đầu dòng][Mã trong dòng][Khối mã]   |  Bài tập ...  |
|  [ textarea phông thường ]  [Lên][Xuống][Xóa khối]            |  Tổng kết ... |
|  [+ Thêm khối mới v]                        |  (cuộn riêng)                 |
+--------------------------------------------+-------------------------------+
| (thanh dính) Có thay đổi chưa lưu   [< Bước trước][Bước sau >][Lưu bài học]  |
```

**Soạn bài tập**
```
Bước 1 Đề bài | 2 Ví dụ | 3 Bộ kiểm tra chấm bài | 4 Đáp án và mã khởi tạo | 5 Nâng cao và xuất bản
Bước 3: Bộ kiểm tra chấm bài (cần 3 đến 50)      [Chạy thử với đáp án]   [+ Thêm bộ kiểm tra]
 # | Dữ liệu vào (học sinh nhập)  | Kết quả đúng (in ra)  | Kiểm tra ẩn   | Hành động
 1 | 5                            | 55                    | (o) Hiện      | Nhân đôi, Xóa
 2 | 1                            | 1                     | (x) Ẩn: học sinh không thấy |
```

### 2.10 Bảng thuật ngữ thay đổi (khu soạn bài; nguồn chuỗi duy nhất)

| Cũ | Mới (nhãn) | Giải thích ngắn dưới ô |
|---|---|---|
| Tiêu đề bài học | `Tên bài học` | `Học sinh thấy tên này ở đầu trang bài.` |
| Mô tả ngắn (hiển thị trên thẻ bài học) | `Mô tả ngắn` | `Một hai câu cho biết bài nói về gì; hiện dưới tên bài ở danh sách.` |
| Mã bài học (tùy chọn) | `Địa chỉ bài (không bắt buộc)` | `Hệ thống tự tạo từ tên bài. Chỉ đặt tay khi cần; dùng chữ thường không dấu, số và dấu gạch ngang; không đổi được sau khi tạo.` |
| Khối | `Đoạn` (chung); chip loại `Đoạn văn`, `Mục danh sách`, `Đoạn mã` | `Mỗi đoạn hiển thị thành một đoạn riêng trong bài.` |
| Những điểm cần lưu ý (ít nhất 1 mục) | `Những điểm cần lưu ý` | `Mỗi mục là một ý ngắn, hiện thành dạng gạch đầu dòng. Cần ít nhất một mục.` |
| Xuất bản / Ẩn (về bản nháp) | `Xuất bản cho học sinh` / `Chuyển về bản nháp` | `Bản nháp: chỉ giáo viên thấy. Đã xuất bản: học sinh được giao bài mới thấy.` |
| Lưu trữ / Khôi phục | `Cất đi (lưu trữ)` / `Khôi phục` | `Bài được cất khỏi danh sách học sinh, không giao mới được; bài nộp cũ vẫn giữ.` |
| Bộ test case (3–50) | `Bộ kiểm tra chấm bài` | `Mỗi bộ gồm dữ liệu vào và kết quả đúng. Hệ thống chấm bài học sinh bằng cách so kết quả; cần từ 3 đến 50 bộ.` |
| Test ẩn | `Kiểm tra ẩn: học sinh không thấy` (công tắc có chữ) | `Nên ẩn vài bộ, nhất là trường hợp đặc biệt (số 0, số âm, giá trị lớn nhất).` |
| Input / Output | `Dữ liệu vào` / `Kết quả ra` (ví dụ); `Vào` / `Ra` trên thẻ | |
| Đầu ra mong đợi | `Kết quả đúng` | |
| Định dạng dữ liệu vào / ra | `Mô tả dữ liệu vào` / `Mô tả kết quả cần in ra` | |
| Mã khởi tạo | `Khung mã cho học sinh (không bắt buộc)` | `Đoạn mã có sẵn trong ô soạn code khi học sinh mở bài.` |
| Đáp án (mã Python) | `Đáp án mẫu (không bắt buộc)` | `Để trống nếu không muốn học sinh có nút Đáp án.` |
| Giải thích đáp án | `Giải thích đáp án` | `Nên viết ít nhất một câu nếu có đáp án mẫu.` |
| Giới hạn thời gian mỗi test (mili giây) | `Thời gian tối đa cho mỗi bộ kiểm tra (mili giây)` | `5000 mili giây bằng 5 giây; chọn từ 500 đến 5000.` |
| Luôn hỏi xác nhận mỗi lần xem đáp án | `Luôn hỏi lại trước khi học sinh xem đáp án` | |
| Xung đột phiên bản | Hộp thoại `Bài học vừa được lưu ở nơi khác` | `Có người (hoặc một tab khác của bạn) vừa lưu bản mới.` |
| Kiểm tra đáp án | `Chạy thử với đáp án` | `Chạy đáp án mẫu qua mọi bộ kiểm tra ngay trên máy bạn; không gửi gì lên máy chủ.` |
| lượt giao / bài nộp | `lượt giao bài` / `bài nộp` | giữ |

---

## 3. Tiêu chí Nghiệm thu (Acceptance Criteria) – bổ sung từ AC-319

### 3.1 Định tuyến và bảo vệ trang (QD-62, QD-63)

- [AC-319]: **Khách xem landing.** `GET /` và `HEAD /` khi **chưa** có cookie phiên trả **200**, `Content-Type: text/html`, thân chứa `<title>Học Tin học 12 với Python – DK Edu</title>` và đúng một `<h1>`; **không** có `Location`. Có đủ header bảo mật `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`. `GET /landing.html` chưa đăng nhập cũng trả 200 cùng nội dung.
- [AC-320]: **Người đã đăng nhập không xem landing.** `GET /` và `GET /landing.html` với phiên **học sinh** trả **302** `Location: /dashboard.html`; phiên **giáo viên** trả 302 `/teacher/dashboard.html`; tài khoản `mustChangePassword` trả 302 `/change-password.html`. Cookie hết hạn/sai chữ ký được coi là chưa đăng nhập (trả 200 landing). `HEAD` cho kết quả như `GET`.
- [AC-321]: **Mọi trang khác bảo vệ như cũ.** Chưa đăng nhập: `GET` từng URL trong `{/index.html, /dashboard.html, /assignments.html, /lesson.html?id=x, /problem.html, /change-password.html, /teacher/, /teacher/index.html, /teacher/dashboard.html, /teacher/lesson-edit.html, /teacher/exercise-edit.html, /teacher/assignments.html, /teacher/progress.html, /teacher/students.html}` trả 302 `Location` bắt đầu bằng `/login.html?returnUrl=` (đã mã hóa URL). `/login.html`, `/css/*`, `/js/*`, `/fonts/*`, `/img/*` vẫn 200 khi chưa đăng nhập. `/teacher/` đã đăng nhập giáo viên vẫn trả 200 (`teacher/index.html`) nhờ `DefaultFileNames = { "landing.html", "index.html" }` (QD-62).
- [AC-322]: **Phân quyền theo role cho trang mới.** Giáo viên `GET /dashboard.html` → 302 `/teacher/dashboard.html`. Học sinh `GET /teacher/dashboard.html` (và mọi `/teacher/*`) → 302 `/dashboard.html?notice=forbidden`. Giáo viên `GET /assignments.html` → 302 `/teacher/dashboard.html` (đích mới của `TeacherHome`). Đăng nhập thành công trả `redirectUrl` = `/dashboard.html` (học sinh) hoặc `/teacher/dashboard.html` (giáo viên) khi không có `returnUrl` hợp lệ; `returnUrl` hợp lệ vẫn được ưu tiên.
- [AC-323]: **ReturnUrlPolicy.** `StudentHome == "/dashboard.html"`, `TeacherHome == "/teacher/dashboard.html"`, `HomeFor` theo role. `Sanitize` trả `null` cho: `/`, `/landing.html`, `/landing.html?x=1`, `/api/…`, `/login.html…`, `//evil.com`, ký tự điều khiển; với giáo viên cũng `null` cho `/assignments.html` và `/dashboard.html`; với học sinh `null` cho `/teacher/…`. Có test đơn vị cho từng ca.
- [AC-324]: **Bộ nhớ đệm.** Phản hồi của `/` và `/landing.html` có `Cache-Control: no-cache` và `Vary: Cookie` (nội dung của `/` phụ thuộc đăng nhập); các trang bảo vệ khác giữ `no-store`.
- [AC-325]: **Phía trình duyệt khớp phía máy chủ.** `api.homeFor(user)` trả `/dashboard.html` hoặc `/teacher/dashboard.html`; `initPage({role:'Teacher'})` với học sinh chuyển tới `/dashboard.html?notice=forbidden`; `initPage({role:'Student'})` với giáo viên chuyển tới `/teacher/dashboard.html`; `goToLogin()` không đặt `returnUrl=/`. Đăng xuất vẫn chuyển tới `/login.html`.

### 3.2 Landing page (QD-81 đến QD-85)

- [AC-326]: **Cấu trúc và landmark.** `landing.html` có đúng 1 `<header>` (thanh điều hướng, `<nav aria-label="Điều hướng trang giới thiệu">`), 1 `<main id="main">`, 1 `<footer>`; liên kết "Bỏ qua, tới nội dung chính" đầu trang; đúng 1 `<h1>`; mỗi mục là `<section>` có `<h2>` và `id` theo thứ tự: `hero` (h1), `dai-niem-tin` (dải số liệu), `vai-tro`, `cach-hoat-dong`, `tinh-nang`, `hoi-dap`, `bat-dau` (CTA cuối); không nhảy cấp tiêu đề (h1→h2→h3); không `<script>` nội dòng; nạp đúng `theme-init.js` và một module `landing.js`.
- [AC-327]: **Thanh điều hướng công khai.** Gồm logo (liên kết `href="#hero"`, `aria-label="DK Edu – về đầu trang"`), 4 liên kết neo đúng nhãn và `href` ở mục 2.8, nút đổi sáng/tối (`createThemeButton`), và **một** nút `Đăng nhập` (`href="/login.html"`, `btn btn--primary`) là phần tử duy nhất có kiểu nút chính trên thanh. Dưới 900 px các liên kết neo gom vào nút `Menu` (`aria-expanded`, `aria-controls`), Esc đóng và trả tiêu điểm; nút `Đăng nhập` và nút giao diện luôn hiển thị ngoài menu.
- [AC-328]: **Nav dính, kính mờ.** `position: sticky; top: 0`, nền `var(--glass-bg)` + `backdrop-filter` với phương án nền đặc khi không hỗ trợ (`@supports not`, `prefers-reduced-transparency`); chữ đạt >= 4,5:1 trên nền trong cả hai giao diện; `scroll-margin-top` của mọi `section` >= chiều cao nav để tiêu đề không bị che sau khi bấm liên kết neo; liên kết của mục đang xem có `aria-current="location"` (cập nhật bằng IntersectionObserver dùng chung, không có listener cuộn).
- [AC-329]: **Hero.** >= 900 px hai cột (chữ trái, khung mã phải), dưới 900 px xếp dọc (chữ trước). `h1` đúng chuỗi mục 2.8, **tối đa 2 dòng** ở viewport 1280 px (đo `getClientRects` hoặc chiều cao <= 2 x line-height); mô tả đúng chuỗi mục 2.8 và **<= 20 từ** (tách theo khoảng trắng); đúng **2** CTA trong hero: `Đăng nhập` (`href="/login.html"`, chính) và `Xem cách hoạt động` (`href="#cach-hoat-dong"`, phụ, cuộn mượt; không cuộn mượt khi `prefers-reduced-motion`).
- [AC-330]: **Khung trình soạn mã là thành phần thật.** Dựng bằng phần tử HTML/CSS (thanh tiêu đề `main.py`, cột số dòng, các dòng mã là text node, panel kết quả); **không** dùng `<img>`, `<canvas>`, `<video>`, `background-image` để thể hiện mã. Mã mẫu đúng mục 2.8, hợp lệ Python, và kết quả `55` đúng với đầu vào `5` (1+4+9+16+25). Có nhãn `Ví dụ minh họa`. Vùng khung có `role="group"` và `aria-label="Ví dụ minh họa trình soạn mã"`; bản mã đầy đủ và kết quả có trong DOM cho trình đọc màn hình (bản gõ động là bản `aria-hidden`). Văn bản mã không chứa 5 tiêu đề bài mẫu cấm ở QD-75.
- [AC-331]: **Hiệu ứng gõ mã chạy một lần.** Khi hero vào khung nhìn: mã hiện dần theo ký tự trong **<= 4 giây**, sau đó hiện kết quả `55` và ba dòng `Bộ kiểm tra n: Đạt` (biểu tượng + chữ); dừng ở trạng thái cuối, **không lặp**. Không dùng `setInterval`; hủy khi tab ẩn/`pagehide`. Khi `prefers-reduced-motion: reduce` (kể cả đổi lúc đang chạy) hiện ngay trạng thái cuối, không timer.
- [AC-332]: **Dải điều đúng sự thật.** Đúng 4 ô (biểu tượng SVG + chữ) với nội dung mục 2.8. Trang landing **không chứa** số lượng người dùng/trường/bài nộp (kiểm bằng regex `\d+\s*(học sinh|giáo viên|trường|người dùng|lượt|bài nộp)` và `\d+[.,]?\d*\s*(sao|/5)` không khớp), không có từ "đánh giá", "nhận xét của", logo hay ảnh người; không `<img>` nào ngoài logo DK Edu.
- [AC-333]: **Dành cho học sinh / giáo viên (tab).** `role="tablist"` với hai `role="tab"` (`Dành cho học sinh`, `Dành cho giáo viên`), `aria-selected`, `aria-controls`, `tabindex` luân phiên; Mũi tên trái/phải, Home, End chuyển tab và đưa tiêu điểm; mặc định chọn tab học sinh; mỗi `tabpanel` có **>= 4** lợi ích (`<li>`) đúng ý mục 2.8, mỗi lợi ích >= 6 từ và có biểu tượng. Khi JS không chạy cả hai panel đều hiện. Tab >= 44 px cao.
- [AC-334]: **Cách hoạt động (timeline).** `<ol>` đúng 3 mục theo mục 2.8; số bước hiển thị bằng **chữ số** (không chỉ màu); >= 900 px nằm ngang, dưới 900 px dọc; đường nối nằm giữa các bước và được "vẽ" khi cuộn bằng `transform: scale` (CSS scroll-driven `animation-timeline: view()` khi hỗ trợ, nếu không thì thêm lớp `is-drawn` một lần bằng IntersectionObserver); khi reduced-motion hoặc không hỗ trợ: đường nối hiện đầy đủ ngay.
- [AC-335]: **Bento tính năng.** Đúng 6 ô theo mục 2.8, mỗi ô có `<h3>`, 1 đến 2 câu mô tả cụ thể, và **một minh họa nhỏ bằng phần tử thật (không ảnh) khác nhau** (ví dụ: ô soạn bài = khung hai cột nhập/xem trước; giao bài = danh sách có chip hạn nộp; chấm tự động = ba dòng Đạt/Chưa đạt; tiến độ = lưới ô có chữ; xem trước = nút và băng rôn; sáng/tối = hai nửa nền). >= 900 px lưới có ít nhất **3 kích thước ô khác nhau** (ví dụ 2x2, 1x1, 2x1) và **không** là 3 thẻ bằng nhau; phần minh họa `aria-hidden="true"` còn ý nghĩa nằm ở chữ. Spotlight/nghiêng dùng lại `fx.enableCardEffects`.
- [AC-336]: **Câu hỏi thường gặp.** >= 6 mục dùng `<details>/<summary>` (hoặc `button[aria-expanded]` + vùng `aria-controls`); `summary` cao >= 44 px, thao tác bằng Enter/Space; mặc định đóng tất cả; nội dung đúng sự thật theo sáu câu ở mục 2.8, mỗi câu trả lời >= 2 câu; tuyệt đối không hứa tính năng không có (không "đăng ký tự do", không "điểm chính thức").
- [AC-337]: **CTA cuối và footer.** Mục `bat-dau` có `h2` `Sẵn sàng vào lớp?` và nút `Đăng nhập` (`/login.html`). Footer gọn gồm `DK Edu`, `Tin học 12`, 4 liên kết neo giống nav, và `© ` + năm hiện tại (JS đặt bằng `new Date().getFullYear()`, bản HTML tĩnh ghi `2026`); không liên kết ra ngoài.
- [AC-338]: **Văn phong.** Toàn bộ chữ landing là tiếng Việt tự nhiên; quét văn bản hiển thị: **không** các từ `tối ưu`, `đột phá`, `toàn diện`, `vượt trội`, `hàng đầu`, `cách mạng`, `số 1`, `thế hệ mới`, `giải pháp`; không em-dash (U+2014); không emoji (khối Unicode như AC-313); en-dash chỉ được xuất hiện trong `<title>`.
- [AC-339]: **SEO cơ bản.** `<title>` đúng mục 2.8 (<= 60 ký tự); `<meta name="description">` đúng mục 2.8, 110 đến 160 ký tự, chứa "DK Edu"; có `og:title`, `og:description`, `og:type=website`, `og:locale=vi_VN`, `og:image` trỏ PNG tồn tại trong `/img` (trả 200 `image/png`); `<html lang="vi">`; `<meta name="viewport">`; favicon và apple-touch-icon đúng hai dòng `<link>` hiện có; không có thẻ `robots` chặn.
- [AC-340]: **Chuyển cảnh sang đăng nhập.** Logo ở nav landing và logo ở `login.html` cùng `view-transition-name: brand-mark` (mỗi trang đúng một phần tử mang tên này); bấm `Đăng nhập` điều hướng tới `/login.html` bình thường trên trình duyệt không hỗ trợ View Transitions (không lỗi console); tắt khi reduced-motion (đã có từ v4).
- [AC-341]: **Hiệu ứng cuộn.** Hiện dần khi cuộn dùng `fx.revealOnView` (một `IntersectionObserver` dùng chung), chỉ `opacity`+`transform`; nội dung mặc định **hiện** (chỉ ẩn khi JS đã thêm lớp `js-reveal` lên `<html>`), nên khi JS lỗi không mất nội dung. Parallax của khung mã **nếu làm** chỉ là `transform: translateY` <= 24 px bằng CSS scroll-driven, và **chỉ** khi trình duyệt hỗ trợ và không reduced-motion; nếu không hỗ trợ thì không có parallax (không dùng listener `scroll`).
- [AC-342]: **Hiệu năng và độ ổn định landing.** Mở `/` tạo **0** yêu cầu tới `/api/**`; không `<img>` thiếu `width`/`height`; CLS < 0,1 ở 360 và 1280 px; LCP <= 2,5 s (Fast 4G, CPU 4x chậm), phần tử LCP là `h1` hoặc logo; chỉ JS chặn vẽ là `theme-init.js`; không tải Pyodide trên landing.

### 3.3 App shell (QD-65, QD-66, QD-67)

- [AC-343]: **Cấu trúc shell.** Mọi trang sau đăng nhập (12 trang cũ trừ `login.html`, cộng 2 dashboard) có `<body data-shell>`, `<aside id="site-menu" class="app-sidebar">` chứa logo (`a.brand` với `aria-label="DK Edu – về trang chủ"`, `href` = trang chủ theo role, `locked` thì `/change-password.html`) và `<nav id="site-nav" class="site-nav" aria-label="Điều hướng chính">`, `<header id="site-header" class="site-header">` là topbar, `<main id="main">`. Mỗi trang có đúng 1 landmark `banner`, 1 `main`, các `nav` có tên phân biệt, đúng 1 `h1` (topbar **không** dùng `h1`).
- [AC-344]: **Mục điều hướng theo role (đúng nhãn, thứ tự, đích).**
  - Học sinh: `Tổng quan` (`/dashboard.html`); nhóm `Học tập`: `Bài học` (`/index.html`), `Bài tập của tôi` (`/assignments.html`).
  - Giáo viên: `Tổng quan` (`/teacher/dashboard.html`); nhóm `Soạn bài`: `Bài học của tôi` (`/teacher/index.html`), `Giao bài` (`/teacher/assignments.html`); nhóm `Lớp học`: `Tiến độ` (`/teacher/progress.html`), `Học sinh` (`/teacher/students.html`); nhóm `Khác`: `Xem như học sinh (xem trước)` (`/index.html`).
  - Mỗi mục có biểu tượng SVG + chữ; tiêu đề nhóm là chữ (không chỉ vạch). Tài khoản `mustChangePassword`: không có mục điều hướng nào (như AC-145 cũ), chỉ logo (về `/change-password.html`) và menu người dùng.
- [AC-345]: **Mục hiện tại.** Đúng **một** liên kết có `aria-current="page"`, được tô bằng nền `--primary-soft` **và** thanh dọc 3 px bên trái **và** chữ đậm (không chỉ bằng màu). Quy tắc khóa `active`: học sinh ở `lesson.html`/`problem.html` → `Bài học`; giáo viên ở `lesson-edit.html`/`exercise-edit.html` → `Bài học của tôi`; giáo viên ở `index.html` (xem trước) → `Xem như học sinh (xem trước)`; trang `change-password.html` → không mục nào (menu người dùng `aria-current`).
- [AC-346]: **Thu gọn thành rail.** >= 900 px có nút `Thu gọn thanh bên` / `Mở rộng thanh bên` (`aria-expanded`, cao >= 44 px); thu gọn còn 72 px chỉ hiện biểu tượng, **tên truy cập của từng liên kết vẫn đủ chữ** (`aria-label` hoặc chữ `sr-only`) và có tooltip `title`; chọn lưu `localStorage["th12.sidebar"]` (`collapsed`/`expanded`); tải lại trang giữ trạng thái và **không nháy** (do `theme-init.js` đặt `data-sidebar` trước khi vẽ); giá trị lạ hoặc `localStorage` bị chặn → mở rộng, vẫn đổi được trong phiên; `theme-init.js` <= 1,5 KB và không `console.*`.
- [AC-347]: **Ngăn kéo dưới 900 px.** Sidebar ẩn khỏi bố cục; nút `Menu` ở topbar (`aria-expanded`, `aria-controls="site-menu"`) mở ngăn kéo rộng <= 320 px hoặc 85vw; khi mở: bẫy tiêu điểm, phần còn lại `inert`, cuộn nền bị khóa, bấm lớp phủ hoặc nhấn **Esc** đóng và trả tiêu điểm về nút `Menu`; chọn liên kết thì điều hướng bình thường; chuyển động chỉ `transform`/`opacity` (<= 250 ms); khi nới rộng cửa sổ >= 900 px lúc đang mở thì tự đóng, không kẹt `inert`.
- [AC-348]: **Topbar.** Từ trái sang phải: nút `Menu` (chỉ < 900 px), breadcrumb/tiêu đề trang (văn bản, `nav aria-label="Vị trí hiện tại"` với `ol`, mục cuối `aria-current="page"`; trang sâu hiện đủ cấp, ví dụ `Bài học của tôi / Soạn bài học`), khoảng trống, nút `Tìm nhanh`, nút đổi giao diện, nút người dùng. Topbar `position: sticky`, cao <= 64 px, nền `--glass-bg` có phương án đặc.
- [AC-349]: **Menu người dùng.** Nút hiển thị tên (cắt bằng dấu chấm lửng, `title` đầy đủ) và nhãn vai trò (`Giáo viên`/`Học sinh`); `aria-expanded`, `aria-haspopup="true"`, `aria-controls`. Panel chứa tên đầy đủ, vai trò, liên kết `Đổi mật khẩu` (`/change-password.html`) và nút `Đăng xuất` (`id="btn-logout"`). Esc hoặc bấm ngoài đóng và trả tiêu điểm; mũi tên lên/xuống di chuyển giữa các mục; từ trang bất kỳ chỉ cần **<= 2 thao tác** (mở menu, bấm Đăng xuất) để đăng xuất (AC-145). Tên chứa `<script>alert(1)</script>` hiển thị như văn bản.
- [AC-350]: **Đăng xuất giữ nguyên hành vi.** Bấm `Đăng xuất`: nút khóa, chữ `Đang đăng xuất…`, gọi `api.logout()` đúng 1 lần, dù lỗi mạng vẫn chuyển `/login.html`.
- [AC-351]: **Bố cục shell không dịch chuyển.** Sidebar 264 px (mở rộng) / 72 px (rail), cao `100dvh` (có dự phòng `vh` ngay trước theo AC-303), dính; `main` không bị che bởi sidebar/topbar; CLS < 0,1 ở 360 và 1280 px (cột sidebar được giữ chỗ nhờ `data-shell`); `--header-h` cập nhật cho `scroll-margin-top` và băng rôn `#preview-banner` dính dưới topbar (AC-123 giữ chữ).
- [AC-352]: **Tìm nhanh.** Nút `Tìm nhanh` hoặc **Ctrl+K** mở hộp thoại (dùng `modal.js`) với ô `Tìm trang hoặc bài học`; kết quả gồm các đích điều hướng của role (luôn có) và tên bài học; lọc không phân biệt hoa/thường và dấu tiếng Việt (ví dụ gõ `bai hoc` khớp `Bài học`); mũi tên lên/xuống chọn, Enter mở, Esc đóng; kết quả là liên kết thật. **Không có yêu cầu mạng nào trước lần mở đầu tiên**; lần mở đầu gọi đúng 1 API (`GET /api/lessons` hoặc `GET /api/teacher/lessons`), cache trong phiên trang; lỗi API thì vẫn dùng các đích điều hướng và hiện dòng `Chưa tải được danh sách bài học`. Học sinh mở bài tìm thấy tới `/lesson.html?id=...`, giáo viên tới `/teacher/lesson-edit.html?id=...`.
- [AC-353]: **Dọn thanh cũ.** Không còn trong DOM/CSS: `nav-indicator`, `header-sentinel`, `sheet-layer`, `sheet-scrim`, `sheet-item`, viên thuốc nổi (`border-radius: var(--r-pill)` trên `.site-header`); vẫn còn và đúng nghĩa các id/class bất biến ở mục 0.3 (`site-header`, `site-menu`, `site-nav`, `btn-logout`, `brand`, `brand__logo`, `brand__name`, `role-badge`, `nav-toggle`, `site-user*`). Tổng số mục điều hướng không đổi ngoài `Tổng quan` được thêm.
- [AC-354]: **Thành phần shell đạt chuẩn chạm/chữ.** Mọi mục sidebar, nút topbar, mục menu cao >= 44 px, rộng >= 44 px, chữ >= 16 px (trừ biểu tượng trang trí), tương phản chữ/nền >= 4,5:1 và đường viền/biểu tượng chức năng >= 3:1 ở **cả hai giao diện**; `forced-colors` giữ viền và vòng focus.

### 3.4 Dashboard học sinh (QD-64, QD-68, QD-69)

- [AC-355]: **Trang và quyền.** `dashboard.html`: `<title>Tổng quan – DK Edu</title>`, `h1` `Tổng quan`, `<body data-shell>`, `initPage({ role: 'Student', active: 'overview' })`; `?notice=forbidden` hiện băng rôn `Bạn không có quyền truy cập khu vực giáo viên.` (chuỗi cũ). Lời chào `Xin chào, {displayName}` bằng `textContent`.
- [AC-356]: **Yêu cầu mạng.** Ngoài `GET /api/auth/me`, trang gọi đúng **2** yêu cầu song song: `GET /api/lessons` và `GET /api/me/assignments`; không gọi gì khác; mỗi nguồn lỗi độc lập (nguồn kia vẫn hiển thị).
- [AC-357]: **KPI 1 "Tiến độ tổng".** Vòng SVG: `Σ passedCount / Σ exerciseCount` trên `GET /api/lessons` (`passed = min(Σ passedCount, Σ exerciseCount)`), phần trăm làm tròn; chữ `{p}/{t} bài tập đã đạt`; `t = 0` → `Chưa có bài tập được giao`, vòng 0%, không `NaN`/`Infinity`. Ví dụ kiểm: lessons `[{3,5},{0,0},{2,2}]` → 5/7 = 71%.
- [AC-358]: **KPI 2 "Đã đạt" + phân bố.** Số lớn = số bài tập (theo `exerciseId`, QD-68) có `status == "Passed"`; kèm thanh chồng 3 đoạn `Đã đạt`, `Đang làm` (`InProgress`), `Chưa làm` (`NotStarted`) với **số và chữ** trên từng đoạn/chú giải; tổng 3 đoạn bằng số bài tập khác nhau; không có dữ liệu → thẻ ghi `Chưa có bài tập được giao`, không vẽ thanh giả.
- [AC-359]: **KPI 3 "Sắp đến hạn".** Đếm bài tập có `status != "Passed"`, `isOverdue == false`, `dueAt != null` và `now <= dueAt <= now + 7 ngày`; thẻ ghi số và dòng phụ `Gần nhất: dd/MM/yyyy HH:mm` (JetBrains Mono); 0 → `Không có bài nào sắp đến hạn`. Kiểm bằng dữ liệu cố định (đồng hồ giả) gồm đúng 2 bài trong 7 ngày, 1 bài 8 ngày, 1 bài đã `Passed`: kết quả 2.
- [AC-360]: **KPI 4 "Quá hạn".** Đếm `isOverdue == true` và `status != "Passed"`; số kèm biểu tượng cảnh báo và chữ (không chỉ màu đỏ); > 0 thẻ có tông `--danger-*` và liên kết `Xem bài quá hạn` tới `/assignments.html`; 0 → `Không có bài quá hạn`.
- [AC-361]: **Thẻ "Tiếp tục học".** Dùng nguyên công thức và chuỗi QD-51 / mục 2.8 v4 (`Tiếp tục học`, `Bắt đầu học`, `Bạn đã hoàn thành mọi bài tập được giao`, ẩn khi `t = 0`); là liên kết tới `/lesson.html?id=...` của bài đầu tiên (theo `order`) còn bài chưa đạt.
- [AC-362]: **Danh sách "Hạn nộp sắp tới".** Tiêu đề `h2` `Hạn nộp sắp tới`; tối đa **5** bài tập (theo `exerciseId`) có `dueAt` và `status != "Passed"`, sắp theo `dueAt` tăng dần (bài quá hạn tự nằm đầu); mỗi mục: tên bài tập (liên kết `/problem.html?lesson=<lessonId>&problem=<exerciseId>`), tên bài học, hạn `dd/MM/yyyy HH:mm` (mono), chip `Quá hạn` hoặc `Còn N ngày`/`Còn N giờ`/`Hết hạn hôm nay` (chữ, có biểu tượng); liên kết `Xem tất cả` tới `/assignments.html`; rỗng → `Chưa có hạn nộp nào`.
- [AC-363]: **Lưới bài học.** `h2` `Bài học của bạn`, tối đa **6** thẻ đầu theo `order` dùng lại `.lesson-card` (số thứ tự, tiêu đề, mô tả, `n/m bài tập đã đạt`, thanh tiến độ `--p`, huy hiệu `Đã hoàn thành`), liên kết `Xem tất cả bài học` tới `/index.html`. Thứ tự DOM theo `order` (AC-01).
- [AC-364]: **Trạng thái đầy đủ.** Đang tải: skeleton đúng hình bố cục (KPI x4, hai khối) và vùng `aria-busy="true"` + dòng `sr-only role="status"` `Đang tải tổng quan…`; rỗng toàn bộ (không bài học, không bài tập): `stateBox` `Chưa có bài học nào` với nút `Tải lại`; lỗi: mỗi thẻ/khối lỗi hiện `stateBox kind=error` (`role="alert"`) với nút `Thử lại` chỉ gọi lại nguồn của khối đó, không tải lại trang.
- [AC-365]: **Đáp ứng.** Hàng KPI: 4 cột >= 1100 px, 2 cột >= 600 px, 1 cột dưới 600 px; hai khối dưới KPI xếp 2 cột >= 1100 px; không cuộn ngang ở 360 px; không `NaN`, `undefined`, `null` xuất hiện trong văn bản trang với bộ dữ liệu thiếu trường (`passedCount` thiếu, `dueAt: null`).

### 3.5 Dashboard giáo viên (QD-68 đến QD-70)

- [AC-366]: **Trang, quyền, đích.** `teacher/dashboard.html`: `<title>Tổng quan giáo viên – DK Edu</title>`, `h1` `Tổng quan`, `<body data-shell data-area="teacher">`, `initPage({ role: 'Teacher', active: 'overview' })`; là `TeacherHome`. Có ba lối tắt ở đầu trang: `Tạo bài học` (`btn--primary`, `/teacher/lesson-edit.html`), `Giao bài` (`/teacher/assignments.html`), `Xem tiến độ` (`/teacher/progress.html`).
- [AC-367]: **Yêu cầu mạng.** Ngoài `GET /api/auth/me`, đúng **3** yêu cầu song song: `GET /api/teacher/lessons?includeArchived=false`, `GET /api/teacher/assignments`, `GET /api/teacher/students?page=1&pageSize=1` (QD-69). Mỗi nguồn lỗi độc lập; không thêm endpoint.
- [AC-368]: **KPI "Bài học".** Số lớn = số bài học không lưu trữ; dòng phụ `{a} đã xuất bản, {b} bản nháp` với `a = count(isPublished)`, `b = count(!isPublished)`; thêm biểu đồ thanh chồng 2 đoạn có chữ. 0 bài → `Chưa có bài học nào`.
- [AC-369]: **KPI "Bài tập".** `Σ exerciseCount` trên các bài học không lưu trữ; dòng phụ `{Σ publishedExerciseCount} đã xuất bản`. KPI "Học sinh" = `total` của phản hồi `students` (chữ `tài khoản học sinh`); nếu nguồn lỗi, thẻ ghi lỗi tại chỗ.
- [AC-370]: **KPI "Tỉ lệ nộp và đạt".** Với các giao bài chưa hủy (`cancelledAt == null`): `A = Σ assignedStudents`, `S = Σ submittedStudents`, `P = Σ passedStudents`; hiển thị `Đã nộp {S}/{A} ({round(100S/A)}%)` và `Đã đạt {P}/{A} ({round(100P/A)}%)`; `A = 0` → `Chưa có dữ liệu`, không chia cho 0. Kiểm bằng ví dụ: hai giao bài `(A,S,P) = (30,20,10)` và `(10,10,10)` → nộp 30/40 (75%), đạt 20/40 (50%). Giá trị `S > A` hoặc `P > S` được cắt về hợp lệ trước khi tính.
- [AC-371]: **"Việc cần làm".** Ba nhóm, mỗi nhóm có tiêu đề kèm số và danh sách tối đa 5 mục có liên kết:
  1. `Bài học chưa xuất bản` (`isPublished == false` và không lưu trữ): mỗi mục tên bài, nút `Soạn tiếp` (`/teacher/lesson-edit.html?id=...`).
  2. `Bài giao sắp hết hạn`: giao bài chưa hủy, `dueAt` trong 72 giờ tới, và `submittedStudents < assignedStudents`; hiển thị tên bài tập, hạn (mono), `{S}/{A} đã nộp`; liên kết `/teacher/progress.html?lesson=<lessonId>`.
  3. `Giao bài chưa có người nộp`: giao bài chưa hủy có `assignedStudents > 0` và `submittedStudents == 0`, liên kết như trên.
  Nhóm rỗng hiện `Không có việc nào`; tất cả rỗng hiện lời `Mọi thứ đã gọn gàng` (không emoji).
- [AC-372]: **Biểu đồ tiến độ theo bài học.** Gộp giao bài chưa hủy theo `lessonId`, lấy tối đa **6** bài học có giao bài gần nhất (`createdAt` lớn nhất); mỗi bài một thanh chồng 3 đoạn `Đã đạt` (`ΣP`), `Đã nộp, chưa đạt` (`ΣS − ΣP`), `Chưa nộp` (`ΣA − ΣS`) theo thang `ΣA`; số và chữ hiện ngay cạnh thanh (không chỉ màu); chú giải chữ; tên bài học liên kết tới tiến độ của bài đó. Không có giao bài → `Chưa giao bài nào` và nút `Giao bài`.
- [AC-373]: **"Hoạt động gần đây" (QD-70).** Hai danh sách có dữ liệu thật: `Giao bài gần đây` (5 giao bài theo `createdAt` giảm dần: tên bài tập, lớp hoặc `Tất cả học sinh`, ngày giờ) và `Bài học sửa gần đây` (5 bài theo `updatedAt` giảm dần, bỏ qua `null`). Không có mục "bài nộp mới nhất" (không bịa dữ liệu); mỗi danh sách rỗng có chữ riêng. Không thêm endpoint nào trừ khi Khách hàng duyệt QD-70.
- [AC-374]: **Trạng thái và lỗi từng khối.** Skeleton đúng hình; mỗi khối lỗi có `Thử lại` chỉ tải lại nguồn của nó; khi giáo viên chưa có bài học nào: khối "Việc cần làm" thay bằng hướng dẫn 3 bước (như AC-427) với nút `Tạo bài học`.
- [AC-375]: **Đáp ứng.** KPI 4 cột >= 1100 px, 2 cột >= 600 px, 1 cột dưới; "Việc cần làm" và biểu đồ 2 cột >= 1100 px; không cuộn ngang ở 360 px; thanh biểu đồ co giãn theo bề rộng; không `NaN`/`undefined` với dữ liệu thiếu.
- [AC-376]: **Mô tả nguồn KPI hiển thị được.** Mỗi thẻ KPI có `title` hoặc chú thích nhỏ `Tính từ: ...` ngắn gọn (ví dụ `Tính từ danh sách bài học`) để giáo viên hiểu số từ đâu; nội dung khớp công thức ở AC-368 đến AC-372.

### 3.6 Biểu đồ (QD-80)

- [AC-377]: **Tự viết, không thư viện.** Mọi biểu đồ dựng trong `js/charts.js` bằng `createElementNS` (SVG) và phần tử CSS; không `<canvas>`, không thư viện, không `innerHTML`; hàm dùng chung `ring()`, `stackedBar()` nhận dữ liệu thô và trả về nút DOM.
- [AC-378]: **Tên truy cập.** Mỗi biểu đồ có `role="img"` và `aria-label` tóm tắt **đủ số liệu** bằng tiếng Việt (ví dụ `Đã đạt 5 trên 7 bài tập, 71 phần trăm`) **hoặc** kèm bảng dữ liệu `sr-only` (`<table>` có `caption`, `th scope`) và chính biểu đồ `aria-hidden`; kiểm bằng cây truy cập.
- [AC-379]: **Màu không phải kênh duy nhất.** Mỗi đoạn/cột có (a) nhãn chữ hoặc chú giải chữ kèm số, và (b) họa tiết hoặc biểu tượng/độ rộng phân biệt (ví dụ đoạn `Đã đạt` có dấu tích, `Chưa nộp` có nền sọc); độ tương phản đối tượng đồ họa với nền >= 3:1 và chữ >= 4,5:1 ở cả hai giao diện (kiểm từng cặp token); chú giải không dựa vào màu.
- [AC-380]: **Dữ liệu trống và an toàn số học.** Tổng bằng 0 → không vẽ biểu đồ mà hiện thông báo chữ; không `NaN`/chia cho 0/độ rộng âm hoặc > 100%; không vẽ số liệu mặc định hay giả.
- [AC-381]: **Chuyển động.** Vòng tiến độ chỉ animate `stroke-dashoffset`, thanh chỉ `transform: scaleX`, mỗi biểu đồ **một lần** khi vào khung nhìn (dùng `fx.onceInView`), tối đa `--dur-ring`; reduced-motion hoặc không IntersectionObserver → vẽ ngay giá trị cuối.
- [AC-382]: **Token màu.** Không có màu hex/rgb viết cứng trong `charts.js`; mọi màu qua biến CSS (`--primary`, `--brand-blue` chỉ làm nền/hình, `--success-*`, `--warn-*`, `--danger-*`, `--heat-*`, `--border`); bật giao diện tối vẫn đạt AC-379.

### 3.7 Soạn bài học (QD-71 đến QD-78)

- [AC-383]: **Id bất biến.** Trang `lesson-edit.html` vẫn có: `page-title`, `status`, `content`, `title`, `description`, `lesson-id`, `summary`, `notes`, `conclusion`, `btn-save`, `btn-publish` (ở bước 4 hoặc thanh dính), `preview-title` hoặc tương đương; `h1` `Soạn bài học: {tên}` hoặc `Tạo bài học mới` như cũ.
- [AC-384]: **Thanh bước dính.** Ngay dưới topbar có `<nav aria-label="Các bước soạn bài">` với `<ol>` đúng 4 nút `1. Thông tin bài`, `2. Nội dung bài`, `3. Bài tập`, `4. Xem lại và xuất bản`; `position: sticky`; bước đang xem có `aria-current="step"`; mỗi bước có trạng thái **chữ + biểu tượng** `Hoàn thành` / `Chưa xong` / `Có lỗi` (xem AC-385); bấm bất kỳ bước nào chuyển ngay, **không mất dữ liệu đang gõ**, không gọi API; có nút `Bước trước` / `Bước sau` ở thanh lưu; tham số `?step=1..4` chọn bước khi mở trang.
- [AC-385]: **Chỉ báo hoàn thành từng bước.** `Hoàn thành` khi: bước 1: tên và mô tả đều có chữ; bước 2: cả ba phần có >= 1 đoạn và không đoạn nào trống; bước 3: >= 1 bài tập; bước 4: mọi mục kiểm tra (AC-398) đạt. `Có lỗi` khi có ô đang `aria-invalid="true"` thuộc bước đó. Còn lại `Chưa xong`. Cập nhật <= 300 ms sau khi gõ.
- [AC-386]: **Tiêu đề, giải thích, ví dụ cho từng bước.** Mỗi bước mở đầu bằng `h2` bằng lời thường (đúng 4 tên bước), **một câu "Mục này để làm gì"** (<= 25 từ, không thuật ngữ trong bảng 2.10), và **hộp ví dụ** `Ví dụ` (văn bản mẫu từ `samples.js`, hiển thị chữ rõ, không thay thế nhãn). Bước 2 có ba phần con `Tóm tắt`, `Những điểm cần lưu ý`, `Tổng kết` mỗi phần có `h3`, một câu giải thích và ví dụ riêng.
- [AC-387]: **Bố cục hai cột và xem trước dính.** >= 1100 px: cột trái biểu mẫu, cột phải `Xem trước` dính (`position: sticky; top` bằng chiều cao topbar + thanh bước), cao tối đa `calc(100dvh - ...)` và **cuộn riêng**; dưới 1100 px: một cột với `tablist` `Soạn` / `Xem trước` (`role="tab"`, mũi tên chuyển, `aria-selected`), nội dung xem trước **chỉ có một bản trong DOM** (di chuyển nút, không nhân bản) và chuyển tab giữ nguyên vị trí cuộn của mỗi tab.
- [AC-388]: **Xem trước trung thực.** Khung xem trước dùng **cùng hàm dựng** với `lesson.html` (Dev tách hàm dùng chung nếu cần, giữ nguyên id/class của `lesson.html`): đủ đầu bài (tên, mô tả), bốn mục `h2` đúng tên và **thứ tự** như học sinh thấy (Tóm tắt, Lưu ý, Bài tập, Tổng kết), danh sách bài tập đã lưu (hoặc `Chưa có bài tập`). Kiểm bằng cách so chuỗi tiêu đề và loại phần tử (`p`, `ul>li`, `pre.codeblock`) của khung xem trước với `lesson.html?id=` trên cùng dữ liệu đã lưu: giống nhau. Cập nhật <= 300 ms sau thao tác (debounce <= 150 ms); đoạn trống bị bỏ qua; tên trống hiện `(chưa có tiêu đề)`. Có nút phụ `Mở trang học sinh trong tab mới` (kèm nhãn `Bản đã lưu` để không nhầm).
- [AC-389]: **Bước 1 – nhãn và gợi ý.** Ô tên: nhãn `Tên bài học`, gợi ý theo bảng 2.10, đếm ký tự `n/200`; ô mô tả: nhãn `Mô tả ngắn`, `n/500`; mục `Địa chỉ bài (không bắt buộc)` (chỉ khi tạo mới) đặt trong `<details>` mặc định đóng với giải thích đầy đủ theo bảng 2.10. Nhãn là `<label for>` hiển thị; **placeholder chỉ là ví dụ** và không thay nhãn (mọi ô có `<label>` hiển thị; placeholder đạt tương phản >= 4,5:1).
- [AC-390]: **Trình soạn khối: loại khối suy ra từ nội dung.** Mỗi phần tử của mảng `summary/notes/conclusion` hiện thành một thẻ có chip loại: bắt đầu bằng ``` → `Đoạn mã`; bắt đầu bằng `- ` → `Mục danh sách`; còn lại `Đoạn văn`; chuỗi có dạng lạ (ví dụ ``` mở mà không đóng) → `Văn bản thô` với ô hiện **nguyên văn** (không ẩn ký hiệu). Với `Đoạn văn`: ô hiện nguyên văn; với `Mục danh sách`: ô **không** hiện tiền tố `- `; với `Đoạn mã`: ô chỉ hiện phần mã (phông mono), ẩn dòng mở ```ngôn-ngữ và dòng đóng. Phông chữ ô văn bản là `--font-ui` (không mono); chỉ `Đoạn mã` dùng mono.
- [AC-391]: **Khả nghịch dữ liệu (round-trip).** Với tập kiểm >= 10 mảng khối mẫu (đoạn văn; `- mục`; ```` ```python\nprint(1)\n``` ````; ```` ``` ```` không ngôn ngữ; mã có dòng trống đầu/cuối; mục danh sách liên tiếp; văn bản chứa `` `mã` `` trong dòng; chuỗi chỉ có khoảng trắng; ``` mở không đóng): nạp vào trình soạn rồi lưu **không sửa gì** → `JSON.stringify(payload.summary/notes/conclusion)` **bằng đúng** mảng gốc. Khi giáo viên sửa, định dạng sinh ra là: đoạn mã `"```" + ngônNgữ + "\n" + mã + "\n```"` (mặc định `python` cho khối mã mới), mục danh sách `"- " + chữ`, đoạn văn nguyên văn.
- [AC-392]: **Thanh công cụ định dạng theo khối.** Mỗi thẻ khối có `role="toolbar"` + `aria-label="Định dạng khối {n}"` với các nút **chữ + biểu tượng**, cao >= 44 px: `Gạch đầu dòng` (chuyển Đoạn văn ↔ Mục danh sách), `Mã trong dòng` (bọc phần đang chọn bằng dấu huyền; không chọn gì thì chèn cặp dấu huyền và đặt con trỏ giữa; nếu lựa chọn đã nằm trong cặp dấu huyền thì bỏ cặp), `Khối mã` (chuyển thành Đoạn mã, giữ chữ đang có làm nội dung mã). **Không có nút "In đậm"**; nút chỉ chèn cú pháp hiện có, không đổi `dom.js`. Chèn bằng `execCommand('insertText')` trước, dự phòng `setRangeText`, **giữ được Ctrl+Z** ở trình duyệt Chromium và phát sự kiện `input` (cập nhật nháp, xem trước, trạng thái "chưa lưu"). Mũi tên trái/phải, Home, End di chuyển giữa nút (roving tabindex); nút trong phần "Lưu ý" ẩn `Gạch đầu dòng` và `Khối mã` (phần này luôn là danh sách).
- [AC-393]: **Thêm khối theo loại.** Nút `Thêm khối mới` (biểu tượng + chữ) mở menu 3 lựa chọn `Đoạn văn`, `Mục danh sách`, `Đoạn mã` (`aria-haspopup`, Esc đóng, mũi tên chọn); khối mới được chèn ngay sau khối đang chọn (hoặc cuối), tiêu điểm vào ô của nó, tối đa 50 khối/phần như cũ (nút `disabled` có giải thích khi đầy). `Mục danh sách`: Enter trong ô tạo mục mới ngay dưới; Backspace khi ô trống xóa mục và đưa tiêu điểm lên mục trên. Các `Mục danh sách` liên tiếp được **nhóm trực quan** thành một thẻ `Danh sách` nhưng vẫn là các phần tử mảng riêng (QD-71).
- [AC-394]: **Di chuyển, xóa và hoàn tác.** Mỗi khối có nút `Lên`, `Xuống` (biểu tượng SVG + chữ, thay `↑`, `↓`), `Xóa khối`; nút đầu/cuối bị `disabled` có `aria-label` giải thích; `aria-label` nêu rõ khối (ví dụ `Xóa khối 2 của Tóm tắt`). Xóa khối hiện toast `Đã xóa khối 2` với nút `Hoàn tác` (tồn tại >= 10 giây; `toast.js` được thêm tham số hành động); Hoàn tác khôi phục đúng vị trí và nội dung. Khối cuối cùng của phần không xóa được (vẫn >= 1 khối như `min=1`).
- [AC-395]: **Khối trống không thành lỗi gây khó.** Khi lưu, các khối **hoàn toàn trống** (chỉ khoảng trắng) hiển thị lỗi tại khối như cũ nhưng kèm nút `Xóa các khối trống` trong tổng hợp lỗi (AC-401) để giáo viên không phải xóa từng khối; thông báo lỗi đổi thành lời thường: `Khối {n} của {phần} đang để trống. Hãy nhập nội dung hoặc xóa khối này.`
- [AC-396]: **Điền mẫu.** Mỗi phần (bước 1, `Tóm tắt`, `Những điểm cần lưu ý`, `Tổng kết`) có nút `Điền mẫu` (biểu tượng + chữ) điền văn bản mẫu từ `samples.js` vào ô trên màn hình, **không gọi API**, không tự lưu; nếu phần đang có chữ: hộp thoại xác nhận `Thay nội dung hiện tại bằng bản mẫu?`; sau khi điền hiện toast `Đã điền mẫu` với nút `Hoàn tác điền mẫu` khôi phục đúng giá trị trước đó. Văn bản mẫu: tiếng Việt, chung chung, **không chứa** 5 tiêu đề cấm ở QD-75, có đủ ba dạng (đoạn văn, danh sách, đoạn mã Python hợp lệ). Nếu giáo viên xuất bản khi một phần **vẫn bằng đúng bản mẫu** thì danh sách kiểm tra ghi `Phần ... vẫn là bản mẫu` (AC-398).
- [AC-397]: **Bước 3 – Bài tập.** Bài học đã lưu: danh sách thẻ bài tập (số thứ tự, tên liên kết, huy hiệu độ khó, chip trạng thái, dòng `n bộ kiểm tra (m ẩn), n lượt giao bài, n bài nộp`), nút chính `Soạn` và `Xem trước`, **menu `Thêm`** chứa `Xuất bản/Chuyển về bản nháp`, `Cất đi (lưu trữ)/Khôi phục`, `Xóa`, `Lên`, `Xuống` (hộp thoại xác nhận cho Xóa như cũ, chữ cũ giữ nguyên: tiêu đề `Xóa bài tập?`), nút nổi bật `Thêm bài tập`; trạng thái rỗng giải thích `Bài học chưa có bài tập. Mỗi bài tập gồm đề bài, ví dụ và bộ kiểm tra chấm bài.` Bài học **chưa lưu** (tạo mới): thay danh sách bằng đoạn giải thích `Hãy lưu bài học trước, sau đó bạn mới thêm được bài tập.` và nút `Lưu bài học và thêm bài tập` (validate, `POST` như cũ rồi `location.replace` tới trang sửa với `&created=1&step=3`) (QD-78).
- [AC-398]: **Bước 4 – danh sách kiểm tra trước khi xuất bản.** `<ul>` có tiêu đề `Kiểm tra trước khi xuất bản`, mô tả một câu, mỗi mục có biểu tượng (dấu tích / cảnh báo) **và chữ** `Đạt`/`Còn thiếu`: (1) có tên bài học; (2) có mô tả ngắn; (3) `Tóm tắt` có ít nhất 1 đoạn có chữ; (4) `Những điểm cần lưu ý` có ít nhất 1 mục có chữ; (5) `Tổng kết` có ít nhất 1 đoạn có chữ; (6) có ít nhất 1 bài tập; (7) mỗi bài tập có ít nhất 3 bộ kiểm tra (theo `testCases.length` của `GET /api/teacher/lessons/{id}`); (8) không phần nào còn là bản mẫu nguyên văn; (9, gợi ý, không tính vào "đủ") có ít nhất một bài tập đã xuất bản. Mục `Còn thiếu` có nút `Đi tới` đưa sang đúng bước và **đặt tiêu điểm vào ô/khối thiếu**. Cập nhật tức thời khi dữ liệu đổi.
- [AC-399]: **Nút xuất bản và trạng thái đủ/chưa đủ.** Bài chưa xuất bản: nút `btn-publish` ghi `Xuất bản cho học sinh`; bên cạnh là huy hiệu `Đủ điều kiện` hoặc `Còn {k} mục chưa đủ` (chữ + biểu tượng). Có thay đổi chưa lưu → nút ghi `Lưu và xuất bản`. **Không bị `disabled`** vì thiếu mục (QD-76): nếu `k > 0` thì mở hộp thoại xác nhận liệt kê mục thiếu với nút `Vẫn xuất bản` / `Quay lại sửa`. Bài đã xuất bản: nút ghi `Chuyển về bản nháp` và giữ hộp thoại xác nhận cũ khi bài đang được giao (tiêu đề `Ẩn bài học đang được giao?`, nội dung cũ). Có đoạn giải thích trạng thái theo bảng 2.10. Lỗi máy chủ khi xuất bản hiển thị đúng thông điệp (QD-76). Khi tạo mới chưa lưu: hiện giải thích `Lưu bài học trước, rồi mới xuất bản được.` thay vì ẩn nút.
- [AC-400]: **Thanh hành động dính.** Một thanh `position: sticky; bottom: 0` (có `safe-area-inset-bottom`, nền không trong suốt, `z-index` thấp hơn hộp thoại và ngăn kéo) luôn hiện trên mọi bước với: dòng trạng thái lưu (AC-402), `Bước trước`, `Bước sau`, và nút chính `Lưu bài học` (`id="btn-save"`). Thanh không che ô đang focus (dùng `scroll-padding-bottom` >= chiều cao thanh); không vượt quá 72 px cao ở 360 px.
- [AC-401]: **Lỗi tại ô và tổng hợp ở đầu.** Khi kiểm tra phía trình duyệt hoặc máy chủ trả 422: (a) mỗi ô lỗi giữ `aria-invalid="true"`, `aria-describedby` và thông báo lỗi bên dưới (như cũ, đã viết lại lời thường); (b) xuất hiện hộp `role="alert"` ở đầu vùng nội dung: `Có {n} chỗ cần sửa` + danh sách liên kết `Bước 2, Tóm tắt, khối 2: ...` bấm vào chuyển bước và đặt tiêu điểm vào ô; (c) tiêu điểm đầu tiên vào hộp tổng hợp; (d) bước chứa lỗi hiện `Có lỗi` (AC-385). Lỗi mạng/5xx giữ nguyên nội dung và hiện `Chưa lưu được ... hãy bấm "Lưu bài học" để thử lại` (chuỗi cũ giữ nguyên).
- [AC-402]: **Chỉ báo lưu.** Một vùng `role="status"` hiển thị đúng một trong các câu: `Chưa có thay đổi`, `Có thay đổi chưa lưu`, `Đã tự lưu tạm trên máy lúc hh:mm` (sau khi `formDraft.save` chạy), `Đang lưu…`, `Đã lưu bài học lúc hh:mm` (sau `PUT/POST` thành công), `Lưu chưa được, hãy thử lại`; `hh:mm` là giờ địa phương 24 giờ. Có `aria-live="polite"` nhưng không đọc lại mỗi lần gõ (chỉ đổi giữa các trạng thái).
- [AC-403]: **Phím tắt Ctrl+S.** Ctrl+S (và Cmd+S) trong trang soạn bài học: gọi cùng hàm `save()` như nút `Lưu bài học`, `preventDefault()` (không hiện hộp thoại lưu trang), hoạt động khi tiêu điểm đang ở trong `textarea`/`input`; bị bỏ qua khi hộp thoại đang mở hoặc đang lưu (không gửi 2 yêu cầu); trang hiển thị gợi ý `Mẹo: nhấn Ctrl+S để lưu` (chữ, cạnh thanh lưu, ẩn trên thiết bị cảm ứng).
- [AC-404]: **Cảnh báo rời trang, nháp và khôi phục giữ nguyên.** `guardUnsaved` vẫn hỏi khi đóng/tải lại với thay đổi chưa lưu; `formDraft` vẫn lưu nháp 500 ms sau thay đổi và có băng rôn khôi phục (đổi lời thường: `Có bản soạn dở chưa lưu từ lần trước. Bạn muốn khôi phục không?` với nút `Khôi phục` / `Bỏ qua`); khóa nháp không đổi (`lesson:<id|new>`); lưu thành công xóa nháp.
- [AC-405]: **Xung đột phiên bản giữ logic, đổi lời.** Khi `PUT` trả `CONFLICT`: hộp thoại tiêu đề `Bài học vừa được lưu ở nơi khác`, nội dung giải thích 2 câu (`Có người, hoặc một tab khác của bạn, vừa lưu bản mới. Bạn muốn làm gì?`), nút `Tải lại bản mới (bỏ thay đổi của tôi)` và `Giữ bản của tôi`; chọn "Giữ" → lấy `version` mới (`GET /api/teacher/lessons/{id}`), giữ nguyên nội dung đang soạn, toast `Đã lấy bản mới nhất. Bấm "Lưu bài học" lần nữa để ghi đè.` Hành vi đúng như cũ (không tự lưu đè).
- [AC-406]: **Cảnh báo bài đã giao giữ nguyên.** Bài có `assignmentCount > 0` hoặc `submissionCount > 0` luôn hiện băng rôn cảnh báo ở đầu mọi bước (nội dung cũ, thay `lượt giao` → `lượt giao bài` cho rõ) và vẫn hỏi xác nhận `Lưu thay đổi vào bài đã giao?` (nút `Vẫn lưu`) trước khi `PUT`.
- [AC-407]: **Tạo mới có hướng dẫn.** Ở `lesson-edit.html` không có `id`: bước 1 hiển thị đoạn dẫn nhập `Bạn đang tạo bài học mới. Điền tên và mô tả, rồi sang bước 2 để viết nội dung.`; sau khi tạo thành công vẫn hiện toast cũ `Đã tạo bài học (bản nháp). Hãy thêm bài tập rồi xuất bản.`
- [AC-408]: **Bàn phím cho mọi thao tác soạn bài học.** Hoàn tất một bài (nhập tên, mô tả, thêm/sửa/xóa/di chuyển khối, chèn định dạng, điền mẫu, chuyển bước, lưu, xuất bản) **chỉ bằng bàn phím**; thứ tự Tab logic (thanh bước → nội dung → thanh lưu); vòng focus nhìn thấy (>= 3:1) mọi nút; sau khi thêm/xóa/di chuyển khối, tiêu điểm đặt hợp lý (khối mới, khối kế cận); mọi nút đều có tên truy cập duy nhất nêu khối và phần.

### 3.8 Soạn bài tập

- [AC-409]: **Id bất biến.** `exercise-edit.html` giữ các id: `page-title`, `status`, `content`, `title`, `difficulty`, `statement`, `inputFormat`, `outputFormat`, `constraints`, `starterCode`, `solutionCode`, `solutionExplanation`, `timeLimitMs`, `exercise-id`, `confirmEveryTime`, `examples`, `testCases`, `btn-save`, `btn-check`, `btn-publish`; id ô con của danh sách giữ dạng `${id}-${i}-${key}` (`examples-0-input`, `testCases-2-expectedOutput`, `testCases-1-isHidden`) và thuộc tính `data-col`.
- [AC-410]: **Thanh bước và cơ chế chung.** Dùng lại thành phần của `editor-kit.js` (thanh bước dính, thanh lưu dính, chỉ báo lưu, Ctrl+S, tổng hợp lỗi, nháp, khôi phục, xung đột, cảnh báo bài đã giao) với 5 bước đúng tên `1. Đề bài`, `2. Ví dụ`, `3. Bộ kiểm tra chấm bài`, `4. Đáp án và mã khởi tạo`, `5. Nâng cao và xuất bản`; **các AC-384, AC-385 (điều kiện riêng ở AC-419), AC-400 đến AC-406 áp dụng tương ứng cho trang này** (chuỗi `Lưu bài tập`, `Bài tập vừa được lưu ở nơi khác`, khóa nháp `exercise:<id|new:<lessonId>>`, tiêu đề xung đột `Bài tập vừa được lưu ở nơi khác`). Mỗi bước có tiêu đề, một câu "Mục này để làm gì" và ví dụ minh họa (AC-386).
- [AC-411]: **Bước 1 – Đề bài.** Thứ tự và nhãn: `Tên bài tập`, `Độ khó` (`Dễ`/`Trung bình`/`Khó`, cùng giá trị `Easy/Medium/Hard`), `Nội dung đề bài`, `Mô tả dữ liệu vào`, `Mô tả kết quả cần in ra`, `Ràng buộc (không bắt buộc)`; mỗi ô có nhãn hiển thị, gợi ý một câu và placeholder ví dụ (không thay nhãn). Ô `Nội dung đề bài` có thanh công cụ: `Mã trong dòng` (như AC-392) và `Gạch đầu dòng` (đặt `- ` đầu **mỗi dòng đang chọn**, vì `renderText` yêu cầu mọi dòng của đoạn đều bắt đầu `- `), kèm gợi ý `Cách đoạn bằng một dòng trống`. Phông ô là `--font-ui`. Có nút `Điền mẫu` cho bước.
- [AC-412]: **Bước 2 – Ví dụ.** Mỗi ví dụ là thẻ trực quan gồm `Vào`, `Ra`, `Giải thích (không bắt buộc)` (nhãn hiển thị, phông mono cho Vào/Ra), có tiêu đề `Ví dụ n`; tối thiểu 1, tối đa 10 (nút `Thêm ví dụ` `disabled` khi đủ, có giải thích); khung `Học sinh sẽ thấy` bên dưới hiển thị thẻ ví dụ bằng các class `example*` của trang làm bài; mỗi thẻ có nút `Thêm ví dụ này vào bộ kiểm tra` (tạo một bộ kiểm tra mới với cùng dữ liệu vào và kết quả đúng, mặc định không ẩn; chỉ thao tác trên màn hình).
- [AC-413]: **Bước 3 – Bộ kiểm tra chấm bài (bảng).** Tiêu đề `h2` `Bộ kiểm tra chấm bài`, giải thích theo bảng 2.10 (nêu rõ so sánh kết quả đúng với kết quả học sinh in ra, **đúng theo hành vi thật của `classify()` trong `grader.js`**; Dev đọc `grader.js` và nêu chính xác việc xử lý khoảng trắng/xuống dòng thừa, QA đối chiếu). Bảng cột `#`, `Dữ liệu vào (học sinh nhập)`, `Kết quả đúng (in ra)`, `Kiểm tra ẩn`, `Hành động`; dưới 700 px mỗi bộ thành một thẻ xếp dọc. `<table>` có `caption` (có thể `sr-only`) và `th scope`. Bộ đếm `n bộ kiểm tra (cần 3 đến 50), m ẩn` cập nhật tức thời; nút `Thêm bộ kiểm tra`, mỗi dòng `Nhân đôi`, `Xóa`, `Lên`, `Xuống` (biểu tượng + chữ).
- [AC-414]: **Công tắc ẩn bằng chữ.** Cột `Kiểm tra ẩn` là `input[type=checkbox][role=switch]` (id `testCases-i-isHidden`) kèm chữ đổi theo trạng thái: tắt `Học sinh thấy`, bật `Ẩn: học sinh không thấy` (biểu tượng mắt / mắt gạch); không chỉ dựa vào màu; vùng bấm >= 44 px. Cảnh báo chất lượng (chưa có bộ ẩn, ít hơn 5 bộ) hiện **thành danh sách riêng từng ý** (`<ul>`), không gộp một đoạn, lời thường (`Chưa có bộ kiểm tra nào ẩn: học sinh sẽ thấy toàn bộ. Nên ẩn vài bộ, nhất là trường hợp đặc biệt.`); không chặn lưu như cũ.
- [AC-415]: **"Chạy thử với đáp án".** Nút `btn-check` ghi `Chạy thử với đáp án` nằm ở bước 3 (và nhắc lại ở bước 4), **tái dùng nguyên** `checkSolution()` (nạp `PyRunner` lười, chạy trên trình duyệt, không gửi gì lên máy chủ). Chưa có đáp án → thông báo `Hãy nhập Đáp án mẫu ở bước 4 trước.` kèm nút `Đi tới bước 4`. Kết quả hiển thị ngay dưới nút: tóm tắt `Đạt {x}/{y} bộ kiểm tra` (biểu tượng + chữ), mỗi bộ một dòng `Bộ kiểm tra n (ẩn)` + trạng thái + thời gian, bộ không đạt mở rộng `Kết quả đúng` / `Kết quả của đáp án` (không còn thuật ngữ `test`). Không sinh yêu cầu mạng tới `/api` (ngoài tải Pyodide đã có).
- [AC-416]: **Bước 4 – Đáp án và mã khởi tạo.** Thứ tự: `Khung mã cho học sinh (không bắt buộc)`, `Đáp án mẫu (không bắt buộc)`, `Giải thích đáp án`; ô mã dùng phông mono, `spellcheck=false`, gợi ý theo bảng 2.10; nhắc `Học sinh chỉ thấy đáp án sau khi bấm nút Đáp án và xác nhận.` (đúng luồng AC-37). Có `Điền mẫu` cho bước.
- [AC-417]: **Bước 5 – Nâng cao (thu gọn) và xuất bản.** `<details>` `Cấu hình nâng cao` **mặc định đóng** chứa `Thời gian tối đa cho mỗi bộ kiểm tra (mili giây)` (số, 500 đến 5000, mặc định 5000, hint `5000 mili giây bằng 5 giây`), công tắc có chữ `Luôn hỏi lại trước khi học sinh xem đáp án` (id `confirmEveryTime`), và (khi tạo mới) `Địa chỉ bài tập (không bắt buộc)`. Bên ngoài `details`: danh sách kiểm tra (AC-419) và nút `Xuất bản cho học sinh`/`Chuyển về bản nháp` (id `btn-publish`) với mọi hành vi tương tự AC-399 (hộp thoại `Ẩn bài tập đang được giao?` giữ nguyên chuỗi cũ). Nếu `details` chứa ô lỗi thì tự mở khi có lỗi.
- [AC-418]: **Dữ liệu gửi đi không đổi.** Với cùng nội dung nhập, thân `POST /api/teacher/lessons/{id}/exercises`, `PUT /api/teacher/exercises/{id}`, `POST/PUT /api/teacher/lessons…` (bài học, mục 3.7) **bằng đúng từng khóa và giá trị** so với phiên bản trước thay đổi (so sánh JSON): ví dụ `examples[].explanation` là `null` khi trống, `timeLimitMs` số, `testCases[].isHidden` boolean, `version` chỉ có khi sửa, `id` chỉ có khi tạo; số yêu cầu mạng khi lưu không đổi (đúng 1).
- [AC-419]: **Chỉ báo hoàn thành và danh sách kiểm tra của bài tập.** Điều kiện `Hoàn thành`: bước 1: có tên, đề bài, mô tả dữ liệu vào, mô tả kết quả ra; bước 2: >= 1 ví dụ có `Vào` và `Ra` không trống; bước 3: >= 3 bộ kiểm tra mỗi bộ có kết quả đúng không trống; bước 4: có đáp án mẫu **và** giải thích (hoặc để trống cả hai là `Chưa xong` nhưng không phải lỗi; vì đáp án không bắt buộc, bước này `Hoàn thành` khi cả hai trống hoặc cả hai có, `Chưa xong` khi chỉ có đáp án thiếu giải thích); bước 5: thời gian trong 500 đến 5000. Danh sách kiểm tra gồm các mục tương ứng + `Có ít nhất 1 bộ kiểm tra ẩn` (gợi ý) + `Đáp án mẫu chạy đạt mọi bộ kiểm tra` (chỉ khi đã bấm `Chạy thử với đáp án` trong phiên; chưa chạy → `Chưa kiểm tra`), mỗi mục `Đạt/Còn thiếu` + nút `Đi tới` đặt tiêu điểm vào ô thiếu.
- [AC-420]: **Lỗi và nhãn của bài tập đã viết lại lời thường.** Ví dụ: `Hãy nhập tên bài tập.`, `Hãy nhập nội dung đề bài.`, `Hãy cho biết dữ liệu vào gồm những gì.`, `Thời gian phải từ 500 đến 5000 mili giây.`, `Cần ít nhất 1 ví dụ.`, `Cần ít nhất 3 bộ kiểm tra.`, `Tối đa 50 bộ kiểm tra.`; thông điệp từ máy chủ hiển thị nguyên văn. Lỗi `examples[i].field`/`testCases[i].field` của máy chủ vẫn gắn đúng ô (hàm `mapServerErrors` giữ).

### 3.9 Danh sách bài học của giáo viên

- [AC-421]: **Đầu trang rõ ràng.** `teacher/index.html` giữ `<title>Bài học của tôi – DK Edu</title>`; có lời dẫn một câu và nút `Tạo bài học` (`btn--primary`, biểu tượng dấu cộng SVG, `href="/teacher/lesson-edit.html"`) đặt đầu trang, to hơn nút phụ; ô tìm `Tìm theo tên bài học` (id `search`).
- [AC-422]: **Tab lọc.** `role="tablist"` với 4 tab `Tất cả`, `Bản nháp`, `Đã xuất bản`, `Lưu trữ`, mỗi tab kèm số lượng (ví dụ `Bản nháp (2)`); mặc định `Tất cả` (gồm cả lưu trữ, QD-79); `Bản nháp` = không xuất bản và không lưu trữ; `Đã xuất bản` = xuất bản và không lưu trữ; `Lưu trữ` = `isArchived`; mũi tên chuyển tab; kết hợp với ô tìm kiếm (debounce 150 ms); số lượng luôn tính trên toàn bộ danh sách, không theo ô tìm kiếm. Bỏ `select#filter` cũ (id `filter` bỏ, ghi ở mục 11).
- [AC-423]: **Hàng bài học dễ quét.** Mỗi bài (`.lesson-row`, `data-lesson`, tiêu đề là `h3 > a`) gồm: số thứ tự, tên (liên kết tới trang soạn), mô tả cắt 2 dòng, chip trạng thái (chữ + biểu tượng), `n bài tập (m đã xuất bản)`, `n lượt giao bài`, `Cập nhật dd/MM/yyyy HH:mm` (mono); bài `Lưu trữ` làm mờ nhưng vẫn đạt tương phản >= 4,5:1. Bỏ dòng `Tạo bởi ...` khỏi dòng chính (có thể đưa vào `title`).
- [AC-424]: **Hành động chính và menu "Thêm".** Mỗi hàng chỉ có 3 hành động trực tiếp, cao >= 44 px: `Soạn` (nút chính), `Xem trước`, `Giao bài` (`/teacher/assignments.html?lesson=`); và một nút `Thêm` (`aria-haspopup="menu"`, `aria-expanded`) mở menu `role="menu"` gồm `Xuất bản cho học sinh`/`Chuyển về bản nháp`, `Cất đi (lưu trữ)`/`Khôi phục`, `Đưa lên`, `Đưa xuống`, `Xóa bài học` (kiểu nguy hiểm, ngăn cách bằng đường kẻ). Mũi tên lên/xuống di chuyển, Enter/Space chọn, Esc đóng và trả tiêu điểm về nút `Thêm`; bấm ngoài đóng; chỉ một menu mở cùng lúc.
- [AC-425]: **Hộp thoại xác nhận giữ nguyên.** `Xóa bài học?` (danger, chữ cũ), `Lưu trữ bài học?`/`Khôi phục bài học?`, `Ẩn bài học đang được giao?` giữ nguyên tiêu đề và nội dung, vẫn trả tiêu điểm cho nút `Thêm` khi đóng; đổi thứ tự dùng cùng `api.reorderLessons` với cùng thuật toán hoán đổi láng giềng trong danh sách đang hiển thị; thông báo thành công qua toast (như cũ).
- [AC-426]: **Tiêu điểm sau hành động.** Sau `reload(focusId)` tiêu điểm trở về liên kết tiêu đề `[data-lesson="id"] h3 a` như cũ; sau `Xóa` đưa tiêu điểm vào nút `Tạo bài học`.
- [AC-427]: **Trạng thái rỗng hướng dẫn.** Chưa có bài học: `stateBox kind=empty` với tiêu đề `Bắt đầu soạn bài đầu tiên` và danh sách ba bước có số: `1. Tạo bài học và viết nội dung`, `2. Thêm bài tập và bộ kiểm tra chấm bài`, `3. Xuất bản rồi giao bài cho học sinh`, kèm nút `Tạo bài học`. Có bài nhưng bộ lọc rỗng: `Không có bài học phù hợp` + nút `Xem tất cả`. Đang tải: skeleton `rows`; lỗi: `Thử lại`.
- [AC-428]: **Đáp ứng.** >= 900 px hàng chia cột (thông tin | số liệu | hành động); dưới 900 px xếp dọc, hành động xuống dưới; không cuộn ngang ở 360 px.
- [AC-429]: **Hành vi dữ liệu không đổi.** Cùng bộ API: `GET /api/teacher/lessons?includeArchived=true`, `PATCH publish/archive`, `DELETE`, `POST reorder` với cùng thân; thứ tự `order` hiển thị không đổi.

### 3.10 Ngôn ngữ và nhãn

- [AC-430]: **Bảng thuật ngữ được áp dụng.** Trong `teacher/lesson-edit.js`, `exercise-edit.js`, `blocks.js`, `index.js` và các tệp mới của khu soạn bài, **không còn** chuỗi hiển thị chứa (không phân biệt hoa/thường): `test case`, `Input`, `Output` (làm nhãn), `Đầu ra mong đợi`, `Mã khởi tạo`, `Mã bài học`, `Bộ test`, `Xung đột`, `stdin`, `khối` (như danh từ không giải thích) ngoài những chỗ bảng 2.10 cho phép; kiểm bằng quét chuỗi nguồn và DOM.
- [AC-431]: **Mọi ô có nhãn, gợi ý, ví dụ.** Mỗi `input/select/textarea` trong hai trình soạn có `<label for>` hiển thị (không chỉ `aria-label`), một dòng gợi ý dưới ô liên kết bằng `aria-describedby` (trừ ô tên và ô tìm kiếm), và với các ô văn bản dài một ví dụ (placeholder hoặc hộp `Ví dụ`); placeholder không bao giờ là nguồn duy nhất của ý nghĩa ô; placeholder tương phản >= 4,5:1.
- [AC-432]: **Câu giải thích ngắn, thường.** Mỗi "Mục này để làm gì" <= 25 từ; mọi gợi ý <= 30 từ; câu dùng ngôi thứ hai hoặc mệnh lệnh nhẹ; không dùng từ viết tắt tiếng Anh ngoài `Python`, `CSV`, `Ctrl+S`.
- [AC-433]: **Nút có hành động rõ.** Tên nút là cụm động từ (`Lưu bài học`, `Thêm bài tập`, `Xuất bản cho học sinh`, `Thêm khối mới`, `Chạy thử với đáp án`, `Điền mẫu`); các ký hiệu `↑ ↓ ← → +` đứng làm chữ trong nút mới/đã sửa được thay bằng biểu tượng SVG (`icon()`) + chữ; mọi biểu tượng `aria-hidden="true"`.
- [AC-434]: **Chuỗi mới sạch.** Mọi chuỗi mới/sửa ở v5: tiếng Việt, **không em-dash**, **không emoji**, không dùng dấu gạch làm dấu ngắt câu (en-dash chỉ trong `<title>` theo quy ước cũ); quét `wwwroot/**/*.{html,js,css}` theo AC-313 vẫn đạt (không có ký tự trong khối emoji, không U+2014).
- [AC-435]: **Chuỗi cố định v4 vẫn còn.** Các nhãn điều hướng, `Đang đăng nhập…`, `Chế độ xem trước – kết quả sẽ không được lưu`, `Gửi lại kết quả` và các chuỗi trong mục 0.3 v4 vẫn xuất hiện nguyên văn ở trang tương ứng. Những chuỗi bị thay có chủ đích ở khu soạn bài chỉ gồm các dòng trong bảng 2.10 và mục 11.
- [AC-436]: **Không lộ ký hiệu cho giáo viên.** Trong bước 2 của bài học không có đoạn chữ nào hướng dẫn gõ `- ` hay ba dấu huyền; hướng dẫn thay bằng tên nút (`Gạch đầu dòng`, `Mã trong dòng`, `Khối mã`). Chỉ phần `Văn bản thô` (ca lạ) được phép nhắc ký hiệu.

### 3.11 Truy cập

- [AC-437]: **Bàn phím toàn bộ thành phần mới.** Sidebar, rail, ngăn kéo, menu người dùng, tìm nhanh, tab (landing, soạn bài, danh sách), accordion, menu `Thêm`, menu `Thêm khối mới`, thanh công cụ, công tắc, thanh bước thao tác đủ bằng bàn phím theo mẫu ARIA APG tương ứng; Esc đóng lớp trên cùng và trả tiêu điểm; không bẫy tiêu điểm ngoài hộp thoại/ngăn kéo.
- [AC-438]: **Vòng focus và tên truy cập.** Mọi phần tử tương tác có vòng focus nhìn thấy (>= 3:1 với nền kề, >= 2 px) ở hai giao diện; mọi nút/liên kết chỉ biểu tượng có tên truy cập; tên truy cập phải chứa chữ nhìn thấy (WCAG 2.5.3).
- [AC-439]: **Tương phản, cỡ chữ, vùng chạm.** Chữ >= 16 px (QD-60), vùng chạm >= 44 x 44 px, tương phản chữ >= 4,5:1 (chữ lớn >= 3:1) và thành phần giao diện >= 3:1, ở **sáng và tối**, trên landing, shell, hai dashboard, hai trình soạn, danh sách; `forced-colors: active` giữ viền; thu phóng 200% không mất chức năng, không cuộn ngang trang.
- [AC-440]: **Vùng thông báo.** Trạng thái lưu và toast dùng `aria-live="polite"`; lỗi tổng hợp `role="alert"`; skeleton có dòng `sr-only role="status"`; không phát lại thông báo khi gõ; thay đổi sáng/tối vẫn phát `Đã chuyển sang giao diện …` (v4).
- [AC-441]: **axe-core.** Chạy axe-core trên 15 trang HTML (landing, login, 2 dashboard, 12 trang còn lại tùy role) ở 360 px và 1280 px, **hai giao diện**: 0 vi phạm `serious`/`critical`; mọi `img` có `alt`; mọi `section` có tên; không `aria-hidden` bao phần tử có thể focus; ngăn kéo/hộp thoại đóng thì `inert` hoặc `hidden`.
- [AC-442]: **Hộp thoại xác nhận cho hành động phá hủy giữ nguyên.** Xóa bài học, xóa bài tập, lưu trữ, ẩn bài đang giao, lưu vào bài đã giao đều vẫn đòi xác nhận bằng `confirmDialog` (nút nguy hiểm `btn--danger` cho Xóa) với `opener` để trả tiêu điểm; xóa **khối nội dung** dùng hoàn tác (AC-394) thay cho xác nhận (khối chưa lưu lên máy chủ).
- [AC-443]: **Ngôn ngữ và mã hóa.** Mọi trang mới `<html lang="vi">`, `<meta charset="utf-8">`; tiếng Việt có dấu hiển thị đúng ở cả hai phông (Be Vietnam Pro, JetBrains Mono có subset `vietnamese`).
- [AC-444]: **Thứ tự đọc và tiêu đề.** Mỗi trang đúng một `h1`; heading không nhảy cấp; thứ tự DOM khớp thứ tự hiển thị (sidebar → topbar → nội dung; xem trước sau biểu mẫu); trên di động tab `Soạn` đứng trước `Xem trước`.

### 3.12 Chuyển động

- [AC-445]: **Reduced-motion toàn bộ.** Với `prefers-reduced-motion: reduce` (kể cả đổi khi đang mở trang): không hiệu ứng gõ mã, không reveal, không parallax, không vẽ đường nối, ngăn kéo/menu/hộp thoại mở tức thời (fade <= 1 ms hoặc không), vòng/thanh biểu đồ hiện giá trị cuối ngay; không mất thông tin nào; `scroll-behavior: auto`.
- [AC-446]: **Chỉ transform/opacity (QD-46).** Hiệu ứng mới (sidebar/rail, ngăn kéo, reveal, timeline, biểu đồ, tab, accordion, menu) chỉ animate `transform`, `opacity`, `stroke-dashoffset`; thu gọn rail đổi bề rộng cột bằng **thao tác tức thời hoặc `transform`** (không `transition` trên `width`, `grid-template-columns`, `margin`, `padding`); accordion dùng `<details>` không animate chiều cao (hoặc `grid-template-rows` bị cấm); quét CSS theo QD-46 đạt.
- [AC-447]: **Không vòng lặp vô hạn, không setInterval.** Không `infinite`/`setInterval` mới; hiệu ứng gõ mã dùng chuỗi `requestAnimationFrame` hữu hạn <= 4 giây (QD-81); skeleton tối đa 3 lượt (QD-47).
- [AC-448]: **Dọn dẹp.** `destroyFx()` và hủy rAF/observer chạy ở `pagehide`; không rò rỉ listener `resize`/`matchMedia` khi chuyển giữa chế độ drawer và rail.
- [AC-449]: **Chuyển cảnh trang.** `@view-transition { navigation: auto }` (v4) vẫn hoạt động giữa các trang shell; sidebar và topbar có `view-transition-name` cố định (`app-sidebar`, `app-topbar`) để không nhấp nháy khi đổi trang; nội dung chính chuyển mờ <= 200 ms; tắt khi reduced-motion.
- [AC-450]: **Hiệu ứng thẻ.** Spotlight/nghiêng chỉ trên thiết bị `hover: hover` và `pointer: fine`, dùng lại `fx.enableCardEffects`; thẻ KPI và thẻ bài học có hiệu ứng nhẹ; thiết bị cảm ứng không có.

### 3.13 Hồi quy, kỹ thuật, tài liệu

- [AC-451]: **API và luồng nghiệp vụ không đổi.** So với trước v5, bốn luồng ở AC-309 (đăng nhập → học → nộp → đăng xuất; xem đáp án; giáo viên soạn → giao → xem tiến độ; đổi mật khẩu) phát ra **cùng tập yêu cầu mạng** (cùng phương thức, đường dẫn, thân), trừ: (a) `GET /api/me/assignments` và `GET /api/lessons` trên `dashboard.html`, (b) 3 yêu cầu của dashboard giáo viên (AC-367), (c) yêu cầu tìm nhanh ở lần mở đầu (AC-352). Không endpoint nào được thêm vào `src/Web/Endpoints` (kiểm bằng diff).
- [AC-452]: **Bất biến id/class.** Script đối chiếu trên DOM đã dựng của các trang cũ xác nhận mọi id/class ở mục 0.3 (v4 và v5) vẫn xuất hiện với nghĩa cũ (ví dụ `#btn-run`, `#stdin`, `.lesson-card`, `.testitem`, `.chip--passed`, `.table-wrap`, `#btn-save`, `#btn-publish`, `#btn-check`, `.block-item`, `.lesson-row`, `#site-nav`, `#btn-logout`). Id/class **bị bỏ có chủ đích** chỉ gồm những mục nêu ở mục 11.
- [AC-453]: **Không `innerHTML` và an toàn XSS.** Không `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `eval(`, `new Function(` trong `wwwroot/js`; dữ liệu từ API (tên bài, mô tả, tên lớp, tên học sinh, tên người dùng, nội dung khối) hiển thị bằng `textContent`; kiểm bằng dữ liệu `<img src=x onerror=alert(1)>`, `<script>alert(1)</script>` tại tên người dùng, tên bài học, tên bài tập, tên lớp, nội dung khối, kết quả tìm nhanh, chú thích biểu đồ: không `alert`, không phần tử HTML sinh ra.
- [AC-454]: **Lưu trữ trình duyệt.** `localStorage`/`sessionStorage` chỉ xuất hiện trong `storage.js` và `theme-init.js`; khóa mới duy nhất: `th12.sidebar` (không `userId`); nháp soạn bài giữ `formDraft` theo `userId`; `storage.js` vẫn không chứa `password|token|csrf|secret`; không lưu dữ liệu bài học nhạy cảm ngoài nháp hiện có.
- [AC-455]: **Ngân sách tệp (AC-299, QD-86).** Tổng `*.js`+`*.css` tự viết trong `wwwroot` <= 500 KB; `/img` chỉ chứa PNG (landing không thêm ảnh khác, minh họa bằng DOM/SVG trong JS); không thêm font; `theme-init.js` <= 1,5 KB; Dev báo kích thước trước và sau.
- [AC-456]: **Không script nội dòng, không phụ thuộc ngoài.** Mọi HTML mới tuân AC-305: chỉ `<script src="/js/…">` cùng nguồn, nạp `theme-init.js` đồng bộ trước `style.css`, đúng một module điểm vào (`landing.js`, `dashboard.js`, `teacher/dashboard.js`), không CDN, không `@import`.
- [AC-457]: **Ba tệp HTML mới giữ quy ước head.** `<meta name="color-scheme" content="light dark">`, hai `theme-color`, favicon và apple-touch-icon đúng hai dòng `<link>` hiện có, `href="/css/style.css"`, preload font; `landing.html` có thêm các thẻ SEO (AC-339).
- [AC-458]: **Console sạch.** Trên 15 trang, theo các luồng AC-451 và ở hai giao diện, 0 `console.error`/`Uncaught`/`Failed to load resource`, kể cả khi không hỗ trợ View Transitions/`animation-timeline`/`backdrop-filter`, `localStorage` bị chặn, `prefers-reduced-motion` bật, thiết bị cảm ứng; `/api` trả lỗi 500/mạng chỉ gây trạng thái lỗi tại chỗ, không `Uncaught`.
- [AC-459]: **Đơn vị viewport và CLS.** Chiều cao theo viewport dùng `dvh` kèm dự phòng `vh` ngay trước (AC-303); CLS < 0,1 trên landing, hai dashboard, hai trình soạn, danh sách tại 360 và 1280 px ("Fast 4G"); skeleton khớp bố cục.
- [AC-460]: **Hiệu năng dashboard.** Mỗi dashboard tương tác được <= 2 s ở 10 Mbps với dữ liệu 13 bài học, 50 giao bài, 200 học sinh; render không dùng vòng lặp tốn kém (O(n) theo số giao bài); các yêu cầu chạy song song (không nối tiếp).
- [AC-461]: **Hồi quy đủ hai giao diện.** Chạy lại bộ kiểm thử chức năng của AC-316 (v2/v3/v4) ở giao diện sáng và tối, 360/768/1280/1920 px, với route mới; các AC cũ không nêu ở mục 11 đều đạt.
- [AC-462]: **Test hiện có được cập nhật (bởi Khách hàng/Dev) và pass.** Các test ở mục 8 sửa đúng danh sách; `dotnet build D:\Education\src\TinHoc12.sln` 0 lỗi; `dotnet test` pass 100%; bổ sung test mới cho AC-319 đến AC-325 (định tuyến, `ReturnUrlPolicy`) và test tĩnh cho AC-338, AC-430, AC-434, AC-453, AC-454, AC-455, AC-456.
- [AC-463]: **Test tĩnh bảo vệ nội dung mẫu.** Không tệp nguồn nào trong `src` chứa 5 tiêu đề bài mẫu của `ArchitectureTests.Sample_lesson_content_is_not_hard_coded_in_source` (kể cả `samples.js`, `landing.js`, `landing.html`); `ArchitectureTests` vẫn pass.
- [AC-464]: **Tài liệu và bàn giao.** `README.md` thêm mục ngắn: sơ đồ định tuyến `/` (landing vs chuyển hướng), trang đích theo role, cách thêm mục vào sidebar (`header.js`), nguồn công thức KPI (trỏ về AC-357 đến AC-372), cách thêm văn bản mẫu (`samples.js`) và ràng buộc 5 tiêu đề cấm; chú thích đầu `style.css` liệt kê lớp mới (`lp-*`, `app-*`, `dash-*`, `ed-*`).

---

## 4. Thứ tự triển khai (3 giai đoạn) và lưu ý cho Dev

**Giai đoạn 1 – Landing + định tuyến (AC-319 đến AC-342, AC-455 đến AC-459 phần landing).**
1. Đo kích thước JS+CSS hiện tại (QD-86). 2. `ReturnUrlPolicy` (đổi `StudentHome`/`TeacherHome`, `Sanitize`), `PageGuardMiddleware` (nhánh `/` và `/landing.html`, `/dashboard.html` cho giáo viên, `?notice=forbidden` về `/dashboard.html`), `Program.cs` (`DefaultFileNames`), `api.js`/`auth.js`. 3. Tạo **trang tạm** `dashboard.html` và `teacher/dashboard.html` (chỉ `h1` + tiêu đề) để định tuyến chạy; cập nhật test định tuyến. 4. `landing.html`, `landing.js`, CSS `lp-*`. 5. Chạy `dotnet test`, kiểm axe landing.

**Giai đoạn 2 – App shell + hai dashboard (AC-343 đến AC-382, AC-437 đến AC-450).**
1. `header.js` viết lại (sidebar, topbar, drawer, rail, menu người dùng), `theme-init.js` đọc `th12.sidebar`, thêm `data-shell` và `<aside id="site-menu">` vào các HTML. 2. `charts.js`. 3. `dashboard.js` (chuyển khối dashboard khỏi `index.js`), `teacher/dashboard.js`. 4. `search.js`. 5. Cập nhật test header/định tuyến/tiêu đề, kiểm axe, CLS.

**Giai đoạn 3 – Soạn bài (AC-383 đến AC-436, AC-451 đến AC-454).**
1. `editor-kit.js` (thanh bước, thanh lưu, chỉ báo, Ctrl+S, tổng hợp lỗi, checklist, tab Soạn/Xem trước). 2. `blocks.js`: bộ soạn khối có loại, thanh công cụ, hoàn tác; **kiểm tra round-trip (AC-391) trước khi nối vào trang**. 3. `lesson-edit.js` (4 bước, xem trước dùng hàm dựng chung với `lesson.js`; nếu `lesson.js` chưa có hàm dùng chung thì tách mà không đổi id/class). 4. `exercise-edit.js` (5 bước, bảng bộ kiểm tra, ví dụ, `Chạy thử với đáp án`). 5. `teacher/index.js` (tab, hàng, menu `Thêm`). 6. `samples.js` (tránh 5 tiêu đề cấm). 7. Cập nhật test, hồi quy hai giao diện.

**Lưu ý kỹ thuật**
- Giữ đúng định dạng dữ liệu cũ (QD-71). Ưu tiên dựng giao diện **bọc quanh** `blockList`/`recordList` (giữ `values`, `setError`, `clearErrors`, `focusFirstError`, `data-col`) thay vì viết lại logic.
- `modal.js` đặt `inert` lên mọi con của `<body>`: ngăn kéo, thanh lưu dính, toast và `#toast-region` phải được xử lý (ngoại trừ vùng toast) như v4.
- `PageGuardMiddleware` dùng `context.AuthenticateAsync` như hiện có; chỉ đọc cookie phiên khi có cookie để tiết kiệm.
- Quy ước tên lớp CSS mới: `lp-` (landing), `app-` (shell), `dash-` (dashboard), `ed-` (trình soạn); chú thích ở đầu `style.css`.
- Trạng thái `InProgress`, `NotStarted`, `Passed` lấy theo `statusChip()` hiện có; Dev kiểm thử với đúng chuỗi trả về của API.

---

## 5. Rủi ro & lưu ý (mâu thuẫn với mã/tài liệu hiện có đã phát hiện)

1. **ID/class cũ bị đổi hoặc bỏ có chủ đích:** `select#filter` của `teacher/index.js` (thay bằng tab), thanh viên thuốc (`nav-indicator`, `header-sentinel`, `sheet-*`, `.site-header` dạng viên), nút `Ẩn (về bản nháp)`/`Xuất bản`/`Lưu trữ` đổi chữ. JS/test nào tham chiếu các thứ này phải cập nhật. Các id trong mục 0.3 được **giữ**.
2. **Test hiện có sẽ FAIL cho đến khi được cập nhật (Khách hàng tự xử lý):** xem mục 8. Đáng chú ý là mọi nơi khóa `/index.html` và `/teacher/index.html` làm trang đích sau đăng nhập, và `Root_redirects_to_plain_login…`.
3. **Mâu thuẫn spec v3/v4 → ghi ở mục 11:** AC-145 (header), AC-308 ("không thêm/bỏ liên kết điều hướng"; nay thêm `Tổng quan`), AC-309 ("trang chủ không thêm yêu cầu `/api/**`" nay áp dụng cho `index.html`, còn `dashboard.html` có yêu cầu riêng), QD-48/QD-49/QD-50 (header), AC-302 (`theme-init.js` <= 1 KB → 1,5 KB), AC-210 đến AC-225 (trang chủ học sinh).
4. **Parser không hỗ trợ in đậm:** khách hàng có thể mong nút "In đậm"; không làm (QD-71). Nếu cần phải sửa `dom.js` và thêm AC.
5. **Round-trip khối nội dung (AC-391) là điểm rủi ro cao nhất:** ẩn ký hiệu `- ` và hàng rào mã rồi dựng lại có thể làm lệch dữ liệu cũ nếu có định dạng lạ; do đó có lớp `Văn bản thô` làm lưới an toàn.
6. **`pageSize=1` cho `GET /api/teacher/students`:** BA chưa đọc `StudentManagementUseCase.ListAsync` để xác nhận giới hạn dưới; Dev xác nhận (QD-69).
7. **KPI chính xác phụ thuộc ngữ nghĩa API:** (a) `isOverdue` có thể đã bao hàm `Passed`; AC-360 chặn bằng `status != Passed`. (b) `submittedStudents` có bao gồm `passedStudents` không: BA giả định có, Dev xác nhận bằng dữ liệu thật; nếu không thì công thức AC-372 đổi thành `ΣS` riêng. (c) `Σ passedCount` (từ `/api/lessons`) và số bài `Passed` (từ `/api/me/assignments`) có thể lệch khi một bài tập được giao nhiều lần; đã dùng phép đếm theo `exerciseId` (QD-68).
8. **"Hoạt động gần đây" không có luồng bài nộp** (QD-70); nếu Khách hàng muốn bắt buộc thì phải thêm endpoint (một thay đổi ngoài giao diện).
9. **Ctrl+S trong trình duyệt:** một số tiện ích mở rộng chiếm phím; `preventDefault` chỉ hiệu lực khi trang nhận được sự kiện.
10. **Hoàn tác khi chèn định dạng:** `execCommand('insertText')` đã bị đánh dấu lỗi thời; có dự phòng `setRangeText` (mất Ctrl+Z ở một số trình duyệt). Chấp nhận, ghi chú.
11. **Landing cho người đã đăng nhập bị chuyển hướng:** không thể xem lại landing khi đang đăng nhập (QD-62); nếu Khách hàng muốn xem được, đổi thành chỉ chuyển hướng riêng `/` mà để `/landing.html` công khai (một dòng trong `PageGuardMiddleware`).
12. **Open Graph:** `og:image`/`og:url` tuyệt đối cần tên miền (chưa có); mạng xã hội có thể không hiện ảnh nếu dùng đường dẫn tương đối.
13. **Khối lượng lớn:** ba giai đoạn độc lập, nên `dotnet test` + axe sau mỗi giai đoạn; tổng JS+CSS có thể sát trần 500 KB (QD-86).
14. **Mặc định "Tất cả" gồm bài lưu trữ** (QD-79) làm danh sách dài hơn trước; bài lưu trữ được làm mờ.
15. **BA chưa đọc** `js/lesson.js`, `problem.js`, `assignments.js` (học sinh), `grader.js`, `modal.js`, `toast.js` chi tiết, `StudentManagementUseCase`, `LessonAuthoringUseCase`; các AC liên quan (AC-388, AC-399, AC-413, AC-394, QD-76) nêu rõ Dev phải xác nhận ngay khi bắt tay làm.

---

## 6. Bàn giao
Đặc tả phiên bản 5 hoàn tất. Bàn giao cho `@dev-engineer` triển khai theo mục 4 (ba giai đoạn) tại `D:\Education\src\Web\wwwroot` và các tệp C# nêu ở mục 0.2; sau mỗi giai đoạn chạy build và `dotnet test`. `@qa-tester` kiểm thử theo **AC-319 … AC-464** và hồi quy AC cũ (AC-461). Mọi logic phát sinh (công thức KPI, quy tắc định tuyến `/`, danh sách kiểm tra xuất bản, quy tắc suy loại khối) **đã** được định nghĩa trong tài liệu này trước khi code (CLAUDE.md mục 3).

## 7. Số lượng AC mới
**146 AC mới: AC-319 … AC-464** (định tuyến 7; landing 17; app shell 12; dashboard học sinh 11; dashboard giáo viên 11; biểu đồ 6; soạn bài học 26; soạn bài tập 12; danh sách giáo viên 9; ngôn ngữ 7; truy cập 8; chuyển động 6; hồi quy và kỹ thuật 14). Quyết định mới: **QD-62 … QD-86** (25 mã).

## 8. Test hiện có phải cập nhật (`tests/`; Khách hàng/Dev tự xử lý)

| Test | Vấn đề | Cập nhật |
|---|---|---|
| `Web.Tests/PageAndRoleTests` › `Root_redirects_to_plain_login_and_login_page_and_assets_stay_public` | Kỳ vọng `/` chuyển tới login | `GET /` chưa đăng nhập = 200 landing (AC-319); thêm kiểm `/landing.html`, tài nguyên `/js/landing.js` công khai |
| `…` các test redirect theo role (dòng ~92 đến 156) | Khóa `Location` = `/index.html`, `/teacher/index.html`, `/index.html?notice=forbidden` | Đổi sang `/dashboard.html`, `/teacher/dashboard.html`, `/dashboard.html?notice=forbidden`; thêm `/dashboard.html` cho giáo viên (302) và danh sách trang bảo vệ mới (AC-321, AC-322); `/` với phiên đăng nhập (AC-320) |
| `…` danh sách `[InlineData]` các trang (dòng 17 đến 22) | Chưa có trang mới | Thêm `/dashboard.html` (HS) và `/teacher/dashboard.html` (GV) |
| `Web.Tests/AuthApiTests` | `redirectUrl` `/index.html` và `/teacher/index.html` (dòng 29 đến 31, 72 đến 75) | Đổi theo `HomeFor` mới; `returnUrl` xấu vẫn về trang chủ role (giờ là dashboard) |
| `Application.Tests/AuthTests` | `RedirectUrl` (dòng 26, 27, 40 đến 46, 283) | Đổi theo `ReturnUrlPolicy`; thêm ca `/landing.html`, `/dashboard.html` cho giáo viên |
| `Web.Tests/StudentAndTeacherFlowTests` (dòng ~275 đến 284) | Dùng `/index.html` cho kiểm tra phân quyền | Giữ nếu mục đích là "Bài học"; rà lại các kỳ vọng liên quan trang chủ |
| `Application.Tests/FrontendRulesTests` | Danh sách 12 HTML (dòng 31 đến 32), `[InlineData]` `<title>` (dòng 92 đến 93), regex `.site-header` | Thêm 3 HTML mới và 3 `<title>` mới (`Học Tin học 12 với Python – DK Edu`, `Tổng quan – DK Edu`, `Tổng quan giáo viên – DK Edu`); cập nhật regex header cho shell; thêm kiểm `data-shell`, `theme-init.js` <= 1,5 KB, không `<script>` nội dòng, landing không có từ cấm (AC-338), quét thuật ngữ (AC-430), emoji/em-dash |
| `…` test màu/tương phản | Token không đổi; thêm cặp mới nếu thêm token | Bổ sung nếu có token mới |
| `…` `Web_storage_is_only_used_by_storage_js…` | Khóa `th12.sidebar` trong `theme-init.js` | Cho phép khóa này |
| `Application.Tests/ArchitectureTests` › `Sample_lesson_content_is_not_hard_coded_in_source` | Phải vẫn pass | Giữ (AC-463) |
| **Mới** | AC-319 đến AC-325 (định tuyến, `ReturnUrlPolicy`) | Thêm test tích hợp/đơn vị |
| **Mới (trình duyệt/QA)** | AC-326 đến AC-352, AC-355 đến AC-382, AC-384 đến AC-420 | Playwright/axe ngoài `dotnet test` |

## 9. Câu hỏi và giả định cần Khách hàng xác nhận
1. **Người đã đăng nhập mở `/` thì chuyển thẳng vào Tổng quan** (không xem lại landing). Đồng ý? (QD-62)
2. **Trang đích sau đăng nhập đổi** từ danh sách bài sang **Tổng quan** (`/dashboard.html`, `/teacher/dashboard.html`); `Bài học` và `Bài học của tôi` vẫn còn ở sidebar. Đồng ý? (QD-63)
3. **Không có nút "In đậm"** vì trang học sinh không hỗ trợ; chỉ có Gạch đầu dòng, Mã trong dòng, Khối mã. Có cần thêm in đậm (phải sửa cách hiển thị bài) không? (QD-71)
4. **"Hoạt động gần đây" của giáo viên** hiển thị "Giao bài gần đây" và "Bài học sửa gần đây" thay vì "bài nộp mới nhất"; muốn có bài nộp mới nhất thì duyệt thêm endpoint `GET /api/teacher/activity` (QD-70).
5. **Tab "Tất cả" ở danh sách bài học gồm cả bài lưu trữ** và là mặc định. Hay muốn "Tất cả" không gồm lưu trữ như trước? (QD-79)
6. **Nhãn mới** `Cất đi (lưu trữ)`, `Chuyển về bản nháp`, `Xuất bản cho học sinh`, `Tên bài học`, `Bộ kiểm tra chấm bài`... (bảng 2.10) và nội dung/câu hỏi thường gặp trên landing (mục 2.8) do BA đề xuất; Khách hàng được sửa chữ.
7. **Tìm nhanh (Ctrl+K)** có giữ không? Nếu ngân sách 500 KB chật sẽ bỏ trước (QD-86).
8. **Tên miền** của DK Edu để bổ sung `og:url`/`canonical`/đường dẫn tuyệt đối cho `og:image`.
9. **Bỏ thanh tab dưới ở mobile**, chỉ dùng ngăn kéo (QD-65). Đồng ý?
10. **Dải "điều đúng sự thật" và FAQ** nêu rõ kết quả chấm chạy trên trình duyệt học sinh nên chỉ để theo dõi; Khách hàng có muốn nói khác không?

## 10. Rủi ro tổng hợp
Xem mục 5.

## 11. Nhật ký thay đổi so với v2, v3, v4

### 11.1 AC/QD bị **thay thế hoặc diễn giải lại**
| Mã cũ | Thay đổi bởi v5 |
|---|---|
| **QD-48** (header viên thuốc kính nổi), **QD-49** (viên chỉ báo trượt), **QD-50** (hamburger theo role: HS < 768, GV < 1360) | **Thay thế** bởi QD-65 (sidebar + topbar, ngăn kéo < 900 px, mục hiện tại tô bằng nền + thanh dọc + chữ đậm) |
| **AC-145** (header: <= 768 px hamburger, <= 2 thao tác tới Đăng xuất, aria-current, Esc) | **Giữ nội dung và nhãn**, đổi cơ chế: ngăn kéo < 900 px (AC-347), <= 2 thao tác qua menu người dùng (AC-349), `aria-current="page"` (AC-345) |
| **AC-179 đến AC-192** (header v4, gồm chỉ báo trượt AC-182, chiều cao header từ khung đầu AC-185, ô logo AC-189, `top` của băng rôn xem trước AC-192) | **Thay thế một phần**: các AC mô tả viên thuốc nổi và viên chỉ báo trượt không còn hiệu lực; chiều cao topbar/sidebar giữ chỗ từ khung đầu thay AC-185 (AC-351); `#preview-banner` dính dưới topbar thay AC-192 (AC-351); logo vẫn trong ô logo trắng. Dev rà AC-179 đến AC-192 và đánh dấu từng mã cụ thể (BA chưa đọc nguyên văn các AC này, chỉ căn cứ các tham chiếu ở mục 0.4 và 11 của v4) |
| **AC-210 đến AC-225** (trang chủ học sinh = bảng điều khiển học tập: lời chào, vòng tiến độ, "Tiếp tục học", bento) | **Diễn giải lại**: khối bảng điều khiển chuyển sang `dashboard.html` (AC-355 đến AC-365, dùng lại công thức QD-51 và chuỗi 2.8 v4); `index.html` chỉ còn tiêu đề, lời dẫn và lưới bài học |
| **QD-51** (trang chủ học sinh = bảng điều khiển) | **Chuyển** sang QD-64; công thức giữ nguyên |
| **AC-302** (`theme-init.js` <= 1 KB) | **Nâng** lên <= 1,5 KB (QD-66, AC-346) |
| **AC-308** (không thêm/bỏ liên kết điều hướng; 12 URL cũ) | **Điều chỉnh**: thêm liên kết `Tổng quan`, thêm 3 URL (`/landing.html`, `/dashboard.html`, `/teacher/dashboard.html`); 12 URL cũ vẫn tồn tại với cùng `<title>` |
| **AC-309** (trang chủ không thêm yêu cầu `/api/**`; bốn luồng cùng tập yêu cầu) | **Điều chỉnh**: áp dụng cho `index.html`; ngoại lệ ở AC-451 |
| **AC-81, AC-95, AC-98 và Root test** (`/` luôn chuyển tới login; đích sau đăng nhập `index.html`) | **Thay thế** bởi AC-319 đến AC-325 |
| **AC-146** (một `h1`, skip link), **AC-125** (không `innerHTML`), **AC-313** (không emoji/em-dash), **AC-299** (500 KB) | **Giữ** (AC-338, AC-434, AC-453, AC-455) |
| **AC-37, AC-112, AC-120 đến AC-130 và các AC soạn bài v3** | **Giữ hành vi**; chỉ đổi cách nhập và lời (mục 3.7, 3.8); API/dữ liệu không đổi |

### 11.2 Chuỗi/Id bị đổi có chủ đích
Nút `Xuất bản`/`Ẩn (về bản nháp)` → `Xuất bản cho học sinh`/`Chuyển về bản nháp`; `Lưu trữ` → `Cất đi (lưu trữ)` (chip trạng thái `Lưu trữ` giữ nguyên); `Tiêu đề bài học` → `Tên bài học`; `Bộ test case` → `Bộ kiểm tra chấm bài`; `Test ẩn` → `Kiểm tra ẩn: học sinh không thấy`; `Kiểm tra đáp án` → `Chạy thử với đáp án`; `select#filter` bị bỏ (tab thay thế); các lỗi kiểm tra và hộp thoại xung đột được viết lại lời thường (logic giữ). Xem bảng 2.10.

### 11.3 Quyết định (QD) mới
QD-62 … QD-86 (mục 0.4).

### 11.4 Không đổi
Mọi AC v2/v3/v4 không nêu ở 11.1 giữ nguyên và phải đạt ở **cả hai giao diện** (AC-461).

# Đặc tả Tính năng: Tách Frontend độc lập (HTML/CSS/JS thuần) khỏi ASP.NET, gọi API qua URL cấu hình được (Phiên bản 6)

> **Quan hệ với tài liệu cũ.** Đây là phần bổ sung (phiên bản 6) của `docs/specs/landing-dashboard-soan-bai.md` (v5), `docs/specs/thiet-ke-lai-giao-dien.md` (v4), `docs/specs/dang-nhap-phan-quyen.md` (v3) và `docs/specs/trang-hoc-tin-hoc-lop-12.md` (v2). Đã grep `docs/`: mã AC lớn nhất **được định nghĩa** là **AC-464** (v5 có nhắc "AC-476" ở dòng đầu nhưng không định nghĩa AC nào sau AC-464; để không trùng, BA bắt đầu từ mã sau số lớn nhất từng được nhắc) và QD lớn nhất là **QD-86**. Vì vậy tài liệu này dùng **AC-477 … AC-535** và **QD-87 … QD-99**. Khi mâu thuẫn, **v6 thắng v5/v4/v3/v2**; các điểm bị thay thế hoặc diễn giải lại nằm ở **mục 10 (Nhật ký thay đổi)**. Giữ nguyên: ngôn ngữ thị giác, nội dung, hành vi nghiệp vụ, hợp đồng API, HTML/CSS/JS thuần (ES modules, không build step, không thư viện ngoài, không `innerHTML`).

## 0. Bối cảnh, hiện trạng & Quyết định mặc định

**Khách hàng:** Giáo viên Tin học lớp 12.
**Yêu cầu gốc (phiên bản 6):** "chuyển UI của hệ thống sang HTML, CSS, JS". Đã hỏi lại, Khách hàng chọn phương án **TÁCH FRONTEND ĐỘC LẬP**: toàn bộ HTML/CSS/JS ra thư mục riêng `frontend/` ở gốc repo (`D:\Education\frontend`), chạy được bằng bất kỳ static server nào, gọi API ASP.NET qua URL cấu hình được.

### 0.1 Hiện trạng khảo sát mã nguồn (BA đã đọc trực tiếp)

- UI đã là HTML/CSS/JS thuần, ES modules, không build step, nằm trong `src/Web/wwwroot/`: **15 trang** `.html` (gốc: `landing`, `login`, `index`, `dashboard`, `assignments`, `change-password`, `lesson`, `problem`; `teacher/`: `index`, `dashboard`, `lesson-edit`, `exercise-edit`, `assignments`, `progress`, `students`), `css/style.css`, `js/` (29 tệp) + `js/teacher/` (11 tệp), `fonts/` (13 tệp: 12 `woff2` + `OFL.txt`), `img/` (5 tệp PNG). (Yêu cầu giao việc nói "16 trang"; thực tế đếm được 15, khớp "15 trang" ở AC-458 của v5.)
- Mọi HTML dùng **đường dẫn tuyệt đối từ gốc**: `/css/style.css`, `/js/...`, `/img/...`, `/fonts/...`; các module import `'/js/...'`. Vì vậy frontend phải được phục vụ **ở gốc của origin** (QD-91).
- `js/api.js` là nơi **duy nhất** có `fetch(`: 3 chỗ (`loadCsrf` -> `/api/auth/csrf`, `identityChanged` -> `/api/auth/me`, `call` -> mọi endpoint khác), tất cả đường dẫn tương đối `/api/...` và `credentials: 'same-origin'`; chỉ đọc một header phản hồi: `Retry-After` (dòng 125). Ba tệp khác (`auth.js`, `dashboard.js`, `search.js`) chỉ nhắc `/api/...` trong **chú thích**. CSRF lấy từ `GET /api/auth/csrf`, gửi bằng header `X-CSRF-TOKEN`.
- `src/Web/Program.cs`: `UseDefaultFiles` (`landing.html` rồi `index.html`), `UseStaticFiles` (font `/fonts` cache 1 năm `immutable`), `PageGuardMiddleware` (chặn HTML phía máy chủ), `SecurityHeadersMiddleware` (nosniff, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin` cho mọi phản hồi; `no-store` cho `/api`). Chưa có CORS. Cookie phiên `dkedu.auth` và cookie antiforgery `dkedu.csrf`: `HttpOnly`, `SameSite=Strict`, `Secure` ở Production.
- `PageGuardMiddleware` đang làm 7 việc (sẽ chuyển sang phía trình duyệt, mục 2.2): (1) chưa đăng nhập -> 302 `login.html?returnUrl=`; (2) `MustChangePassword` -> `change-password.html`; (3) học sinh vào `/teacher/` -> `/dashboard.html?notice=forbidden`; (4) giáo viên vào `/assignments.html` hoặc `/dashboard.html` -> `/teacher/dashboard.html`; (5) `login.html` khi đã đăng nhập -> trang theo role (qua `ReturnUrlPolicy.Sanitize`); (6) `/` và `/landing.html` khi đã đăng nhập -> trang chủ theo role; (7) HTML cần đăng nhập đặt `Cache-Control: no-store` (để nút Back không hiện lại trang cũ).
- **Phía trình duyệt đã có sẵn một phần guard**: mọi trang bảo vệ đều gọi `initPage({ role, active })` trong `js/auth.js` (gọi `GET /api/auth/me`, 401 -> `goToLogin`, `mustChangePassword` -> `/change-password.html`, sai role -> chuyển trang). Còn thiếu so với PageGuard: các việc (5), (6), (7), chống nhá nội dung, và `expired=1` hiện đang được gắn cả khi khách chưa từng đăng nhập (vì trước đây máy chủ đã chuyển hướng trước khi JS chạy).
- Test đọc trực tiếp `wwwroot`: `tests/Application.Tests/FrontendRulesTests.cs` (hằng `Wwwroot`, dòng 9), `tests/Application.Tests/ArchitectureTests.cs` (dòng 178-189), `tests/Web.Tests/PageAndRoleTests.cs` (lớp `PageGuardTests` và 2 test font/ảnh dòng 63, 78), `tests/Web.Tests/AuthApiTests.cs` (dòng 269, 276), `tests/Web.Tests/StudentAndTeacherFlowTests.cs` (dòng 275, 284). `FrontendRulesTests` có luật "chỉ cho phép URL tuyệt đối `http(s)://` thuộc danh sách" (dòng ~762) nên `config.js` cần được thêm vào danh sách; `ArchitectureTests` cấm mọi `*.json` trong thư mục UI (nên **không** đặt `serve.json`/`package.json` trong `frontend/`).
- Backend có **43** lời gọi `Map(Get|Post|Put|Patch|Delete)` trong `src/Web/Endpoints` (Auth 5, Content 7, Teacher 31). Mốc này dùng cho AC-500.
- **Cảnh báo baseline (BA không chạy được lệnh):** BA chỉ có công cụ đọc/ghi tệp nên chưa chạy `dotnet test`. Khi đọc mã thấy một số test hiện có đã lệch so với hành vi hiện tại (ví dụ `AuthTests` dòng 26-27, 42, 283 và `AuthApiTests` dòng 29-31, 75 vẫn kỳ vọng `redirectUrl` là `/index.html` và `/teacher/index.html`, trong khi `ReturnUrlPolicy.StudentHome = "/dashboard.html"`, `TeacherHome = "/teacher/dashboard.html"`; `FrontendRulesTests` dòng 208 đòi `theme-init.js <= 1024` byte trong khi v5 đã nâng lên 1,5 KB). Dev phải chạy `dotnet test` **trước khi sửa** để ghi nhận mốc, rồi sửa các kỳ vọng lỗi thời cho khớp hành vi hiện hành (không đổi hành vi) như một phần của việc này (AC-525).

### 0.2 Quyết định mặc định (BA đã chốt, Khách hàng có thể điều chỉnh)

| Mã | Quyết định |
|---|---|
| QD-87 | **Cấu trúc:** `D:\Education\frontend\` chứa **toàn bộ** nội dung `wwwroot` hiện tại (di chuyển, không sao chép trùng; sau đó `src/Web/wwwroot` không còn). `src/Web` thành **API-only**: bỏ `UseDefaultFiles`, `UseStaticFiles`, `PageGuardMiddleware`. Không giữ chế độ "ASP.NET phục vụ luôn frontend" (xem câu hỏi 5, mục 9). `ReturnUrlPolicy` **giữ** ở `Application` vì API đăng nhập vẫn dùng. |
| QD-88 | **Cấu hình API:** một tệp `frontend/js/config.js` xuất `API_BASE` (chuỗi). Mặc định `'http://localhost:5000'`. Chuỗi rỗng `''` nghĩa là cùng nguồn (dành cho triển khai sau reverse proxy chuyển `/api` về backend). Mọi lời gọi API đi qua `api.js`, dùng `API_BASE` và `credentials: 'include'`. Triển khai thật chỉ sửa tệp này (không build). `API_BASE` **không** đọc từ URL, `localStorage` hay DOM (chống bị dẫn tới API giả mạo). |
| QD-89 | **CORS phía backend:** đọc `Cors:AllowedOrigins` (mảng chuỗi). Dev: `appsettings.Development.json` đặt `["http://localhost:5173"]`; `appsettings.json` đặt mảng rỗng (Production mặc định **không** cho origin nào). Chính sách: `AllowCredentials`, chỉ origin được liệt kê khớp tuyệt đối (scheme + host + port), methods `GET, POST, PUT, PATCH, DELETE, OPTIONS`, headers `Content-Type` và `X-CSRF-TOKEN`, **expose `Retry-After`** (vì `api.js` đọc header này và trình duyệt chặn header không an toàn nếu không expose), `Access-Control-Max-Age` 600 giây. Cấm wildcard, cấm `AllowAnyOrigin`, cấm "phản chiếu mọi origin". Cấu hình sai (chứa `*`, có đường dẫn, dấu `/` cuối, scheme không phải http/https, chuỗi rỗng) làm ứng dụng báo lỗi rõ ràng khi khởi động, như `Auth:*`. |
| QD-90 | **Cookie giữ nguyên `SameSite=Strict`** (phiên và antiforgery). Hợp lệ khi frontend và API **cùng "site"** (cùng tên miền đăng ký, khác cổng hoặc khác subdomain đều được): ví dụ `localhost:5173` và `localhost:5000`; `app.example.com` và `api.example.com`. **Không hợp lệ:** `127.0.0.1:5173` với `localhost:5000` (hai site khác nhau, trình duyệt từ chối lưu cookie `Strict` từ phản hồi cross-site: đăng nhập trả 200 nhưng phiên không được lưu), hay hai tên miền gốc khác nhau. Production **bắt buộc HTTPS ở cả hai phía** (trộn HTTP/HTTPS bị chặn và cookie `Secure`). Ràng buộc này phải được ghi rõ trong README. |
| QD-91 | **Phục vụ ở gốc origin:** `frontend/` phải được phục vụ tại `/` của origin (giữ đường dẫn tuyệt đối `/js/...`, `/css/...`). Không hỗ trợ triển khai ở đường dẫn con như `/dkedu/`. Không sửa hàng trăm đường dẫn để đổi sang tương đối. Mở bằng `file://` không được hỗ trợ (ES modules và CORS). |
| QD-92 | **Trang mặc định:** static server phục vụ `index.html` cho `/` và cho thư mục con. Để `/` vẫn là landing: **đổi tên** `landing.html` -> `index.html`, và `index.html` hiện tại (danh sách bài học của học sinh) -> `lessons.html`; module `js/index.js` -> `js/lessons.js`. `teacher/index.html` giữ nguyên tên (tự là mặc định của `/teacher/`). Mọi liên kết nội bộ cập nhật theo (AC-479). Tiêu đề trang không đổi: `index.html` = `Học Tin học 12 với Python – DK Edu`; `lessons.html` = `Danh sách bài học – DK Edu`. `ReturnUrlPolicy.Sanitize` cập nhật theo (AC-501). |
| QD-93 | **Guard phía trình duyệt** thay `PageGuardMiddleware`: (a) mở rộng `initPage` và thêm `js/guard.js` (hàm thuần `sanitizeReturnUrl`, `homeFor`, `redirectIfSignedIn`, `onRestoreFromCache`); (b) **chống nhá nội dung**: 13 trang bảo vệ mang `<html data-guard>`, CSS ẩn `body` (`visibility: hidden`, không đổi bố cục nên không gây CLS) cho đến khi guard xác nhận, kèm `<noscript>` mở lại; (c) `pageshow` với `persisted` thì tải lại (thay `Cache-Control: no-store` cũ để nút Back sau đăng xuất không hiện dữ liệu cũ). |
| QD-94 | **Gợi ý "đã từng đăng nhập":** landing và `login.html` là trang công khai nhưng phải chuyển người đang đăng nhập đi. Cookie `HttpOnly` và khác origin nên JS không đọc được. Để **không** gọi `/api/auth/me` (và không sinh lỗi 401 trong console) với mọi khách vô danh, `storage.js` lưu một cờ không nhạy cảm `th12.signedin = "1"` (không chứa id/role/token) khi đăng nhập thành công hoặc khi `initPage` thành công, xóa khi đăng xuất hoặc gặp 401. Landing/login chỉ gọi `/api/auth/me` **khi có cờ**; 200 -> chuyển trang; 401 -> xóa cờ, hiển thị bình thường. Cờ chỉ là gợi ý UX, không phải bằng chứng đăng nhập. Phương án thay thế (luôn gọi `/me`) ở câu hỏi 2, mục 9. |
| QD-95 | **API là lớp bảo mật thật.** Guard phía trình duyệt chỉ là trải nghiệm người dùng (chuyển hướng, che nội dung). Mọi dữ liệu nghiệp vụ chỉ có qua API và vẫn do máy chủ phân quyền 401/403/404 như v3 (AC-96, AC-97). Trang HTML không chứa dữ liệu nghiệp vụ nên việc bỏ chặn HTML phía máy chủ không làm lộ dữ liệu. |
| QD-96 | **Chạy dev:** frontend ở **cổng 5173**, backend ở cổng 5000. Công cụ: `python -m http.server 5173 --directory frontend` (không cần cài thêm) hoặc `npx http-server frontend -p 5173 -c-1` (không dùng `serve`, vì `serve` mặc định "clean URLs" đổi `/login.html` thành `/login` làm hỏng kiểm tra đường dẫn và cần `serve.json` bị cấm ở QD-87). Cập nhật `.claude/launch.json` thêm cấu hình `frontend`. Không thêm bundler, framework, `package.json` bắt buộc. |
| QD-97 | **Header bảo mật và cache** trước đây do ASP.NET đặt cho trang nay thuộc static host. README ghi cấu hình mẫu (nginx) gồm: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`, `Cache-Control: no-cache` cho `.html/.js/.css` (vì không có tên tệp băm), `/fonts/` cache `public, max-age=31536000, immutable`, MIME `font/woff2` và `text/javascript` cho `.js`. Không bắt buộc code. Header bảo mật trên **API** vẫn do `SecurityHeadersMiddleware` đặt. |
| QD-98 | **Test:** đường dẫn trong `FrontendRulesTests`/`ArchitectureTests` trỏ `frontend/`; test `Web.Tests` về PageGuard/tệp tĩnh được thay bằng test CORS và test "backend không còn phục vụ HTML"; các kỳ vọng lỗi thời được sửa cho khớp hành vi hiện hành (mục 0.1). `dotnet build` và `dotnet test` phải xanh. |
| QD-99 | **Không đổi** hành vi nghiệp vụ, giao diện, hợp đồng API (đường dẫn, phương thức, thân yêu cầu/phản hồi, mã lỗi, chính sách cookie/CSRF/phiên). Chỉ thay đổi nơi đặt tệp, cách tìm API, CORS, guard phía trình duyệt và tài liệu. |

### 0.3 Ánh xạ di chuyển và danh sách tệp được phép sửa nội dung

**Di chuyển:** `src/Web/wwwroot/<p>` -> `frontend/<p>` cho mọi tệp (cùng đường dẫn tương đối), **ngoại trừ**:

| Cũ (`src/Web/wwwroot/`) | Mới (`frontend/`) |
|---|---|
| `landing.html` | `index.html` |
| `index.html` | `lessons.html` |
| `js/index.js` | `js/lessons.js` |

**Tệp mới:** `frontend/js/config.js`, `frontend/js/guard.js`, `tests/frontend/guard.test.mjs`, `docs/frontend-manifest.sha256`.

**Tệp được phép sửa nội dung** (mọi tệp khác phải **giống hệt từng byte** bản gốc, đối chiếu bằng manifest, AC-482):
- JS: `js/api.js`, `js/auth.js`, `js/storage.js`, `js/login.js`, `js/landing.js` (chỉ phần chuyển hướng khi đã đăng nhập), `js/header.js`, `js/lessons.js` (đổi tên, sửa liên kết nếu có), `js/dashboard.js`, `js/assignments.js`, `js/problem.js`, `js/lesson.js`, `js/search.js` (chỉ nếu có liên kết `/index.html` thật; Dev grep xác nhận), `js/change-password.js` (chỉ nếu cần).
- CSS: `css/style.css` (chỉ **thêm** khối luật `html[data-guard]`, ≤ 300 byte; cập nhật chú thích đầu tệp nếu nhắc `landing.html`).
- HTML: 13 trang bảo vệ (thêm `data-guard` và `<noscript>`); `lessons.html` (đổi `src` module); mọi HTML nếu có liên kết `/index.html` hay `/landing.html`.
- Ngoài `frontend/`: `src/Web/Program.cs`, `src/Web/Middleware/PageGuardMiddleware.cs` (xóa), `src/Web/appsettings*.json`, một tệp C# mới cho CORS (ví dụ `src/Web/Security/CorsSetup.cs`), `src/Application/Security/ReturnUrlPolicy.cs`, `.claude/launch.json`, `README.md`, `tests/**`.

### 0.4 Giả định cần Khách hàng xác nhận
1. Production dùng **hai địa chỉ cùng site** (ví dụ `app.<tên-miền>` và `api.<tên-miền>`) hoặc cùng origin sau reverse proxy; cả hai bằng HTTPS. Nếu bắt buộc khác site, cần đổi cookie sang `SameSite=None` (làm yếu phòng thủ CSRF, đòi hỏi quyết định riêng; ngoài phạm vi).
2. Chấp nhận quy ước phục vụ ở gốc origin (QD-91).
3. Chấp nhận đổi tên trang (QD-92): ghi chú **dấu trang cũ `/index.html`** (danh sách bài học học sinh) nay mở landing, và người đã đăng nhập bị chuyển tới Tổng quan (dấu trang cũ `/landing.html` thành 404).
4. Cờ `th12.signedin` (QD-94) là chấp nhận được.
5. Môi trường dev có Python 3 hoặc Node.js (một trong hai).

### 0.5 Ngoài phạm vi
Viết lại UI, đổi thiết kế, thêm framework hoặc bundler; đổi `SameSite`; đổi hợp đồng API; thêm endpoint (kể cả `/health`); Docker, CI/CD, CDN, hosting thực tế; tối ưu hóa hiệu năng tải (hash tên tệp, tree shaking); chế độ ASP.NET phục vụ luôn frontend; PWA/service worker.

---

## 1. Mục tiêu & User Story

- Là một **nhà phát triển/quản trị hệ thống**, tôi muốn **frontend là một thư mục HTML/CSS/JS độc lập chạy được bằng bất kỳ static server nào** để **triển khai, lưu trữ và nâng cấp giao diện tách biệt với backend ASP.NET**.
- Là một **nhà phát triển**, tôi muốn **địa chỉ API nằm ở đúng một tệp cấu hình** để **đổi môi trường (dev/thật) mà không sửa mã hay build lại**.
- Là một **chủ hệ thống**, tôi muốn **backend chỉ cho phép đúng các nguồn frontend được liệt kê gọi API kèm cookie** để **việc tách đôi không làm yếu bảo mật hiện có**.
- Là một **học sinh/giáo viên**, tôi muốn **mọi chức năng, giao diện và luồng đăng nhập/phân quyền hoạt động đúng như trước** để **không phải học lại và không mất dữ liệu**.

---

## 2. Luồng nghiệp vụ (Workflow)

### 2.1 Kiến trúc chạy

- Bước 1: Trình duyệt mở frontend tại `http://localhost:5173/` (static server). Nhận `index.html` (landing). Trang công khai, không gọi API nếu chưa từng đăng nhập (QD-94).
- Bước 2: Bấm "Đăng nhập" -> `login.html`. `login.js` gọi `GET {API_BASE}/api/auth/csrf` (CORS, `credentials: 'include'`), rồi `POST {API_BASE}/api/auth/login` kèm `X-CSRF-TOKEN`. Trình duyệt gửi preflight `OPTIONS` tự động; backend trả header CORS (QD-89). Thành công: cookie `dkedu.auth` (HttpOnly, SameSite=Strict) được lưu cho site; `storage.js` bật cờ `th12.signedin`.
- Bước 3: `login.js` chuyển (`location.href`) tới `redirectUrl` do API trả (đường dẫn tương đối, ví dụ `/dashboard.html`) **trên origin frontend**.
- Bước 4: Trang bảo vệ tải với `<html data-guard>` (body ẩn). Module gọi `initPage` -> `GET /api/auth/me`. 200 và đúng role -> bỏ `data-guard`, dựng giao diện, các lời gọi API sau đó như cũ. 401 -> `login.html?returnUrl=...`. Sai role hoặc phải đổi mật khẩu -> chuyển trang theo bảng 2.2.
- Bước 5: Đăng xuất: `POST /api/auth/logout` -> xóa cờ `th12.signedin` -> `location.href = '/login.html'`.

### 2.2 Bảng quy tắc guard phía trình duyệt (tương đương `PageGuardMiddleware`)

| # | Điều kiện | Hành động | Thực hiện ở |
|---|---|---|---|
| G1 | Trang bảo vệ, `GET /api/auth/me` = 401 | `location.href = /login.html?returnUrl=<đường dẫn + query hiện tại>`; thêm `expired=1` **chỉ khi** cờ `th12.signedin` đang bật (đã từng đăng nhập); xóa cờ. Bỏ `returnUrl` nếu đang ở `/` hoặc `/index.html` | `api.js` (`goToLogin`), `auth.js` |
| G2 | `mustChangePassword` và trang hiện tại khác `/change-password.html` | `location.replace('/change-password.html')` | `auth.js` |
| G3 | Học sinh mở trang khai báo `role: 'Teacher'` (`/teacher/*`) | `location.replace('/dashboard.html?notice=forbidden')` | `auth.js` |
| G4 | Giáo viên mở trang khai báo `role: 'Student'` (`/dashboard.html`, `/assignments.html`) | `location.replace('/teacher/dashboard.html')` | `auth.js` |
| G5 | `login.html`, cờ bật, `/me` = 200 | `location.replace(mustChangePassword ? '/change-password.html' : (sanitizeReturnUrl(returnUrl, role) ?? homeFor(user)))` | `login.js` + `guard.js` |
| G6 | `index.html` (landing), cờ bật, `/me` = 200 | `location.replace(mustChangePassword ? '/change-password.html' : homeFor(user))` | `landing.js` + `guard.js` |
| G7 | Trang được khôi phục từ bộ nhớ đệm back/forward (`pageshow.persisted`) | `location.reload()` (chạy lại guard) | `guard.js` |
| G8 | Mọi trang: tab lấy lại tiêu điểm, `/me` trả `id` khác hoặc 401 | Giữ hành vi `watchIdentity` hiện có (AC-100) | `auth.js` |

`homeFor(user)`: giáo viên -> `/teacher/dashboard.html`, học sinh -> `/dashboard.html` (đúng `ReturnUrlPolicy.HomeFor`). Các trang `lessons.html`, `lesson.html`, `problem.html`, `change-password.html` không khai báo role (`any`).

### 2.3 Luồng của nhà phát triển (dev)

- Bước 1: Terminal A: `dotnet run --project src/Web` (API, cổng 5000, Development, tự áp migration/seed như cũ).
- Bước 2: Terminal B: `python -m http.server 5173 --directory frontend` (hoặc `npx http-server frontend -p 5173 -c-1`).
- Bước 3: Mở `http://localhost:5173/` (dùng đúng tên `localhost` cho cả hai; **không** trộn `127.0.0.1`).
- Bước 4: Đổi môi trường: sửa `frontend/js/config.js` (`API_BASE`) và `Cors:AllowedOrigins` ở backend (biến môi trường `Cors__AllowedOrigins__0=https://app.example.com`).

### 2.4 Cấu hình mẫu nginx cho static host (thông tin tham khảo cho README, QD-97)

```
server {
  listen 443 ssl;
  server_name app.example.com;
  root /var/www/dkedu/frontend;
  index index.html;
  types { text/html html; text/css css; text/javascript js; font/woff2 woff2; image/png png; }
  add_header X-Content-Type-Options nosniff always;
  add_header X-Frame-Options DENY always;
  add_header Referrer-Policy same-origin always;
  location / { try_files $uri $uri/ =404; add_header Cache-Control "no-cache" always; }
  location /fonts/ { add_header Cache-Control "public, max-age=31536000, immutable" always; }
}
```
(Mỗi `location` có `add_header` riêng sẽ **không kế thừa** `add_header` ở cấp `server` của nginx; Dev lặp lại ba header bảo mật trong từng `location` khi ghi vào README.)

### 2.5 Trường hợp biên (Edge Cases)

- **Khách chưa từng đăng nhập mở thẳng trang bảo vệ** (ví dụ `/dashboard.html`): thấy trang trắng trong thời gian ngắn rồi sang `login.html?returnUrl=%2Fdashboard.html` **không** có thông báo "hết hạn" (G1). Sau đăng nhập về đúng `returnUrl` nếu hợp lệ.
- **Phiên hết hạn khi đang dùng:** API 401 -> G1 với `expired=1` (cờ đang bật) -> thông báo "Phiên đăng nhập đã hết hạn" như cũ.
- **Backend tắt hoặc `API_BASE` sai:** `fetch` ném lỗi -> `ApiError(kind: 'network')`; `initPage` hiện khối lỗi "Không kết nối được máy chủ" và **bỏ ẩn trang** (không để trang trắng vĩnh viễn); login hiện "Không kết nối được máy chủ".
- **Sai CORS** (origin chưa được liệt kê): trình duyệt chặn đọc phản hồi -> cùng hiển thị "Không kết nối được máy chủ"; README có mục xử lý sự cố (kiểm tra `Cors:AllowedOrigins`, đúng scheme/host/cổng, không dấu `/` cuối).
- **Sai site** (`127.0.0.1` với `localhost`): đăng nhập 200 nhưng phiên không được lưu, `/me` ngay sau đó 401 -> quay lại đăng nhập. README ghi triệu chứng này (QD-90).
- **Nút Back sau khi đăng xuất:** trang được trình duyệt khôi phục từ bộ nhớ đệm -> G7 tải lại -> `/me` 401 -> đăng nhập.
- **Cờ `th12.signedin` cũ** (cookie đã hết hạn): landing/login gọi `/me` -> 401 -> xóa cờ, hiển thị bình thường.
- **Cờ mất nhưng cookie còn** (người dùng xóa dữ liệu trang): landing/login hiển thị bình thường; người dùng có thể mở thẳng trang bảo vệ (đã đăng nhập, `initPage` vẫn hoạt động) hoặc đăng nhập lại. Chấp nhận.
- **Mở bằng `file://`:** không hỗ trợ (origin `null` không nằm trong danh sách CORS, ES modules bị chặn); README nêu rõ.
- **`/teacher` không có dấu `/` cuối:** tùy static server (có thể 301 hoặc 404); README khuyến nghị liên kết `/teacher/` (mã hiện luôn dùng `/teacher/index.html` hoặc `/teacher/dashboard.html`).
- **Nhiều origin frontend** (vd. 5173 và 3000): liệt kê cả hai trong `Cors:AllowedOrigins`.
- **Triển khai mới ghi đè JS cũ trong bộ nhớ đệm trình duyệt:** do không băm tên tệp, QD-97 yêu cầu `Cache-Control: no-cache` (tái xác thực bằng ETag) cho `.html/.js/.css`.

---

## 3. Tiêu chí Nghiệm thu (Acceptance Criteria) – bổ sung từ AC-477

### 3.1 Cấu trúc, di chuyển và đổi tên (QD-87, QD-92)

- [AC-477]: **Thư mục `frontend/` đủ và `wwwroot` biến mất.** Tồn tại `D:\Education\frontend\` chứa đủ 15 trang HTML theo bảng 0.3 (`index.html`, `login.html`, `lessons.html`, `dashboard.html`, `assignments.html`, `change-password.html`, `lesson.html`, `problem.html`, `teacher/index.html`, `teacher/dashboard.html`, `teacher/lesson-edit.html`, `teacher/exercise-edit.html`, `teacher/assignments.html`, `teacher/progress.html`, `teacher/students.html`), `css/style.css`, 40 tệp `.js` cũ (29 ở `js/`, 11 ở `js/teacher/`; `index.js` đã thành `lessons.js`) cộng `js/config.js` và `js/guard.js` mới (tổng 42), `fonts/` đủ 13 tệp (12 `woff2` + `OFL.txt`) cùng tên cũ, `img/` đủ 5 PNG cùng tên cũ. **Không** còn thư mục `src/Web/wwwroot` (kiểm bằng `Test-Path`/`ls`), và không có hai bản trùng của `style.css` hay `api.js` ở bất kỳ nơi nào trong repo (loại `bin/`, `obj/`).
- [AC-478]: **Đổi tên đúng.** Không tồn tại `frontend/landing.html` và `frontend/js/index.js`. `frontend/index.html` là landing (có `<title>Học Tin học 12 với Python – DK Edu</title>`, đúng một `<h1>`, nạp đúng `/js/landing.js`); `frontend/lessons.html` có `<title>Danh sách bài học – DK Edu</title>` và nạp `/js/lessons.js` (tệp tồn tại); `frontend/teacher/index.html` vẫn có `<title>Bài học của tôi – DK Edu</title>`.
- [AC-479]: **Liên kết nội bộ không bị gãy sau đổi tên.** Trong `frontend/` (bỏ qua dòng chú thích): (a) 0 lần xuất hiện `landing.html`; (b) 0 lần khớp regex `(?<![A-Za-z0-9_-])/index\.html` trong mã JS và thuộc tính HTML (nghĩa là mọi liên kết "danh sách bài học học sinh" phải là `/lessons.html`, còn `/teacher/index.html` vẫn hợp lệ); (c) `/lessons.html` có mặt ở các điểm trước đây trỏ `/index.html`: `header.js` (mục sidebar "Bài học" và "Xem như học sinh (xem trước)"), `assignments.js`, `dashboard.js`, `problem.js` (`homeHref` khi không phải xem trước), `lesson.js` (`homeHref` khi là học sinh).
- [AC-480]: **Mọi tài nguyên được tham chiếu đều tồn tại.** Một đoạn quét (Dev bổ sung vào `FrontendRulesTests`) duyệt: mọi thuộc tính `href`/`src` bắt đầu bằng `/css/`, `/js/`, `/img/`, `/fonts/` trong 15 HTML; mọi chuỗi `from '/js/...'` và `import('/js/...')` trong JS; đường dẫn Worker và ảnh được nhắc trong CSS (`url(...)`); tất cả phải trỏ tới tệp có thật dưới `frontend/` (0 tệp thiếu).
- [AC-481]: **Không rác, không công cụ build.** Trong `frontend/` và gốc repo: 0 tệp `*.json` trong `frontend/` (giữ `ArchitectureTests`), không có `package.json`, `package-lock.json`, `node_modules`, `vite.config.*`, `webpack.config.*`, `rollup.config.*`, `tsconfig.json`, `.babelrc`; không có thư mục `frontend/data` hay `frontend/dist`. `tests/frontend/guard.test.mjs` là tệp `.mjs` duy nhất thêm vào và nằm **ngoài** `frontend/`.
- [AC-482]: **Bảo toàn nội dung bằng manifest.** Dev tạo `docs/frontend-manifest.sha256` **trước khi di chuyển**: mỗi dòng `<sha256>  <đường dẫn cũ trong wwwroot>  ->  <đường dẫn mới trong frontend>`. QA tính lại SHA-256 của các tệp mới (`Get-FileHash -Algorithm SHA256` hoặc `sha256sum`): mọi tệp **không** thuộc danh sách "được phép sửa" ở mục 0.3 phải khớp hash; tệp được phép sửa thì Dev ghi kèm nhãn `MODIFIED` trong manifest. Số dòng manifest = số tệp của `wwwroot` cũ (40 JS + 15 HTML + 1 CSS + 13 font + 5 ảnh = 74).
- [AC-483]: **Quy ước head và ngân sách tệp giữ nguyên trong `frontend/`.** Mọi HTML vẫn có hai dòng `<link>` favicon/apple-touch đúng chuỗi cũ (AC-75/AC-457), `href="/css/style.css"`, `<meta name="color-scheme" content="light dark">`, không `<script>` nội dòng, không CDN trong HTML; tổng dung lượng `*.js` + `*.css` ≤ 500 KB (AC-455) và `theme-init.js` ≤ 1,5 KB; `img/` chỉ PNG; `fonts/` ≤ 250 KB. Dev báo kích thước trước/sau.

### 3.2 Cấu hình địa chỉ API (QD-88)

- [AC-484]: **`js/config.js`.** Tệp là ES module có đúng một export `API_BASE` kiểu chuỗi, mặc định `'http://localhost:5000'`, kèm chú thích tiếng Việt giải thích: sửa khi triển khai, `''` = cùng origin, không có dấu `/` cuối, phải là origin của backend, và origin của frontend phải nằm trong `Cors:AllowedOrigins` của backend. Không có logic khác (không đọc `location`, `localStorage`, `document`).
- [AC-485]: **`api.js` dùng `API_BASE` cho mọi lời gọi.** `api.js` import `API_BASE` từ `/js/config.js`; có hàm nội bộ `apiUrl(path)` = `API_BASE` (đã cắt mọi dấu `/` cuối) + `path`; cả ba chỗ `fetch(` (`loadCsrf`, `identityChanged`, `call`) dùng `apiUrl(...)`. Với `API_BASE = ''` URL ra là tương đối (`/api/...`); với `'http://localhost:5000/'` URL ra là `http://localhost:5000/api/...` (không `//api`). Có thể kiểm bằng `tests/frontend/guard.test.mjs` hoặc đọc mã.
- [AC-486]: **Cookie gửi kèm.** Mọi `fetch(` trong `frontend/` có `credentials: 'include'`; 0 lần xuất hiện `credentials: 'same-origin'` và `credentials: 'omit'`. Các tùy chọn khác giữ nguyên (`cache: 'no-store'`, header `Accept`, `Content-Type`, `X-CSRF-TOKEN` cho phương thức ghi).
- [AC-487]: **Một cổng ra duy nhất.** Chuỗi `fetch(` chỉ xuất hiện trong `js/api.js` (0 trong các tệp khác của `frontend/`); chuỗi đường dẫn bắt đầu bằng `/api/` (trong dấu `'`, `"`, `` ` ``) chỉ xuất hiện trong `js/api.js` (ngoài chú thích); 0 lần dùng `XMLHttpRequest`, `navigator.sendBeacon`, `EventSource`, `WebSocket`. Điều này cho phép đổi `API_BASE` mà không sửa nơi nào khác.
- [AC-488]: **`API_BASE` không bị chi phối từ ngoài và không rò URL.** `config.js` là nơi duy nhất trong `frontend/` có chuỗi `http://` hoặc `https://` ngoài danh sách đã cho phép (SVG namespace `http://www.w3.org/2000/svg`, `https://cdn.jsdelivr.net/pyodide/`); luật quét URL trong `FrontendRulesTests` được cập nhật để cho phép **đúng** `http://localhost:5000` và **chỉ** ở `js/config.js`. Không mã nào đọc `API_BASE` từ `location.search`, `location.hash`, `localStorage`, `sessionStorage`, cookie hay thuộc tính HTML.
- [AC-489]: **Lỗi mạng/CORS hiển thị đúng và không để trang trắng.** Khi `fetch` ném lỗi (backend tắt, CORS chặn), `api.js` ném `ApiError({ kind: 'network' })` (giữ thông báo "Không kết nối được máy chủ"); `initPage` hiển thị khối lỗi "Không kết nối được máy chủ" **và bỏ thuộc tính `data-guard`** của `<html>` để người dùng thấy thông báo; `login.js` hiển thị "Không kết nối được máy chủ, hãy kiểm tra kết nối rồi thử lại." (đã có). Kiểm bằng đọc mã (đường thoát của `renderFatal` có `removeAttribute('data-guard')`).

### 3.3 CORS phía backend (QD-89)

- [AC-490]: **Cấu hình.** `src/Web/appsettings.json` có `"Cors": { "AllowedOrigins": [] }`; `src/Web/appsettings.Development.json` có `"Cors": { "AllowedOrigins": [ "http://localhost:5173" ] }`. Mã CORS đọc đúng khóa `Cors:AllowedOrigins` (hỗ trợ biến môi trường `Cors__AllowedOrigins__0`); khi mảng rỗng, mọi request cross-origin đều **không** nhận header CORS.
- [AC-491]: **Origin hợp lệ nhận đủ header.** Test tích hợp (`WebApplicationFactory`, `Origin: http://localhost:5173` được cấu hình rõ qua `AppFactory.With(("Cors:AllowedOrigins:0","http://localhost:5173"))`): `GET /api/auth/csrf` trả 200 và `Access-Control-Allow-Origin: http://localhost:5173` (chính xác chuỗi origin, không `*`), `Access-Control-Allow-Credentials: true`, và `Vary` chứa `Origin`.
- [AC-492]: **Origin lạ bị từ chối.** Với cùng cấu hình, request có `Origin` là `http://evil.example`, `http://localhost:5174`, `https://localhost:5173`, `http://127.0.0.1:5173`, `null`: phản hồi **không** có `Access-Control-Allow-Origin` và **không** có `Access-Control-Allow-Credentials`. Preflight `OPTIONS` từ các origin này cũng không có header CORS.
- [AC-493]: **Preflight hợp lệ.** `OPTIONS /api/teacher/lessons` với `Origin: http://localhost:5173`, `Access-Control-Request-Method: PUT`, `Access-Control-Request-Headers: content-type,x-csrf-token` trả **204**, `Access-Control-Allow-Origin: http://localhost:5173`, `Access-Control-Allow-Credentials: true`, `Access-Control-Allow-Methods` chứa đủ `GET, POST, PUT, PATCH, DELETE`, `Access-Control-Allow-Headers` chứa `Content-Type` và `X-CSRF-TOKEN`, `Access-Control-Max-Age: 600`. Thử lại với `Access-Control-Request-Headers: x-evil` thì header đó **không** được liệt kê trong `Access-Control-Allow-Headers`.
- [AC-494]: **Preflight không qua xác thực/giới hạn/CSRF.** `OPTIONS` tới `/api/teacher/lessons` khi **chưa đăng nhập** vẫn 204 (không 401), không cần `X-CSRF-TOKEN` (không 400), và không tính vào giới hạn đăng nhập: với `Auth:LoginRatePerMinute = 3`, gửi 10 `OPTIONS /api/auth/login` hợp lệ rồi 1 `POST /api/auth/login` đúng (có CSRF) vẫn trả 200, không 429.
- [AC-495]: **Phản hồi lỗi vẫn kèm CORS để giao diện đọc được mã lỗi.** Với `Origin` hợp lệ: `GET /api/auth/me` chưa đăng nhập -> 401 có `Access-Control-Allow-Origin`; học sinh `GET /api/teacher/lessons` -> 403 có header; `POST /api/auth/logout` thiếu CSRF -> 400 `CSRF_INVALID` có header; vượt giới hạn đăng nhập -> 429 có `Access-Control-Allow-Origin` **và** `Access-Control-Expose-Headers` chứa `Retry-After`, đồng thời `Retry-After` hiện trong phản hồi.
- [AC-496]: **Kiểm tra cấu hình khi khởi động.** Mỗi giá trị sau của `Cors:AllowedOrigins` làm ứng dụng không khởi động, thông báo lỗi chứa chuỗi `Cors:AllowedOrigins` (kiểm bằng `Assert.Contains` như `Invalid_auth_configuration_stops_startup...`): `*`, `""`, `"localhost:5173"` (thiếu scheme), `"ftp://x.example"`, `"http://localhost:5173/"` (dấu `/` cuối), `"http://localhost:5173/app"` (có đường dẫn), `"http://localhost:5173?x=1"`. Giá trị hợp lệ: `http://localhost:5173`, `https://app.example.com`, `https://app.example.com:8443`.
- [AC-497]: **Không wildcard và đúng thứ tự middleware.** Trong `src/Web` (loại `bin/obj`): 0 lần `AllowAnyOrigin`, 0 lần `SetIsOriginAllowed(` trả về hằng `true`/`_ => true`, 0 lần `WithOrigins("*")`. Trong `Program.cs`, `UseCors(...)` đứng **trước** `UseAuthentication`, `CsrfMiddleware`, `PasswordChangeGateMiddleware`, `UseAuthorization`, `UseRateLimiter` (và `RequestLimitsMiddleware`), để preflight và lỗi 401/403/429 đều nhận header CORS.

### 3.4 Backend chỉ còn là API (QD-87, QD-95, QD-99)

- [AC-498]: **Mã backend không còn phục vụ giao diện.** `Program.cs` không còn `UseDefaultFiles`, `UseStaticFiles`, `StaticFileOptions`, `FileExtensionContentTypeProvider`, `DefaultFilesOptions`; tệp `src/Web/Middleware/PageGuardMiddleware.cs` bị xóa và 0 tham chiếu `PageGuardMiddleware` trong `src/` và `tests/`. `dotnet build src/TinHoc12.sln` không phát sinh cảnh báo `wwwroot` hay thiếu thư mục web root.
- [AC-499]: **HTML và tĩnh trả 404 từ backend.** Test tích hợp (khách chưa đăng nhập và đã đăng nhập, `AllowAutoRedirect = false`): `GET /`, `/index.html`, `/login.html`, `/lessons.html`, `/dashboard.html`, `/teacher/index.html`, `/css/style.css`, `/js/api.js`, `/fonts/OFL.txt`, `/img/logo-dk-edu.png` đều trả **404**, **không** có header `Location`, thân không phải HTML. Header bảo mật (nosniff, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`) vẫn có trên phản hồi 404 này (do `SecurityHeadersMiddleware`).
- [AC-500]: **API không đổi.** Số lời gọi `Map(Get|Post|Put|Patch|Delete)(` trong `src/Web/Endpoints` vẫn là **43** (Auth 5, Content 7, Teacher 31) và không đường dẫn nào thay đổi; mọi test `AuthorizationMatrixTests` (ma trận quyền AC-96/AC-97), `AuthApiTests`, `StudentAndTeacherFlowTests` vẫn đạt; `/api/**` vẫn có `Cache-Control: no-store`; Production vẫn bật HSTS và chuyển HTTP sang HTTPS; cookie `dkedu.auth` và `dkedu.csrf` giữ `HttpOnly`, `SameSite=Strict`, `Path=/`, `Secure` ở Production.
- [AC-501]: **`ReturnUrlPolicy` khớp tên trang mới.** `Sanitize` trả `null` cho: `/`, `/index.html`, `/index.html?x=1`, `/index.html#a`, `/INDEX.HTML` (landing mới), `/landing.html` (trang cũ không còn; giữ `null` để không dẫn tới 404), `/api/...`, `/login.html...`, `//evil.com`, `/\evil.com`, chuỗi có ký tự điều khiển, độ dài > 300; với giáo viên cũng `null` cho `/assignments.html`, `/dashboard.html` và `/dashboard.html?x=1`; với học sinh `null` cho `/teacher`, `/teacher/`, `/teacher/index.html`. **Hợp lệ** (trả lại nguyên chuỗi) cho cả hai role: `/lessons.html`, `/lesson.html?id=x`, `/problem.html?lesson=a&problem=b`, `/change-password.html`; học sinh: `/dashboard.html`, `/assignments.html`; giáo viên: `/teacher/index.html`, `/teacher/`, `/teacher/dashboard.html`. `/teacher/index.html` **không** bị nhầm với `/index.html`. `StudentHome`/`TeacherHome`/`HomeFor` không đổi. Có test đơn vị `Application.Tests` cho từng ca trên.
- [AC-502]: **Hợp đồng đăng nhập không đổi.** `POST /api/auth/login` vẫn trả `{ user, redirectUrl }`, trong đó `redirectUrl` luôn là **đường dẫn tương đối** bắt đầu bằng đúng một `/` (không URL tuyệt đối) và đúng `HomeFor`/`returnUrl` hợp lệ/`/change-password.html` (AC-82). Frontend ghép nó với origin của chính nó, không bao giờ với `API_BASE`.

### 3.5 Cookie, CSRF và ràng buộc "cùng site" (QD-90, QD-91)

- [AC-503]: **Không làm yếu cookie.** Trong `src/Web`: 0 lần `SameSiteMode.None`, 0 lần `SameSiteMode.Lax`, 0 lần `SameSite = SameSiteMode.Unspecified`; cả hai cookie vẫn `SameSiteMode.Strict`, `HttpOnly = true`, và `SecurePolicy = Always` ở Production; `Path = "/"` giữ nguyên. Frontend không đọc/ghi `document.cookie` (giữ luật hiện có) và không lưu token/mật khẩu trong `localStorage`/`sessionStorage` (AC-83).
- [AC-504]: **CSRF vẫn đủ.** Mọi `POST/PUT/PATCH/DELETE` từ frontend vẫn gửi `X-CSRF-TOKEN` lấy từ `GET {API_BASE}/api/auth/csrf` (chuỗi `'X-CSRF-TOKEN'` và `call('POST', '/api/auth/login', body` còn trong `api.js`, như `Password_never_appears_in_urls_and_login_posts_json`); cơ chế thử lại một lần khi `CSRF_INVALID` giữ nguyên; `CsrfMiddleware` không bị nới lỏng (0 thay đổi trong lớp này ngoài thứ tự middleware).
- [AC-505]: **README ghi ràng buộc triển khai.** `README.md` có mục riêng về "cùng site" gồm bảng ít nhất 4 ví dụ **hợp lệ/không hợp lệ** (bắt buộc có: `localhost:5173` + `localhost:5000` hợp lệ; `127.0.0.1:5173` + `localhost:5000` không hợp lệ; `app.example.com` + `api.example.com` hợp lệ; hai tên miền gốc khác nhau không hợp lệ), nêu triệu chứng khi sai (đăng nhập 200 nhưng quay lại trang đăng nhập), yêu cầu HTTPS cả hai phía ở Production, và nêu rõ không hỗ trợ khác site vì cần `SameSite=None` (ngoài phạm vi).
- [AC-506]: **Phục vụ ở gốc origin được ghi rõ.** README nêu: `frontend/` phải được phục vụ ở `/` của origin (không đường dẫn con), không mở bằng `file://`; mọi HTML vẫn dùng đường dẫn tuyệt đối `/css/`, `/js/`, `/img/`, `/fonts/` (kiểm lại bằng AC-480).

### 3.6 Guard phía trình duyệt (QD-93, QD-94, QD-95)

- [AC-507]: **`js/guard.js` có hàm thuần `sanitizeReturnUrl(returnUrl, role)`** (không chạm DOM/`location`/`window`, import được trong Node) phản chiếu **từng quy tắc** của `ReturnUrlPolicy.Sanitize` (AC-501): bắt đầu bằng `/`; không `//` hoặc `/\` ở đầu; không ký tự điều khiển (`\u0000-\u001f`, `\u007f-\u009f`) hay `\`; dài ≤ 300; cắt ở `?`/`#` để lấy `path`; `null` cho `/`, `/index.html` (so sánh khớp tuyệt đối, không phân biệt hoa/thường), `/landing.html...`, `/api/...`, `/login.html...`; `/teacher` và `/teacher/...` chỉ cho `Teacher`; giáo viên không được `/assignments.html` và `/dashboard.html`. Trả về chuỗi gốc nếu hợp lệ, ngược lại `null`.
- [AC-508]: **Một nguồn cho `homeFor` và các quy tắc G1-G4.** `homeFor(user)` được định nghĩa một lần (ở `guard.js`, `api.js` re-export để không đổi nơi gọi) trả `/teacher/dashboard.html` cho `Teacher`, ngược lại `/dashboard.html`. `initPage` giữ đủ hành vi: `mustChangePassword` và không ở `/change-password.html` -> `location.replace('/change-password.html')`; `role: 'Teacher'` mà user không phải giáo viên -> `location.replace('/dashboard.html?notice=forbidden')`; `role: 'Student'` mà user không phải học sinh -> `location.replace('/teacher/dashboard.html')`; trang giáo viên đều khai báo `role: 'Teacher'` (7 trang `teacher/*.js`), `dashboard.js` và `assignments.js` khai báo `role: 'Student'`. Banner "Bạn không có quyền truy cập khu vực giáo viên." vẫn hiển thị khi `?notice=forbidden` (đã có ở `dashboard.js`).
- [AC-509]: **Chống nhá nội dung: đúng 13 trang.** Đúng 13 HTML mang `<html lang="vi" data-guard>`: `lessons`, `dashboard`, `assignments`, `change-password`, `lesson`, `problem` và 7 trang `teacher/*`. `index.html` (landing) và `login.html` **không** có `data-guard`. `style.css` có luật `html[data-guard] body { visibility: hidden }` (trong `@layer base` hoặc `pages`, ≤ 300 byte, không `display: none` để không đổi bố cục). Mỗi trong 13 HTML có trong `<head>` khối `<noscript><style>html[data-guard] body{visibility:visible}</style></noscript>` (không phải `<script>` nội dòng, giữ AC-456).
- [AC-510]: **Khi nào bỏ ẩn.** `initPage` gọi `document.documentElement.removeAttribute('data-guard')` (a) ngay sau khi `me` thành công, đúng role, không cần đổi mật khẩu, **trước** `renderHeader`; (b) trong nhánh `renderFatal`. **Không** bỏ ẩn trong các nhánh chuyển hướng (G1-G4). Mọi trang bảo vệ gọi `initPage` đúng một lần ở đầu module (0 trang có logic hiển thị nội dung trước khi `await initPage(...)`).
- [AC-511]: **Nút Back/bộ nhớ đệm.** `guard.js` đăng ký `window.addEventListener('pageshow', e => { if (e.persisted) location.reload(); })` và `initPage` (13 trang), `login.js`, `landing.js` đều kích hoạt nó (qua hàm `onRestoreFromCache()` gọi một lần mỗi trang). Sau khi đăng xuất, quay lại bằng nút Back không còn hiện dữ liệu cũ trong một khung hình khả kiến (kiểm tay, xem AC-534); kiểm tự động: đọc mã.
- [AC-512]: **Cờ `th12.signedin`.** `storage.js` là nơi duy nhất truy cập `localStorage` cho cờ này (giữ luật "chỉ `storage.js` và `theme-init.js` dùng web storage"), cung cấp `markSignedIn()`, `clearSignedIn()`, `hasSignedInHint()`; giá trị lưu là chuỗi `"1"` (không có id, role, token, tên). `markSignedIn` được gọi sau `login` thành công và sau `initPage` thành công; `clearSignedIn` được gọi khi `logout` (đúng cả khi API logout lỗi), khi bất kỳ lời gọi API nhận 401 ngoài trang đăng nhập, và khi `/me` của landing/login trả 401. `localStorage` bị chặn (ném lỗi) thì mọi hàm cờ không ném và coi như không có cờ. `storage.js` vẫn không chứa các từ `password|token|csrf|secret` (luật hiện có) và khóa mới `th12.signedin` được thêm vào danh sách khóa cho phép của AC-454.
- [AC-513]: **`login.html` chuyển người đã đăng nhập (G5).** Khi tải: nếu **không** có cờ -> **0** yêu cầu `/api/**`, hiển thị form ngay như cũ. Nếu có cờ -> gọi `api.me({ anonymous: true })`; 200 -> `location.replace(...)` theo G5 (dùng `sanitizeReturnUrl` với role của user; `mustChangePassword` luôn về `/change-password.html`); 401 -> `clearSignedIn()` và hiển thị form. Form không bị ẩn trong lúc chờ. Thông báo `expired=1` vẫn hiển thị như cũ (AC-91).
- [AC-514]: **Landing chuyển người đã đăng nhập (G6) và điều chỉnh AC-342.** `landing.js` chỉ gọi `api.me({ anonymous: true })` **khi có cờ**; 200 -> `location.replace(mustChangePassword ? '/change-password.html' : homeFor(user))`; 401 -> xóa cờ, trang giữ nguyên. Vì vậy AC-342 ("mở `/` tạo 0 yêu cầu `/api/**`") **vẫn đúng** cho khách chưa từng đăng nhập hoặc đã đăng xuất; với người có cờ, tối đa **1** yêu cầu `GET /api/auth/me` và không thêm yêu cầu nào khác. Nội dung landing hiển thị ngay, không bị ẩn trong lúc chờ. Landing không import `auth.js`/`header.js` (không dựng sidebar).
- [AC-515]: **Chuyển hướng sau đăng nhập an toàn ở client.** `login.js` chỉ gán `location.href = result.redirectUrl` khi giá trị là chuỗi bắt đầu bằng đúng một `/` (không `//`, không `/\`, không chứa ký tự điều khiển, không có `:` trước dấu `/` đầu tiên); nếu không hợp lệ thì dùng `homeFor(result.user)`. Không bao giờ ghép `redirectUrl` với `API_BASE`. `returnUrl` lấy từ query vẫn bị cắt ≤ 300 ký tự trước khi gửi (đã có) và máy chủ vẫn là nơi sanitize chính thức.
- [AC-516]: **401 đúng ngữ cảnh, không vòng lặp.** `goToLogin`: không thêm `returnUrl` khi đường dẫn hiện tại là `/`, `/index.html`, hay bắt đầu bằng `/login.html`; thêm `expired=1` **chỉ khi** `hasSignedInHint()` là true; luôn `clearSignedIn()`; chỉ chạy một lần (`redirecting`). Khách chưa từng đăng nhập mở `/dashboard.html` được chuyển tới `/login.html?returnUrl=%2Fdashboard.html` **không** có `expired=1` (sửa lỗi thông báo sai nêu ở mục 0.1). `watchIdentity` (AC-100) giữ nguyên và dùng `homeFor` mới.
- [AC-517]: **Test tương đương cho guard.** Tồn tại `tests/frontend/guard.test.mjs` (Node `node:test`, không phụ thuộc gói ngoài) import `frontend/js/guard.js` và kiểm tra `sanitizeReturnUrl` với **bảng véc-tơ ở mục 4.1** (đủ hai role) và `homeFor`; `api.js`'s `apiUrl` được kiểm với `API_BASE` = `''`, `'http://localhost:5000'`, `'http://localhost:5000/'` (nếu `apiUrl` được export hoặc tách ra `config.js`/`guard.js` để import không cần DOM; Dev chọn cách tách miễn import được trong Node). Nếu máy QA có Node: `node --test tests/frontend` -> 0 fail. Nếu không có Node: QA đối chiếu bảng véc-tơ với mã bằng đọc và ghi chú trong báo cáo. Kết quả phải **cùng** bảng véc-tơ với test C# của AC-501.

### 3.7 Chạy dev và triển khai (QD-96, QD-97)

- [AC-518]: **`.claude/launch.json` hợp lệ và có cấu hình frontend.** Là JSON hợp lệ (phân tích được bằng `ConvertFrom-Json`/`python -m json.tool`); giữ nguyên cấu hình `tinhoc12` (`dotnet run --project src/Web`, `port: 5000`); thêm cấu hình tên `frontend` với `port: 5173` chạy một static server phục vụ thư mục `frontend` (`python -m http.server 5173 --directory frontend` hoặc `npx http-server frontend -p 5173 -c-1`). `src/Web/Properties/launchSettings.json` giữ `http://localhost:5000`.
- [AC-519]: **README hướng dẫn chạy 2 tiến trình.** `README.md` có mục "Chạy" gồm đúng hai bước (backend `dotnet run --project src/Web` cổng 5000; frontend một trong hai lệnh ở QD-96 cổng 5173), địa chỉ mở `http://localhost:5173/`, nhắc dùng đúng `localhost` cho cả hai, nhắc yêu cầu Python 3 hoặc Node.js, và nêu `serve` (clean URLs) **không** được khuyến nghị cùng lý do.
- [AC-520]: **README mô tả cấu hình.** `README.md` mô tả: `frontend/js/config.js` và `API_BASE` (ý nghĩa `''`), `Cors:AllowedOrigins` (mảng, ví dụ biến môi trường `Cors__AllowedOrigins__0`, quy tắc định dạng origin ở AC-496), bảng đổi tên trang (mục 0.3), mục xử lý sự cố (CORS, sai site, `file://`, `/teacher` thiếu dấu `/`).
- [AC-521]: **README có cấu hình mẫu static host production.** `README.md` chứa cấu hình mẫu (nginx) có đủ: ba header bảo mật (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`), `Cache-Control: no-cache` cho trang/JS/CSS, `Cache-Control: public, max-age=31536000, immutable` cho `/fonts/`, MIME `font/woff2` và `text/javascript`, lưu ý `add_header` không kế thừa giữa các `location`, và `API_BASE`/`Cors:AllowedOrigins` dạng HTTPS. Có đoạn nêu header bảo mật cho **API** do backend đặt.
- [AC-522]: **README không còn mô tả cũ sai.** `README.md` có 0 lần `wwwroot` (ngoài một ghi chú lịch sử nếu có, nhưng không chỉ đường dẫn đang dùng); phần "Cấu trúc" liệt kê `frontend/`; câu "trang HTML được máy chủ chặn khi chưa đăng nhập/sai vai trò" được thay bằng mô tả guard phía trình duyệt + API là lớp bảo mật thật (QD-95); đường dẫn font/CSS/ảnh trong README trỏ `frontend/...`; câu "Mở http://localhost:5000/ -> chuyển tới trang đăng nhập" được sửa theo luồng mới. `database/HUONG-DAN-THEM-BAI.md` dòng 15 (mở `http://localhost:5000/`) được cập nhật sang `http://localhost:5173/` kèm nhắc chạy frontend.
- [AC-523]: **Không thêm công cụ bắt buộc.** Chạy ứng dụng chỉ cần `dotnet` + một static server tùy chọn; không `package.json`/`npm install` bắt buộc (AC-481). README nhắc kiểm tra static server trả `.js` với `Content-Type` JavaScript (`text/javascript`) vì ES modules sẽ không chạy nếu là `text/plain` (một số máy Windows cấu hình registry sai khiến `python -m http.server` trả sai MIME; khi gặp thì dùng `npx http-server`).

### 3.8 Test và build (QD-98)

- [AC-524]: **Build sạch.** `dotnet build D:\Education\src\TinHoc12.sln` thành công, 0 lỗi, và không thêm cảnh báo so với mốc Dev đã ghi (Dev báo số cảnh báo trước/sau).
- [AC-525]: **`dotnet test` xanh 100%.** `dotnet test D:\Education\src\TinHoc12.sln` không có test fail (test tự bỏ qua theo thiết kế như SQL Server/Python vẫn được phép bỏ qua). Dev báo cáo **mốc trước khi sửa** (số pass/fail/skip) và danh sách test lỗi thời được sửa cho khớp hành vi hiện hành (mục 0.1: `AuthTests`, `AuthApiTests`, `FrontendRulesTests` dòng ~208 và các test tương tự), **không sửa mã sản xuất để hợp với test cũ**, và không xóa test mà không có thay thế tương đương.
- [AC-526]: **Test giao diện tĩnh trỏ `frontend/`.** `RepoPaths` có thuộc tính `Frontend` = `Root/frontend`; `FrontendRulesTests` và `ArchitectureTests` dùng nó (0 lần `"Web", "wwwroot"` hay `Wwwroot => ...Src` trong `tests/`, ngoại trừ test khẳng định `src/Web/wwwroot` **không tồn tại**); danh sách trang kỳ vọng khớp 15 trang mới (có `index.html`, `lessons.html`, `dashboard.html`, các trang `teacher/*`) và tiêu đề khớp AC-478; luật URL cho phép `http://localhost:5000` chỉ ở `js/config.js` (AC-488); có test cho AC-479, AC-480, AC-481, AC-486, AC-487, AC-509 (đếm đúng 13 trang `data-guard`, landing và login không có), AC-512 (storage), AC-484.
- [AC-527]: **Test `Web.Tests` thay thế đúng.** Lớp `PageGuardTests` cũ và hai test đọc `wwwroot` (font, ảnh) được **gỡ**; thay bằng: (a) `CorsTests` phủ AC-491 đến AC-497 (kể cả test cấu hình sai); (b) `ApiOnlyTests` phủ AC-499; (c) test font/ảnh đọc từ `frontend/` ở `Application.Tests` (13 font, 5 ảnh PNG, `img` chỉ `.png`). Các test còn lại sửa: `RateLimitAndConfigTests.Production_over_plain_http_...` dùng đường dẫn `/api/lessons` thay `/login.html`; `AuthApiTests.There_is_no_self_registration` kiểm `frontend/register.html` không tồn tại thay vì `GET /register.html`; `Security_headers_are_present...` chỉ lặp trên URL `/api/...` và một URL 404 (AC-499); `StudentAndTeacherFlowTests` dòng ~275 và ~284 bỏ các kiểm tra `GET /index.html` (302/200) và thay bằng kiểm tra API tương đương (`/api/lessons` 403 `PASSWORD_CHANGE_REQUIRED` rồi 200 sau khi đổi mật khẩu, đã có ở dòng 271-283).
- [AC-528]: **Test `ReturnUrlPolicy` mới.** `Application.Tests` có test (ví dụ `ReturnUrlPolicyTests`) phủ toàn bộ ca ở AC-501 và `AuthTests` dùng `/dashboard.html`, `/teacher/dashboard.html` cho `RedirectUrl`; bảng véc-tơ ở mục 4.1 được dùng làm dữ liệu cho cả test C# và `guard.test.mjs`.
- [AC-529]: **Tổng số test không giảm vô cớ.** Số test (`dotnet test --list-tests | Measure-Object`) sau khi hoàn tất ≥ số test trước khi sửa trừ các test của `PageGuardTests` (số lượng được ghi cụ thể trong báo cáo của Dev), và mọi AC cũ có test tự động (AC-95 đến AC-99 phần API, AC-319 đến AC-325 phần `ReturnUrlPolicy`) vẫn còn test tương đương hoặc có ghi chú thay thế trong mục 10.
- [AC-530]: **Không còn tham chiếu mồ côi.** Trong `src/` và `tests/` (loại `bin/obj`): 0 lần `wwwroot` (ngoại trừ test AC-526 khẳng định thư mục không tồn tại), 0 lần `landing.html` ngoài `ReturnUrlPolicy` (AC-501) và test của nó, 0 lần `UseStaticFiles`/`UseDefaultFiles`; trong chú thích ở đầu `style.css` các nhắc `landing.html` được đổi thành `index.html`.

### 3.9 Bất biến và hồi quy (QD-99)

- [AC-531]: **Tập yêu cầu mạng không đổi.** Với bốn luồng ở AC-309/AC-451 (đăng nhập -> học -> nộp -> đăng xuất; xem đáp án; giáo viên soạn -> giao -> theo dõi; đổi mật khẩu), tập yêu cầu `/api/**` (phương thức, đường dẫn, thân) **giống** trước khi tách, trừ các khác biệt được phép: (a) `OPTIONS` preflight do trình duyệt tự gửi; (b) tối đa 1 `GET /api/auth/me` ở landing hoặc login khi có cờ `th12.signedin` (AC-513, AC-514); (c) các điểm ngoại lệ đã nêu ở AC-451. Tiền tố của mọi URL là `API_BASE` (kiểm bằng dò `fetch` hoặc theo dõi mạng).
- [AC-532]: **Bất biến id/class DOM và giao diện.** Ngoài việc thêm thuộc tính `data-guard`, khối `<noscript>`, đổi tên đích liên kết và hai tệp mới, **không** id/class/văn bản hiển thị nào bị đổi (AC-452); `style.css` chỉ thêm khối của AC-509; ngày giờ, chuỗi tiếng Việt, tiêu đề trang (trừ do đổi tên tệp, vẫn cùng chuỗi cũ) giữ nguyên. Đối chiếu bằng manifest (AC-482) và bằng đọc diff của các tệp trong danh sách "được phép sửa".
- [AC-533]: **An toàn XSS và quy ước mã giữ nguyên.** Trong `frontend/js`: 0 `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `eval(`, `new Function(` (AC-453); 0 `console.log`/`console.debug`; 0 `document.cookie`; không `setInterval`; các luật `FrontendRulesTests` hiện có vẫn đạt trên thư mục mới.
- [AC-534]: **Kiểm tay luồng hai-origin (Thủ công, do Khách hàng/Dev thực hiện; QA ghi nhận kết quả nếu có trình duyệt).** Với backend ở `http://localhost:5000` và frontend ở `http://localhost:5173`: (1) mở `/` chưa đăng nhập thấy landing, tab Network không có yêu cầu `/api`; (2) mở thẳng `/dashboard.html` -> sang `/login.html?returnUrl=%2Fdashboard.html` không có thông báo hết hạn; (3) đăng nhập `hs01` / `HocSinh@2026` -> `/dashboard.html`, tải dữ liệu bình thường, Application cookie `dkedu.auth` có `HttpOnly` và `SameSite=Strict`; (4) mở `/teacher/index.html` -> `/dashboard.html?notice=forbidden`; (5) đăng xuất rồi nhấn Back -> trang tải lại và về đăng nhập; (6) đăng nhập `giaovien` -> `/teacher/dashboard.html`; mở `/dashboard.html` -> `/teacher/dashboard.html`; soạn bài, giao bài (các thao tác ghi dùng CSRF) thành công; (7) đăng nhập lỗi 3 lần -> thông báo chung; vượt giới hạn -> thông báo "Bạn thử quá nhiều lần" (đọc được `Retry-After`); (8) đổi `127.0.0.1:5173` -> đăng nhập không giữ được phiên (đúng như README mô tả); (9) tắt backend -> "Không kết nối được máy chủ", trang không bị trắng. Mỗi mục ghi Pass/Fail.
- [AC-535]: **Hồi quy các AC cũ.** Các AC v2-v5 không nêu ở mục 10 vẫn đạt; riêng các AC mà bằng chứng kiểm tra là "máy chủ trả header/redirect cho HTML" (AC-95, AC-99 phần HTML, AC-319 đến AC-322, AC-324, AC-80 phần phục vụ ảnh, AC-173/AC-304 phần cache font) **được diễn giải lại** theo bảng mục 10 và kiểm bằng các AC tương ứng của tài liệu này, không còn tính là lỗi.

---

## 4. Bảng véc-tơ chuẩn và thứ tự triển khai

### 4.1 Véc-tơ kiểm thử `sanitizeReturnUrl` / `ReturnUrlPolicy.Sanitize` (dùng chung cho C# và Node)

| Role | Đầu vào | Kết quả |
|---|---|---|
| Student | `/lessons.html` | giữ nguyên |
| Student | `/lesson.html?id=x` | giữ nguyên |
| Student | `/problem.html?lesson=a&problem=b` | giữ nguyên |
| Student | `/dashboard.html?x=1` | giữ nguyên |
| Student | `/assignments.html` | giữ nguyên |
| Student | `/change-password.html` | giữ nguyên |
| Student | `/teacher`, `/teacher/`, `/teacher/index.html`, `/TEACHER/dashboard.html` | `null` |
| Student, Teacher | `/`, `/index.html`, `/index.html?x=1`, `/index.html#a`, `/INDEX.HTML`, `/landing.html`, `/landing.html?x=1` | `null` |
| Student, Teacher | `/login.html`, `/login.html?returnUrl=%2F`, `/api/lessons`, `/API/me` | `null` |
| Student, Teacher | `//evil.com`, `/\evil.com`, `https://evil.com`, `javascript:alert(1)`, `data:text/html,x`, `/a\r\nb`, `/a\u007fb`, `""`, chuỗi `/` + 400 ký tự `a` | `null` |
| Student, Teacher | `null`/`undefined` (chỉ Node) | `null` |
| Teacher | `/teacher/index.html`, `/teacher/`, `/teacher/dashboard.html` | giữ nguyên |
| Teacher | `/lessons.html`, `/lesson.html?id=x`, `/change-password.html` | giữ nguyên |
| Teacher | `/assignments.html`, `/dashboard.html`, `/dashboard.html?x=1` | `null` |

(Quy ước: `ReturnUrlPolicy` hiện không phân biệt hoa/thường ở `/api/`, `/login.html`, `/teacher/`; riêng các dòng có `/INDEX.HTML`, `/TEACHER/...`, `/API/...` Dev dùng `OrdinalIgnoreCase` cho khớp.)

### 4.2 Thứ tự triển khai cho Dev (3 giai đoạn, mỗi giai đoạn chạy build + test)

**Giai đoạn 0 – Mốc.** `dotnet build`, `dotnet test` ghi mốc; tạo `docs/frontend-manifest.sha256` (hash toàn bộ tệp `wwwroot` hiện tại); ghi kích thước JS+CSS.

**Giai đoạn 1 – Backend API-only + CORS.** 1. Thêm `Cors` vào `appsettings*.json`, lớp cấu hình/kiểm tra, `UseCors` đúng vị trí (AC-497). 2. Cập nhật `ReturnUrlPolicy` (AC-501) + test đơn vị (AC-528). 3. Gỡ `PageGuardMiddleware`, `UseDefaultFiles`, `UseStaticFiles` (AC-498). 4. Viết `CorsTests`, `ApiOnlyTests`; gỡ `PageGuardTests`; sửa test lỗi thời (AC-525, AC-527). 5. `dotnet test` xanh (lúc này test UI trong `tests/` vẫn trỏ `wwwroot` nên giữ tạm bằng cách làm giai đoạn 2 ngay sau hoặc làm cùng lúc để không đỏ).

**Giai đoạn 2 – Di chuyển frontend + đổi tên + cấu hình API.** 1. Di chuyển `src/Web/wwwroot` -> `frontend/` (AC-477), đổi tên 3 tệp (AC-478), cập nhật liên kết (AC-479). 2. `config.js`, `api.js` (AC-484 đến AC-488). 3. `RepoPaths.Frontend` và các test UI (AC-526). 4. Chạy quét tài nguyên (AC-480).

**Giai đoạn 3 – Guard + tài liệu + công cụ dev.** 1. `guard.js`, `storage.js` (cờ), `auth.js`, `login.js`, `landing.js`, `api.js` (`goToLogin`) (AC-507 đến AC-516). 2. `data-guard`, `<noscript>`, CSS (AC-509). 3. `guard.test.mjs` (AC-517). 4. `.claude/launch.json`, README, `database/HUONG-DAN-THEM-BAI.md` (AC-518 đến AC-523). 5. Cập nhật manifest nhãn `MODIFIED`; `dotnet build`, `dotnet test` xanh; kiểm thử tay AC-534.

**Lưu ý kỹ thuật cho Dev**
- Đặt `UseCors` ngay sau `SecurityHeadersMiddleware` và trước `RequestLimitsMiddleware`; `ApiExceptionMiddleware` đứng ngoài cùng nên 500 nhận CORS hay không tùy thứ tự: nếu `ApiExceptionMiddleware` ghi lại phản hồi từ đầu thì header CORS có thể mất; Dev kiểm bằng một test 500 giả (tùy chọn, không thuộc AC bắt buộc).
- Chính sách CORS phải dùng danh sách origin đã kiểm tra, `AllowCredentials()`, `WithMethods(...)`, `WithHeaders("Content-Type", "X-CSRF-TOKEN")`, `WithExposedHeaders("Retry-After")`, `SetPreflightMaxAge(TimeSpan.FromSeconds(600))`.
- Với `api.js`: giữ nguyên chữ ký `call(method, path, body, opts)` và các chuỗi `'/api/...'` trong bảng `api` để test hiện có (`call('POST', '/api/auth/login', body`) tiếp tục đạt; chỉ `fetch` thêm `apiUrl`.
- `identityChanged`, `loadCsrf` cũng phải dùng `apiUrl` và `credentials: 'include'`.
- `landing.js`/`login.js` không import `auth.js` (tránh kéo `header.js`); dùng `api.me` và `guard.js` trực tiếp.
- Vì `api.js` import `config.js` và `guard.js` import `api.js` (hoặc ngược lại), tránh vòng phụ thuộc: đặt `homeFor`, `sanitizeReturnUrl`, `apiUrl` ở `guard.js`/`config.js` thuần (không import gì), `api.js` import từ đó.
- CSS: luật `html[data-guard] body` đặt đầu `@layer base` và không dùng `!important`.

---

## 5. Rủi ro & lưu ý

1. **Rủi ro cao nhất: sai site làm mất phiên** (QD-90). Triệu chứng dễ nhầm với lỗi backend; bắt buộc có README (AC-505) và khuyến nghị dùng `localhost` nhất quán.
2. **Static server trả MIME sai cho `.js`** (một số máy Windows với `python -m http.server`) làm ES module không tải được (AC-523). Phương án: `npx http-server`.
3. **Mất lớp chặn HTML phía máy chủ:** trang HTML nay công khai với mọi người biết URL; chỉ lộ giao diện, không lộ dữ liệu (QD-95). Bất kỳ người nào có thể xem mã nguồn JS vốn đã công khai trong mô hình cũ (trang tĩnh).
4. **Nhá nội dung và bộ nhớ đệm:** thay `no-store` của máy chủ bằng `data-guard` + `pageshow` (QD-93). Nếu `<noscript>` hay CSS thiếu ở một trang, trang đó nhá khung trống hoặc hiện sớm; AC-509 kiểm đủ 13 trang.
5. **Đổi tên trang làm gãy dấu trang cũ** (câu hỏi 3). Không có chuyển hướng 301 vì static server không biết.
6. **CORS cấu hình sai** là lỗi triển khai thường gặp: README có mục xử lý sự cố; backend báo lỗi sớm khi cấu hình định dạng sai (AC-496). Dev không dùng `AllowAnyOrigin`.
7. **`Retry-After` bị trình duyệt chặn nếu quên `Expose-Headers`** (AC-495): giao diện đăng nhập mất thông tin chờ lại; đã đưa vào AC.
8. **Production phụ thuộc HTTPS ở cả hai phía:** cookie `Secure` không được lưu qua HTTP; README nhắc.
9. **Cờ `th12.signedin` là gợi ý, không bảo mật:** không ai có thể dựa vào nó; nếu bị giả mạo chỉ khiến landing/login gọi `/me` thêm một lần.
10. **Baseline test không rõ:** BA không chạy được `dotnet test` (mục 0.1); nếu số test lỗi thời lớn, Dev báo ngay để Khách hàng biết phạm vi sửa test.
11. **Hiệu năng:** thêm một preflight `OPTIONS` cho mỗi lời gọi ghi/`Content-Type: application/json` ở lần đầu (sau đó được cache 600 giây theo từng URL); chấp nhận được, không thêm AC hiệu năng.
12. **Không có dedupe hash tên tệp:** cache trình duyệt có thể giữ JS cũ sau khi cập nhật nếu static host cấu hình cache mạnh; QD-97 yêu cầu `no-cache`.
13. **BA chưa đọc nguyên văn** `js/header.js` (chỉ các dòng liên quan), `js/landing.js`, `js/search.js`, `js/storage.js`, `js/runner.js`: các điểm cần sửa trong chúng (liên kết `/index.html`, vị trí `Worker`, hàm trong `storage.js`) do Dev xác nhận bằng grep theo AC-479/AC-480/AC-512.

---

## 6. Bàn giao

Đặc tả phiên bản 6 hoàn tất, bàn giao cho `@dev-engineer` triển khai theo mục 4.2 (giai đoạn 0 đến 3) tại `D:\Education\frontend` (mới), `D:\Education\src\Web`, `D:\Education\src\Application\Security\ReturnUrlPolicy.cs`, `D:\Education\tests`, `D:\Education\README.md`, `D:\Education\.claude\launch.json`; sau mỗi giai đoạn chạy `dotnet build` và `dotnet test`. `@qa-tester` kiểm thử theo **AC-477 … AC-535** (AC-534 là kiểm tay) và hồi quy theo AC-535. Mọi logic phát sinh (bảng guard G1-G8, cờ `th12.signedin`, quy tắc CORS, ánh xạ đổi tên, véc-tơ `Sanitize`) đã được định nghĩa trong tài liệu này trước khi code (CLAUDE.md mục 3).

## 7. Số lượng AC mới

**59 AC mới: AC-477 … AC-535** (cấu trúc 7; cấu hình API 6; CORS 8; backend API-only 5; cookie/CSRF/site 4; guard phía trình duyệt 11; chạy dev & tài liệu 6; test & build 7; bất biến & hồi quy 5). Quyết định mới: **QD-87 … QD-99** (13 mã).

## 8. Test hiện có phải cập nhật (`tests/`)

| Test | Vấn đề | Cập nhật |
|---|---|---|
| `Application.Tests/FrontendRulesTests` (hằng `Wwwroot`, dòng 9) | Trỏ `src/Web/wwwroot` | Trỏ `RepoPaths.Frontend`; danh sách trang (dòng 31-33) thành 15 trang mới; `[InlineData]` tiêu đề (dòng 92-93) thêm `lessons.html`/`index.html` mới; luật URL cho phép (dòng ~762) thêm `http://localhost:5000` chỉ cho `js/config.js`; ngưỡng `theme-init.js` (dòng 208) cho khớp 1,5 KB |
| `Application.Tests/ArchitectureTests` (dòng 178-189) | Trỏ `wwwroot` | Trỏ `RepoPaths.Frontend`; giữ luật "không `*.json`, không thư mục `data`, không `fetch(...json)`" |
| `Application.Tests/RepoPaths` | Chưa có `Frontend` | Thêm `public static string Frontend => Path.Combine(Root, "frontend");` |
| `Application.Tests/AuthTests` (dòng 26-27, 42, 46, 283) | Kỳ vọng `/index.html`, `/teacher/index.html` (lỗi thời) | `/dashboard.html`, `/teacher/dashboard.html`; thêm `ReturnUrlPolicyTests` (AC-528) |
| `Web.Tests/PageAndRoleTests` › `PageGuardTests` | Kiểm chặn HTML phía máy chủ | **Gỡ**; thay bằng `ApiOnlyTests` (AC-499) và `CorsTests` (AC-491 đến AC-497); các test font/ảnh chuyển sang `Application.Tests` đọc `frontend/` |
| `Web.Tests/PageAndRoleTests` › `RateLimitAndConfigTests.Production_over_plain_http...` | Dùng `/login.html` | Dùng `/api/lessons` |
| `Web.Tests/AuthApiTests` (dòng 29-31, 72-75, 269, 276) | `redirectUrl` lỗi thời, đọc `wwwroot/register.html`, kiểm header trên HTML | Sửa theo AC-502/AC-527 |
| `Web.Tests/StudentAndTeacherFlowTests` (dòng 275, 284) | `GET /index.html` | Bỏ, dùng kiểm tra API tương đương |
| **Mới** | AC-490 đến AC-497, AC-499, AC-501 | `CorsTests`, `ApiOnlyTests`, `ReturnUrlPolicyTests` |
| **Mới (Node, tùy chọn)** | AC-517 | `tests/frontend/guard.test.mjs` |
| **Mới (kiểm tay/trình duyệt)** | AC-534 | Danh sách kiểm tay |

## 9. Câu hỏi và giả định cần Khách hàng xác nhận

1. **Mô hình triển khai thật:** frontend và API sẽ cùng "site" (ví dụ `app.<tên-miền>` + `api.<tên-miền>`, hoặc cùng origin sau reverse proxy), cả hai HTTPS? Nếu bắt buộc **khác site**, phải đổi cookie sang `SameSite=None` (yếu hơn, quyết định riêng, ngoài phạm vi). (QD-90)
2. **Chuyển người đã đăng nhập khỏi landing/login:** BA mặc định dùng cờ `th12.signedin` trong `localStorage` để tránh gọi API thừa và tránh lỗi 401 trong console với khách vô danh. Phương án thay thế: luôn gọi `GET /api/auth/me` khi mở landing/login (đơn giản hơn, nhưng mọi khách vô danh tạo 1 yêu cầu 401 và AC-342 "0 yêu cầu API" phải bỏ). Đồng ý với mặc định? (QD-94)
3. **Đổi tên trang** (`landing.html` -> `index.html`, `index.html` -> `lessons.html`): dấu trang `/index.html` cũ của học sinh sẽ mở landing rồi tự vào Tổng quan; đồng ý? (QD-92)
4. **Công cụ chạy frontend:** Python (`http.server`) hay Node (`http-server`) là chính trong tài liệu? BA liệt kê cả hai; máy Khách hàng/Dev có cái nào? Production dự kiến dùng nginx, IIS, hay dịch vụ lưu trữ tĩnh/CDN (ảnh hưởng ví dụ cấu hình ở README, QD-97)?
5. **Có cần giữ chế độ "một lệnh chạy cả hai" (ASP.NET vẫn phục vụ `frontend/` bằng `UseStaticFiles` trỏ thư mục này) cho tiện dev/triển khai nhỏ không?** BA mặc định **không** (API-only theo yêu cầu "tách độc lập"); nếu cần, thêm như tùy chọn cấu hình ở phiên bản sau.
6. **Test lỗi thời:** một số test hiện có đang kỳ vọng URL đích cũ (mục 0.1). Dev sẽ sửa test cho khớp hành vi hiện hành, không đổi hành vi; xác nhận đây là hướng đúng.

## 10. Nhật ký thay đổi so với v2, v3, v4, v5

### 10.1 AC/QD bị **thay thế hoặc diễn giải lại**

| Mã cũ | Thay đổi bởi v6 |
|---|---|
| **QD-17** ("cùng nguồn/same-origin" cho cookie phiên) | Diễn giải lại: **cùng site** (QD-90); cookie không đổi thuộc tính |
| **QD-25**, **AC-95** (chặn trang HTML phía máy chủ, 302) | **Thay thế** bởi guard phía trình duyệt (QD-93, AC-507 đến AC-516) + API 401/403 (QD-95). Backend trả 404 cho HTML (AC-499) |
| **QD-62**, **AC-319**, **AC-320**, **AC-321** (`DefaultFileNames = { landing.html, index.html }`; `/` phục vụ landing; chuyển hướng người đã đăng nhập ở máy chủ) | **Thay thế**: `/` là `index.html` (landing) do static server; chuyển người đã đăng nhập do `landing.js` (AC-514). Phần "trang bảo vệ trả 302 khi chưa đăng nhập" nay do `initPage` (G1) |
| **AC-322** (phân quyền theo role cho trang mới, 302 ở máy chủ) | Hành vi giữ, cơ chế đổi sang G3, G4 (AC-508); phần `redirectUrl` của API giữ nguyên (AC-502) |
| **AC-323** (`ReturnUrlPolicy`) | Giữ; `index.html` (landing mới) bị chặn như `/`; `/lessons.html` thay vai trò `/index.html` cũ; mục 4.1 là bảng chuẩn mới (AC-501) |
| **AC-324** (`Cache-Control: no-cache` + `Vary: Cookie` cho `/`; `no-store` cho trang bảo vệ) | **Bỏ phần máy chủ**; thay bằng `data-guard` + `pageshow` (AC-509 đến AC-511) và khuyến nghị cache ở static host (AC-521) |
| **AC-325** (client khớp server) | Giữ, thêm `guard.js` (AC-508) |
| **AC-342** (landing: 0 yêu cầu `/api/**`) | Giữ cho khách chưa từng đăng nhập; với người có cờ tối đa 1 `GET /api/auth/me` (AC-514) |
| **AC-80** (ảnh 200 `image/png` do ASP.NET) | Cơ chế đổi: static host; kiểm bằng AC-477, AC-480, test đọc tệp |
| **AC-90** (nút Back không hiện lại dữ liệu cũ nhờ `no-store`) | Cơ chế đổi: `pageshow.persisted` -> reload (AC-511) |
| **AC-99** ("mọi phản hồi" có header bảo mật) | Giữ cho mọi phản hồi **của backend** (kể cả 404, AC-499); header cho trang tĩnh chuyển sang static host (QD-97, AC-521) |
| **AC-173, AC-304** (font cache 1 năm do ASP.NET) | Chuyển sang cấu hình static host (AC-521); tệp font vẫn đủ 13 (AC-477) |
| **AC-63** (frontend chỉ lấy dữ liệu qua `/api`) | Giữ, mạnh hơn: mọi lời gọi qua `api.js` với `API_BASE` (AC-485, AC-487) |
| **AC-83** (quét `wwwroot/js` tìm `localStorage.setItem` với dữ liệu xác thực) | Quét `frontend/js`; ngoại lệ duy nhất: cờ `th12.signedin` không nhạy cảm (AC-512) |
| **AC-454** (khóa `localStorage` mới duy nhất `th12.sidebar`) | Thêm khóa `th12.signedin` (AC-512) |
| **AC-455 đến AC-457** (ngân sách tệp, head, không script nội dòng) | Giữ nguyên, áp dụng cho `frontend/` (AC-483); thêm khối `<noscript><style>` (không phải script) |
| **AC-451** (cùng tập yêu cầu mạng) | Giữ, ngoại lệ thêm: preflight và tối đa 1 `/me` ở landing/login (AC-531) |
| **AC-462, AC-464** (test hiện có; README) | Cập nhật bởi AC-525 đến AC-530 và AC-518 đến AC-523 |
| **README mục "Chạy", "Cấu trúc", "Bảo mật", "Giao diện"** | Cập nhật theo AC-519 đến AC-522 |

### 10.2 Chuỗi/Id bị đổi có chủ đích
Tên tệp: `landing.html` -> `index.html`; `index.html` -> `lessons.html`; `js/index.js` -> `js/lessons.js`. Địa chỉ phục vụ giao diện: từ `http://localhost:5000/` sang `http://localhost:5173/`. Không có id/class/chuỗi hiển thị nào khác bị đổi.

### 10.3 Quyết định (QD) mới
QD-87 … QD-99 (mục 0.2).

### 10.4 Không đổi
Toàn bộ hợp đồng API (43 endpoint), mô hình dữ liệu, xác thực/phiên/CSRF/khóa tạm, phân quyền, nghiệp vụ, giao diện (CSS, chuỗi, bố cục), và mọi AC v2-v5 không nêu ở 10.1.

# DK Edu – trang học Tin học lớp 12 (Python)

Hệ thống gồm hai phần **độc lập**: **API** ASP.NET Core (.NET 10, Clean Architecture) và **giao diện** HTML/CSS/JS thuần trong thư mục `frontend/` (ES modules, không build, không thư viện ngoài). Có **đăng nhập** và **2 vai trò**:
- **Giáo viên** soạn bài, soạn bài tập (kèm test case, đáp án), xuất bản, **giao bài** cho cả lớp/tất cả học sinh kèm hạn nộp, theo dõi tiến độ và xem mã học sinh đã nộp, quản lý lớp và tài khoản học sinh.
- **Học sinh** đọc bài, xem các bài tập **được giao** (kèm hạn nộp, trạng thái), làm bài với trình soạn code, chạy/chấm ngay trên trình duyệt (Pyodide trong Web Worker); khi **Nộp bài** kết quả được gửi lên máy chủ để giáo viên theo dõi.

Thương hiệu: logo và tên **DK Edu** (ảnh gốc ở `docs/brand/`, bản tối ưu ở `frontend/img/`).

## Cấu trúc
```
src/TinHoc12.sln
  Domain/          Entity (Lesson, Exercise, TestCase, User, SchoolClass, Assignment, Submission...) – không phụ thuộc gì
  Application/     Use case, interface repository/IPasswordHasher/IClock, DTO (chỉ phụ thuộc Domain)
  Infrastructure/  AppDbContext (EF Core), cấu hình entity, Migrations, repository, PBKDF2 hasher
  Web/             CHỈ là API (/api/auth, /api/me, /api/teacher...), CORS, middleware bảo mật – không phục vụ HTML/CSS/JS
frontend/          Giao diện độc lập: index.html (landing), login.html, lessons.html, dashboard.html..., teacher/, css/, js/, fonts/, img/
tests/             Application.Tests, Infrastructure.Tests, Web.Tests (xUnit; Web.Tests dùng WebApplicationFactory), frontend/guard.test.mjs (Node)
database/          schema.sql, seed-*.sql, them-bai-moi.sql, mo-khoa-tai-khoan.sql, HUONG-DAN-*.md
docs/              specs/ (đặc tả v2 đến v6), brand/ (logo gốc), frontend-manifest.sha256 (băm tệp khi tách frontend)
```

## Yêu cầu
- .NET SDK 10, kết nối Internet lần đầu (tải Pyodide từ CDN jsdelivr, phiên bản cố định v0.26.4).
- SQL Server Express là cơ sở dữ liệu duy nhất của ứng dụng.
- Để chạy giao diện tách riêng: **Python 3** hoặc **Node.js** (một trong hai, chỉ dùng làm static server; không cần `npm install`). Cách chạy nhanh bên dưới không cần cái nào.

## Chạy nhanh (một lệnh)
```
dotnet run --project src/Web
```
Ở Development, backend phục vụ luôn thư mục `frontend/` tại **http://localhost:5000/** (cùng origin với API) và tự mở trình duyệt. Sửa tệp trong `frontend/` rồi tải lại trang là thấy ngay (không cache).

Cấu hình trong `src/Web/appsettings.Development.json`, mục `Frontend`:
- `ServeFromBackend` (mặc định `false`; Development bật `true`): phục vụ `frontend/` từ backend. Production giữ `false`, backend chỉ là API.
- `Path` (mặc định `../../frontend`, tính từ `src/Web`): thư mục giao diện; thiếu `index.html` thì ứng dụng báo lỗi rõ ràng khi khởi động.
- `OpenBrowser` (Development bật `true`): tự mở trình duyệt khi khởi động; đặt `false` nếu không muốn.

`/api/**` không bị ảnh hưởng; đường dẫn không có tệp vẫn trả 404.

## Chạy tách riêng (hai tiến trình)
1. **Backend (API, cổng 5000)** – terminal A:
   ```
   dotnet run --project src/Web
   ```
2. **Frontend (cổng 5173)** – terminal B, một trong hai lệnh:
   ```
   python -m http.server 5173 --directory frontend
   npx http-server frontend -p 5173 -c-1
   ```

Mở **http://localhost:5173/** (landing; bấm "Đăng nhập"). Dùng đúng tên `localhost` cho **cả hai** địa chỉ, không trộn với `127.0.0.1` (xem mục "Cùng site").

- Không khuyến nghị `npx serve`: `serve` mặc định bật "clean URLs" (đổi `/login.html` thành `/login`), làm hỏng kiểm tra đường dẫn của giao diện và cần tệp cấu hình `serve.json`.
- Static server phải trả tệp `.js` với `Content-Type: text/javascript` (ES modules sẽ không chạy nếu là `text/plain`). Một số máy Windows có registry sai khiến `python -m http.server` trả sai MIME; khi gặp thì dùng `npx http-server`.
- Ở môi trường Development, khi khởi động API tự áp migration lên DB `DKEducation` của `.\SQLEXPRESS` (tạo mới nếu chưa có, không xóa dữ liệu đã có) và nạp `database/sqlserver/seed-sample.sql` + `database/sqlserver/seed-users.sql` nếu bảng bài học/tài khoản còn trống. Production không tự đụng tới DB.

**Tài khoản mẫu** (đổi trước khi triển khai thật): giáo viên `giaovien` / `GiaoVien@2026`; học sinh `hs01`, `hs02`, `hs03` / `HocSinh@2026`. Chi tiết: `docs/guides/HUONG-DAN-TAI-KHOAN.md`.

### Đổi chuỗi kết nối
Sửa `ConnectionStrings:DefaultConnection` trong `src/Web/appsettings.json` hoặc đặt biến môi trường `ConnectionStrings__DefaultConnection`.

### Nạp seed / tạo DB thủ công
- Tạo/nâng cấp DB: `dotnet ef database update --project src/Infrastructure --startup-project src/Web` hoặc chạy `database/sqlserver/schema.sql` (idempotent).
- Nạp dữ liệu mẫu: `database/sqlserver/seed-sample.sql` rồi `database/sqlserver/seed-users.sql` (`sqlcmd -S ".\SQLEXPRESS" -d DKEducation -I -f 65001 -i <tệp>`).

Môi trường thật phải dùng **HTTPS** (`ASPNETCORE_ENVIRONMENT=Production`); xem mục cấu hình `Auth:*` và khóa Data Protection trong `docs/guides/HUONG-DAN-TAI-KHOAN.md`.

Thêm bài bằng giao diện (khu giáo viên) hoặc bằng script SQL (`docs/guides/HUONG-DAN-THEM-BAI.md`).

## Cấu hình địa chỉ API và CORS
**Địa chỉ API (`frontend/js/config.js`).** Tệp xuất đúng một hằng `API_BASE`, mặc định `'http://localhost:5000'`. Khi triển khai chỉ cần sửa tệp này (không build).
- `API_BASE` là origin của backend, **không** có dấu `/` ở cuối và không có đường dẫn; ví dụ `'https://api.example.com'`.
- Chuỗi rỗng `''` nghĩa là cùng origin với trang giao diện (khi reverse proxy chuyển `/api` về backend).
- Giá trị này không đọc từ URL, `localStorage` hay DOM (chống bị dẫn tới API giả mạo). Mọi lời gọi API đi qua `frontend/js/api.js`.

**CORS ở backend (`Cors:AllowedOrigins`).** Mảng các origin giao diện được phép gọi API kèm cookie.
- Development (`appsettings.Development.json`): `["http://localhost:5173"]`. Mặc định (`appsettings.json`): mảng rỗng, tức không origin nào được phép.
- Biến môi trường: `Cors__AllowedOrigins__0=https://app.example.com`, `Cors__AllowedOrigins__1=...`.
- Mỗi giá trị phải có dạng `http(s)://tên-miền[:cổng]`: khớp tuyệt đối (scheme + tên miền + cổng), **không** dùng `*`, không có đường dẫn, không có dấu `/` cuối, không có `?` hay `#`. Giá trị sai làm API báo lỗi chứa `Cors:AllowedOrigins` ngay khi khởi động. Hợp lệ: `http://localhost:5173`, `https://app.example.com`, `https://app.example.com:8443`.
- Chính sách: cho phép cookie (`Access-Control-Allow-Credentials`), phương thức `GET, POST, PUT, PATCH, DELETE, OPTIONS`, header `Content-Type` và `X-CSRF-TOKEN`, công khai header `Retry-After`, cache preflight 600 giây.
- Nhiều origin giao diện (ví dụ 5173 và 3000): liệt kê tất cả.

### Cùng site (bắt buộc) và phục vụ ở gốc origin
Cookie phiên `dkedu.auth` và cookie CSRF `dkedu.csrf` giữ `SameSite=Strict`. Vì vậy giao diện và API phải **cùng "site"** (cùng tên miền đăng ký; khác cổng hoặc khác subdomain đều được):

| Giao diện | API | Kết quả |
|---|---|---|
| `http://localhost:5173` | `http://localhost:5000` | Hợp lệ |
| `https://app.example.com` | `https://api.example.com` | Hợp lệ (cùng tên miền gốc `example.com`) |
| `https://app.example.com` | `https://app.example.com` (reverse proxy chuyển `/api`, `API_BASE = ''`) | Hợp lệ |
| `http://127.0.0.1:5173` | `http://localhost:5000` | **Không hợp lệ** (hai site khác nhau) |
| `https://app.example.com` | `https://api.other-domain.com` | **Không hợp lệ** (hai tên miền gốc khác nhau) |

- **Triệu chứng khi sai site:** đăng nhập trả 200 nhưng ngay sau đó bị đưa về trang đăng nhập, vì trình duyệt từ chối lưu cookie `SameSite=Strict` từ phản hồi khác site.
- **Production bắt buộc HTTPS ở cả hai phía** (cookie `Secure`; trộn HTTP/HTTPS bị trình duyệt chặn).
- **Không hỗ trợ khác site**: sẽ cần đổi cookie sang `SameSite=None`, làm yếu phòng thủ CSRF, là quyết định riêng nằm ngoài phạm vi hiện tại.
- **Phục vụ ở gốc origin:** thư mục `frontend/` phải được phục vụ tại `/` của origin (không đường dẫn con như `/dkedu/`). Mọi trang dùng đường dẫn tuyệt đối `/css/`, `/js/`, `/img/`, `/fonts/`. Mở bằng `file://` **không** được hỗ trợ (ES modules và CORS).

### Đổi tên trang (so với phiên bản trước khi tách frontend)
| Trước | Sau |
|---|---|
| `landing.html` | `index.html` (landing, mở ở `/`) |
| `index.html` (danh sách bài học của học sinh) | `lessons.html` |
| `js/index.js` | `js/lessons.js` |

`teacher/index.html` giữ nguyên tên. Dấu trang cũ `/index.html` của học sinh nay mở landing (người đã đăng nhập được chuyển tới Tổng quan); dấu trang `/landing.html` cũ không còn (404). Nên dùng liên kết `/teacher/` (có dấu `/` cuối) vì `/teacher` không có dấu `/` tùy static server có thể trả 301 hoặc 404.

### Xử lý sự cố
- **"Không kết nối được máy chủ" ngay khi mở trang:** API chưa chạy, `API_BASE` sai, hoặc CORS chặn. Kiểm tra `Cors:AllowedOrigins` có đúng origin giao diện (đúng scheme, tên miền, cổng; không dấu `/` cuối).
- **Đăng nhập xong lại quay về trang đăng nhập:** sai site (xem bảng trên), thường do mở `127.0.0.1` thay vì `localhost`.
- **Trang trắng và console báo lỗi tải module / MIME:** static server trả `.js` sai `Content-Type`; dùng `npx http-server`.
- **Mở thẳng `file://...`:** không hỗ trợ; chạy qua static server.
- **Sau khi gỡ thư mục giao diện cũ khỏi `src/Web`, build/test báo `DirectoryNotFoundException` ở thư mục web gốc cũ:** thư mục `bin`/`obj` của `src/Web` còn bản kê tài nguyên tĩnh cũ; chạy `dotnet clean src/TinHoc12.sln` (hoặc xóa `src/Web/bin` và `src/Web/obj`) rồi build lại.

## Triển khai thật (ví dụ nginx cho frontend)
Header bảo mật và cache của trang tĩnh do static host đặt (backend chỉ đặt chúng cho phản hồi của **API**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`, và `Cache-Control: no-store` cho `/api`). Lưu ý nginx **không kế thừa** `add_header` ở cấp `server` vào `location` có `add_header` riêng, nên ba header bảo mật được lặp lại trong từng `location`:
```
server {
  listen 443 ssl;
  server_name app.example.com;
  root /var/www/dkedu/frontend;
  index index.html;
  types { text/html html; text/css css; text/javascript js; font/woff2 woff2; image/png png; }

  location / {
    try_files $uri $uri/ =404;
    add_header X-Content-Type-Options nosniff always;
    add_header X-Frame-Options DENY always;
    add_header Referrer-Policy same-origin always;
    add_header Cache-Control "no-cache" always;        # html/js/css không có tên tệp băm: luôn tái xác thực
  }
  location /fonts/ {
    add_header X-Content-Type-Options nosniff always;
    add_header X-Frame-Options DENY always;
    add_header Referrer-Policy same-origin always;
    add_header Cache-Control "public, max-age=31536000, immutable" always;
  }
}
```
Với cấu hình trên: `frontend/js/config.js` đặt `export const API_BASE = 'https://api.example.com';` và backend đặt `Cors__AllowedOrigins__0=https://app.example.com` (cả hai HTTPS, cùng site). IIS hoặc dịch vụ lưu trữ tĩnh cũng được, miễn đủ các điểm: phục vụ ở gốc, MIME `text/javascript` cho `.js`, `font/woff2` cho `.woff2`, và `Cache-Control: no-cache` cho `.html/.js/.css`.

## Bảo mật (tóm tắt)
Cookie phiên `HttpOnly`, `SameSite=Strict` (`Secure` ở Production); token CSRF (`X-CSRF-TOKEN`) cho mọi yêu cầu ghi; CORS chỉ cho các origin được liệt kê, không wildcard; mật khẩu băm PBKDF2-HMAC-SHA512; khóa tạm sau 5 lần sai; giới hạn tần suất đăng nhập theo IP; mọi API trả 401/403/404 đúng quy tắc; nội dung do người dùng nhập luôn hiển thị bằng `textContent`.

**Phân quyền:** trang HTML là tĩnh và công khai (không chứa dữ liệu nghiệp vụ). Phía trình duyệt có *guard* (`frontend/js/auth.js`, `frontend/js/guard.js`) chỉ để chuyển hướng và che nội dung khi chưa đăng nhập hoặc sai vai trò (trang bảo vệ mang `<html data-guard>` cho tới khi xác nhận xong). **Lớp bảo mật thật là API**: mọi dữ liệu chỉ lấy được qua API, và máy chủ luôn phân quyền 401/403/404. Cờ `th12.signedin` trong `localStorage` chỉ là gợi ý "đã từng đăng nhập" (không chứa id, vai trò hay token) để landing/đăng nhập biết có nên hỏi `/api/auth/me`.

## Build và kiểm thử
```
dotnet build src/TinHoc12.sln
dotnet test src/TinHoc12.sln
node --test "tests/frontend/*.test.mjs"      # tùy chọn: kiểm thử guard phía trình duyệt (Node >= 20.19 hoặc >= 22.7)
```
Test do nhóm kiểm thử quản lý, không cần SQL Server hay mạng (hiện dùng SQLite in-memory, repository giả, `WebApplicationFactory`); riêng một test kiểm thử migration/script T-SQL chạy trên SQL Server Express nếu máy có (tự bỏ qua nếu không).

## Giao diện
- Concept "Sổ tay lập trình": một màu nhấn lấy từ logo, hai giao diện Sáng/Tối (mặc định Tự động theo hệ điều hành, lưu ở `localStorage["th12.theme"]`). Toàn bộ CSS nằm trong `frontend/css/style.css` (`@layer tokens, base, components, pages`), mọi màu chỉ khai báo trong khối token ở đầu tệp.
- Thêm token mới: khai báo cùng tên ở ba nơi (`:root`, `:root[data-theme="dark"]` và khối `@media (prefers-color-scheme: dark)`), rồi dùng bằng `var(--tên)` ở nơi khác.
- Font tự lưu tại `frontend/fonts` (không gọi CDN lúc chạy): Be Vietnam Pro 400/600/700 và JetBrains Mono, giấy phép SIL OFL (`fonts/OFL.txt`), tải từ gói `@fontsource` qua jsDelivr, subset `latin`, `latin-ext`, `vietnamese`. Muốn thay font: thay tệp `woff2`, sửa `@font-face` và `--font-ui`/`--font-mono` trong `style.css`, giữ tổng dung lượng font <= 250 KB.
- Kiểm thử giao diện tĩnh nằm ở `tests/Application.Tests/FrontendRulesTests.cs` và `FrontendStructureTests.cs` (chạy bằng `dotnet test`); kiểm thử trên trình duyệt (axe, CLS, hai giao diện, luồng hai origin) do QA thực hiện riêng.

## API chính
- Xác thực: `GET /api/auth/csrf`, `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`, `POST /api/auth/change-password`.
- Nội dung: `GET /api/lessons`, `GET /api/lessons/{id}`, `GET /api/exercises/{id}/solution` (học sinh chỉ thấy bài đã xuất bản và bài tập được giao).
- Học sinh: `GET /api/me/assignments`, `POST /api/exercises/{id}/submissions`, `GET /api/me/exercises/{id}/submissions`, `GET /api/me/submissions/{id}`.
- Giáo viên: `/api/teacher/lessons`, `/exercises`, `/assignments`, `/progress`, `/students`, `/classes`.
- Lỗi: JSON `{error, message, field?, errors?}` (`UNAUTHENTICATED` 401, `FORBIDDEN` 403, `NOT_FOUND` 404, `CONFLICT`/`HAS_DEPENDENCIES`/`DUPLICATE_ID`/`DUPLICATE_USERNAME`/`EXERCISE_CHANGED` 409, `VALIDATION_FAILED` 422, `TOO_MANY_REQUESTS` 429...).

## Ghi chú
- Chấm bài vẫn ở trình duyệt học sinh nên kết quả **chưa được xác minh** phía máy chủ; chỉ để theo dõi, không dùng làm điểm thi chính thức. Test ẩn vẫn được gửi xuống trình duyệt (lý do kỹ thuật chấm ở client); đáp án chỉ lấy qua endpoint riêng, sau khi xác nhận và với bài được giao.
- Máy không có Python thì test chạy lời giải thật (AC-51) tự bỏ qua; trình duyệt vẫn chấm bằng Pyodide.

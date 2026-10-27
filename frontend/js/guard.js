// Guard phía trình duyệt thay cho PageGuardMiddleware cũ (QD-93, AC-507 đến AC-511, AC-515).
// Chỉ là trải nghiệm người dùng (chuyển hướng, che nội dung); lớp bảo mật thật là API (QD-95).
// Tệp KHÔNG import gì để api.js, auth.js, login.js, landing.js dùng chung và để Node import được khi kiểm thử (AC-517).
// Các hàm chạm DOM/location chỉ làm việc bên trong thân hàm, không chạy khi import.

const CHANGE_PASSWORD_PAGE = '/change-password.html';
const MAX_RETURN_URL = 300;

/** Nối origin API với đường dẫn; cắt mọi dấu '/' cuối của origin để không sinh '//api' (AC-485). */
export function joinApiUrl(base, path) {
  return String(base ?? '').replace(/\/+$/, '') + path;
}

/** Trang chủ theo vai trò (đúng ReturnUrlPolicy.HomeFor ở backend). */
export function homeFor(user) {
  return user && user.role === 'Teacher' ? '/teacher/dashboard.html' : '/dashboard.html';
}

/** Đường dẫn tương đối an toàn để chuyển hướng: đúng một '/' ở đầu, không '//', '/\', ký tự điều khiển (AC-515). */
export function isSafeRedirect(url) {
  if (typeof url !== 'string' || url.length === 0 || url.length > MAX_RETURN_URL) return false;
  if (url[0] !== '/' || url[1] === '/' || url[1] === '\\') return false;
  return !/[\u0000-\u001f\u007f-\u009f\\]/.test(url);
}

/**
 * Phản chiếu từng quy tắc của ReturnUrlPolicy.Sanitize (AC-501, AC-507): trả lại chuỗi gốc nếu hợp lệ và role được vào, ngược lại null.
 * @param {string|null|undefined} returnUrl
 * @param {'Teacher'|'Student'|string} role
 */
export function sanitizeReturnUrl(returnUrl, role) {
  if (!isSafeRedirect(returnUrl)) return null;
  const cut = returnUrl.search(/[?#]/);
  const path = (cut >= 0 ? returnUrl.slice(0, cut) : returnUrl).toLowerCase();

  // "/" và "/index.html" là landing; "/landing.html" là tên cũ đã bỏ; trang API và đăng nhập không phải đích sau đăng nhập.
  // (Dùng regex để chuỗi đường dẫn API chỉ xuất hiện trong api.js, AC-487, và không có liên kết '/index.html' nào trong mã, AC-479.)
  if (path === '/' || /^\/(index\.html$|api\/|login\.html|landing\.html)/.test(path)) return null;

  const teacherArea = path === '/teacher' || path.startsWith('/teacher/');
  if (teacherArea && role !== 'Teacher') return null;
  if (role === 'Teacher' && (path === '/assignments.html' || path === '/dashboard.html')) return null;
  return returnUrl;
}

/**
 * Đích chuyển hướng cho người ĐÃ đăng nhập (G5, G6): đổi mật khẩu trước, rồi returnUrl hợp lệ, cuối cùng trang chủ theo vai trò.
 */
export function signedInTarget(user, returnUrl = null) {
  if (user.mustChangePassword) return CHANGE_PASSWORD_PAGE;
  return sanitizeReturnUrl(returnUrl, user.role) ?? homeFor(user);
}

/**
 * Dùng ở landing và login (G5, G6, QD-94): chỉ hỏi /api/auth/me khi có gợi ý "đã từng đăng nhập".
 * 200 thì chuyển trang; 401 thì xóa gợi ý và để trang hiển thị bình thường. Trả về true nếu đã chuyển hướng.
 * Các phụ thuộc truyền vào để tệp này không phải import api.js/storage.js (tránh vòng phụ thuộc).
 */
export async function redirectIfSignedIn({ me, hasHint, clearHint, returnUrl = null }) {
  if (!hasHint()) return false;
  try {
    const user = await me({ anonymous: true });
    location.replace(signedInTarget(user, returnUrl));
    return true;
  } catch (err) {
    if (err && err.status === 401) clearHint();
    return false;
  }
}

let restoreHooked = false;

/** G7: trang được khôi phục từ bộ nhớ đệm back/forward thì tải lại để chạy lại guard (AC-511). Gọi nhiều lần vẫn chỉ đăng ký một lần. */
export function onRestoreFromCache() {
  if (restoreHooked) return;
  restoreHooked = true;
  window.addEventListener('pageshow', (e) => { if (e.persisted) location.reload(); });
}

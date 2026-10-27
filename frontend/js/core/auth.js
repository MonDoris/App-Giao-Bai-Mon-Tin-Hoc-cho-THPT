// Khởi tạo trang sau đăng nhập và là guard phía trình duyệt (QD-93, AC-508, AC-510): lấy người dùng (GET /api/auth/me),
// kiểm tra role, dựng thanh điều hướng, và phát hiện đổi tài khoản ở tab khác (AC-93, AC-100).
// Trang bảo vệ mang <html data-guard> (nội dung bị ẩn bằng CSS) cho tới khi xác nhận xong; các nhánh chuyển hướng KHÔNG bỏ ẩn.
import { api, setPageUserId, homeFor } from '/js/core/api.js';
import { onRestoreFromCache } from '/js/core/guard.js';
import { renderHeader } from '/js/components/header.js';
import { markSignedIn, setStorageUser } from '/js/core/storage.js';

let watching = false;

function never() {
  return new Promise(() => {});
}

/**
 * @param {{role?: 'any'|'Teacher'|'Student', active?: string}} options
 * @returns {Promise<{id:number, username:string, displayName:string, role:string, mustChangePassword:boolean}>}
 */
export async function initPage({ role = 'any', active = '' } = {}) {
  onRestoreFromCache(); // G7: quay lại bằng nút Back thì tải lại để chạy lại guard
  let user;
  try {
    user = await api.me();
  } catch (err) {
    if (err && err.status === 401) return never(); // api.js đã chuyển tới trang đăng nhập
    renderFatal('Không kết nối được máy chủ', 'Hãy kiểm tra kết nối rồi tải lại trang.');
    return never();
  }

  const onPasswordPage = location.pathname.toLowerCase() === '/change-password.html';
  if (user.mustChangePassword && !onPasswordPage) {
    location.replace('/change-password.html');
    return never();
  }
  if (role === 'Teacher' && user.role !== 'Teacher') {
    location.replace('/dashboard.html?notice=forbidden');
    return never();
  }
  if (role === 'Student' && user.role !== 'Student') {
    location.replace('/teacher/dashboard.html');
    return never();
  }

  // Đã xác nhận đúng người, đúng vai trò: hiện trang (trước khi dựng header) và ghi nhớ "đã từng đăng nhập" (QD-94).
  document.documentElement.removeAttribute('data-guard');
  markSignedIn();
  setPageUserId(user.id);
  setStorageUser(user.id);
  renderHeader(user, active);
  watchIdentity(user);
  return user;
}

function renderFatal(title, message) {
  document.documentElement.removeAttribute('data-guard'); // không để trang trắng vĩnh viễn khi không kết nối được (AC-489)
  const main = document.getElementById('main') || document.body;
  const box = document.createElement('div');
  box.className = 'state state--error';
  box.setAttribute('role', 'alert');
  const strong = document.createElement('strong');
  strong.textContent = title;
  const p = document.createElement('p');
  p.textContent = message;
  box.append(strong, p);
  main.append(box);
}

/** Khi tab lấy lại tiêu điểm mà tài khoản đã khác (đăng nhập tài khoản khác ở tab khác) thì chuyển đúng trang của tài khoản mới. */
export function watchIdentity(user) {
  if (watching) return;
  watching = true;
  let checking = false;
  const check = async () => {
    if (checking || document.hidden) return;
    checking = true;
    try {
      const now = await api.me({ anonymous: true });
      if (now.id !== user.id) location.href = homeFor(now);
    } catch (err) {
      if (err && err.status === 401) location.href = '/login.html?expired=1';
    } finally {
      checking = false;
    }
  };
  document.addEventListener('visibilitychange', check);
  window.addEventListener('focus', check);
}

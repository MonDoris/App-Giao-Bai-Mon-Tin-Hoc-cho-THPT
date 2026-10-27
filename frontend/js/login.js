import { api, ApiError } from '/js/api.js';
import { homeFor, isSafeRedirect, onRestoreFromCache, redirectIfSignedIn } from '/js/guard.js';
import { clearSignedIn, hasSignedInHint } from '/js/storage.js';
import { createThemeButton } from '/js/theme.js';
import { start as startLoginFx } from '/js/login-fx.js';

const form = document.getElementById('login-form');
const usernameInput = document.getElementById('username');
const passwordInput = document.getElementById('password');
const button = document.getElementById('btn-login');
const alertBox = document.getElementById('form-alert');
const notice = document.getElementById('notice');
const params = new URLSearchParams(location.search);

// Nút đổi giao diện dùng được khi chưa đăng nhập (AC-204); hiệu ứng panel thương hiệu chỉ để trang trí (QD-52).
document.getElementById('theme-slot').append(createThemeButton());
startLoginFx();

function show(box, kind, text) {
  while (box.firstChild) box.removeChild(box.firstChild);
  if (!text) return;
  const div = document.createElement('div');
  div.className = 'banner banner--' + kind;
  div.textContent = text;
  box.append(div);
}

function fieldError(input, message) {
  const err = document.getElementById(input.id + '-error');
  err.hidden = !message;
  err.textContent = message || '';
  if (message) input.setAttribute('aria-invalid', 'true');
  else input.removeAttribute('aria-invalid');
}

// Rung nhẹ form đúng một lần khi đăng nhập lỗi (AC-203); CSS tự tắt khi giảm chuyển động.
function shake() {
  form.classList.remove('is-shake');
  requestAnimationFrame(() => {
    form.classList.add('is-shake');
    form.addEventListener('animationend', () => form.classList.remove('is-shake'), { once: true });
  });
}

function returnUrl() {
  const value = params.get('returnUrl');
  return value && value.length <= 300 ? value : null;
}

if (params.get('expired') === '1') show(notice, 'warn', 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.');

// G5 (AC-513): chỉ khi từng đăng nhập (có cờ) mới hỏi /api/auth/me; 200 thì chuyển theo vai trò/returnUrl hợp lệ, 401 thì hiện form bình thường.
// Form không bị ẩn trong lúc chờ. Nút Back khôi phục trang từ bộ nhớ đệm thì tải lại (G7).
onRestoreFromCache();
redirectIfSignedIn({ me: api.me, hasHint: hasSignedInHint, clearHint: clearSignedIn, returnUrl: returnUrl() });

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  if (button.disabled) return;
  show(alertBox, 'error', '');
  const username = usernameInput.value;
  const password = passwordInput.value;

  fieldError(usernameInput, username.trim() ? '' : 'Vui lòng nhập tên đăng nhập.');
  fieldError(passwordInput, password.trim() ? '' : 'Vui lòng nhập mật khẩu.');
  if (!username.trim()) return usernameInput.focus();
  if (!password.trim()) return passwordInput.focus();

  button.disabled = true;
  button.setAttribute('aria-busy', 'true');
  button.textContent = 'Đang đăng nhập…';
  let navigating = false;
  try {
    const result = await api.login({ username: username.trim(), password, returnUrl: returnUrl() });
    navigating = true;
    // Chỉ đi theo đường dẫn tương đối an toàn trên origin của giao diện, không bao giờ ghép với API_BASE (AC-515).
    location.href = isSafeRedirect(result.redirectUrl) ? result.redirectUrl : homeFor(result.user);
    return;
  } catch (err) {
    passwordInput.value = '';
    let message = 'Không đăng nhập được, vui lòng thử lại.';
    if (err instanceof ApiError) {
      if (err.status === 401) message = err.message || 'Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.';
      else if (err.status === 429) message = 'Bạn thử quá nhiều lần, vui lòng đợi ít phút.';
      else if (err.status === 422) message = 'Vui lòng nhập đúng tên đăng nhập và mật khẩu.';
      else if (err.kind === 'network') message = 'Không kết nối được máy chủ, hãy kiểm tra kết nối rồi thử lại.';
    }
    show(alertBox, 'error', message);
    passwordInput.focus();
    shake();
  } finally {
    if (!navigating) {
      button.disabled = false;
      button.removeAttribute('aria-busy');
      button.textContent = 'Đăng nhập';
    }
  }
});

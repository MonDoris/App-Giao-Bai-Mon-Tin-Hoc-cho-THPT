import { api, ApiError, homeFor } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { banner, debounce } from '/js/core/ui.js';
import { clear } from '/js/core/dom.js';

const user = await initPage({ active: 'password' });

const form = document.getElementById('password-form');
const alertBox = document.getElementById('form-alert');
const inputs = {
  currentPassword: document.getElementById('current'),
  newPassword: document.getElementById('new'),
  again: document.getElementById('again'),
};
const button = document.getElementById('btn-save');
const strength = document.getElementById('strength');
const strengthLabel = document.getElementById('strength-label');
const strengthSegments = Array.from(strength.querySelectorAll('.strength__seg'));

if (user.mustChangePassword) {
  document.getElementById('must-change').append(banner('warn', 'Đây là lần đăng nhập đầu tiên (hoặc mật khẩu vừa được đặt lại). Bạn cần đổi mật khẩu để tiếp tục.'));
}

/**
 * Độ mạnh mật khẩu CHỈ là gợi ý (QD-53, AC-206): tính hoàn toàn trên trình duyệt, không chặn nút gửi,
 * không gọi API, không ghi log hay lưu mật khẩu. Trả về { level 0..3, label } hoặc null khi ô rỗng.
 */
export function rateStrength(password) {
  if (!password) return null;
  const hasLetter = /\p{L}/u.test(password);
  const hasDigit = /\p{Nd}/u.test(password);
  if (password.length < 8 || !hasLetter || !hasDigit) return { level: 0, label: 'Chưa đạt yêu cầu' };
  const score =
    (password.length >= 12 ? 1 : 0) +
    (/\p{Lu}/u.test(password) && /\p{Ll}/u.test(password) ? 1 : 0) +
    (/[^\p{L}\p{N}]/u.test(password) ? 1 : 0);
  if (score === 0) return { level: 1, label: 'Tạm được' };
  if (score === 1) return { level: 2, label: 'Khá' };
  return { level: 3, label: 'Mạnh' };
}

function showStrength() {
  const rating = rateStrength(inputs.newPassword.value);
  strength.setAttribute('data-level', rating ? String(rating.level) : '');
  strengthLabel.textContent = rating ? rating.label : '';
  strengthSegments.forEach((segment, index) => segment.classList.toggle('is-on', Boolean(rating) && index <= rating.level));
}

inputs.newPassword.addEventListener('input', debounce(showStrength, 150));

function setError(input, message) {
  const err = document.getElementById(input.id + '-error');
  err.hidden = !message;
  err.textContent = message || '';
  if (message) input.setAttribute('aria-invalid', 'true');
  else input.removeAttribute('aria-invalid');
}

function show(kind, text) {
  clear(alertBox);
  if (text) alertBox.append(banner(kind, text));
}

function release() {
  button.disabled = false;
  button.removeAttribute('aria-busy');
  button.textContent = 'Đổi mật khẩu';
}

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  if (button.disabled) return;
  show('error', '');
  Object.values(inputs).forEach((i) => setError(i, ''));

  const current = inputs.currentPassword.value;
  const next = inputs.newPassword.value;
  const again = inputs.again.value;
  let bad = null;
  if (!current) { setError(inputs.currentPassword, 'Vui lòng nhập mật khẩu hiện tại.'); bad = bad || inputs.currentPassword; }
  if (!next) { setError(inputs.newPassword, 'Vui lòng nhập mật khẩu mới.'); bad = bad || inputs.newPassword; }
  if (next && again !== next) { setError(inputs.again, 'Mật khẩu nhập lại không khớp.'); bad = bad || inputs.again; }
  if (bad) return bad.focus();

  button.disabled = true;
  button.setAttribute('aria-busy', 'true');
  button.textContent = 'Đang lưu…';
  try {
    await api.changePassword({ currentPassword: current, newPassword: next });
    form.reset();
    showStrength();
    show('ok', 'Đã đổi mật khẩu thành công. Đang chuyển về trang chủ…');
    setTimeout(() => { location.href = homeFor(user); }, 1500);
    button.removeAttribute('aria-busy');
  } catch (err) {
    inputs.currentPassword.value = '';
    inputs.newPassword.value = '';
    inputs.again.value = '';
    showStrength();
    if (err instanceof ApiError && err.status === 422) {
      const target = err.field === 'currentPassword' ? inputs.currentPassword : inputs.newPassword;
      setError(target, err.message || 'Dữ liệu không hợp lệ.');
      target.focus();
    } else if (err instanceof ApiError && err.status === 429) {
      show('error', 'Bạn thao tác quá nhanh, hãy thử lại sau ít phút.');
    } else if (!(err instanceof ApiError) || err.status !== 401) {
      show('error', 'Không đổi được mật khẩu, vui lòng thử lại.');
    }
    release();
  }
});

// Tiện ích chung của khu giáo viên.
import { ApiError, describeError } from '/js/core/api.js';
import { el, clear } from '/js/core/dom.js';
import { banner } from '/js/core/ui.js';
import { toast } from '/js/core/toast.js';

export const DIFFICULTIES = [
  { value: 'Easy', label: 'Dễ' },
  { value: 'Medium', label: 'Trung bình' },
  { value: 'Hard', label: 'Khó' },
];

export function difficultyLabel(value) {
  const found = DIFFICULTIES.find((d) => d.value === value);
  return found ? found.label : String(value ?? '');
}

/**
 * Phản hồi cho người dùng (QD-57): ok/info hiện bằng toast; warn/error giữ tại chỗ trong #status (aria-live)
 * cho tới khi bị thay thế.
 */
export function say(kind, text) {
  const host = document.getElementById('status');
  if (kind === 'ok' || kind === 'info') {
    if (host) clear(host);
    if (text) toast(kind, text);
    return;
  }
  if (!host) return;
  clear(host);
  if (text) host.append(banner(kind, text));
}

/** Câu thông báo lỗi tiếng Việt cho một lỗi API chưa được xử lý riêng. */
export function errorText(err) {
  if (err instanceof ApiError && err.message && err.status >= 400 && err.status < 500) return err.message;
  return describeError(err).title;
}

export function pageContent() {
  return document.getElementById('content');
}

export function finishLoading() {
  pageContent().setAttribute('aria-busy', 'false');
}

/** Hộp liên kết "← Quay lại ..." đầu trang. */
export function backLink(href, text) {
  return el('p', { class: 'breadcrumb' }, el('a', { class: 'back-link', href }, text));
}

export function plural(n, word) {
  return `${n} ${word}`;
}

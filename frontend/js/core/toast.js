// Toast (AC-273): phản hồi tạm thời trong vùng aria-live. Nội dung luôn chèn bằng textContent.
import { el } from '/js/core/dom.js';
import { icon } from '/js/core/icons.js';

const MAX_TOASTS = 3;
const ICONS = { ok: 'check', info: 'info', warn: 'alert', error: 'alert-circle' };
const live = [];
let region = null;

function ensureRegion() {
  if (region && region.isConnected) return region;
  region = document.getElementById('toast-region');
  if (!region) {
    region = el('div', { id: 'toast-region', role: 'status', 'aria-live': 'polite' });
    document.body.append(region);
  }
  return region;
}

function reduced() {
  return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function dismiss(entry) {
  const index = live.indexOf(entry);
  if (index < 0) return;
  live.splice(index, 1);
  clearTimeout(entry.timer);
  entry.node.classList.add('is-leaving');
  setTimeout(() => entry.node.remove(), reduced() ? 0 : 220);
}

/**
 * Hiện một toast. kind: ok | info | warn | error. timeout (ms): mặc định 5000, lỗi 8000.
 * action: { label, onClick } thêm nút hành động (ví dụ Hoàn tác), tồn tại 12 giây.
 * Tạm dừng đếm giờ khi rê chuột hoặc có tiêu điểm trong toast.
 */
export function toast(kind, text, { timeout, action } = {}) {
  const host = ensureRegion();
  const total = timeout ?? (action ? 12000 : kind === 'error' ? 8000 : 5000);
  const close = el('button', { type: 'button', class: 'btn btn--ghost toast__close', 'aria-label': 'Đóng thông báo' }, icon('x'));
  const node = el(
    'div',
    { class: 'toast toast--' + kind, role: kind === 'error' ? 'alert' : null },
    el('span', { class: 'toast__icon', 'aria-hidden': 'true' }, icon(ICONS[kind] || 'info')),
    el('p', { class: 'toast__text' }, text),
    action ? el('button', { type: 'button', class: 'btn btn--small toast__action', onclick: () => { dismiss(entry); action.onClick(); } }, action.label) : null,
    close,
  );
  const entry = { node, timer: 0, remaining: total, startedAt: 0 };

  const start = () => {
    entry.startedAt = Date.now();
    entry.timer = setTimeout(() => dismiss(entry), entry.remaining);
  };
  const pause = () => {
    if (!entry.timer) return;
    clearTimeout(entry.timer);
    entry.timer = 0;
    entry.remaining = Math.max(1000, entry.remaining - (Date.now() - entry.startedAt));
  };
  const resume = () => {
    if (!live.includes(entry) || entry.timer) return;
    start();
  };
  node.addEventListener('pointerenter', pause);
  node.addEventListener('pointerleave', resume);
  node.addEventListener('focusin', pause);
  node.addEventListener('focusout', resume);
  close.addEventListener('click', () => dismiss(entry));

  live.push(entry);
  while (live.length > MAX_TOASTS) dismiss(live[0]);
  host.append(node);
  start();
  return () => dismiss(entry);
}

if (document.body) ensureRegion();

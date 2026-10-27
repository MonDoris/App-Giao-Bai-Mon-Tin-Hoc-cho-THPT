// Nút đổi giao diện Tự động / Sáng / Tối (AC-167..172). Dùng ở header và trang đăng nhập.
import { el } from '/js/core/dom.js';
import { icon } from '/js/core/icons.js';
import { uiPrefs } from '/js/core/storage.js';
import { toast } from '/js/core/toast.js';

const ORDER = ['auto', 'light', 'dark'];
const LABEL = { auto: 'Tự động', light: 'Sáng', dark: 'Tối' };
const ANNOUNCE = {
  auto: 'Đã chuyển sang giao diện tự động',
  light: 'Đã chuyển sang giao diện sáng',
  dark: 'Đã chuyển sang giao diện tối',
};
const ICON = { auto: 'auto', light: 'sun', dark: 'moon' };
const META_COLOR = { light: '#f1f4f9', dark: '#0b1424' };

const root = document.documentElement;
const systemDark = window.matchMedia('(prefers-color-scheme: dark)');
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
const buttons = new Set();
let watching = false;

function currentPref() {
  const fromAttr = root.getAttribute('data-theme-pref');
  if (ORDER.includes(fromAttr)) return fromAttr;
  return uiPrefs.getTheme() || 'auto';
}

function resolve(pref) {
  if (pref === 'auto') return systemDark.matches ? 'dark' : 'light';
  return pref;
}

function updateMeta(pref, resolved) {
  document.querySelectorAll('meta[name="theme-color"]').forEach((meta) => {
    const media = meta.getAttribute('media') || '';
    const own = media.includes('dark') ? 'dark' : 'light';
    meta.setAttribute('content', META_COLOR[pref === 'auto' ? own : resolved]);
  });
}

function nextOf(pref) {
  return ORDER[(ORDER.indexOf(pref) + 1) % ORDER.length];
}

function refreshButtons() {
  const pref = currentPref();
  const next = nextOf(pref);
  buttons.forEach((button) => {
    button.setAttribute('aria-label', `Giao diện: ${LABEL[pref]}. Bấm để chuyển sang ${LABEL[next]}`);
    const holder = button.querySelector('.theme-btn__icon');
    if (holder) holder.replaceChildren(icon(ICON[pref]));
    const label = button.querySelector('.theme-btn__label');
    if (label) label.textContent = `Giao diện: ${LABEL[pref]}`;
  });
}

function apply(pref) {
  const resolved = resolve(pref);
  root.setAttribute('data-theme', resolved);
  root.setAttribute('data-theme-pref', pref);
  updateMeta(pref, resolved);
  refreshButtons();
}

function onSystemChange() {
  if (currentPref() === 'auto') apply('auto');
}

function watchSystem() {
  if (watching) return;
  watching = true;
  systemDark.addEventListener('change', onSystemChange);
  window.addEventListener('pagehide', destroyTheme, { once: true });
}

/** Gỡ listener và quên các nút (gọi ở pagehide). */
export function destroyTheme() {
  systemDark.removeEventListener('change', onSystemChange);
  buttons.clear();
  watching = false;
}

function cycle() {
  const next = nextOf(currentPref());
  const run = () => {
    uiPrefs.setTheme(next === 'auto' ? null : next);
    apply(next);
  };
  if (typeof document.startViewTransition === 'function' && !reducedMotion.matches) {
    try {
      const transition = document.startViewTransition(run);
      transition.ready.catch(() => {});
      transition.finished.catch(() => {});
    } catch (e) {
      run();
    }
  } else {
    run();
  }
  toast('info', ANNOUNCE[next], { timeout: 2500 });
}

/** Tạo nút đổi giao diện (id="btn-theme"). withLabel: hiện thêm chữ "Giao diện: …" (dùng trong menu). */
export function createThemeButton() {
  watchSystem();
  const button = el(
    'button',
    { type: 'button', id: 'btn-theme', class: 'btn btn--ghost theme-btn', title: 'Đổi giao diện' },
    el('span', { class: 'theme-btn__icon', 'aria-hidden': 'true' }),
    el('span', { class: 'theme-btn__label' }),
  );
  button.addEventListener('click', cycle);
  buttons.add(button);
  refreshButtons();
  return button;
}

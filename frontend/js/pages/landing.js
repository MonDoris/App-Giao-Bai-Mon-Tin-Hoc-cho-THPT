// Trang giới thiệu công khai (index.html): khách chưa từng đăng nhập không phát sinh yêu cầu API nào (AC-342, AC-514). Gồm menu thu gọn, tab vai trò, hiệu ứng gõ mã một lần,
// đường nối timeline, hiện dần khi cuộn và đánh dấu mục đang xem. Mọi nội dung đã có sẵn trong HTML; mã này chỉ bổ sung.
import { api } from '/js/core/api.js';
import { onRestoreFromCache, redirectIfSignedIn } from '/js/core/guard.js';
import { clearSignedIn, hasSignedInHint } from '/js/core/storage.js';
import { icon } from '/js/core/icons.js';
import { createThemeButton } from '/js/core/theme.js';
import { revealOnView, enableCardEffects, onceInView, prefersReducedMotion, watchReducedMotion } from '/js/core/fx.js';

const root = document.documentElement;
root.classList.add('js-reveal');

const narrowQuery = window.matchMedia('(max-width: 899.98px)');

// ---------- Biểu tượng trang trí ----------
document.querySelectorAll('[data-icon]').forEach((holder) => {
  holder.append(icon(holder.getAttribute('data-icon')));
});

// ---------- Nút giao diện, năm bản quyền ----------
document.getElementById('theme-slot').append(createThemeButton());
document.getElementById('lp-year').textContent = String(new Date().getFullYear());

// ---------- Menu thu gọn dưới 900 px ----------
function initMenu() {
  const nav = document.getElementById('lp-nav');
  const button = document.getElementById('lp-menu-btn');
  const links = document.getElementById('lp-links');
  button.hidden = false;
  nav.classList.add('is-ready');

  const setOpen = (open, restoreFocus = false) => {
    nav.setAttribute('data-open', open ? 'true' : 'false');
    button.setAttribute('aria-expanded', open ? 'true' : 'false');
    if (!open && restoreFocus) button.focus();
  };
  setOpen(false);

  button.addEventListener('click', () => setOpen(button.getAttribute('aria-expanded') !== 'true'));
  links.addEventListener('click', (event) => {
    if (event.target instanceof Element && event.target.closest('a')) setOpen(false);
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && button.getAttribute('aria-expanded') === 'true') {
      event.preventDefault();
      setOpen(false, true);
    }
  });
  document.addEventListener('click', (event) => {
    if (button.getAttribute('aria-expanded') === 'true' && event.target instanceof Node && !nav.contains(event.target)) setOpen(false);
  });
  narrowQuery.addEventListener('change', () => {
    if (!narrowQuery.matches) setOpen(false);
  });
}

// ---------- Mục đang xem trên thanh điều hướng (một IntersectionObserver, không nghe sự kiện cuộn) ----------
function initCurrentSection() {
  if (!('IntersectionObserver' in window)) return;
  const anchors = Array.from(document.querySelectorAll('.lp-nav__links a[href^="#"]'));
  const byId = new Map(anchors.map((a) => [a.getAttribute('href').slice(1), a]));
  const observer = new IntersectionObserver((records) => {
    records.forEach((record) => {
      if (!record.isIntersecting) return;
      anchors.forEach((a) => a.removeAttribute('aria-current'));
      const link = byId.get(record.target.id);
      if (link) link.setAttribute('aria-current', 'location');
    });
  }, { rootMargin: '-40% 0px -55% 0px' });
  document.querySelectorAll('main > section[id]').forEach((section) => observer.observe(section));
  window.addEventListener('pagehide', () => observer.disconnect(), { once: true });
}

// ---------- Tab vai trò (cải tiến dần: không có JS thì cả hai bảng đều hiện) ----------
function initTabs() {
  const list = document.getElementById('lp-tablist');
  const tabs = Array.from(list.querySelectorAll('[role="tab"]'));
  const panels = tabs.map((tab) => document.getElementById(tab.getAttribute('aria-controls')));
  list.hidden = false;
  document.getElementById('vai-tro').classList.add('has-tabs');

  const select = (index, focus = false) => {
    tabs.forEach((tab, i) => {
      const on = i === index;
      tab.setAttribute('aria-selected', on ? 'true' : 'false');
      tab.tabIndex = on ? 0 : -1;
      panels[i].hidden = !on;
    });
    if (focus) tabs[index].focus();
  };
  tabs.forEach((tab, index) => tab.addEventListener('click', () => select(index)));
  list.addEventListener('keydown', (event) => {
    const current = tabs.indexOf(document.activeElement);
    if (current < 0) return;
    let next = -1;
    if (event.key === 'ArrowRight') next = (current + 1) % tabs.length;
    else if (event.key === 'ArrowLeft') next = (current - 1 + tabs.length) % tabs.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = tabs.length - 1;
    if (next < 0) return;
    event.preventDefault();
    select(next, true);
  });
  select(0);
}

// ---------- Gõ mã trong khung trình soạn: một lần, tối đa 4 giây, không setInterval (QD-81) ----------
const TYPE_MS = 3200;

function initTyping() {
  const editor = document.getElementById('lp-editor');
  const lines = Array.from(document.querySelectorAll('#lp-code .lp-code__src'));
  if (!editor || !lines.length || prefersReducedMotion()) return;

  const parts = [];
  lines.forEach((line, lineIndex) => {
    Array.from(line.children).forEach((span) => {
      parts.push({ node: span, text: span.textContent, line: lineIndex });
    });
  });
  const total = parts.reduce((sum, p) => sum + p.text.length, 0);
  const caret = document.createElement('span');
  caret.className = 'lp-caret';

  let frame = 0;
  let startAt = 0;
  let finished = false;
  let stopWatch = () => {};

  const render = (count) => {
    let left = count;
    let lastLine = 0;
    parts.forEach((p) => {
      const take = Math.max(0, Math.min(p.text.length, left));
      left -= take;
      p.node.textContent = p.text.slice(0, take);
      if (take > 0) lastLine = p.line;
    });
    lines[lastLine].append(caret);
  };

  const finish = () => {
    if (finished) return;
    finished = true;
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
    render(total);
    caret.remove();
    editor.setAttribute('data-state', 'done');
    stopWatch();
    document.removeEventListener('visibilitychange', onVisibility);
    window.removeEventListener('pagehide', finish);
  };

  const onVisibility = () => {
    if (document.hidden) finish();
  };

  const tick = (now) => {
    if (finished) return;
    if (!startAt) startAt = now;
    const ratio = Math.min(1, (now - startAt) / TYPE_MS);
    if (ratio >= 1) {
      finish();
      return;
    }
    render(Math.floor(ratio * total));
    frame = requestAnimationFrame(tick);
  };

  editor.setAttribute('data-state', 'typing');
  render(0);
  stopWatch = watchReducedMotion((event) => {
    if (event.matches) finish();
  });
  document.addEventListener('visibilitychange', onVisibility);
  window.addEventListener('pagehide', finish, { once: true });
  onceInView(editor, () => {
    if (finished) return;
    if (document.hidden) {
      finish();
      return;
    }
    frame = requestAnimationFrame(tick);
  }, 0.35);
}

// ---------- Đường nối timeline: ưu tiên CSS scroll-driven, nếu không thì thêm lớp một lần ----------
function initLine() {
  const line = document.getElementById('lp-line');
  if (!line) return;
  const supported = window.CSS && CSS.supports && CSS.supports('animation-timeline: view()');
  if (supported && !prefersReducedMotion()) return;
  onceInView(line, () => line.classList.add('is-drawn'), 0.3);
}

// G6 (AC-514): chỉ khi từng đăng nhập (có cờ) mới hỏi /api/auth/me một lần; 200 thì chuyển tới trang chủ theo vai trò, 401 thì giữ nguyên.
// Nội dung landing hiển thị ngay, không bị ẩn trong lúc chờ. Nút Back khôi phục trang từ bộ nhớ đệm thì tải lại (G7).
onRestoreFromCache();
redirectIfSignedIn({ me: api.me, hasHint: hasSignedInHint, clearHint: clearSignedIn });

initMenu();
initCurrentSection();
initTabs();
initTyping();
initLine();

revealOnView(document.querySelectorAll('.lp-reveal'));
const bento = document.getElementById('lp-bento');
revealOnView(bento.querySelectorAll('.lp-tile'));
enableCardEffects(bento, '.lp-tile');

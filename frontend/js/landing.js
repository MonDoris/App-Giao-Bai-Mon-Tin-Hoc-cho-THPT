// Trang giới thiệu công khai (index.html): khách chưa từng đăng nhập không phát sinh yêu cầu API nào (AC-342, AC-514). Gồm menu thu gọn, tab vai trò, hiệu ứng gõ mã một lần,
// đường nối timeline, hiệu ứng cuộn 2 chiều (lên/xuống) và đánh dấu mục đang xem. Mọi nội dung đã có sẵn trong HTML; mã này chỉ bổ sung.
import { api } from '/js/api.js';
import { onRestoreFromCache, redirectIfSignedIn } from '/js/guard.js';
import { clearSignedIn, hasSignedInHint } from '/js/storage.js';
import { icon } from '/js/icons.js';
import { createThemeButton } from '/js/theme.js';
import { revealOnView, enableCardEffects, onceInView, prefersReducedMotion, watchReducedMotion } from '/js/fx.js';

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

// ---------- Đường nối timeline: ưu tiên CSS scroll-driven, nếu không thì thêm lớp ----------
function initLine() {
  const line = document.getElementById('lp-line');
  if (!line) return;
  const supported = window.CSS && CSS.supports && CSS.supports('animation-timeline: view()');
  if (supported && !prefersReducedMotion()) return;
  onceInView(line, () => line.classList.add('is-drawn'), 0.3);
}

// Hiệu ứng cuộn 2 chiều (lên/xuống) bằng IntersectionObserver đa ngưỡng
function initBidirectionalScroll() {
  if (!('IntersectionObserver' in window) || prefersReducedMotion()) return;
  const targets = Array.from(document.querySelectorAll('.lp-reveal, .lp-tile, .lp-stage, #lp-line'));
  if (!targets.length) return;
  const observer = new IntersectionObserver((records) => {
    if (prefersReducedMotion()) return;
    records.forEach((r) => {
      const el = r.target;
      el.classList.toggle('is-in', r.isIntersecting);
      el.classList.toggle('is-active', r.isIntersecting);
      if (!r.isIntersecting) el.classList.remove('is-settled');
      if (el.id === 'lp-line') el.classList.toggle('is-drawn', r.isIntersecting);
    });
  }, { threshold: [0, 0.12, 0.35, 0.7] });
  targets.forEach((el) => observer.observe(el));
  const stopMotion = watchReducedMotion((e) => {
    if (e.matches) targets.forEach((el) => el.classList.add('is-in', 'is-active', 'is-settled'));
  });
  window.addEventListener('pagehide', () => { observer.disconnect(); stopMotion(); }, { once: true });
}

// ---------- Hiệu ứng 3D Long Vân Linh Khí, Đom Đóm Sen Vàng & Parallax đa tầng ----------
function initDragon3D() {
  const canvas = document.getElementById('lp-dragon-canvas');
  const hero = document.getElementById('hero');
  if (!canvas || prefersReducedMotion()) return;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  let w = 0, h = 0, dpr = 1, raf = 0, mx = 0, my = 0, tx = 0, ty = 0, sy = 0;
  const fov = 600;
  const sparks = Array.from({ length: 44 }, (_, i) => ({
    x: ((i * 97) % 100 - 50) * 15,
    y: ((i * 53) % 100 - 50) * 10,
    z: ((i * 37) % 100) * 3.2 - 90,
    s: (i % 3) * 0.9 + 1.3,
    c: i % 3 === 0 ? '#00e5ff' : (i % 4 === 0 ? '#fffbeb' : '#fcd34d'),
  }));

  const resize = () => {
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    w = window.innerWidth;
    h = window.innerHeight;
    canvas.width = w * dpr;
    canvas.height = h * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  };
  resize();

  const onMove = (e) => {
    const nx = e.clientX / Math.max(1, w) - 0.5;
    const ny = e.clientY / Math.max(1, h) - 0.5;
    tx = nx * 160;
    ty = ny * 110;
    if (hero) {
      hero.style.setProperty('--cam-x', (nx * 14).toFixed(2) + 'px');
      hero.style.setProperty('--cam-y', (ny * 10).toFixed(2) + 'px');
    }
  };

  const render = (now) => {
    if (document.hidden || prefersReducedMotion()) { raf = 0; return; }
    const t = now * 0.0012;
    const targetSy = window.scrollY || 0;
    sy += (targetSy - sy) * 0.08;
    mx += (tx - mx) * 0.06;
    my += (ty - my) * 0.06;

    ctx.clearRect(0, 0, w, h);
    const cx = w * 0.5, cy = h * 0.5;

    for (let i = 0; i < sparks.length; i++) {
      const p = sparks[i];
      const py = ((p.y - sy * 0.22 - t * 24 + 6000) % 920) - 460;
      const pz = p.z + Math.sin(t * 1.1 + i) * 38;
      const sc = fov / Math.max(140, fov + pz);
      const sx = cx + (p.x + Math.sin(t * 0.8 + i) * 26 + mx * 0.28) * sc;
      const sy2 = cy + (py + my * 0.22) * sc;
      const r = p.s * sc;

      ctx.globalAlpha = Math.max(0.08, Math.min(0.32, sc * 0.22));
      ctx.fillStyle = p.c;
      ctx.beginPath();
      ctx.arc(sx, sy2, r * 2.8, 0, Math.PI * 2);
      ctx.fill();

      ctx.globalAlpha = Math.max(0.25, Math.min(0.88, sc * 0.65));
      ctx.beginPath();
      ctx.arc(sx, sy2, r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
    raf = requestAnimationFrame(render);
  };

  const start = () => { if (!raf && !document.hidden && !prefersReducedMotion()) raf = requestAnimationFrame(render); };
  const stop = () => { if (raf) { cancelAnimationFrame(raf); raf = 0; } ctx.clearRect(0, 0, w, h); };
  const onVis = () => (document.hidden ? stop() : start());
  const stopWatch = watchReducedMotion((e) => (e.matches ? stop() : start()));

  window.addEventListener('resize', resize);
  window.addEventListener('pointermove', onMove, { passive: true });
  document.addEventListener('visibilitychange', onVis);
  window.addEventListener('pagehide', () => {
    stop();
    stopWatch();
    window.removeEventListener('resize', resize);
    window.removeEventListener('pointermove', onMove);
    document.removeEventListener('visibilitychange', onVis);
  }, { once: true });
  start();
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
initDragon3D();

revealOnView(document.querySelectorAll('.lp-reveal'));
const bento = document.getElementById('lp-bento');
revealOnView(bento.querySelectorAll('.lp-tile'));
enableCardEffects(bento, '.lp-tile');
const heroGrid = document.querySelector('.lp-hero__grid');
if (heroGrid) enableCardEffects(heroGrid, '.lp-3d-card, .lp-3d-stage');
const stripList = document.querySelector('.lp-strip__list');
if (stripList) enableCardEffects(stripList, '.lp-scrub-card');
const stepsList = document.querySelector('.lp-steps');
if (stepsList) enableCardEffects(stepsList, '.lp-scrub-card');
initBidirectionalScroll();

// Hiệu ứng dùng chung: giảm chuyển động, IntersectionObserver dùng chung, hiện dần theo stagger, viền sáng + nghiêng nhẹ cho thẻ.
// Mọi hiệu ứng chỉ đổi biến CSS / class (không tạo hay xóa DOM khi di chuyển chuột); không dùng listener cuộn của cửa sổ.
// destroyFx() gỡ observer, listener và rAF; được gọi ở pagehide (AC-286).

const reducedQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
const fineQuery = window.matchMedia('(hover: hover) and (pointer: fine)');
const wideQuery = window.matchMedia('(min-width: 700px)');

export function prefersReducedMotion() {
  return reducedQuery.matches;
}

/** Gọi cb(reduced) khi người dùng đổi thiết lập giảm chuyển động; trả về hàm hủy đăng ký. */
export function watchReducedMotion(cb) {
  reducedQuery.addEventListener('change', cb);
  return () => reducedQuery.removeEventListener('change', cb);
}

const observers = new Map();
const pending = new Set();
const cleanups = [];
let destroyed = false;

function getObserver(threshold) {
  let entry = observers.get(threshold);
  if (!entry) {
    const callbacks = new Map();
    const observer = new IntersectionObserver((records) => {
      const entering = records.filter((r) => r.isIntersecting);
      entering.forEach((record, index) => {
        const cb = callbacks.get(record.target);
        callbacks.delete(record.target);
        observer.unobserve(record.target);
        if (cb) cb(record.target, index, entering.length);
      });
    }, { threshold });
    entry = { observer, callbacks };
    observers.set(threshold, entry);
  }
  return entry;
}

/** Gọi cb(node) đúng một lần khi node vào khung nhìn (không có IntersectionObserver thì gọi ngay). */
export function onceInView(node, cb, threshold = 0.15) {
  if (!('IntersectionObserver' in window) || reducedQuery.matches) {
    cb(node, 0, 1);
    return;
  }
  const { observer, callbacks } = getObserver(threshold);
  callbacks.set(node, cb);
  observer.observe(node);
}

function settle(node) {
  if (reducedQuery.matches) {
    node.classList.add('is-settled');
    return;
  }
  const onEnd = (event) => {
    if (event.target !== node || event.propertyName !== 'transform') return;
    node.removeEventListener('transitionend', onEnd);
    node.classList.add('is-settled');
  };
  node.addEventListener('transitionend', onEnd);
}

function show(node) {
  pending.delete(node);
  node.classList.add('is-in');
  settle(node);
}

/**
 * Hiện dần các phần tử khi vào khung nhìn, lần lượt cách nhau --stagger (QD: 50 ms, tối đa 10 bước).
 * Giảm chuyển động hoặc không có IntersectionObserver: hiện ngay.
 */
export function revealOnView(nodes) {
  const list = Array.from(nodes).filter(Boolean);
  list.forEach((node, index) => {
    node.classList.add('reveal');
    node.style.setProperty('--i', String(index));
  });
  if (!('IntersectionObserver' in window) || reducedQuery.matches) {
    list.forEach((node) => {
      node.classList.add('is-in', 'is-settled');
    });
    return;
  }
  list.forEach((node) => {
    pending.add(node);
    onceInView(node, (target, batchIndex, batchSize) => {
      if (batchSize > 1) target.style.setProperty('--i', String(batchIndex));
      show(target);
    });
  });
}

function flushPending() {
  pending.forEach((node) => {
    node.classList.add('is-in', 'is-settled');
  });
  pending.clear();
}

/**
 * Viền sáng theo con trỏ + nghiêng 3D nhẹ cho .lesson-card trong grid (AC-218, AC-219).
 * Một listener ủy quyền cho cả lưới; vị trí thẻ đọc một lần khi con trỏ vào thẻ; ghi biến CSS gom bằng rAF.
 * Chỉ bật với chuột thật (hover: hover, pointer: fine) và khi không giảm chuyển động (AC-287).
 */
export function enableCardEffects(grid, selector = '.lesson-card') {
  let attached = false;
  let current = null;
  let rect = null;
  let scrollX0 = 0;
  let scrollY0 = 0;
  let lastX = 0;
  let lastY = 0;
  let frame = 0;

  const reset = (card) => {
    if (!card) return;
    card.classList.remove('is-hot');
    card.style.removeProperty('--rx');
    card.style.removeProperty('--ry');
  };

  const apply = () => {
    frame = 0;
    if (!current || !rect) return;
    const left = rect.left + (scrollX0 - window.scrollX);
    const top = rect.top + (scrollY0 - window.scrollY);
    const x = lastX - left;
    const y = lastY - top;
    const px = Math.min(1, Math.max(0, x / rect.width));
    const py = Math.min(1, Math.max(0, y / rect.height));
    current.style.setProperty('--mx', x.toFixed(1) + 'px');
    current.style.setProperty('--my', y.toFixed(1) + 'px');
    if (wideQuery.matches) {
      current.style.setProperty('--ry', ((px - 0.5) * 8).toFixed(2) + 'deg');
      current.style.setProperty('--rx', (-(py - 0.5) * 8).toFixed(2) + 'deg');
    }
  };

  const onOver = (event) => {
    if (event.pointerType && event.pointerType !== 'mouse') return;
    const card = event.target instanceof Element ? event.target.closest(selector) : null;
    if (card === current) return;
    reset(current);
    current = card && grid.contains(card) ? card : null;
    if (current) {
      rect = current.getBoundingClientRect();
      scrollX0 = window.scrollX;
      scrollY0 = window.scrollY;
      current.classList.add('is-hot');
    }
  };

  const onMove = (event) => {
    if (!current || (event.pointerType && event.pointerType !== 'mouse')) return;
    lastX = event.clientX;
    lastY = event.clientY;
    if (!frame) frame = requestAnimationFrame(apply);
  };

  const onLeave = () => {
    reset(current);
    current = null;
    rect = null;
  };

  const attach = () => {
    if (attached) return;
    attached = true;
    grid.addEventListener('pointerover', onOver);
    grid.addEventListener('pointermove', onMove);
    grid.addEventListener('pointerleave', onLeave);
  };

  const detach = () => {
    if (!attached) return;
    attached = false;
    grid.removeEventListener('pointerover', onOver);
    grid.removeEventListener('pointermove', onMove);
    grid.removeEventListener('pointerleave', onLeave);
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
    onLeave();
  };

  const evaluate = () => {
    if (!destroyed && fineQuery.matches && !reducedQuery.matches) attach();
    else detach();
  };

  evaluate();
  fineQuery.addEventListener('change', evaluate);
  reducedQuery.addEventListener('change', evaluate);
  cleanups.push(() => {
    fineQuery.removeEventListener('change', evaluate);
    reducedQuery.removeEventListener('change', evaluate);
    detach();
  });
}

/** Gỡ toàn bộ observer, listener, rAF (gọi ở pagehide). */
export function destroyFx() {
  destroyed = true;
  observers.forEach(({ observer }) => observer.disconnect());
  observers.clear();
  pending.clear();
  cleanups.splice(0).forEach((fn) => fn());
  reducedQuery.removeEventListener('change', onReducedChange);
}

function onReducedChange() {
  if (reducedQuery.matches) flushPending();
}

reducedQuery.addEventListener('change', onReducedChange);
window.addEventListener('pagehide', destroyFx, { once: true });

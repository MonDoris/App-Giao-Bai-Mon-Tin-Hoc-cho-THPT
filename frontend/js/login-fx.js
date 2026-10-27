// Hiệu ứng trang trí của panel thương hiệu ở trang đăng nhập (AC-196..200, QD-52):
// ánh sáng theo con trỏ (spotlight) và các mảnh mã trôi nhẹ, bị con trỏ đẩy ra bằng vật lý lò xo viết tay.
// Không thư viện. Một vòng requestAnimationFrame duy nhất, tạm dừng khi tab ẩn hoặc panel ngoài khung nhìn.
// start() bắt đầu theo dõi; destroy() hủy toàn bộ (rAF, listener, observer) và được gọi ở pagehide.

const SPRING_K = 90; // s^-2
const DAMPING_C = 14; // s^-1
const PUSH_RADIUS = 140; // px
const PUSH_MAX = 64; // px
const MAX_DT = 1 / 30;
const DEFAULT_X = '30%';
const DEFAULT_Y = '20%';

// Vị trí neo (tỉ lệ trong panel) tránh khối chữ ở giữa; nội dung là mã trang trí.
const PIECES = [
  { text: '/', x: 0.1, y: 0.1, size: 3 },
  { text: '< >', x: 0.72, y: 0.08, size: 2 },
  { text: '{}', x: 0.86, y: 0.34, size: 2.4 },
  { text: 'print()', x: 0.62, y: 0.9, size: 1.5 },
  { text: 'for', x: 0.3, y: 0.86, size: 1.9 },
  { text: 'def', x: 0.8, y: 0.62, size: 1.7 },
  { text: ':', x: 0.46, y: 0.05, size: 2.2 },
  { text: '[]', x: 0.1, y: 0.9, size: 2 },
  { text: '#', x: 0.9, y: 0.88, size: 2 },
];

const wideQuery = window.matchMedia('(min-width: 900px)');
const reducedQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
const fineQuery = window.matchMedia('(hover: hover) and (pointer: fine)');
let pointerAttached = false;

let panel = null;
let layer = null;
let pieces = [];
let started = false;
let running = false;
let frame = 0;
let lastTime = 0;
let simTime = 0;
let pointer = null; // { x, y } theo tọa độ viewport
let inView = true;
let resizeObserver = null;
let intersectionObserver = null;

function ensurePieces() {
  if (pieces.length || !layer) return;
  pieces = PIECES.map((def, index) => {
    const node = document.createElement('span');
    node.className = 'drift';
    node.textContent = def.text;
    node.setAttribute('aria-hidden', 'true');
    node.style.left = (def.x * 100).toFixed(1) + '%';
    node.style.top = (def.y * 100).toFixed(1) + '%';
    node.style.fontSize = def.size + 'rem';
    layer.append(node);
    return {
      node,
      def,
      x: 0,
      y: 0,
      vx: 0,
      vy: 0,
      cx: 0,
      cy: 0,
      ampX: 6 + ((index * 7) % 5),
      ampY: 6 + ((index * 3) % 5),
      periodX: 6 + ((index * 5) % 5),
      periodY: 6 + ((index * 3 + 2) % 5),
      phaseX: Math.random() * Math.PI * 2,
      phaseY: Math.random() * Math.PI * 2,
      rotate: ((index % 3) - 1) * 4,
    };
  });
}

function measure() {
  if (!panel) return;
  const box = panel.getBoundingClientRect();
  for (const piece of pieces) {
    piece.cx = piece.def.x * box.width + piece.node.offsetWidth / 2;
    piece.cy = piece.def.y * box.height + piece.node.offsetHeight / 2;
  }
}

function paintStatic() {
  for (const piece of pieces) piece.node.style.transform = 'translate3d(0px, 0px, 0)';
  if (panel) {
    panel.style.setProperty('--mouse-x', DEFAULT_X);
    panel.style.setProperty('--mouse-y', DEFAULT_Y);
  }
}

function tick(now) {
  frame = 0;
  if (!running) return;
  const dt = Math.min(MAX_DT, Math.max(0, (now - lastTime) / 1000));
  lastTime = now;
  simTime += dt;

  const box = panel.getBoundingClientRect();
  let local = null;
  if (pointer) local = { x: pointer.x - box.left, y: pointer.y - box.top };

  for (const piece of pieces) {
    // Mục tiêu = trôi nhàn rỗi (sin) + đẩy khỏi con trỏ
    let tx = piece.ampX * Math.sin((2 * Math.PI * simTime) / piece.periodX + piece.phaseX);
    let ty = piece.ampY * Math.sin((2 * Math.PI * simTime) / piece.periodY + piece.phaseY);
    if (local) {
      const dx = piece.cx - local.x;
      const dy = piece.cy - local.y;
      const d = Math.hypot(dx, dy);
      if (d < PUSH_RADIUS) {
        const magnitude = PUSH_MAX * (1 - d / PUSH_RADIUS) ** 2;
        const ux = d > 0.001 ? dx / d : 1;
        const uy = d > 0.001 ? dy / d : 0;
        tx += ux * magnitude;
        ty += uy * magnitude;
      }
    }
    // Lò xo: a = -k (x - target) - c v (tích phân Euler nửa ẩn)
    piece.vx += (-SPRING_K * (piece.x - tx) - DAMPING_C * piece.vx) * dt;
    piece.vy += (-SPRING_K * (piece.y - ty) - DAMPING_C * piece.vy) * dt;
    piece.x += piece.vx * dt;
    piece.y += piece.vy * dt;
    const tilt = Math.max(-8, Math.min(8, piece.rotate + piece.vx * 0.02));
    piece.node.style.transform = `translate3d(${piece.x.toFixed(2)}px, ${piece.y.toFixed(2)}px, 0) rotate(${tilt.toFixed(2)}deg)`;
  }

  if (local) {
    panel.style.setProperty('--mouse-x', local.x.toFixed(0) + 'px');
    panel.style.setProperty('--mouse-y', local.y.toFixed(0) + 'px');
  }
  frame = requestAnimationFrame(tick);
}

function loopWanted() {
  return started && wideQuery.matches && !reducedQuery.matches && !document.hidden && inView;
}

function syncLoop() {
  const wanted = loopWanted();
  if (wanted && !running) {
    running = true;
    lastTime = performance.now();
    measure();
    frame = requestAnimationFrame(tick);
  } else if (!wanted && running) {
    running = false;
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
  }
}

function onPointerMove(event) {
  if (event.pointerType && event.pointerType !== 'mouse') return;
  pointer = { x: event.clientX, y: event.clientY };
}

function onPointerLeave() {
  pointer = null;
  if (panel) {
    panel.style.setProperty('--mouse-x', DEFAULT_X);
    panel.style.setProperty('--mouse-y', DEFAULT_Y);
  }
}

function onVisibility() {
  syncLoop();
}

/** Chỉ gắn listener con trỏ khi viewport >= 900 px, không giảm chuyển động, và có chuột thật (AC-199, AC-287). */
function syncPointer() {
  const want = started && !!panel && wideQuery.matches && !reducedQuery.matches && fineQuery.matches;
  if (want && !pointerAttached) {
    panel.addEventListener('pointermove', onPointerMove);
    panel.addEventListener('pointerleave', onPointerLeave);
    pointerAttached = true;
  } else if (!want && pointerAttached) {
    if (panel) {
      panel.removeEventListener('pointermove', onPointerMove);
      panel.removeEventListener('pointerleave', onPointerLeave);
    }
    pointerAttached = false;
    pointer = null;
  }
}

function onModeChange() {
  syncPointer();
  const active = wideQuery.matches && !reducedQuery.matches;
  if (!active) {
    onPointerLeave();
    for (const piece of pieces) {
      piece.x = piece.y = piece.vx = piece.vy = 0;
    }
    paintStatic();
  }
  syncLoop();
}

/** Bắt đầu hiệu ứng (idempotent). Trả về hàm destroy. */
export function start() {
  if (started) return destroy;
  panel = document.querySelector('.auth-brand');
  layer = document.querySelector('.auth-drift');
  if (!panel || !layer) return destroy;
  started = true;
  ensurePieces();
  paintStatic();

  syncPointer();
  document.addEventListener('visibilitychange', onVisibility);
  wideQuery.addEventListener('change', onModeChange);
  reducedQuery.addEventListener('change', onModeChange);
  fineQuery.addEventListener('change', onModeChange);
  window.addEventListener('pagehide', destroy, { once: true });

  if ('ResizeObserver' in window) {
    resizeObserver = new ResizeObserver(() => measure());
    resizeObserver.observe(panel);
  }
  if ('IntersectionObserver' in window) {
    intersectionObserver = new IntersectionObserver((records) => {
      inView = records.some((r) => r.isIntersecting);
      syncLoop();
    });
    intersectionObserver.observe(panel);
  }
  syncLoop();
  return destroy;
}

/** Hủy rAF, gỡ mọi listener và observer. */
export function destroy() {
  started = false;
  running = false;
  if (frame) cancelAnimationFrame(frame);
  frame = 0;
  syncPointer();
  document.removeEventListener('visibilitychange', onVisibility);
  wideQuery.removeEventListener('change', onModeChange);
  reducedQuery.removeEventListener('change', onModeChange);
  fineQuery.removeEventListener('change', onModeChange);
  window.removeEventListener('pagehide', destroy);
  if (resizeObserver) resizeObserver.disconnect();
  if (intersectionObserver) intersectionObserver.disconnect();
  resizeObserver = null;
  intersectionObserver = null;
  pointer = null;
}

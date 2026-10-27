// Biểu đồ tự viết bằng SVG/CSS (không thư viện, không canvas). Màu chỉ đến từ lớp CSS (token trong style.css).
// Mọi biểu đồ có tên truy cập đủ số liệu (role="img" + aria-label), kèm chữ và số hiển thị; chuyển động chạy một lần
// khi vào khung nhìn (fx.onceInView): vòng chỉ đổi stroke-dashoffset, thanh chỉ đổi transform scaleX.
import { el } from '/js/core/dom.js';
import { icon } from '/js/core/icons.js';
import { onceInView } from '/js/core/fx.js';

const SVG_NS = 'http://www.w3.org/2000/svg';
const RADIUS = 44;
const LENGTH = 2 * Math.PI * RADIUS;

/** Số nguyên không âm; thiếu hoặc không hợp lệ coi là 0 (không NaN, không âm). */
export function count(value) {
  const n = Math.floor(Number(value));
  return Number.isFinite(n) && n > 0 ? n : 0;
}

export function percent(part, whole) {
  const w = count(whole);
  return w > 0 ? Math.round((Math.min(count(part), w) / w) * 100) : 0;
}

function svgNode(tag, attrs) {
  const node = document.createElementNS(SVG_NS, tag);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, String(value));
  return node;
}

/** Vòng tiến độ: value/total (đã cắt về hợp lệ). total = 0 vẽ vòng rỗng 0%. */
export function ring({ value, total, unit = 'bài tập' }) {
  const t = count(total);
  const v = Math.min(count(value), t);
  const p = percent(v, t);
  const label = t > 0 ? `Đã đạt ${v} trên ${t} ${unit}, ${p} phần trăm` : `Chưa có ${unit} được giao`;
  const svg = svgNode('svg', { viewBox: '0 0 100 100', role: 'img', 'aria-label': label });
  svg.append(svgNode('circle', { class: 'ring__track', cx: 50, cy: 50, r: RADIUS, 'stroke-width': 8 }));
  const arc = svgNode('circle', {
    class: 'ring__value',
    cx: 50,
    cy: 50,
    r: RADIUS,
    'stroke-width': 8,
    'stroke-linecap': 'round',
    'stroke-dasharray': LENGTH.toFixed(2),
    'stroke-dashoffset': LENGTH.toFixed(2),
    transform: 'rotate(-90 50 50)',
  });
  if (v === 0) arc.style.opacity = '0';
  svg.append(arc);
  const root = el('div', { class: 'ring' }, svg, el('span', { class: 'ring__text', 'aria-hidden': 'true' }, `${p}%`));
  const finalOffset = (LENGTH * (1 - (t > 0 ? v / t : 0))).toFixed(2);
  onceInView(root, () => { arc.style.strokeDashoffset = finalOffset; }, 0.4);
  return root;
}

const TONES = { done: 'check', part: 'half', none: 'dash' };

/**
 * Thanh chồng. segments: [{ label, value, tone: 'done' | 'part' | 'none' }].
 * Trả về nút DOM, hoặc đoạn chữ emptyText khi tổng bằng 0 (không vẽ biểu đồ giả).
 * Chú giải luôn có chữ và số; mỗi tông có họa tiết riêng (tích, nửa, sọc) nên màu không là kênh duy nhất.
 */
export function stackedBar({ segments, name = '', emptyText = 'Chưa có dữ liệu' }) {
  const parts = segments.map((s) => ({ ...s, value: count(s.value) }));
  const total = parts.reduce((sum, s) => sum + s.value, 0);
  if (total === 0) return el('p', { class: 'muted sbar__empty' }, emptyText);
  const summary = parts.map((s) => `${s.label} ${s.value}`).join(', ');
  const fill = el('span', { class: 'sbar__fill' });
  for (const s of parts) {
    if (s.value === 0) continue;
    const seg = el('span', { class: 'sbar__seg sbar__seg--' + s.tone });
    seg.style.width = Math.min(100, (s.value / total) * 100).toFixed(2) + '%';
    fill.append(seg);
  }
  const track = el('span', { class: 'sbar__track', role: 'img', 'aria-label': `${name ? name + ': ' : ''}${summary}, tổng ${total}` }, fill);
  const legend = el(
    'ul',
    { class: 'sbar__legend', 'aria-hidden': 'true' },
    parts.map((s) => el('li', {}, el('span', { class: 'sbar__swatch sbar__seg--' + s.tone }, s.tone === 'none' ? null : icon(TONES[s.tone])), `${s.label}: `, el('strong', { class: 'mono' }, String(s.value)))),
  );
  const root = el('div', { class: 'sbar' }, track, legend);
  onceInView(root, () => fill.classList.add('is-drawn'), 0.2);
  return root;
}

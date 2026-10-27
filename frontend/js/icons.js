// Bộ icon SVG hình học đơn giản (QD-56): dựng bằng createElementNS, viewBox 24, nét 2, trang trí (aria-hidden).
const NS = 'http://www.w3.org/2000/svg';

const ICONS = {
  check: [['path', { d: 'M5 12.5l4.5 4.5L19 7' }]],
  x: [['path', { d: 'M6 6l12 12M18 6L6 18' }]],
  circle: [['circle', { cx: 12, cy: 12, r: 8 }]],
  half: [['circle', { cx: 12, cy: 12, r: 8 }], ['path', { d: 'M12 4a8 8 0 0 1 0 16z', fill: 'currentColor' }]],
  dash: [['path', { d: 'M6 12h12' }]],
  alert: [['path', { d: 'M12 4l9.5 16.5h-19z' }], ['path', { d: 'M12 10v4.5M12 17.6v.01' }]],
  'alert-circle': [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M12 7.5v5.5M12 16.5v.01' }]],
  info: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M12 11v6M12 7.5v.01' }]],
  pencil: [['path', { d: 'M4 20l1-4L16.5 4.5a2.1 2.1 0 0 1 3 3L8 19z' }], ['path', { d: 'M14.5 6.5l3 3' }]],
  published: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M8.5 12.5l2.5 2.5 4.5-5' }]],
  archive: [['path', { d: 'M4 5h16v4H4z' }], ['path', { d: 'M5.5 9v10h13V9M10 13h4' }]],
  sun: [['circle', { cx: 12, cy: 12, r: 4 }], ['path', { d: 'M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4' }]],
  moon: [['path', { d: 'M20 14.5A8.5 8.5 0 0 1 9.5 4 8.5 8.5 0 1 0 20 14.5z' }]],
  auto: [['path', { d: 'M3 4h18v12H3z' }], ['path', { d: 'M8 20h8M12 16v4' }]],
  'arrow-right': [['path', { d: 'M5 12h14M13 6l6 6-6 6' }]],
  menu: [['path', { d: 'M4 7h16M4 12h16M4 17h16' }]],
  lock: [['path', { d: 'M5 11h14v9H5z' }], ['path', { d: 'M8 11V8a4 4 0 0 1 8 0v3' }]],
  key: [['circle', { cx: 8, cy: 15, r: 4 }], ['path', { d: 'M11 12l8-8M16 7l3 3' }]],
  eye: [['path', { d: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z' }], ['circle', { cx: 12, cy: 12, r: 3 }]],
  ban: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M5.6 5.6l12.8 12.8' }]],
  copy: [['path', { d: 'M9 9h11v11H9z' }], ['path', { d: 'M5 15V4h11' }]],
  inbox: [['path', { d: 'M3 13l3-8h12l3 8v6H3z' }], ['path', { d: 'M3 13h5l1 3h6l1-3h5' }]],
  clock: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M12 7v5l3.5 2' }]],
  star: [['path', { d: 'M12 3l2.8 5.7 6.2.9-4.5 4.4 1.1 6.2L12 17.2 6.4 20.2l1.1-6.2L3 9.6l6.2-.9z' }]],
  code: [['path', { d: 'M8 7l-5 5 5 5M16 7l5 5-5 5M14 4l-4 16' }]],
  chart: [['path', { d: 'M4 20v-9M10 20V5M16 20v-6M2 20h20' }]],
  calendar: [['path', { d: 'M4 6h16v14H4z' }], ['path', { d: 'M4 10h16M8 3v4M16 3v4' }]],
  list: [['path', { d: 'M9 6h11M9 12h11M9 18h11M4 6v.01M4 12v.01M4 18v.01' }]],
  users: [['circle', { cx: 9, cy: 8, r: 3.5 }], ['path', { d: 'M2.5 20a6.5 6.5 0 0 1 13 0M16 5a3.5 3.5 0 0 1 0 7M18.5 14.5a6.5 6.5 0 0 1 3 5.5' }]],
  book: [['path', { d: 'M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z' }], ['path', { d: 'M4 19V5' }]],
  grid: [['path', { d: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z' }]],
  clipboard: [['path', { d: 'M8 4h8v3H8zM6 5.5H5v15h14v-15h-1M9 12h6M9 16h4' }]],
  send: [['path', { d: 'M3 11l18-8-8 18-2.5-7.5z' }]],
  search: [['circle', { cx: 11, cy: 11, r: 7 }], ['path', { d: 'M20 20l-4-4' }]],
  user: [['circle', { cx: 12, cy: 8, r: 4 }], ['path', { d: 'M4 20a8 8 0 0 1 16 0' }]],
  'chevron-down': [['path', { d: 'M6 9l6 6 6-6' }]],
  'chevrons-left': [['path', { d: 'M11 7l-5 5 5 5M18 7l-5 5 5 5' }]],
  'log-out': [['path', { d: 'M10 4H5v16h5M15 8l4 4-4 4M9 12h10' }]],
  plus: [['path', { d: 'M12 5v14M5 12h14' }]],
  'arrow-left': [['path', { d: 'M19 12H5M11 6l-6 6 6 6' }]],
  'arrow-up': [['path', { d: 'M12 19V5M6 11l6-6 6 6' }]],
  'arrow-down': [['path', { d: 'M12 5v14M6 13l6 6 6-6' }]],
  trash: [['path', { d: 'M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6' }]],
  'eye-off': [['path', { d: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z' }], ['path', { d: 'M4 4l16 16' }]],
  braces: [['path', { d: 'M8 4C6 4 5 5 5 7v3c0 1-1 2-2 2 1 0 2 1 2 2v3c0 2 1 3 3 3M16 4c2 0 3 1 3 3v3c0 1 1 2 2 2-1 0-2 1-2 2v3c0 2-1 3-3 3' }]],
  sparkle: [['path', { d: 'M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z' }]],
};

/** Tạo icon SVG trang trí. name: tên trong bộ icon; className: class phụ. */
export function icon(name, className = '') {
  const svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '2');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.setAttribute('aria-hidden', 'true');
  svg.setAttribute('focusable', 'false');
  svg.setAttribute('class', ('icon icon--' + name + ' ' + className).trim());
  for (const [tag, attrs] of ICONS[name] || ICONS.circle) {
    const node = document.createElementNS(NS, tag);
    for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, String(value));
    svg.append(node);
  }
  return svg;
}

// Tiện ích DOM: mọi nội dung từ API đều được chèn bằng textContent / createElement để an toàn XSS.
import { icon } from '/js/icons.js';

export function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key === 'class') node.className = value;
    else if (key === 'text') node.textContent = value;
    else if (key.startsWith('on') && typeof value === 'function') node.addEventListener(key.slice(2), value);
    else if (value === true) node.setAttribute(key, '');
    else node.setAttribute(key, String(value));
  }
  for (const child of children.flat()) {
    if (child === null || child === undefined || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

export function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
  return node;
}

/** Chèn văn bản có `mã nội dòng` bằng dấu huyền; phần còn lại là text thuần. */
export function inlineRich(text) {
  const frag = document.createDocumentFragment();
  const parts = String(text ?? '').split('`');
  // Số dấu huyền lẻ: dấu cuối không có cặp, coi như ký tự thường.
  if (parts.length % 2 === 0) {
    const last = parts.pop();
    parts[parts.length - 1] += '`' + last;
  }
  parts.forEach((part, i) => {
    if (part === '') return;
    if (i % 2 === 1) frag.append(el('code', { class: 'inline-code' }, part));
    else frag.append(document.createTextNode(part));
  });
  return frag;
}

/**
 * Khối mã kiểu cửa sổ (AC-233): thanh tiêu đề (3 chấm trang trí, nhãn ngôn ngữ, nút Sao chép) + pre.codeblock > code.
 * Nút Sao chép chép đúng nội dung mã (không kèm số dòng/nhãn); kết quả báo qua toast (vùng aria-live).
 */
export function codeBlockEl(code, lang = '') {
  const label = String(lang || '').trim() || 'Mã';
  const codeNode = el('code', {}, code);
  const copyLabel = el('span', {}, 'Sao chép');
  const button = el(
    'button',
    { type: 'button', class: 'btn btn--small codeblock__copy', 'aria-label': 'Sao chép mã ' + label },
    icon('copy'),
    copyLabel,
  );
  let timer = 0;
  button.addEventListener('click', async () => {
    let ok = false;
    let notify = null;
    try {
      const [{ copyText }, { toast }] = await Promise.all([import('/js/ui.js'), import('/js/toast.js')]);
      ok = await copyText(codeNode.textContent);
      notify = toast;
    } catch (e) {
      ok = false;
    }
    copyLabel.textContent = ok ? 'Đã sao chép' : 'Không sao chép được';
    if (notify) notify(ok ? 'ok' : 'error', ok ? 'Đã sao chép mã vào bộ nhớ tạm' : 'Không sao chép được');
    clearTimeout(timer);
    timer = setTimeout(() => { copyLabel.textContent = 'Sao chép'; }, 2000);
  });
  return el(
    'div',
    { class: 'codeblock-wrap' },
    el(
      'div',
      { class: 'codeblock__bar' },
      el('span', { class: 'codeblock__dots', 'aria-hidden': 'true' }, el('i'), el('i'), el('i')),
      el('span', { class: 'codeblock__lang' }, label),
      button,
    ),
    el('pre', { class: 'codeblock' }, codeNode),
  );
}

function codeBlock(raw) {
  // raw: "```python\n...\n```"
  const lines = raw.replace(/\r\n/g, '\n').split('\n');
  const lang = (lines.shift() || '').replace(/^`+/, '').trim().split(/\s+/)[0];
  if (lines.length && lines[lines.length - 1].trim().startsWith('```')) lines.pop();
  return codeBlockEl(lines.join('\n'), lang);
}

/**
 * Hiển thị mảng chuỗi của một mục bài học.
 * - Phần tử bắt đầu bằng ``` là khối mã (pre/code, monospace).
 * - Các phần tử liên tiếp bắt đầu bằng "- " là danh sách gạch đầu dòng.
 * - Phần tử còn lại là đoạn văn. forceList: mọi phần tử đều là mục danh sách.
 */
export function renderBlocks(items, { forceList = false } = {}) {
  const root = el('div', { class: 'prose' });
  let list = null;
  const pushItem = (text) => {
    if (!list) {
      list = el('ul');
      root.append(list);
    }
    list.append(el('li', {}, inlineRich(text)));
  };

  for (const item of items || []) {
    const text = String(item ?? '');
    if (text.trimStart().startsWith('```')) {
      list = null;
      root.append(codeBlock(text.trim()));
    } else if (forceList) {
      pushItem(text.replace(/^-\s+/, ''));
    } else if (/^-\s+/.test(text)) {
      pushItem(text.replace(/^-\s+/, ''));
    } else {
      list = null;
      root.append(el('p', {}, inlineRich(text)));
    }
  }
  return root;
}

/** Bốn mục cố định của trang bài học; dùng chung giữa lesson.html và khung xem trước của giáo viên. */
export const LESSON_SECTIONS = [
  { key: 'summary', id: 'tom-tat', title: 'Tóm tắt nội dung chính của bài học' },
  { key: 'notes', id: 'luu-y', title: 'Những điểm cần lưu ý' },
  { key: 'exercises', id: 'bai-tap', title: 'Bài tập' },
  { key: 'conclusion', id: 'tong-ket', title: 'Tổng kết bài học' },
];

/** Dựng bốn mục của bài học (cùng một hàm cho trang học sinh và khung xem trước). */
export function lessonSections(lesson, exercisesNode) {
  return LESSON_SECTIONS.map((s) => {
    let body = exercisesNode;
    if (s.key === 'summary') body = renderBlocks(lesson.summary);
    else if (s.key === 'notes') body = renderBlocks(lesson.notes, { forceList: true });
    else if (s.key === 'conclusion') body = renderBlocks(lesson.conclusion);
    return el('section', { class: 'lesson-section', 'aria-labelledby': s.id }, el('h2', { id: s.id }, s.title), body);
  });
}

/** Hiển thị văn bản nhiều đoạn (ngăn cách bằng dòng trống), hỗ trợ danh sách "- " và `mã`. */
export function renderText(text) {
  const root = el('div', { class: 'prose' });
  const paragraphs = String(text ?? '').replace(/\r\n/g, '\n').split(/\n{2,}/);
  for (const para of paragraphs) {
    const lines = para.split('\n');
    if (lines.length && lines.every((l) => /^-\s+/.test(l))) {
      root.append(el('ul', {}, lines.map((l) => el('li', {}, inlineRich(l.replace(/^-\s+/, ''))))));
    } else if (para.trim() !== '') {
      const p = el('p');
      lines.forEach((l, i) => {
        if (i > 0) p.append(document.createElement('br'));
        p.append(inlineRich(l));
      });
      root.append(p);
    }
  }
  return root;
}

export function difficultyInfo(code) {
  switch (code) {
    case 'easy': return { label: 'Dễ', cls: 'badge--easy' };
    case 'medium': return { label: 'Trung bình', cls: 'badge--medium' };
    case 'hard': return { label: 'Khó', cls: 'badge--hard' };
    default: return { label: String(code ?? ''), cls: '' };
  }
}

/** Dải skeleton nhỏ nội dòng, thay cho spinner quay (AC-252). */
export function spinner() {
  return el('span', { class: 'sk sk--inline', 'aria-hidden': 'true' });
}

function skeletonCell(featured) {
  return el(
    'div',
    { class: 'sk-cell' + (featured ? ' sk-cell--featured' : '') },
    el('span', { class: 'sk sk--pill' }),
    el('span', { class: 'sk sk--title' }),
    el('span', { class: 'sk sk--line' }),
    featured ? el('span', { class: 'sk sk--line' }) : null,
  );
}

/**
 * Khung xương đúng hình bố cục (AC-269). shape: lines | rows | bento | dashboard | table | panes | inline.
 * Phần hình là aria-hidden; chỉ có một dòng sr-only role=status đọc nhãn tải (label).
 */
export function skeleton({ shape = 'lines', count = 3, label = '' } = {}) {
  let body;
  if (shape === 'rows') {
    body = el('div', { class: 'sk-stack' }, Array.from({ length: count }, () => el('span', { class: 'sk sk-row' })));
  } else if (shape === 'bento') {
    body = el('div', { class: 'lesson-grid' }, Array.from({ length: count }, (_, i) => skeletonCell(i === 0)));
  } else if (shape === 'dashboard') {
    body = el(
      'div',
      { class: 'sk-dashboard' },
      el('div', { class: 'sk-dash' }, el('span', { class: 'sk sk--circle' }), el('div', { class: 'sk-stack grow' }, el('span', { class: 'sk sk--title' }), el('span', { class: 'sk sk--line' }))),
      el('div', { class: 'sk-dash' }, el('div', { class: 'sk-stack grow' }, el('span', { class: 'sk sk--line' }), el('span', { class: 'sk sk--title' }), el('span', { class: 'sk sk--line' }))),
    );
  } else if (shape === 'table') {
    body = el('div', { class: 'sk-table' }, el('span', { class: 'sk sk--line' }), Array.from({ length: count }, () => el('span', { class: 'sk sk-row' })));
  } else if (shape === 'panes') {
    body = el('div', { class: 'sk-panes' }, el('span', { class: 'sk sk--block' }), el('span', { class: 'sk sk--block' }));
  } else if (shape === 'inline') {
    body = el('span', { class: 'sk sk--inline' });
  } else {
    body = el('div', { class: 'sk-stack' }, el('span', { class: 'sk sk--title' }), Array.from({ length: Math.max(1, count - 1) }, () => el('span', { class: 'sk sk--line' })));
  }
  body.setAttribute('aria-hidden', 'true');
  return el('div', { class: 'sk-wrap' }, label ? el('p', { class: 'sr-only', role: 'status' }, label) : null, body);
}

/**
 * Khối trạng thái dùng chung. kind: info | loading (skeleton, AC-252) | empty | error (role=alert, AC-272).
 * shape/count chỉ dùng cho kind=loading để skeleton khớp bố cục thật.
 */
export function stateBox({ title, message, kind = 'info', actions = [], shape = 'lines', count = 3 }) {
  if (kind === 'loading') {
    return el('div', { class: 'state state--loading' }, skeleton({ shape, count, label: title }));
  }
  const box = el('div', {
    class: 'state' + (kind === 'error' ? ' state--error' : kind === 'empty' ? ' state--empty' : ''),
    role: kind === 'error' ? 'alert' : 'status',
  });
  box.append(el('span', { class: 'state__icon', 'aria-hidden': 'true' }, icon(kind === 'error' ? 'alert' : kind === 'empty' ? 'inbox' : 'info')));
  box.append(el('p', { class: 'state__title' }, el('strong', {}, title)));
  if (message) box.append(el('p', { class: 'muted' }, message));
  if (actions.length) box.append(el('div', { class: 'state__actions' }, actions));
  return box;
}

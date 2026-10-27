// Tiện ích giao diện dùng chung (mọi nội dung chèn bằng textContent/createElement).
import { el, clear } from '/js/core/dom.js';
import { icon } from '/js/core/icons.js';

export { icon };

const pad = (n) => String(n).padStart(2, '0');

/** dd/MM/yyyy HH:mm theo giờ địa phương của trình duyệt. */
export function formatDateTime(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Giá trị cho <input type="datetime-local"> (giờ địa phương). */
export function toLocalInputValue(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Chuyển giá trị datetime-local (giờ địa phương) sang chuỗi ISO UTC; rỗng -> null. */
export function fromLocalInput(value) {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export function timeZoneLabel() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'giờ địa phương';
  } catch (e) {
    return 'giờ địa phương';
  }
}

const STATUS = {
  NotStarted: { label: 'Chưa làm', cls: 'chip--notstarted', icon: 'circle' },
  InProgress: { label: 'Đang làm', cls: 'chip--inprogress', icon: 'half' },
  Passed: { label: 'Đã đạt', cls: 'chip--passed', icon: 'check' },
  NotAssigned: { label: 'Chưa giao', cls: 'chip--notassigned', icon: 'dash' },
};

/** Chip có biểu tượng SVG (aria-hidden) và chữ, không chỉ màu (AC-261, AC-296). */
export function chip(cls, iconName, text) {
  return el('span', { class: 'chip ' + cls }, icon(iconName), text);
}

export function statusChip(status) {
  const s = STATUS[status] || STATUS.NotStarted;
  return chip(s.cls, s.icon, s.label);
}

export function lateChip(text = 'Nộp muộn') {
  return chip('chip--late', 'alert', text);
}

export function overdueChip() {
  return chip('chip--overdue', 'alert', 'Quá hạn');
}

export function stateChip(kind) {
  const map = {
    draft: ['Bản nháp', 'chip--draft', 'pencil'],
    published: ['Đã xuất bản', 'chip--published', 'published'],
    archived: ['Lưu trữ', 'chip--archived', 'archive'],
  };
  const [label, cls, iconName] = map[kind];
  return chip(cls, iconName, label);
}

/** Vùng thông báo trong trang: kind = info | warn | error | ok. role="alert" cho lỗi, "status" cho còn lại. */
export function banner(kind, text, extra = []) {
  return el('div', { class: 'banner banner--' + kind, role: kind === 'error' ? 'alert' : 'status' }, text, extra);
}

export function showBanner(container, kind, text, extra = []) {
  clear(container);
  if (text) container.append(banner(kind, text, extra));
}

/**
 * Trường biểu mẫu có nhãn, gợi ý và vùng lỗi (aria-describedby, aria-invalid).
 * type: text | password | textarea | select | datetime-local | number | file
 */
export function field({ id, label, type = 'text', value = '', hint = '', maxlength = null, rows = 4, required = false, code = false, options = [], autocomplete = null, inputmode = null, counter = false }) {
  const hintId = id + '-hint';
  const errId = id + '-error';
  let input;
  if (type === 'textarea') {
    input = el('textarea', { id, class: 'textarea' + (code ? ' textarea--code' : ''), rows: String(rows), maxlength, spellcheck: code ? 'false' : null });
    input.value = value ?? '';
  } else if (type === 'select') {
    input = el('select', { id, class: 'select' }, options.map((o) => el('option', { value: String(o.value) }, o.label)));
    input.value = String(value ?? '');
  } else {
    input = el('input', { id, class: 'input', type, maxlength, autocomplete, inputmode, required: required ? true : null });
    if (type !== 'file') input.value = value ?? '';
  }
  const describedBy = [hint ? hintId : null, errId].filter(Boolean).join(' ');
  input.setAttribute('aria-describedby', describedBy);
  const hintEl = hint ? el('p', { class: 'hint', id: hintId }, hint) : null;
  const errEl = el('p', { class: 'field-error', id: errId, role: 'alert', hidden: true });
  const counterEl = counter && maxlength ? el('div', { class: 'char-count', 'aria-hidden': 'true' }) : null;
  const root = el('div', { class: 'form-field' }, el('label', { for: id }, label + (required ? ' *' : '')), hintEl, input, counterEl, errEl);
  const updateCounter = () => {
    if (counterEl) counterEl.textContent = `${input.value.length}/${maxlength}`;
  };
  if (counterEl) {
    input.addEventListener('input', updateCounter);
    updateCounter();
  }
  return {
    root,
    input,
    get value() {
      return input.value;
    },
    set value(v) {
      input.value = v;
      updateCounter();
    },
    setError(message) {
      errEl.hidden = !message;
      errEl.textContent = message || '';
      if (message) input.setAttribute('aria-invalid', 'true');
      else input.removeAttribute('aria-invalid');
    },
    clearError() {
      this.setError('');
    },
  };
}

/** Gán lỗi theo trường từ phản hồi 422 (errors[] hoặc field/message) và đưa tiêu điểm tới ô lỗi đầu tiên. */
export function applyFieldErrors(err, fieldsByName, summary = null) {
  Object.values(fieldsByName).forEach((f) => f.clearError && f.clearError());
  const list = err && err.errors && err.errors.length ? err.errors : err && err.field ? [{ field: err.field, message: err.message }] : [];
  let first = null;
  const unmatched = [];
  for (const e of list) {
    const target = fieldsByName[e.field] || fieldsByName[String(e.field).split('[')[0]];
    if (target) {
      const current = target.root ? target : null;
      if (current) current.setError(e.message);
      if (!first) first = current;
    } else {
      unmatched.push(e.message);
    }
  }
  if (first && first.input) first.input.focus();
  if (summary && unmatched.length) showBanner(summary, 'error', unmatched.join(' '));
  return list.length > 0;
}

/** Đặt trạng thái bận cho nút (vô hiệu + đổi chữ) để chống gửi đôi. */
export async function withBusy(button, busyText, task) {
  if (button.disabled) return undefined;
  const original = Array.from(button.childNodes); // giữ cả biểu tượng SVG, không chỉ chữ
  button.disabled = true;
  button.setAttribute('aria-busy', 'true');
  if (busyText) button.textContent = busyText;
  try {
    return await task();
  } finally {
    button.disabled = false;
    button.removeAttribute('aria-busy');
    button.replaceChildren(...original);
  }
}

export async function copyText(text) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch (e) {
    const area = el('textarea', { 'aria-hidden': 'true' });
    area.style.position = 'fixed';
    area.style.left = '-9999px';
    area.value = text;
    document.body.append(area);
    area.select();
    let ok = false;
    try {
      ok = document.execCommand('copy');
    } catch (err) {
      ok = false;
    }
    area.remove();
    return ok;
  }
}

/** Chống CSV injection: giá trị bắt đầu bằng = + - @ (hoặc tab/CR) được thêm dấu ' phía trước (AC-105, AC-141). */
export function csvCell(value) {
  let text = value === null || value === undefined ? '' : String(value);
  if (/^[=+\-@\t\r]/.test(text)) text = "'" + text;
  if (/[",\r\n]/.test(text)) text = '"' + text.replace(/"/g, '""') + '"';
  return text;
}

export function downloadCsv(filename, rows) {
  const text = '﻿' + rows.map((r) => r.map(csvCell).join(',')).join('\r\n') + '\r\n';
  const blob = new Blob([text], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = el('a', { href: url, download: filename });
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Cảnh báo khi rời trang có thay đổi chưa lưu (AC-126). */
export function guardUnsaved(isDirty) {
  window.addEventListener('beforeunload', (event) => {
    if (isDirty()) {
      event.preventDefault();
      event.returnValue = '';
    }
  });
}

export function setTitle(pageName) {
  document.title = `${pageName} – DK Edu`;
}

/** Bảng dữ liệu có <caption> và tiêu đề cột scope="col" (AC-109). */
export function dataTable({ caption, headers, rows, className = '' }) {
  const table = el(
    'table',
    { class: 'data ' + className },
    el('caption', {}, caption),
    el('thead', {}, el('tr', {}, headers.map((h) => el('th', { scope: 'col', class: h.num ? 'num' : null }, h.label ?? h)))),
    el('tbody', {}, rows),
  );
  return el('div', { class: 'table-wrap', role: 'region', 'aria-label': caption, tabindex: '0' }, table);
}

export function debounce(fn, ms = 300) {
  let timer = null;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

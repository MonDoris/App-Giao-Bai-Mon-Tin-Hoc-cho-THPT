// Bộ thành phần dùng chung của hai trình soạn (bài học, bài tập): thanh bước dính, thanh lưu dính, chỉ báo lưu,
// tổng hợp lỗi, danh sách kiểm tra, tab Soạn/Xem trước, menu, công tắc có chữ, nút chèn định dạng, Điền mẫu, Ctrl+S.
// Mọi nội dung chèn bằng textContent/createElement (an toàn XSS).
import { el, clear } from '/js/dom.js';
import { icon } from '/js/icons.js';
import { confirmDialog } from '/js/modal.js';
import { toast } from '/js/toast.js';

const pad2 = (n) => String(n).padStart(2, '0');
export const hhmm = () => {
  const d = new Date();
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
};

/** Nút có biểu tượng SVG (aria-hidden) và chữ. */
export function iconBtn(text, iconName, onClick, { cls = 'btn btn--small', label = null, disabled = false, after = false } = {}) {
  const parts = [iconName ? icon(iconName) : null, el('span', {}, text)];
  const b = el('button', { type: 'button', class: cls, 'aria-label': label }, after ? parts.reverse() : parts);
  b.disabled = disabled;
  if (onClick) b.addEventListener('click', onClick);
  return b;
}

// ---------------------------------------------------------------- Chèn cú pháp vào ô văn bản (giữ Ctrl+Z khi có thể)
function replaceRange(ta, a, b, text, selA, selB) {
  ta.focus();
  ta.setSelectionRange(a, b);
  let ok = false;
  try {
    ok = document.execCommand('insertText', false, text);
  } catch (e) {
    ok = false;
  }
  if (!ok) {
    ta.setRangeText(text, a, b, 'end');
    ta.dispatchEvent(new Event('input', { bubbles: true }));
  }
  ta.setSelectionRange(a + selA, a + selB);
}

/** Bọc phần đang chọn bằng dấu huyền (hoặc bỏ cặp nếu đã nằm trong cặp); không chọn gì thì chèn cặp và đặt con trỏ giữa. */
export function toggleInlineCode(ta) {
  const v = ta.value;
  const a = ta.selectionStart;
  const b = ta.selectionEnd;
  const sel = v.slice(a, b);
  if (a > 0 && v[a - 1] === '`' && v[b] === '`' && !sel.includes('`')) {
    replaceRange(ta, a - 1, b + 1, sel, 0, sel.length);
    return;
  }
  replaceRange(ta, a, b, '`' + sel + '`', 1, 1 + sel.length);
}

/** Thêm hoặc bỏ "- " ở đầu mỗi dòng đang chọn (dòng trống giữ nguyên). */
export function toggleBulletLines(ta) {
  const v = ta.value;
  const start = ta.selectionStart === 0 ? 0 : v.lastIndexOf('\n', ta.selectionStart - 1) + 1;
  let end = v.indexOf('\n', ta.selectionEnd);
  if (end < 0) end = v.length;
  const lines = v.slice(start, end).split('\n');
  const filled = lines.filter((l) => l !== '');
  const all = filled.length > 0 && filled.every((l) => /^-\s+/.test(l));
  const next = lines.map((l) => (l === '' ? l : all ? l.replace(/^-\s+/, '') : /^-\s+/.test(l) ? l : '- ' + l)).join('\n');
  replaceRange(ta, start, end, next, 0, next.length);
}

/** Mũi tên trái/phải, Home, End di chuyển giữa các nút của thanh công cụ (roving tabindex). */
export function roving(bar) {
  const items = () => Array.from(bar.querySelectorAll('button:not(:disabled)'));
  const first = items()[0];
  bar.querySelectorAll('button').forEach((b) => { b.tabIndex = b === first ? 0 : -1; });
  bar.addEventListener('keydown', (e) => {
    const list = items();
    const i = list.indexOf(document.activeElement);
    const j = e.key === 'ArrowRight' ? i + 1 : e.key === 'ArrowLeft' ? i - 1 : e.key === 'Home' ? 0 : e.key === 'End' ? list.length - 1 : null;
    if (j === null || i < 0) return;
    e.preventDefault();
    const target = list[(j + list.length) % list.length];
    bar.querySelectorAll('button').forEach((b) => { b.tabIndex = b === target ? 0 : -1; });
    target.focus();
  });
}

// ---------------------------------------------------------------- Menu bật lên (mẫu ARIA menu button)
let closeOpenMenu = null;

/** items(): [{ text, icon?, onSelect(trigger), danger?, disabled?, title?, sep? }]. Mũi tên/Home/End di chuyển, Esc đóng, bấm ngoài đóng. */
export function menuButton({ text, iconName = null, cls = 'btn btn--small', label = null, items, end = false }) {
  const trigger = el('button', { type: 'button', class: cls, 'aria-haspopup': 'menu', 'aria-expanded': 'false', 'aria-label': label }, iconName ? icon(iconName) : null, el('span', {}, text), icon('chevron-down'));
  const menu = el('div', { class: 'ed-menu__list' + (end ? ' is-end' : ''), role: 'menu', 'aria-label': label || text, hidden: true });
  const root = el('div', { class: 'ed-menu' }, trigger, menu);
  const enabled = () => Array.from(menu.querySelectorAll('[role="menuitem"]:not(:disabled)'));
  const outside = (e) => { if (!root.contains(e.target)) close(false); };

  function close(refocus) {
    if (menu.hidden) return;
    menu.hidden = true;
    trigger.setAttribute('aria-expanded', 'false');
    document.removeEventListener('pointerdown', outside, true);
    if (closeOpenMenu === close) closeOpenMenu = null;
    if (refocus) trigger.focus();
  }

  function open(last = false) {
    if (closeOpenMenu) closeOpenMenu(false);
    clear(menu);
    for (const it of items()) {
      if (it.sep) {
        menu.append(el('hr', { class: 'ed-menu__sep', role: 'separator' }));
        continue;
      }
      const b = el('button', { type: 'button', role: 'menuitem', tabindex: '-1', class: 'ed-menu__item' + (it.danger ? ' is-danger' : ''), title: it.title || null }, it.icon ? icon(it.icon) : null, el('span', {}, it.text));
      b.disabled = !!it.disabled;
      b.addEventListener('click', () => { close(true); it.onSelect(trigger); });
      menu.append(b);
    }
    menu.hidden = false;
    trigger.setAttribute('aria-expanded', 'true');
    document.addEventListener('pointerdown', outside, true);
    closeOpenMenu = close;
    const list = enabled();
    if (list.length) list[last ? list.length - 1 : 0].focus();
  }

  trigger.addEventListener('click', () => (menu.hidden ? open() : close(true)));
  trigger.addEventListener('keydown', (e) => {
    if (menu.hidden && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      e.preventDefault();
      open(e.key === 'ArrowUp');
    }
  });
  menu.addEventListener('keydown', (e) => {
    const list = enabled();
    const i = list.indexOf(document.activeElement);
    const to = (n) => { e.preventDefault(); if (list.length) list[(n + list.length) % list.length].focus(); };
    if (e.key === 'ArrowDown') to(i + 1);
    else if (e.key === 'ArrowUp') to(i - 1);
    else if (e.key === 'Home') to(0);
    else if (e.key === 'End') to(-1);
    else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(true); } else if (e.key === 'Tab') close(false);
  });
  return { root, trigger };
}

// ---------------------------------------------------------------- Thanh bước (không chặn: bấm bước nào cũng được, QD-72)
const STATES = { done: ['check', 'Hoàn thành'], todo: ['circle', 'Chưa xong'], error: ['alert', 'Có lỗi'] };

export function wizard({ label, names, panels }) {
  let index = -1;
  const listeners = [];
  const buttons = names.map((name, i) => {
    const b = el('button', { type: 'button', class: 'ed-step' },
      el('span', { class: 'ed-step__n' }, el('span', { class: 'ed-step__num' }, `${i + 1}.`)),
      el('span', { class: 'ed-step__body' },
        el('span', { class: 'ed-step__t' }, name),
        el('span', { class: 'ed-step__state' })));
    b.addEventListener('click', () => api.go(i));
    return b;
  });
  const nav = el('nav', { class: 'ed-steps', 'aria-label': label }, el('ol', {}, buttons.map((b) => el('li', {}, b))));
  const api = {
    nav,
    get index() { return index; },
    count: names.length,
    on(fn) { listeners.push(fn); },
    /** kind: done | todo | error. Trạng thái luôn có chữ và biểu tượng, không chỉ màu. */
    setStatus(i, kind) {
      const [iconName, text] = STATES[kind];
      const slot = buttons[i].querySelector('.ed-step__state');
      if (slot.dataset.kind === kind) return;
      slot.dataset.kind = kind;
      buttons[i].dataset.state = kind;
      slot.replaceChildren(icon(iconName), el('span', { class: 'ed-step__st' }, text));
      // Vòng số thứ tự: dấu tích khi xong, dấu chấm than khi lỗi (chữ trạng thái vẫn đọc được ngay bên cạnh).
      buttons[i].querySelector('.ed-step__n').replaceChildren(kind === 'todo'
        ? el('span', { class: 'ed-step__num' }, `${i + 1}.`)
        : el('span', { class: 'ed-step__num' }, el('span', { class: 'sr-only' }, `${i + 1}.`), icon(iconName)));
    },
    go(i, focusEl = null) {
      if (i < 0 || i >= names.length) return;
      index = i;
      panels.forEach((p, k) => { p.hidden = k !== i; });
      buttons.forEach((b, k) => (k === i ? b.setAttribute('aria-current', 'step') : b.removeAttribute('aria-current')));
      listeners.forEach((fn) => fn(i));
      const gap = panels[i].getBoundingClientRect().top - nav.getBoundingClientRect().bottom;
      if (gap < 0) window.scrollBy(0, gap - 8);
      if (focusEl) focusEl.focus();
    },
  };
  names.forEach((_, i) => api.setStatus(i, 'todo'));
  return api;
}

// ---------------------------------------------------------------- Thanh lưu dính và chỉ báo lưu (AC-400, AC-402)
export function saveBar({ saveBtn, wiz, noun }) {
  const TEXT = {
    clean: () => 'Chưa có thay đổi',
    dirty: () => 'Có thay đổi chưa lưu',
    draft: (t) => `Đã tự lưu tạm trên máy lúc ${t}`,
    saving: () => 'Đang lưu…',
    saved: (t) => `Đã lưu ${noun} lúc ${t}`,
    fail: () => 'Lưu chưa được, hãy thử lại',
  };
  const status = el('p', { class: 'ed-savebar__status', role: 'status', 'aria-live': 'polite' }, TEXT.clean());
  const prev = iconBtn('Bước trước', 'arrow-left', () => wiz.go(wiz.index - 1), { cls: 'btn ed-nav' });
  const next = iconBtn('Bước sau', 'arrow-right', () => wiz.go(wiz.index + 1), { cls: 'btn ed-nav', after: true });
  const root = el('div', { class: 'ed-savebar' },
    el('div', { class: 'ed-savebar__info' }, status, el('span', { class: 'ed-savebar__tip' }, 'Mẹo: nhấn Ctrl+S để lưu')),
    el('div', { class: 'ed-savebar__btns' }, prev, next, saveBtn));
  wiz.on((i) => { prev.disabled = i <= 0; next.disabled = i >= wiz.count - 1; });
  return {
    root,
    /** kind: clean | dirty | draft | saving | saved | fail. Chỉ đổi chữ khi khác trước (không đọc lại mỗi lần gõ). */
    set(kind) {
      const text = TEXT[kind](hhmm());
      if (status.textContent !== text) status.textContent = text;
    },
  };
}

/** Ctrl+S (Cmd+S): gọi cùng hàm lưu, chặn hộp thoại lưu trang; bỏ qua khi hộp thoại đang mở. */
export function onSave(fn) {
  document.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && String(e.key).toLowerCase() === 's') {
      e.preventDefault();
      if (!document.documentElement.classList.contains('has-modal')) fn();
    }
  });
}

// ---------------------------------------------------------------- Tổng hợp lỗi ở đầu vùng soạn (AC-401)
export function errorSummary() {
  const root = el('div', { class: 'ed-errors' });
  return {
    root,
    clear: () => clear(root),
    /** items: [{ label, message, go }]; extras: nút phụ (ví dụ "Xóa các khối trống"). */
    show(items, extras = []) {
      clear(root);
      if (!items.length) return;
      const head = el('p', { class: 'ed-errors__title', tabindex: '-1' }, `Có ${items.length} chỗ cần sửa`);
      const list = el('ul', {}, items.map((it) => el('li', {}, el('button', { type: 'button', class: 'ed-link', onclick: it.go }, `${it.label}: ${it.message}`))));
      root.append(el('div', { class: 'banner banner--error', role: 'alert' }, head, list, extras.length ? el('div', { class: 'row' }, extras) : null));
      head.focus();
    },
  };
}

// ---------------------------------------------------------------- Danh sách kiểm tra (không chặn)
/** items: [{ text, ok, hint?, pending?, go? }]. Trạng thái có biểu tượng và chữ. */
export function checklist(items) {
  return el('ul', { class: 'ed-check' }, items.map((it) => {
    const label = it.ok ? 'Đạt' : it.pending || 'Còn thiếu';
    return el('li', { class: 'ed-check__item ' + (it.ok ? 'is-ok' : 'is-miss') },
      icon(it.ok ? 'check' : 'alert'),
      el('span', { class: 'ed-check__text' }, it.text, it.hint ? el('span', { class: 'muted' }, ' (gợi ý)') : null),
      el('span', { class: 'ed-check__state' }, label),
      !it.ok && it.go ? el('button', { type: 'button', class: 'btn btn--small', 'aria-label': 'Đi tới: ' + it.text, onclick: it.go }, 'Đi tới') : null);
  }));
}

// ---------------------------------------------------------------- Tab Soạn / Xem trước (dưới 1100 px); từ 1100 px hai cột
export function splitLayout({ main, preview }) {
  const views = [['edit', 'Soạn', main], ['preview', 'Xem trước', preview]];
  const tabs = views.map(([key, text], i) => el('button', {
    type: 'button', role: 'tab', class: 'tab', id: 'ed-tab-' + key, 'aria-controls': 'ed-view-' + key,
    'aria-selected': i === 0 ? 'true' : 'false', tabindex: i === 0 ? '0' : '-1',
  }, text));
  views.forEach(([key, , node], i) => {
    node.id = 'ed-view-' + key;
    node.setAttribute('role', 'tabpanel');
    node.setAttribute('aria-labelledby', tabs[i].id);
  });
  const root = el('div', { class: 'ed-split', 'data-view': 'edit' }, main, preview);
  const tablist = el('div', { class: 'tabs ed-viewtabs', role: 'tablist', 'aria-label': 'Chế độ xem' }, tabs);
  const pos = { edit: 0, preview: 0 };
  function setView(key) {
    const cur = root.dataset.view;
    if (cur === key) return;
    pos[cur] = window.scrollY;
    root.dataset.view = key;
    tabs.forEach((t, i) => {
      const on = views[i][0] === key;
      t.setAttribute('aria-selected', String(on));
      t.tabIndex = on ? 0 : -1;
    });
    window.scrollTo(0, pos[key]);
  }
  tabs.forEach((t, i) => {
    t.addEventListener('click', () => setView(views[i][0]));
    t.addEventListener('keydown', (e) => {
      const j = e.key === 'ArrowRight' ? (i + 1) % 2 : e.key === 'ArrowLeft' ? (i + 1) % 2 : e.key === 'Home' ? 0 : e.key === 'End' ? 1 : -1;
      if (j < 0) return;
      e.preventDefault();
      setView(views[j][0]);
      tabs[j].focus();
    });
  });
  return { tablist, root };
}

// ---------------------------------------------------------------- Công tắc có chữ (role="switch")
/** text(checked) trả chữ hiển thị; iconFor(checked) trả tên biểu tượng. Không chỉ dựa vào màu. */
export function switchEl({ id, checked = false, text, iconFor = null, dataCol = null, onChange = () => {} }) {
  const input = el('input', { type: 'checkbox', role: 'switch', id, class: 'ed-switch__input', 'data-col': dataCol });
  input.checked = checked;
  const label = el('span', { class: 'ed-switch__text' });
  const paint = () => {
    label.replaceChildren(...(iconFor ? [icon(iconFor(input.checked))] : []), el('span', {}, text(input.checked)));
  };
  input.addEventListener('change', () => { paint(); onChange(input.checked); });
  paint();
  const root = el('label', { class: 'ed-switch', for: id }, input, el('span', { class: 'ed-switch__track', 'aria-hidden': 'true' }, el('span', { class: 'ed-switch__thumb' })), label);
  return { root, input, paint };
}

// ---------------------------------------------------------------- Điền mẫu có xác nhận và hoàn tác (AC-396)
export async function fillSample({ opener, hasText, capture, apply, restore }) {
  if (hasText()) {
    const ok = await confirmDialog({
      title: 'Thay nội dung hiện tại bằng bản mẫu?',
      message: 'Nội dung đang có sẽ được thay bằng văn bản mẫu. Bạn có thể hoàn tác ngay sau đó.',
      confirmText: 'Thay bằng bản mẫu',
      opener,
    });
    if (!ok) return;
  }
  const before = capture();
  apply();
  toast('ok', 'Đã điền mẫu', { action: { label: 'Hoàn tác điền mẫu', onClick: () => restore(before) } });
}

/** Một câu "Mục này để làm gì" và ví dụ thu gọn (bấm "Xem ví dụ" mới mở, để ô nhập không bị đẩy xuống). */
export function purposeBox(purpose, example) {
  return [
    el('p', { class: 'ed-purpose' }, purpose),
    example ? el('details', { class: 'ed-example' },
      el('summary', {}, icon('info'), el('span', {}, 'Xem ví dụ')),
      el('p', {}, example)) : null,
  ];
}

/** Đầu mỗi bước: tiêu đề h2 (kèm nút phụ như Điền mẫu ở bên phải), một câu "Mục này để làm gì" và ví dụ thu gọn. */
export function stepHead(id, title, purpose, example, actions = []) {
  return el('div', { class: 'ed-head' },
    el('div', { class: 'ed-head__row' }, el('h2', { id, class: 'ed-head__title' }, title), actions.length ? el('div', { class: 'ed-head__actions' }, actions) : null),
    purposeBox(purpose, example));
}

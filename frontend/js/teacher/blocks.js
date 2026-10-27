// Trình soạn các đoạn nội dung (blockList) và danh sách bản ghi nhiều ô (recordList) – QD-35, QD-71, AC-390..AC-395.
// Dữ liệu lưu lên máy chủ VẪN là mảng chuỗi cũ: loại đoạn được suy ra từ nội dung, ký hiệu kỹ thuật chỉ ẩn trên màn hình
// và được thêm lại khi lấy giá trị (parseBlock/composeBlock đối xứng, nạp rồi lưu không sửa thì chuỗi y nguyên).
// Nội dung giáo viên nhập là văn bản thuần, chỉ hiển thị bằng textContent.
import { el, clear } from '/js/dom.js';
import { icon } from '/js/icons.js';
import { toast } from '/js/toast.js';
import { iconBtn, menuButton, roving, switchEl, toggleInlineCode } from '/js/teacher/editor-kit.js';

const CODE_RE = /^```([^\n`]*)\n([\s\S]*)\n```$/;
const KIND = { p: 'Đoạn văn', li: 'Mục danh sách', code: 'Đoạn mã', raw: 'Văn bản thô', note: 'Mục lưu ý' };

/** Chuỗi -> { type, text, lang }. Dạng lạ (ví dụ ``` mở mà không đóng) thành 'raw' và giữ nguyên văn. */
export function parseBlock(value, forceList = false) {
  const s = String(value ?? '');
  if (forceList) return { type: 'note', text: s, lang: '' };
  const code = CODE_RE.exec(s);
  if (code) return { type: 'code', text: code[2], lang: code[1] };
  if (s.startsWith('```') || (/^-\s/.test(s) && !s.startsWith('- '))) return { type: 'raw', text: s, lang: '' };
  if (s.startsWith('- ')) return { type: 'li', text: s.slice(2), lang: '' };
  return { type: 'p', text: s, lang: '' };
}

export function composeBlock(b) {
  if (b.type === 'code') return '```' + b.lang + '\n' + b.text + '\n```';
  if (b.type === 'li') return '- ' + b.text;
  return b.text;
}

/**
 * @param {{id:string, label:string, hint?:string, initial?:string[], min?:number, max?:number, maxBlock?:number,
 *          onChange?:()=>void, forceList?:boolean, intro?:Node[], placeholder?:string}} options
 *   forceList: phần "Lưu ý" (mọi đoạn là một mục, hiển thị dạng danh sách); intro: nút/ví dụ đặt dưới tiêu đề.
 */
export function blockList({ id, label, hint = '', initial = [], min = 1, max = 50, maxBlock = 5000, onChange = () => {}, forceList = false, intro = [], placeholder = '' }) {
  const root = el('fieldset', { class: 'block-fieldset', id });
  const hintEl = hint ? el('p', { class: 'hint', id: id + '-hint' }, hint) : null;
  const listEl = el('ol', { class: 'block-list' });
  const listError = el('p', { class: 'field-error', id: id + '-error', role: 'alert', hidden: true });
  const fullNote = el('p', { class: 'hint', hidden: true }, `Đã đủ ${max} khối, hãy xóa bớt trước khi thêm khối mới.`);
  const newItem = (type) => ({ type: forceList ? 'note' : type, text: '', lang: type === 'code' ? 'python' : '', error: '' });
  let items = [];
  let focusAfter = null;
  let active = -1;

  const addMenu = forceList ? null : menuButton({
    text: 'Thêm khối mới', iconName: 'plus', label: `Thêm khối mới vào ${label}`, cls: 'btn',
    items: () => [['p', 'Đoạn văn', 'pencil'], ['li', 'Mục danh sách', 'list'], ['code', 'Đoạn mã', 'code']].map(([type, text, ic]) => ({ text, icon: ic, onSelect: () => addAt(type) })),
  });
  const addBtn = forceList ? iconBtn('Thêm mục lưu ý', 'plus', () => addAt('note'), { cls: 'btn' }) : addMenu.trigger;
  root.append(...[el('legend', {}, el('h3', { class: 'ed-sub__title' }, label)), ...intro, hintEl, listEl, listError, fullNote, el('div', { class: 'ed-addrow' }, forceList ? addBtn : addMenu.root)].filter(Boolean));

  const fromValues = (arr) => (arr.length ? arr : Array.from({ length: min }, () => '')).map((v) => ({ ...parseBlock(v, forceList), error: '' }));
  items = fromValues(initial);

  const text = (it) => it.text.trim();
  const syncValues = () => {
    listEl.querySelectorAll('textarea').forEach((ta, i) => { if (items[i]) items[i].text = ta.value; });
  };

  function addAt(type) {
    syncValues();
    const at = Math.min(items.length, active + 1);
    items.splice(at, 0, newItem(type));
    focusAfter = at;
    paint();
    onChange();
  }

  function removeItem(i) {
    syncValues();
    const [removed] = items.splice(i, 1);
    focusAfter = Math.min(i, items.length - 1);
    paint();
    onChange();
    toast('info', `Đã xóa khối ${i + 1}`, {
      action: {
        label: 'Hoàn tác',
        onClick: () => {
          syncValues();
          const at = Math.min(i, items.length);
          items.splice(at, 0, removed);
          focusAfter = at;
          paint();
          onChange();
        },
      },
    });
  }

  function moveItem(i, delta) {
    syncValues();
    const j = i + delta;
    if (j < 0 || j >= items.length) return;
    [items[i], items[j]] = [items[j], items[i]];
    focusAfter = j;
    paint();
    onChange();
  }

  function retype(i, type) {
    syncValues();
    items[i].type = type;
    if (type === 'code' && !items[i].lang) items[i].lang = 'python';
    focusAfter = i;
    paint();
    onChange();
  }

  function paint() {
    clear(listEl);
    items.forEach((item, i) => {
      const taId = `${id}-${i}`;
      const errId = `${taId}-error`;
      const where = `khối ${i + 1} của ${label}`;
      const isCode = item.type === 'code';
      const ta = el('textarea', {
        id: taId,
        class: 'textarea' + (isCode ? ' textarea--code' : ''),
        rows: isCode ? '5' : '3',
        maxlength: maxBlock - (item.type === 'li' ? 2 : isCode ? 20 : 0),
        spellcheck: isCode ? 'false' : null,
        placeholder: isCode ? 'print("Xin chào")' : placeholder || null,
        'aria-describedby': [hintEl ? id + '-hint' : null, errId].filter(Boolean).join(' '),
        'aria-invalid': item.error ? 'true' : null,
      });
      ta.value = item.text;
      const counter = el('div', { class: 'char-count', 'aria-hidden': 'true' }, `${item.text.length}/${maxBlock}`);
      ta.addEventListener('focus', () => { active = i; });
      ta.addEventListener('input', () => {
        item.text = ta.value;
        counter.textContent = `${ta.value.length}/${maxBlock}`;
        onChange();
      });
      ta.addEventListener('keydown', (e) => {
        if (!(item.type === 'li' || item.type === 'note')) return;
        if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
          e.preventDefault();
          active = i;
          addAt(item.type);
        } else if (e.key === 'Backspace' && ta.value === '' && items.length > 1) {
          e.preventDefault();
          items.splice(i, 1);
          focusAfter = Math.max(0, i - 1);
          paint();
          onChange();
        }
      });

      const fmt = (text, ic, onClick, { pressed = null, disabled = false, name = text } = {}) => {
        const b = iconBtn(text, ic, onClick, { cls: 'btn btn--small', label: `${name}, ${where}`, disabled });
        if (pressed !== null) b.setAttribute('aria-pressed', String(pressed));
        return b;
      };
      const plain = item.type === 'p' || item.type === 'li';
      const tools = [];
      if (!forceList) {
        tools.push(fmt('Gạch đầu dòng', 'list', () => retype(i, item.type === 'li' ? 'p' : 'li'), { pressed: item.type === 'li', disabled: !plain }));
      }
      tools.push(fmt('Mã trong dòng', 'braces', () => toggleInlineCode(ta), { disabled: isCode }));
      if (!forceList) {
        tools.push(fmt('Khối mã', 'code', () => retype(i, isCode ? 'p' : 'code'), { pressed: isCode, disabled: !plain && !isCode }));
      }
      const toolbar = el('div', { class: 'ed-toolbar', role: 'toolbar', 'aria-label': `Định dạng khối ${i + 1}` }, tools);
      roving(toolbar);

      const move = (text, ic, delta, edge, edgeText) => {
        const b = iconBtn(text, ic, () => moveItem(i, delta), { cls: 'btn btn--small', label: edge ? `${text} (${edgeText})` : `${text}, ${where}`, disabled: edge });
        return b;
      };
      const grouped = item.type === 'li' && items[i - 1] && items[i - 1].type === 'li';
      listEl.append(el('li', { class: `block-item ed-blk ed-blk--${item.type}${grouped ? ' is-grouped' : ''}`, 'data-item': '' },
        el('div', { class: 'block-item__head' },
          el('label', { class: 'block-item__title', for: taId }, `Đoạn ${i + 1}`),
          el('span', { class: 'chip chip--info' }, KIND[item.type], isCode && item.lang ? el('span', { class: 'mono' }, ' ' + item.lang) : null)),
        toolbar,
        ta, counter,
        el('p', { class: 'field-error', id: errId, role: 'alert', hidden: !item.error }, item.error),
        el('div', { class: 'row ed-blk__moves' },
          move('Lên', 'arrow-up', -1, i === 0, 'đây là khối đầu tiên'),
          move('Xuống', 'arrow-down', 1, i === items.length - 1, 'đây là khối cuối cùng'),
          iconBtn('Xóa khối', 'trash', () => removeItem(i), { cls: 'btn btn--small', label: items.length <= min ? 'Xóa khối (phần này cần ít nhất một khối)' : `Xóa khối ${i + 1} của ${label}`, disabled: items.length <= min }))));
    });
    const full = items.length >= max;
    addBtn.disabled = full;
    fullNote.hidden = !full;
    if (focusAfter !== null) {
      const target = listEl.querySelectorAll('textarea')[focusAfter];
      focusAfter = null;
      if (target) target.focus();
    }
  }

  paint();

  return {
    root,
    get values() {
      syncValues();
      return items.map(composeBlock);
    },
    set values(next) {
      items = fromValues(next);
      active = -1;
      paint();
    },
    /** Giá trị của các đoạn có chữ, dùng cho xem trước và kiểm tra (đoạn trống bị bỏ qua). */
    get filled() {
      syncValues();
      return items.filter((it) => text(it)).map(composeBlock);
    },
    get blanks() {
      syncValues();
      return items.map((it, i) => (text(it) ? -1 : i)).filter((i) => i >= 0);
    },
    removeBlanks() {
      syncValues();
      const keep = items.filter((it) => text(it));
      items = keep.length ? keep : [newItem('p')];
      paint();
      onChange();
    },
    focusItem(i) {
      const ta = listEl.querySelectorAll('textarea')[i];
      if (ta) ta.focus();
    },
    /** index = null: lỗi chung của cả danh sách. */
    setError(index, message) {
      syncValues();
      if (index === null || index === undefined || !items[index]) {
        listError.hidden = !message;
        listError.textContent = message || '';
        return;
      }
      items[index].error = message || '';
      paint();
    },
    clearErrors() {
      syncValues();
      items.forEach((it) => { it.error = ''; });
      listError.hidden = true;
      listError.textContent = '';
      paint();
    },
    focusFirstError() {
      const bad = listEl.querySelector('[aria-invalid="true"]');
      if (bad) bad.focus();
      else if (!listError.hidden) addBtn.focus();
    },
  };
}

/**
 * Danh sách bản ghi nhiều ô: dạng thẻ (ví dụ) hoặc dạng bảng (bộ kiểm tra, table: true).
 * columns: [{ key, label, maxlength, rows, wide, mono(mặc định true), type: 'switch', text(checked), iconFor(checked) }].
 * Id ô con giữ dạng `${id}-${i}-${key}` và có data-col (AC-409).
 */
export function recordList({ id, label, hint = '', itemLabel, initial = [], columns, makeEmpty, min = 1, max = 50, onChange = () => {}, table = false, caption = '', extraActions = null, duplicate = false }) {
  const noun = itemLabel.toLowerCase();
  const root = el('fieldset', { class: 'block-fieldset', id });
  const hintEl = hint ? el('p', { class: 'hint', id: id + '-hint' }, hint) : null;
  const listEl = el(table ? 'tbody' : 'ol', { class: table ? '' : 'block-list' });
  const listError = el('p', { class: 'field-error', id: id + '-error', role: 'alert', hidden: true });
  const fullNote = el('p', { class: 'hint', hidden: true }, `Đã đủ ${max} ${noun}, hãy xóa bớt trước khi thêm.`);
  const addBtn = iconBtn(`Thêm ${noun}`, 'plus', () => { sync(); push({}); }, { cls: 'btn' });
  const head = table ? el('thead', {}, el('tr', {},
    el('th', { scope: 'col' }, '#'),
    columns.map((c) => el('th', { scope: 'col' }, c.label)),
    el('th', { scope: 'col' }, 'Hành động'))) : null;
  const body = table
    ? el('div', { class: 'ed-tablewrap' }, el('table', { class: 'data ed-tests' }, el('caption', { class: 'sr-only' }, caption || label), head, listEl))
    : listEl;
  root.append(...[el('legend', { class: 'sr-only' }, label), hintEl, body, listError, fullNote, el('div', { class: 'ed-addrow' }, addBtn)].filter(Boolean));

  let items = [];
  let focusAfter = null;
  const fresh = (data) => ({ data: { ...makeEmpty(), ...data }, errors: {} });
  const fill = (arr) => (arr.length ? arr : Array.from({ length: min }, () => ({}))).map(fresh);
  items = fill(initial);

  function sync() {
    listEl.querySelectorAll('[data-item]').forEach((row, i) => {
      if (!items[i]) return;
      columns.forEach((c) => {
        const input = row.querySelector(`[data-col="${c.key}"]`);
        if (input) items[i].data[c.key] = c.type === 'switch' ? input.checked : input.value;
      });
    });
  }

  function push(data, at = items.length) {
    if (items.length >= max) return false;
    items.splice(at, 0, fresh(data));
    focusAfter = at;
    paint();
    onChange();
    return true;
  }

  function cellFor(item, i, c) {
    const cid = `${id}-${i}-${c.key}`;
    const eid = cid + '-error';
    const message = item.errors[c.key] || '';
    if (c.type === 'switch') {
      const sw = switchEl({
        id: cid, checked: !!item.data[c.key], text: c.text, iconFor: c.iconFor, dataCol: c.key,
        onChange: (v) => { item.data[c.key] = v; onChange(); },
      });
      return el('div', { class: 'ed-cell ed-cell--switch' }, sw.root);
    }
    const input = el('textarea', {
      id: cid,
      class: 'textarea' + (c.mono === false ? '' : ' textarea--code'),
      rows: String(c.rows || 3),
      maxlength: c.maxlength,
      spellcheck: 'false',
      'data-col': c.key,
      'aria-describedby': eid,
      'aria-invalid': message ? 'true' : null,
    });
    input.value = item.data[c.key] ?? '';
    input.addEventListener('input', () => { item.data[c.key] = input.value; onChange(); });
    return el('div', { class: 'ed-cell' + (c.wide ? ' grow' : '') },
      el('label', { for: cid, class: 'ed-cell__label' }, c.label),
      input,
      el('p', { class: 'field-error', id: eid, role: 'alert', hidden: !message }, message));
  }

  function paint() {
    clear(listEl);
    items.forEach((item, i) => {
      const act = (text, ic, onClick, disabled = false) => iconBtn(text, ic, onClick, { cls: 'btn btn--small', label: `${text} ${noun} ${i + 1}`, disabled });
      const actions = [
        act('Lên', 'arrow-up', () => move(i, -1), i === 0),
        act('Xuống', 'arrow-down', () => move(i, 1), i === items.length - 1),
        duplicate ? act('Nhân đôi', 'copy', () => { sync(); push({ ...item.data }, i + 1); }, items.length >= max) : null,
        act('Xóa', 'trash', () => removeAt(i), items.length <= min),
      ];
      const cells = columns.map((c) => cellFor(item, i, c));
      if (table) {
        listEl.append(el('tr', { 'data-item': '', class: 'ed-row' },
          el('th', { scope: 'row', class: 'ed-row__n' }, el('span', { class: 'ed-row__title' }, `${itemLabel} ${i + 1}`), el('span', { class: 'ed-row__num', 'aria-hidden': 'true' }, String(i + 1))),
          cells.map((c) => el('td', {}, c)),
          el('td', {}, el('div', { class: 'ed-actions' }, actions))));
      } else {
        listEl.append(el('li', { class: 'block-item', 'data-item': '' },
          el('div', { class: 'block-item__head' },
            el('span', { class: 'block-item__title' }, `${itemLabel} ${i + 1}`),
            el('div', { class: 'row' }, actions)),
          el('div', { class: 'testcase-grid' }, cells),
          extraActions ? el('div', { class: 'row' }, extraActions(item.data, i)) : null));
      }
    });
    const full = items.length >= max;
    addBtn.disabled = full;
    fullNote.hidden = !full;
    if (focusAfter !== null) {
      const row = listEl.querySelectorAll('[data-item]')[focusAfter];
      focusAfter = null;
      const first = row && row.querySelector('textarea, input');
      if (first) first.focus();
    }
  }

  function move(i, delta) {
    sync();
    const j = i + delta;
    if (j < 0 || j >= items.length) return;
    [items[i], items[j]] = [items[j], items[i]];
    focusAfter = j;
    paint();
    onChange();
  }

  function removeAt(i) {
    sync();
    const [removed] = items.splice(i, 1);
    focusAfter = Math.min(i, items.length - 1);
    paint();
    onChange();
    toast('info', `Đã xóa ${noun} ${i + 1}`, {
      action: {
        label: 'Hoàn tác',
        onClick: () => { sync(); const at = Math.min(i, items.length); items.splice(at, 0, removed); focusAfter = at; paint(); onChange(); },
      },
    });
  }

  paint();

  return {
    root,
    get values() {
      sync();
      return items.map((it) => ({ ...it.data }));
    },
    set values(next) {
      items = fill(next);
      paint();
    },
    /** Thêm một bản ghi ở cuối; trả về false khi đã đủ số lượng tối đa. */
    add: (data) => { sync(); return push(data); },
    setError(index, key, message) {
      sync();
      if (index === null || index === undefined || !items[index]) {
        listError.hidden = !message;
        listError.textContent = message || '';
        return;
      }
      items[index].errors[key || columns[0].key] = message || '';
      paint();
    },
    clearErrors() {
      sync();
      items.forEach((it) => { it.errors = {}; });
      listError.hidden = true;
      listError.textContent = '';
      paint();
    },
    focusFirstError() {
      const bad = listEl.querySelector('[aria-invalid="true"]');
      if (bad) bad.focus();
      else if (!listError.hidden) addBtn.focus();
    },
    focusFirst() {
      const first = listEl.querySelector('textarea, input');
      (first || addBtn).focus();
    },
  };
}

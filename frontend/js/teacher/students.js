import { api, ApiError } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { el, clear, stateBox, skeleton } from '/js/core/dom.js';
import { confirmDialog, showDialog } from '/js/core/modal.js';
import { banner, chip, copyText, dataTable, debounce, downloadCsv, field, formatDateTime } from '/js/core/ui.js';
import { errorText, finishLoading, pageContent, say } from '/js/teacher/common.js';

await initPage({ role: 'Teacher', active: 'students' });

const content = pageContent();
const PAGE_SIZE = 50;
let classes = [];
let page = 1;

const searchF = field({ id: 'search', label: 'Tìm theo tên hoặc mã học sinh', maxlength: 100 });
const classF = field({ id: 'class-filter', label: 'Lọc theo lớp', type: 'select', options: [{ value: '', label: 'Tất cả lớp' }] });
const classHost = el('div');
const tableHost = el('div', { 'aria-live': 'polite' });
const pager = el('div', { class: 'pager-row' });

// ---------------------------------------------------------------- Hộp thoại mật khẩu tạm (chỉ hiện một lần)
async function showTemporaryPassword(title, rows, opener) {
  const copy = el('button', { type: 'button', class: 'btn btn--small' }, 'Sao chép');
  const live = el('span', { class: 'small', role: 'status' });
  const text = rows.map((r) => (r.name ? `${r.name}: ` : '') + r.password).join('\n');
  copy.addEventListener('click', async () => { live.textContent = (await copyText(text)) ? 'Đã sao chép.' : 'Không sao chép được, hãy bôi đen và chép tay.'; });
  await showDialog({
    title,
    opener,
    content: el('div', {},
      banner('warn', 'Mật khẩu tạm chỉ hiển thị MỘT LẦN. Hãy chép lại và gửi cho học sinh; học sinh sẽ phải đổi mật khẩu ở lần đăng nhập đầu.'),
      ...rows.map((r) => el('p', {}, r.name ? el('strong', {}, r.name + ': ') : null, el('span', { class: 'temp-password' }, r.password))),
      el('div', { class: 'row' }, copy, live)),
    buttons: [{ text: 'Đã chép xong', value: true, kind: 'primary' }],
    closeValue: true,
  });
}

// ---------------------------------------------------------------- Lớp
function renderClasses() {
  clear(classHost);
  const rows = classes.map((c) => {
    const mk = (text, handler, kind = '') => {
      const b = el('button', { type: 'button', class: 'btn btn--small ' + kind }, text);
      b.addEventListener('click', () => handler(c, b));
      return b;
    };
    return el('tr', {}, el('td', {}, c.name), el('td', { class: 'num' }, String(c.memberCount)),
      el('td', {}, el('div', { class: 'cell-actions' }, mk('Đổi tên', renameClass), mk('Xóa lớp', deleteClass, 'btn--danger'))));
  });
  classHost.append(
    el('div', { class: 'row row--between' }, el('h2', { class: 'mt-0' }, 'Lớp'), el('button', { type: 'button', class: 'btn btn--accent', id: 'btn-new-class', onclick: (e) => createClass(e.currentTarget) }, '+ Tạo lớp')),
    rows.length ? dataTable({ caption: 'Danh sách lớp', headers: ['Tên lớp', { label: 'Số học sinh', num: true }, 'Thao tác'], rows }) : el('p', { class: 'muted' }, 'Chưa có lớp nào.'));
  // cập nhật bộ lọc lớp
  const current = classF.value;
  clear(classF.input);
  classF.input.append(el('option', { value: '' }, 'Tất cả lớp'), ...classes.map((c) => el('option', { value: String(c.id) }, c.name)));
  classF.input.value = classes.some((c) => String(c.id) === current) ? current : '';
}

async function nameDialog(title, initial, opener) {
  const f = field({ id: 'class-name', label: 'Tên lớp (1–100 ký tự)', maxlength: 100, value: initial, required: true });
  const result = await showDialog({
    title,
    opener,
    content: f.root,
    focusFirst: () => { f.input.focus(); return f.input; },
    closeValue: null,
    buttons: [
      { text: 'Hủy', value: null },
      {
        text: 'Lưu',
        value: 'ok',
        kind: 'primary',
        action: () => {
          if (!f.value.trim()) { f.setError('Tên lớp không được để trống.'); f.input.focus(); return false; }
          return true;
        },
      },
    ],
  });
  return result === 'ok' ? f.value.trim() : null;
}

async function createClass(opener) {
  const name = await nameDialog('Tạo lớp', '', opener);
  if (!name) return;
  try {
    await api.createClass(name);
    say('ok', `Đã tạo lớp ${name}.`);
    await loadClasses();
  } catch (err) {
    say('error', errorText(err));
  }
}

async function renameClass(c, opener) {
  const name = await nameDialog('Đổi tên lớp', c.name, opener);
  if (!name) return;
  try {
    await api.renameClass(c.id, name);
    say('ok', 'Đã đổi tên lớp.');
    await loadClasses();
    await loadStudents();
  } catch (err) {
    say('error', errorText(err));
  }
}

async function deleteClass(c, opener) {
  const ok = await confirmDialog({
    title: 'Xóa lớp?',
    message: `Xóa lớp ${c.name}? Các học sinh trong lớp (${c.memberCount}) chỉ bị gỡ khỏi lớp, không bị xóa.`,
    confirmText: 'Xóa lớp',
    opener,
    danger: true,
  });
  if (!ok) return;
  try {
    await api.deleteClass(c.id);
    say('ok', `Đã xóa lớp ${c.name}.`);
    await loadClasses();
    await loadStudents();
  } catch (err) {
    say('error', errorText(err));
  }
}

async function loadClasses() {
  classes = (await api.classes()).classes || [];
  renderClasses();
}

// ---------------------------------------------------------------- Học sinh
function classCheckboxes(selected, prefix) {
  const boxes = classes.map((c) => ({ c, box: el('input', { type: 'checkbox', id: `${prefix}-${c.id}`, checked: selected.includes(c.id) }) }));
  const node = boxes.length
    ? el('fieldset', {}, el('legend', {}, 'Lớp'), el('div', { class: 'row' }, boxes.map(({ c, box }) => el('label', { class: 'check', for: box.id }, box, c.name))))
    : el('p', { class: 'muted' }, 'Chưa có lớp nào (có thể tạo lớp ở phần "Lớp").');
  return { node, ids: () => boxes.filter((b) => b.box.checked).map((b) => b.c.id) };
}

async function createStudent(opener) {
  const username = field({ id: 'new-username', label: 'Tên đăng nhập (3–50 ký tự: chữ thường không dấu, số, . _ -)', maxlength: 50, required: true, autocomplete: 'off' });
  const name = field({ id: 'new-name', label: 'Họ tên (1–100 ký tự)', maxlength: 100, required: true });
  const password = field({ id: 'new-password', label: 'Mật khẩu (tùy chọn)', type: 'password', maxlength: 128, autocomplete: 'new-password', hint: 'Để trống để hệ thống sinh mật khẩu tạm ngẫu nhiên.' });
  const cls = classCheckboxes([], 'new-cls');
  const err = el('div', { role: 'alert' });
  let created = null;
  const choice = await showDialog({
    title: 'Tạo tài khoản học sinh',
    opener,
    wide: true,
    closeValue: false,
    focusFirst: () => { username.input.focus(); return username.input; },
    content: el('div', {}, err, username.root, name.root, password.root, cls.node),
    buttons: [
      { text: 'Hủy', value: false },
      {
        text: 'Tạo tài khoản',
        value: true,
        kind: 'primary',
        action: async () => {
          clear(err);
          [username, name, password].forEach((f) => f.clearError());
          if (!username.value.trim()) { username.setError('Vui lòng nhập tên đăng nhập.'); username.input.focus(); return false; }
          if (!name.value.trim()) { name.setError('Vui lòng nhập họ tên.'); name.input.focus(); return false; }
          try {
            created = await api.createStudent({ username: username.value, displayName: name.value, classIds: cls.ids(), password: password.value || null });
          } catch (e) {
            if (e instanceof ApiError && e.code === 'DUPLICATE_USERNAME') username.setError('Tên đăng nhập đã tồn tại.');
            else if (e instanceof ApiError && e.status === 422) {
              const map = { username, displayName: name, password };
              (e.errors.length ? e.errors : [{ field: e.field, message: e.message }]).forEach((x) => (map[x.field] ? map[x.field].setError(x.message) : err.append(banner('error', x.message))));
            } else err.append(banner('error', errorText(e)));
            return false;
          }
          return true;
        },
      },
    ],
  });
  if (!choice || !created) return;
  say('ok', `Đã tạo tài khoản ${created.username}.`);
  await loadClasses();
  await loadStudents();
  if (created.temporaryPassword) {
    await showTemporaryPassword(`Mật khẩu tạm của ${created.username}`, [{ name: created.username, password: created.temporaryPassword }], opener);
  }
}

function parseLines(text) {
  return text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean).map((line) => {
    const at = line.search(/[,;\t]/);
    if (at < 0) return { username: line, displayName: '' };
    return { username: line.slice(0, at).trim(), displayName: line.slice(at + 1).trim().replace(/^"(.*)"$/, '$1') };
  });
}

async function importStudents(opener) {
  const area = field({ id: 'import-text', label: 'Danh sách (mỗi dòng: tên đăng nhập, họ tên)', type: 'textarea', rows: 8, hint: 'Tối đa 200 dòng mỗi lần. Ví dụ: 12a1.nguyenvana, Nguyễn Văn A', code: true });
  const file = field({ id: 'import-file', label: 'Hoặc chọn tệp CSV (UTF-8)', type: 'file' });
  file.input.accept = '.csv,text/csv,text/plain';
  file.input.addEventListener('change', async () => {
    const f = file.input.files && file.input.files[0];
    if (f) area.value = (await f.text()).replace(/^﻿/, '');
  });
  const cf = field({ id: 'import-class', label: 'Thêm vào lớp (tùy chọn)', type: 'select', options: [{ value: '', label: 'Không gán lớp' }, ...classes.map((c) => ({ value: c.id, label: c.name }))] });
  const err = el('div', { role: 'alert' });
  let outcome = null;
  const choice = await showDialog({
    title: 'Nhập danh sách học sinh',
    opener,
    wide: true,
    closeValue: false,
    focusFirst: () => { area.input.focus(); return area.input; },
    content: el('div', {}, err, area.root, file.root, cf.root),
    buttons: [
      { text: 'Hủy', value: false },
      {
        text: 'Nhập danh sách',
        value: true,
        kind: 'primary',
        action: async () => {
          clear(err);
          const rows = parseLines(area.value);
          if (!rows.length) { err.append(banner('error', 'Danh sách đang trống.')); return false; }
          if (rows.length > 200) { err.append(banner('error', 'Mỗi lần nhập tối đa 200 dòng.')); return false; }
          try {
            outcome = await api.importStudents({ classId: cf.value ? Number(cf.value) : null, rows });
          } catch (e) {
            err.append(banner('error', errorText(e)));
            return false;
          }
          return true;
        },
      },
    ],
  });
  if (!choice || !outcome) return;
  await loadClasses();
  await loadStudents();
  await showImportResult(outcome.results, opener);
}

const STATUS_TEXT = { created: 'Đã tạo', skipped: 'Bỏ qua', invalid: 'Không hợp lệ' };

async function showImportResult(results, opener) {
  const created = results.filter((r) => r.status === 'created');
  const rows = results.map((r) => el('tr', {},
    el('td', { class: 'num' }, String(r.row)),
    el('td', {}, r.username),
    el('td', {}, STATUS_TEXT[r.status] || r.status, r.reason ? el('div', { class: 'small muted' }, r.reason) : null),
    el('td', {}, r.temporaryPassword ? el('span', { class: 'temp-password', style: 'font-size:1rem' }, r.temporaryPassword) : '')));
  const csv = el('button', { type: 'button', class: 'btn btn--small' }, 'Tải CSV');
  csv.addEventListener('click', () => downloadCsv('mat-khau-tam.csv', [['Dòng', 'Tên đăng nhập', 'Kết quả', 'Mật khẩu tạm'], ...results.map((r) => [r.row, r.username, STATUS_TEXT[r.status] || r.status, r.temporaryPassword || ''])]));
  const print = el('button', { type: 'button', class: 'btn btn--small' }, 'In danh sách');
  print.addEventListener('click', () => window.print());
  await showDialog({
    title: `Kết quả nhập: ${created.length} tạo mới, ${results.length - created.length} không tạo`,
    opener,
    wide: true,
    closeValue: true,
    content: el('div', { class: 'print-area' },
      banner('warn', 'Mật khẩu tạm chỉ hiển thị MỘT LẦN. Hãy tải CSV hoặc in danh sách ngay bây giờ.'),
      el('div', { class: 'row', style: 'margin-bottom:10px' }, csv, print),
      dataTable({ caption: 'Kết quả nhập danh sách', headers: [{ label: 'Dòng', num: true }, 'Tên đăng nhập', 'Kết quả', 'Mật khẩu tạm'], rows })),
    buttons: [{ text: 'Đóng', value: true, kind: 'primary' }],
  });
}

function statusChips(s) {
  const chips = [];
  if (!s.isActive) chips.push(chip('chip--late', 'ban', 'Bị khóa'));
  else chips.push(chip('chip--passed', 'check', 'Đang hoạt động'));
  if (s.isLocked) chips.push(chip('chip--inprogress', 'lock', 'Khóa tạm'));
  if (s.mustChangePassword) chips.push(chip('chip--info', 'key', 'Chưa đổi mật khẩu'));
  return el('div', { class: 'row' }, chips);
}

async function resetPassword(s, opener) {
  const ok = await confirmDialog({
    title: 'Đặt lại mật khẩu?',
    message: `Đặt lại mật khẩu của ${s.displayName} (${s.username}). Mật khẩu cũ sẽ không dùng được nữa và mọi phiên đang đăng nhập của em sẽ bị đăng xuất.`,
    confirmText: 'Đặt lại mật khẩu',
    opener,
    danger: true,
  });
  if (!ok) return;
  try {
    const r = await api.resetPassword(s.id);
    await loadStudents();
    await showTemporaryPassword(`Mật khẩu tạm của ${s.username}`, [{ name: s.username, password: r.temporaryPassword }], opener);
  } catch (err) {
    say('error', errorText(err));
  }
}

async function toggleActive(s, opener) {
  const disabling = s.isActive;
  const ok = await confirmDialog({
    title: disabling ? 'Khóa tài khoản?' : 'Mở khóa tài khoản?',
    message: disabling
      ? `Khóa ${s.displayName} (${s.username}): em sẽ không đăng nhập được và mọi phiên đang mở bị đăng xuất. Toàn bộ bài nộp được giữ.`
      : `Mở khóa ${s.displayName} (${s.username}) để em đăng nhập lại.`,
    confirmText: disabling ? 'Khóa tài khoản' : 'Mở khóa',
    opener,
    danger: disabling,
  });
  if (!ok) return;
  try {
    await api.updateStudent(s.id, { isActive: !s.isActive });
    say('ok', disabling ? 'Đã khóa tài khoản.' : 'Đã mở khóa tài khoản.');
    await loadStudents();
  } catch (err) {
    say('error', errorText(err));
  }
}

async function unlock(s, opener) {
  const ok = await confirmDialog({ title: 'Mở khóa tạm?', message: `Xóa khóa tạm do nhập sai mật khẩu của ${s.username}.`, confirmText: 'Mở khóa tạm', opener });
  if (!ok) return;
  try {
    await api.unlockStudent(s.id);
    say('ok', 'Đã mở khóa tạm.');
    await loadStudents();
  } catch (err) {
    say('error', errorText(err));
  }
}

async function changeClasses(s, opener) {
  const cls = classCheckboxes(s.classIds, 'chg-cls');
  const ok = await showDialog({
    title: `Đổi lớp cho ${s.displayName}`,
    opener,
    closeValue: false,
    content: el('div', {}, el('p', { class: 'muted' }, 'Thay đổi lớp có hiệu lực ngay: bài được giao theo lớp sẽ hiện/ẩn ở lần tải trang kế tiếp của học sinh.'), cls.node),
    buttons: [{ text: 'Hủy', value: false }, { text: 'Lưu', value: true, kind: 'primary' }],
  });
  if (!ok) return;
  try {
    await api.updateStudent(s.id, { classIds: cls.ids() });
    say('ok', 'Đã cập nhật lớp.');
    await loadClasses();
    await loadStudents();
  } catch (err) {
    say('error', errorText(err));
  }
}

async function loadStudents() {
  tableHost.setAttribute('aria-busy', 'true');
  if (!tableHost.firstChild) tableHost.append(skeleton({ shape: 'table', count: 8, label: 'Đang tải danh sách học sinh…' }));
  try {
    const data = await api.students({ search: searchF.value.trim(), classId: classF.value, page, pageSize: PAGE_SIZE });
    clear(tableHost);
    clear(pager);
    if (!data.students.length) {
      tableHost.append(stateBox({ kind: 'empty', title: 'Không có học sinh phù hợp', message: 'Hãy tạo tài khoản hoặc nhập danh sách học sinh bằng các nút phía trên, hoặc đổi bộ lọc.' }));
      return;
    }
    const rows = data.students.map((s) => {
      const mk = (text, handler, kind = '') => {
        const b = el('button', { type: 'button', class: 'btn btn--small ' + kind, 'aria-label': `${text} – ${s.username}` }, text);
        b.addEventListener('click', () => handler(s, b));
        return b;
      };
      return el('tr', {},
        el('th', { scope: 'row' }, s.username),
        el('td', {}, s.displayName),
        el('td', {}, s.classNames.join(', ') || 'Chưa xếp lớp'),
        el('td', {}, statusChips(s)),
        el('td', { class: 'nowrap' }, s.lastLoginAt ? formatDateTime(s.lastLoginAt) : 'Chưa đăng nhập'),
        el('td', {}, el('div', { class: 'cell-actions' },
          mk('Đặt lại mật khẩu', resetPassword),
          mk(s.isActive ? 'Khóa' : 'Mở khóa', toggleActive, s.isActive ? 'btn--danger' : ''),
          s.isLocked ? mk('Mở khóa tạm', unlock) : null,
          mk('Đổi lớp', changeClasses))));
    });
    tableHost.append(dataTable({
      caption: `Danh sách học sinh (${data.total})`,
      headers: ['Tên đăng nhập', 'Họ tên', 'Lớp', 'Trạng thái', 'Đăng nhập gần nhất', 'Thao tác'],
      rows,
    }));
    const pages = Math.max(1, Math.ceil(data.total / PAGE_SIZE));
    const prev = el('button', { type: 'button', class: 'btn btn--small' }, '← Trang trước');
    const next = el('button', { type: 'button', class: 'btn btn--small' }, 'Trang sau →');
    prev.disabled = page <= 1;
    next.disabled = page >= pages;
    prev.addEventListener('click', () => { page -= 1; loadStudents(); });
    next.addEventListener('click', () => { page += 1; loadStudents(); });
    pager.append(el('span', {}, `Trang ${page}/${pages}`), el('div', { class: 'row' }, prev, next));
  } catch (err) {
    clear(tableHost);
    tableHost.append(stateBox({ kind: 'error', title: errorText(err), actions: [el('button', { type: 'button', class: 'btn btn--primary', onclick: loadStudents }, 'Thử lại')] }));
  } finally {
    tableHost.setAttribute('aria-busy', 'false');
  }
}

async function build() {
  const addBtn = el('button', { type: 'button', class: 'btn btn--primary' }, '+ Tạo học sinh');
  addBtn.addEventListener('click', () => createStudent(addBtn));
  const importBtn = el('button', { type: 'button', class: 'btn' }, 'Nhập danh sách');
  importBtn.addEventListener('click', () => importStudents(importBtn));

  content.append(
    el('section', { class: 'card', 'aria-labelledby': 'classes-title' }, classHost),
    el('section', { class: 'stack', style: 'margin-top:24px' },
      el('div', { class: 'row row--between' }, el('h2', { id: 'students-title', class: 'mt-0' }, 'Học sinh'), el('div', { class: 'row' }, addBtn, importBtn)),
      el('div', { class: 'filters' }, searchF.root, classF.root),
      tableHost,
      pager));
  try {
    await loadClasses();
  } catch (err) {
    classHost.append(stateBox({ kind: 'error', title: errorText(err) }));
  }
  searchF.input.addEventListener('input', debounce(() => { page = 1; loadStudents(); }, 300));
  classF.input.addEventListener('change', () => { page = 1; loadStudents(); });
  await loadStudents();
  finishLoading();
}

build();

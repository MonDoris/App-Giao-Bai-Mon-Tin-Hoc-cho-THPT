import { api } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { el, clear, stateBox, skeleton } from '/js/core/dom.js';
import { showDialog } from '/js/core/modal.js';
import { banner, chip, dataTable, debounce, downloadCsv, field, formatDateTime, icon, lateChip, statusChip } from '/js/core/ui.js';
import { errorText, finishLoading, pageContent, say } from '/js/teacher/common.js';

await initPage({ role: 'Teacher', active: 'progress' });

const content = pageContent();
const PAGE_SIZE = 50;
let classes = [];
let lessons = [];
let data = null;
let page = 1;
let sortKey = 'name';

const classF = field({ id: 'class', label: 'Lớp', type: 'select', options: [{ value: '', label: 'Tất cả học sinh' }] });
const lessonF = field({ id: 'lesson', label: 'Bài học', type: 'select', options: [] });
const searchF = field({ id: 'search', label: 'Tìm theo tên hoặc mã học sinh', maxlength: 100 });
const sortF = field({ id: 'sort', label: 'Sắp xếp theo', type: 'select', value: 'name', options: [{ value: 'name', label: 'Tên học sinh' }, { value: 'passed', label: 'Số bài đã đạt (giảm dần)' }] });
const exportBtn = el('button', { type: 'button', class: 'btn' }, 'Xuất CSV');
const tableHost = el('div', { 'aria-live': 'polite' });
const pager = el('div', { class: 'pager-row' });

// Chú giải heatmap: mẫu màu + biểu tượng + chữ (AC-263), dùng lại nhãn chip hiện có.
const LEGEND = [
  { cls: 'progress-cell--passed', icon: 'check', text: 'Đã đạt' },
  { cls: 'progress-cell--heat2', icon: 'half', text: 'Đang làm' },
  { cls: 'progress-cell--none', icon: 'circle', text: 'Chưa làm' },
  { cls: 'progress-cell--unassigned', icon: 'dash', text: 'Chưa giao' },
  { cls: 'progress-cell--late', icon: 'alert', text: 'Nộp muộn' },
];
const legend = el('ul', { class: 'heat-legend', 'aria-label': 'Chú giải màu ô tiến độ' },
  LEGEND.map((item) => el('li', {}, el('span', { class: 'heat-swatch progress-cell ' + item.cls, 'aria-hidden': 'true' }, icon(item.icon)), item.text)));

content.append(
  el('p', { class: 'hint' }, 'Chấm tại trình duyệt học sinh – chưa xác minh. Chỉ dùng để theo dõi, không dùng làm điểm thi chính thức.'),
  legend,
  el('div', { class: 'filters' }, classF.root, lessonF.root, searchF.root, sortF.root, el('div', { class: 'form-field' }, el('span', { class: 'form-label' }, ' '), exportBtn)),
  tableHost,
  pager,
);

function passedCount(row) {
  return row.cells.filter((c) => c.status === 'Passed').length;
}

function sortedRows() {
  return data.rows; // đã được máy chủ sắp xếp trên toàn bộ học sinh (tham số sort)
}

/** Lớp nền heatmap theo trạng thái (AC-262): Đang làm chia 3 mức theo bestPassedTests/totalTests (<34%, <67%, từ 67%). */
function cellClass(cell) {
  if (cell.status === 'NotAssigned') return 'progress-cell--unassigned';
  if (cell.passedLate) return 'progress-cell--late';
  if (cell.status === 'Passed') return 'progress-cell--passed';
  if (cell.status === 'InProgress') {
    const ratio = cell.totalTests ? cell.bestPassedTests / cell.totalTests : 0;
    return ratio < 0.34 ? 'progress-cell--heat1' : ratio < 0.67 ? 'progress-cell--heat2' : 'progress-cell--heat3';
  }
  return 'progress-cell--none';
}

function cellView(row, cell, exercise) {
  const cls = 'progress-cell ' + cellClass(cell);
  if (cell.status === 'NotAssigned') return el('div', { class: cls }, statusChip('NotAssigned'));
  const inner = [
    statusChip(cell.status),
    cell.attempts ? el('span', { class: 'small mono' }, `${cell.bestPassedTests}/${cell.totalTests} test · ${cell.attempts} lần`) : null,
    cell.passedLate ? lateChip('Nộp muộn') : null,
    cell.hasViewedSolution ? chip('chip--info', 'eye', 'Đã xem đáp án') : null,
    cell.lastSubmittedAt ? el('span', { class: 'small mono' }, formatDateTime(cell.lastSubmittedAt)) : null,
  ];
  if (!cell.attempts) return el('div', { class: cls }, inner);
  const open = el('button', { type: 'button', 'aria-label': `Xem bài nộp của ${row.displayName} cho bài ${exercise.title}` }, inner);
  open.addEventListener('click', () => openSubmissions(row, exercise, open));
  return el('div', { class: cls }, open);
}

function render() {
  clear(tableHost);
  clear(pager);
  if (!data || !data.exercises.length) {
    tableHost.append(stateBox({ kind: 'empty', title: 'Bài học này chưa có bài tập', message: 'Hãy chọn bài học khác hoặc thêm bài tập trong trang soạn bài.' }));
    return;
  }
  if (!data.rows.length) {
    tableHost.append(stateBox({
      kind: 'empty',
      title: 'Không có học sinh phù hợp',
      message: 'Hãy đổi lớp hoặc xóa từ khóa tìm kiếm.',
      actions: [el('button', { type: 'button', class: 'btn btn--primary', onclick: clearFilters }, 'Xóa bộ lọc')],
    }));
    return;
  }
  const rows = sortedRows();
  const header = ['Học sinh', 'Lớp', ...data.exercises.map((e) => ({ label: e.title })), { label: 'Số bài đạt', num: true }];
  const body = rows.map((row) => el('tr', {},
    el('th', { scope: 'row', class: 'sticky-col' }, row.displayName, el('div', { class: 'small muted' }, row.username)),
    el('td', {}, row.className || 'Chưa xếp lớp'),
    ...row.cells.map((cell, i) => el('td', {}, cellView(row, cell, data.exercises[i]))),
    el('td', { class: 'num' }, `${passedCount(row)}/${row.cells.filter((c) => c.status !== 'NotAssigned').length}`)));

  const summary = el('tr', { class: 'summary-row' },
    el('th', { scope: 'row', class: 'sticky-col' }, 'Tổng hợp (trong trang)'),
    el('td', {}, ''),
    ...data.exercises.map((e, i) => {
      const assigned = rows.filter((r) => r.cells[i].status !== 'NotAssigned').length;
      const done = rows.filter((r) => r.cells[i].status === 'Passed').length;
      return el('td', {}, `${done}/${assigned} đã đạt`);
    }),
    el('td', {}, ''));
  body.push(summary);

  const wrap = dataTable({ caption: `Tiến độ ${data.total} học sinh`, headers: header, rows: body });
  // Cột đầu là tiêu đề hàng: tiêu đề cột đầu cũng cần scope="col" (đã do dataTable đặt).
  tableHost.append(wrap);

  const pages = Math.max(1, Math.ceil(data.total / PAGE_SIZE));
  const prev = el('button', { type: 'button', class: 'btn btn--small' }, '← Trang trước');
  const next = el('button', { type: 'button', class: 'btn btn--small' }, 'Trang sau →');
  prev.disabled = page <= 1;
  next.disabled = page >= pages;
  prev.addEventListener('click', () => { page -= 1; load(); });
  next.addEventListener('click', () => { page += 1; load(); });
  pager.append(el('span', {}, `Trang ${page}/${pages} · ${data.total} học sinh`), el('div', { class: 'row' }, prev, next));
}

function clearFilters() {
  classF.value = '';
  searchF.value = '';
  page = 1;
  load();
}

async function load() {
  tableHost.setAttribute('aria-busy', 'true');
  if (!tableHost.firstChild) tableHost.append(skeleton({ shape: 'table', count: 6, label: 'Đang tải tiến độ học sinh…' }));
  try {
    data = await api.progress({ classId: classF.value, lessonId: lessonF.value, search: searchF.value.trim(), sort: sortKey, page, pageSize: PAGE_SIZE });
    render();
  } catch (err) {
    clear(tableHost);
    tableHost.append(stateBox({ kind: 'error', title: errorText(err), actions: [el('button', { type: 'button', class: 'btn btn--primary', onclick: load }, 'Thử lại')] }));
  } finally {
    tableHost.setAttribute('aria-busy', 'false');
  }
}

/** Hộp thoại xem các lần nộp (mã chỉ đọc, escape an toàn), điều hướng bằng nút Trước/Sau. */
async function openSubmissions(row, exercise, opener) {
  let list;
  try {
    list = (await api.studentSubmissions(row.studentId, exercise.id)).submissions || [];
  } catch (err) {
    say('error', errorText(err));
    return;
  }
  if (!list.length) {
    say('info', 'Học sinh này chưa có bài nộp nào cho bài tập này.');
    return;
  }
  let index = list.length - 1;
  const host = el('div');
  const status = el('p', { class: 'muted small', 'aria-live': 'polite' });
  const code = el('pre', { class: 'dialog-pre code-view', tabindex: '0', 'aria-label': 'Mã đã nộp (chỉ đọc)' });
  const results = el('div');
  const prev = el('button', { type: 'button', class: 'btn btn--small' }, '← Lần nộp trước');
  const next = el('button', { type: 'button', class: 'btn btn--small' }, 'Lần nộp sau →');

  const show = () => {
    const s = list[index];
    status.textContent = `Lần nộp ${s.attemptNo}/${list.length} · ${formatDateTime(s.submittedAt)} · ${s.passedTests}/${s.totalTests} test đạt${s.isLate ? ' · Nộp muộn' : ''}`;
    code.textContent = s.code;
    clear(results);
    if (s.results && s.results.length) {
      results.append(el('p', { class: 'small' }, 'Từng test: ' + s.results.map((r, i) => `#${i + 1} ${r.status}`).join(', ')));
    }
    prev.disabled = index === 0;
    next.disabled = index === list.length - 1;
  };
  prev.addEventListener('click', () => { index -= 1; show(); });
  next.addEventListener('click', () => { index += 1; show(); });
  host.append(
    banner('info', 'Chấm tại trình duyệt học sinh – chưa xác minh. Hãy đối chiếu mã đã nộp bằng mắt.'),
    el('div', { class: 'row' }, prev, next),
    status,
    code,
    results);
  show();
  await showDialog({
    title: `${row.displayName} – ${exercise.title}`,
    content: host,
    buttons: [{ text: 'Đóng', value: true }],
    wide: true,
    opener,
    describe: false,
  });
}

async function exportCsv() {
  exportBtn.disabled = true;
  try {
    const rows = [];
    let p = 1;
    let first = null;
    for (;;) {
      const chunk = await api.progress({ classId: classF.value, lessonId: lessonF.value, search: searchF.value.trim(), sort: sortKey, page: p, pageSize: 100 });
      first = first || chunk;
      rows.push(...chunk.rows);
      if (rows.length >= chunk.total || !chunk.rows.length) break;
      p += 1;
    }
    const header = ['Tên đăng nhập', 'Họ tên', 'Lớp', ...first.exercises.map((e) => e.title), 'Số bài đạt'];
    const out = [header];
    for (const r of rows) {
      out.push([
        r.username, r.displayName, r.className,
        ...r.cells.map((c) => (c.status === 'NotAssigned' ? 'Chưa giao' : `${statusText(c.status)} (${c.bestPassedTests}/${c.totalTests}${c.passedLate ? ', muộn' : ''})`)),
        passedCount(r),
      ]);
    }
    downloadCsv('tien-do-hoc-sinh.csv', out);
    say('ok', 'Đã tạo tệp CSV (UTF-8).');
  } catch (err) {
    say('error', errorText(err));
  } finally {
    exportBtn.disabled = false;
  }
}

function statusText(status) {
  return { NotStarted: 'Chưa làm', InProgress: 'Đang làm', Passed: 'Đã đạt' }[status] || status;
}

async function build() {
  try {
    [classes, lessons] = await Promise.all([
      api.classes().then((d) => d.classes || []),
      api.teacherLessons(true).then((d) => d.lessons || []),
    ]);
  } catch (err) {
    content.append(stateBox({ kind: 'error', title: errorText(err) }));
    finishLoading();
    return;
  }
  for (const c of classes) classF.input.append(el('option', { value: String(c.id) }, c.name));
  for (const l of lessons) lessonF.input.append(el('option', { value: l.id }, `${l.order}. ${l.title}${l.isPublished ? '' : ' (bản nháp)'}${l.isArchived ? ' (lưu trữ)' : ''}`));
  const preset = new URLSearchParams(location.search).get('lesson');
  if (preset) lessonF.value = preset;
  if (!lessons.length) {
    tableHost.append(stateBox({ kind: 'empty', title: 'Chưa có bài học nào', message: 'Hãy tạo bài học trước khi theo dõi tiến độ.', actions: [el('a', { class: 'btn btn--primary', href: '/teacher/lesson-edit.html' }, 'Tạo bài học')] }));
    finishLoading();
    return;
  }
  const reload = () => { page = 1; load(); };
  classF.input.addEventListener('change', reload);
  lessonF.input.addEventListener('change', reload);
  searchF.input.addEventListener('input', debounce(reload, 300));
  sortF.input.addEventListener('change', () => { sortKey = sortF.value; page = 1; load(); });
  exportBtn.addEventListener('click', exportCsv);
  await load();
  finishLoading();
}

build();

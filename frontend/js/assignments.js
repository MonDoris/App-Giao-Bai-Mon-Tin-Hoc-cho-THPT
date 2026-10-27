import { api, describeError } from '/js/api.js';
import { initPage } from '/js/auth.js';
import { el, clear, stateBox, difficultyInfo } from '/js/dom.js';
import { dataTable, formatDateTime, lateChip, overdueChip, statusChip } from '/js/ui.js';

await initPage({ role: 'Student', active: 'assignments' });

const host = document.getElementById('list');
const filter = document.getElementById('filter-status');
let all = [];

function matches(a, status) {
  if (status === 'all') return true;
  if (status === 'Overdue') return a.isOverdue;
  return a.status === status;
}

function render() {
  clear(host);
  if (all.length === 0) {
    host.append(stateBox({
      kind: 'empty',
      title: 'Bạn chưa được giao bài tập nào',
      message: 'Khi giáo viên giao bài, danh sách sẽ hiện ở đây.',
      actions: [el('a', { class: 'btn btn--primary', href: '/lessons.html' }, 'Về danh sách bài học')],
    }));
    return;
  }
  const items = all.filter((a) => matches(a, filter.value));
  if (items.length === 0) {
    host.append(stateBox({
      kind: 'empty',
      title: 'Không có bài tập nào ở trạng thái này',
      message: 'Hãy chọn trạng thái khác để xem các bài tập còn lại.',
      actions: [el('button', { type: 'button', class: 'btn', onclick: () => { filter.value = 'all'; render(); filter.focus(); } }, 'Xem tất cả')],
    }));
    return;
  }

  const rows = items.map((a) => {
    const d = difficultyInfo(a.difficulty);
    const href = `/problem.html?lesson=${encodeURIComponent(a.lessonId)}&problem=${encodeURIComponent(a.exerciseId)}`;
    return el(
      'tr',
      {},
      el('td', {}, el('a', { href }, a.exerciseTitle), a.note ? el('div', { class: 'small muted' }, 'Ghi chú: ' + a.note) : null),
      el('td', {}, a.lessonTitle),
      el('td', {}, el('span', { class: 'badge ' + d.cls }, d.label)),
      el('td', { class: 'nowrap mono' }, a.dueAt ? formatDateTime(a.dueAt) : 'Không có hạn'),
      el('td', {}, el('div', { class: 'row' }, statusChip(a.status), a.status === 'Passed' && a.passedLate ? lateChip('Nộp muộn') : a.isOverdue ? overdueChip() : null)),
      el('td', { class: 'num' }, `${a.bestPassedTests}/${a.totalTests}`),
      el('td', { class: 'num' }, String(a.attempts)),
      el('td', { class: 'nowrap mono' }, a.lastSubmittedAt ? formatDateTime(a.lastSubmittedAt) : 'Chưa nộp'),
    );
  });

  host.append(dataTable({
    caption: 'Danh sách bài tập được giao',
    headers: ['Bài tập', 'Bài học', 'Độ khó', 'Hạn nộp', 'Trạng thái', { label: 'Test đạt tốt nhất', num: true }, { label: 'Số lần nộp', num: true }, 'Lần nộp gần nhất'],
    rows,
  }));
}

async function load(refocus = false) {
  host.setAttribute('aria-busy', 'true');
  clear(host);
  host.append(stateBox({ kind: 'loading', title: 'Đang tải danh sách bài tập…', shape: 'rows', count: 6 }));
  try {
    all = (await api.myAssignments()).assignments || [];
    render();
  } catch (err) {
    const info = describeError(err);
    clear(host);
    const actions = info.retry ? [el('button', { type: 'button', class: 'btn btn--primary', onclick: () => load(true) }, 'Thử lại')] : [];
    host.append(stateBox({ kind: 'error', title: info.title, message: info.message, actions }));
  } finally {
    host.setAttribute('aria-busy', 'false');
    if (refocus) host.focus({ preventScroll: true });
  }
}

filter.addEventListener('change', render);
load();

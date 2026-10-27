// Tổng quan của giáo viên (QD-68 đến QD-70): đúng 3 yêu cầu song song, không thêm endpoint. Công thức: AC-368 đến AC-373.
// "Hoạt động gần đây" chỉ gồm dữ liệu thật có sẵn (giao bài gần đây, bài học sửa gần đây); chưa có luồng bài nộp (QD-70).
import { api } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { el } from '/js/core/dom.js';
import { formatDateTime, stateChip } from '/js/core/ui.js';
import { enableCardEffects, revealOnView } from '/js/core/fx.js';
import { count, percent, stackedBar } from '/js/components/charts.js';
import { createSource, kpiCard, setSlot } from '/js/components/dash-kit.js';

await initPage({ role: 'Teacher', active: 'overview' });
const $ = (id) => document.getElementById(id);
const HOUR = 3600000;
const time = (iso) => (iso ? new Date(iso).getTime() : NaN);
const progressHref = (lessonId) => '/teacher/progress.html?lesson=' + encodeURIComponent(lessonId);

/** Trạng thái đã tải của hai nguồn, để quyết định lời "Mọi thứ đã gọn gàng" (cần cả hai). */
const state = { lessons: null, assignments: null, draftCount: 0, soonCount: 0, noneCount: 0 };

function updateClear() {
  const ready = state.lessons && state.assignments;
  $('todo-clear').hidden = !(ready && state.lessons.length > 0 && state.draftCount + state.soonCount + state.noneCount === 0);
}

function group(title, total, items, emptyText = 'Không có việc nào') {
  return el(
    'div',
    { class: 'dash-group' },
    el('h3', {}, `${title} (${total})`),
    items.length ? el('ul', { class: 'dash-list' }, items) : el('p', { class: 'muted' }, emptyText),
  );
}

// ---------------- Nguồn 1: bài học ----------------
function renderLessons(lessons) {
  state.lessons = lessons;
  const published = lessons.filter((l) => l.isPublished).length;
  const drafts = lessons.filter((l) => !l.isPublished);
  const exercises = lessons.reduce((sum, l) => sum + count(l.exerciseCount), 0);
  const publishedExercises = lessons.reduce((sum, l) => sum + count(l.publishedExerciseCount), 0);

  setSlot(
    $('slot-lessons'),
    kpiCard({
      label: 'Bài học',
      iconName: 'book',
      value: lessons.length,
      sub: lessons.length ? `${published} đã xuất bản, ${drafts.length} bản nháp` : 'Chưa có bài học nào',
      visual: lessons.length ? stackedBar({ name: 'Bài học', segments: [{ label: 'Đã xuất bản', value: published, tone: 'done' }, { label: 'Bản nháp', value: drafts.length, tone: 'none' }] }) : null,
      source: 'danh sách bài học (không gồm bài lưu trữ)',
    }),
  );
  setSlot(
    $('slot-exercises'),
    kpiCard({
      label: 'Bài tập',
      iconName: 'code',
      value: exercises,
      sub: `${publishedExercises} đã xuất bản`,
      source: 'bài tập trong các bài học đang dùng',
    }),
  );

  const guide = $('slot-guide');
  const empty = lessons.length === 0;
  guide.hidden = !empty;
  $('todo-groups').hidden = empty;
  if (empty) {
    setSlot(
      guide,
      el('p', {}, el('strong', {}, 'Bắt đầu soạn bài đầu tiên')),
      el('ol', { class: 'dash-steps' }, ['Tạo bài học và viết nội dung', 'Thêm bài tập và bộ kiểm tra chấm bài', 'Xuất bản rồi giao bài cho học sinh'].map((text) => el('li', {}, text))),
      el('a', { class: 'btn btn--primary', href: '/teacher/lesson-edit.html' }, 'Tạo bài học'),
    );
  } else {
    setSlot(guide);
  }

  state.draftCount = drafts.length;
  setSlot(
    $('slot-draft'),
    group(
      'Bài học chưa xuất bản',
      drafts.length,
      drafts.slice(0, 5).map((l) =>
        el(
          'li',
          { class: 'dash-list__item' },
          el('div', { class: 'dash-list__main' }, el('strong', {}, l.title)),
          el('a', { class: 'btn btn--small', href: '/teacher/lesson-edit.html?id=' + encodeURIComponent(l.id) }, 'Soạn tiếp'),
        ),
      ),
    ),
  );

  const edited = lessons
    .filter((l) => Number.isFinite(time(l.updatedAt)))
    .sort((a, b) => time(b.updatedAt) - time(a.updatedAt))
    .slice(0, 5);
  setSlot(
    $('slot-recent-lessons'),
    edited.length
      ? el(
          'ul',
          { class: 'dash-list' },
          edited.map((l) =>
            el(
              'li',
              { class: 'dash-list__item' },
              el('div', { class: 'dash-list__main' }, el('a', { href: '/teacher/lesson-edit.html?id=' + encodeURIComponent(l.id) }, l.title), el('span', { class: 'mono muted' }, formatDateTime(l.updatedAt))),
              stateChip(l.isPublished ? 'published' : 'draft'),
            ),
          ),
        )
      : el('p', { class: 'muted' }, 'Chưa có bài học nào được chỉnh sửa'),
  );
  updateClear();
}

// ---------------- Nguồn 2: giao bài ----------------
function renderAssignments(all) {
  const active = all.filter((a) => !a.cancelledAt);
  state.assignments = active;
  const now = Date.now();
  // Cắt về hợp lệ trước khi tính: P <= S <= A
  const rows = active.map((a) => {
    const assigned = count(a.assignedStudents);
    const submitted = Math.min(count(a.submittedStudents), assigned);
    return { a, assigned, submitted, passed: Math.min(count(a.passedStudents), submitted) };
  });
  const A = rows.reduce((s, r) => s + r.assigned, 0);
  const S = rows.reduce((s, r) => s + r.submitted, 0);
  const P = rows.reduce((s, r) => s + r.passed, 0);

  setSlot(
    $('slot-rate'),
    kpiCard({
      label: 'Nộp bài và đạt',
      iconName: 'chart',
      sub: A > 0
        ? [
            el('p', { class: 'dash-kpi__sub' }, 'Đã nộp ', el('strong', { class: 'mono' }, `${S}/${A} (${percent(S, A)}%)`)),
            el('p', { class: 'dash-kpi__sub' }, 'Đã đạt ', el('strong', { class: 'mono' }, `${P}/${A} (${percent(P, A)}%)`)),
          ]
        : 'Chưa có dữ liệu',
      source: 'các lượt giao bài chưa hủy',
    }),
  );

  const link = (r) => el('a', { href: progressHref(r.a.lessonId) }, r.a.exerciseTitle);
  const soon = rows
    .filter((r) => Number.isFinite(time(r.a.dueAt)) && time(r.a.dueAt) >= now && time(r.a.dueAt) <= now + 72 * HOUR && r.submitted < r.assigned)
    .sort((x, y) => time(x.a.dueAt) - time(y.a.dueAt));
  const none = rows.filter((r) => r.assigned > 0 && r.submitted === 0);
  state.soonCount = soon.length;
  state.noneCount = none.length;
  setSlot(
    $('slot-soon'),
    group(
      'Bài giao sắp hết hạn',
      soon.length,
      soon.slice(0, 5).map((r) =>
        el('li', { class: 'dash-list__item' }, el('div', { class: 'dash-list__main' }, link(r), el('span', { class: 'mono muted' }, formatDateTime(r.a.dueAt))), el('span', { class: 'mono nowrap' }, `${r.submitted}/${r.assigned} đã nộp`)),
      ),
    ),
  );
  setSlot(
    $('slot-none'),
    group(
      'Giao bài chưa có người nộp',
      none.length,
      none.slice(0, 5).map((r) => el('li', { class: 'dash-list__item' }, el('div', { class: 'dash-list__main' }, link(r), el('span', { class: 'muted' }, r.a.lessonTitle)), el('span', { class: 'mono nowrap' }, `0/${r.assigned} đã nộp`))),
    ),
  );

  // Biểu đồ: tối đa 6 bài học có giao bài gần nhất; thang theo tổng số học sinh được giao của bài học đó
  const byLesson = new Map();
  for (const r of rows) {
    const g = byLesson.get(r.a.lessonId) || { id: r.a.lessonId, title: r.a.lessonTitle, A: 0, S: 0, P: 0, latest: 0 };
    g.A += r.assigned;
    g.S += r.submitted;
    g.P += r.passed;
    g.latest = Math.max(g.latest, Number.isFinite(time(r.a.createdAt)) ? time(r.a.createdAt) : 0);
    byLesson.set(r.a.lessonId, g);
  }
  const top = [...byLesson.values()].sort((x, y) => y.latest - x.latest).slice(0, 6);
  setSlot(
    $('slot-chart'),
    top.length
      ? el(
          'ul',
          { class: 'dash-bars' },
          top.map((g) =>
            el(
              'li',
              {},
              el('a', { class: 'dash-bars__name', href: progressHref(g.id) }, g.title),
              stackedBar({
                name: g.title,
                emptyText: 'Chưa có học sinh được giao',
                segments: [
                  { label: 'Đã đạt', value: g.P, tone: 'done' },
                  { label: 'Đã nộp, chưa đạt', value: g.S - g.P, tone: 'part' },
                  { label: 'Chưa nộp', value: g.A - g.S, tone: 'none' },
                ],
              }),
            ),
          ),
        )
      : el('div', {}, el('p', { class: 'muted' }, 'Chưa giao bài nào'), el('a', { class: 'btn', href: '/teacher/assignments.html' }, 'Giao bài')),
  );

  const recent = [...active].filter((a) => Number.isFinite(time(a.createdAt))).sort((x, y) => time(y.createdAt) - time(x.createdAt)).slice(0, 5);
  setSlot(
    $('slot-recent-assign'),
    recent.length
      ? el(
          'ul',
          { class: 'dash-list' },
          recent.map((a) =>
            el(
              'li',
              { class: 'dash-list__item' },
              el('div', { class: 'dash-list__main' }, el('a', { href: progressHref(a.lessonId) }, a.exerciseTitle), el('span', { class: 'muted' }, a.assignedToAll || !(a.classNames || []).length ? 'Tất cả học sinh' : a.classNames.join(', '))),
              el('span', { class: 'mono nowrap' }, formatDateTime(a.createdAt)),
            ),
          ),
        )
      : el('p', { class: 'muted' }, 'Chưa giao bài nào'),
  );
  updateClear();
}

// ---------------- Nguồn 3: số học sinh ----------------
function renderStudents(data) {
  setSlot(
    $('slot-students'),
    kpiCard({ label: 'Học sinh', iconName: 'users', value: count(data.total), sub: 'tài khoản học sinh', source: 'danh sách học sinh' }),
  );
}

// ---------------- Điều phối ----------------
const board = $('board');
const status = $('board-status');
const runLessons = createSource({
  slots: [$('slot-lessons'), $('slot-exercises'), $('slot-draft'), $('slot-recent-lessons')],
  load: async () => {
    state.lessons = null;
    return (await api.teacherLessons(false)).lessons || [];
  },
  render: renderLessons,
});
const runAssignments = createSource({
  slots: [$('slot-rate'), $('slot-soon'), $('slot-none'), $('slot-chart'), $('slot-recent-assign')],
  load: async () => {
    state.assignments = null;
    return (await api.assignments()).assignments || [];
  },
  render: renderAssignments,
});
const runStudents = createSource({ slots: [$('slot-students')], load: () => api.students({ page: 1, pageSize: 1 }), render: renderStudents });

revealOnView(board.querySelectorAll('.dash-section, .dash-kpis'));
enableCardEffects(board.querySelector('.dash-kpis'), '.dash-card');
Promise.all([runLessons(), runAssignments(), runStudents()]).then(() => {
  board.setAttribute('aria-busy', 'false');
  status.textContent = '';
  updateClear();
});

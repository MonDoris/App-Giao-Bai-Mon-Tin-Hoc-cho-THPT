// Tổng quan của học sinh (QD-64, QD-68, QD-69): đúng 2 yêu cầu song song, GET /api/lessons và GET /api/me/assignments.
// Công thức KPI: AC-357 đến AC-362 (xem docs/specs/landing-dashboard-soan-bai.md). Mỗi nguồn lỗi độc lập.
import { api } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { el, stateBox } from '/js/core/dom.js';
import { banner, chip, formatDateTime, icon, overdueChip } from '/js/core/ui.js';
import { enableCardEffects, revealOnView } from '/js/core/fx.js';
import { count, percent, ring, stackedBar } from '/js/components/charts.js';
import { createSource, kpiCard, lessonCard, setSlot } from '/js/components/dash-kit.js';

const user = await initPage({ role: 'Student', active: 'overview' });
const $ = (id) => document.getElementById(id);

if (new URLSearchParams(location.search).get('notice') === 'forbidden') {
  $('notice').append(banner('warn', 'Bạn không có quyền truy cập khu vực giáo viên.'));
}
$('greeting').textContent = `Xin chào, ${user.displayName}`;

const DAY = 24 * 60 * 60 * 1000;
const dueTime = (a) => (a.dueAt ? new Date(a.dueAt).getTime() : NaN);

/** Mỗi bài tập một lần theo exerciseId (QD-68): có lượt Passed thì lấy lượt đó, ngược lại lấy lượt có hạn sớm nhất. */
function uniqueByExercise(list) {
  const best = new Map();
  for (const a of list) {
    const cur = best.get(a.exerciseId);
    if (!cur) best.set(a.exerciseId, a);
    else if (cur.status !== 'Passed' && (a.status === 'Passed' || (Number.isFinite(dueTime(a)) && !(dueTime(a) >= dueTime(cur))))) best.set(a.exerciseId, a);
  }
  return [...best.values()];
}

// ---------------- Nguồn 1: bài học ----------------
function renderLessons(lessons) {
  const sorted = [...lessons].sort((a, b) => count(a.order) - count(b.order));
  let rawPassed = 0;
  let total = 0;
  for (const l of sorted) {
    rawPassed += count(l.passedCount);
    total += count(l.exerciseCount);
  }
  const passed = Math.min(rawPassed, total);
  const target = sorted.find((l) => count(l.exerciseCount) > 0 && count(l.passedCount) < count(l.exerciseCount)) || null;

  setSlot(
    $('slot-ring'),
    kpiCard({
      label: 'Tiến độ tổng',
      iconName: 'chart',
      visual: ring({ value: passed, total }),
      sub: total > 0 ? `${passed}/${total} bài tập đã đạt` : 'Chưa có bài tập được giao',
      source: 'danh sách bài học',
    }),
  );

  const resume = $('slot-resume');
  if (total === 0) {
    resume.hidden = true;
  } else if (!target) {
    setSlot(resume, el('div', { class: 'resume-done' }, el('p', {}, icon('star'), ' Bạn đã hoàn thành mọi bài tập được giao'), el('a', { href: '/assignments.html' }, 'Bài tập của tôi')));
  } else {
    setSlot(
      resume,
      el(
        'a',
        { class: 'resume-card', href: '/lesson.html?id=' + encodeURIComponent(target.id) },
        el('p', { class: 'resume-card__eyebrow' }, icon('book'), rawPassed === 0 && passed === 0 ? 'Bắt đầu học' : 'Tiếp tục học'),
        el('p', { class: 'resume-card__title' }, target.title),
        el('p', { class: 'resume-card__meta' }, `${count(target.passedCount)}/${count(target.exerciseCount)} bài tập đã đạt`),
        el('span', { class: 'resume-card__arrow', 'aria-hidden': 'true' }, icon('arrow-right')),
      ),
    );
  }

  const host = $('slot-lessons');
  if (sorted.length === 0) {
    setSlot(host, stateBox({ kind: 'empty', title: 'Chưa có bài học nào', message: 'Giáo viên chưa xuất bản bài học nào.' }));
    return;
  }
  const grid = el('div', { class: 'lesson-grid dash-lessons' }, sorted.slice(0, 6).map((l) => lessonCard(l, { level: 'h3' })));
  setSlot(host, grid, el('p', { class: 'dash-more' }, el('a', { href: '/lessons.html' }, 'Xem tất cả bài học', icon('arrow-right'))));
  enableCardEffects(grid);
}

// ---------------- Nguồn 2: bài tập được giao ----------------
function dueChip(a, now) {
  const left = dueTime(a) - now;
  if (a.isOverdue || left < 0) return overdueChip();
  if (left >= DAY) return chip('chip--info', 'clock', `Còn ${Math.floor(left / DAY)} ngày`);
  if (left >= 3600000) return chip('chip--info', 'clock', `Còn ${Math.floor(left / 3600000)} giờ`);
  return chip('chip--late', 'alert', 'Hết hạn hôm nay');
}

function renderAssignments(list) {
  const items = uniqueByExercise(list);
  const now = Date.now();
  const open = items.filter((a) => a.status !== 'Passed');
  const passed = items.length - open.length;
  const inProgress = open.filter((a) => a.status === 'InProgress').length;

  setSlot(
    $('slot-done'),
    kpiCard({
      label: 'Đã đạt',
      iconName: 'check',
      value: items.length ? passed : null,
      sub: items.length ? null : 'Chưa có bài tập được giao',
      visual: items.length
        ? stackedBar({
            name: 'Phân bố bài tập',
            segments: [
              { label: 'Đã đạt', value: passed, tone: 'done' },
              { label: 'Đang làm', value: inProgress, tone: 'part' },
              { label: 'Chưa làm', value: open.length - inProgress, tone: 'none' },
            ],
          })
        : null,
      source: 'danh sách bài tập được giao',
    }),
  );

  const soon = open
    .filter((a) => !a.isOverdue && Number.isFinite(dueTime(a)) && dueTime(a) >= now && dueTime(a) <= now + 7 * DAY)
    .sort((a, b) => dueTime(a) - dueTime(b));
  setSlot(
    $('slot-soon'),
    kpiCard({
      label: 'Sắp đến hạn',
      iconName: 'calendar',
      value: soon.length,
      sub: soon.length
        ? ['Trong 7 ngày tới', el('p', { class: 'dash-kpi__sub' }, 'Gần nhất: ', el('span', { class: 'mono' }, formatDateTime(soon[0].dueAt)))]
        : 'Không có bài nào sắp đến hạn',
      source: 'hạn nộp trong 7 ngày tới',
    }),
  );

  const late = open.filter((a) => a.isOverdue);
  setSlot(
    $('slot-late'),
    kpiCard({
      label: 'Quá hạn',
      iconName: late.length ? 'alert' : 'clock',
      value: late.length,
      sub: late.length ? 'Bài chưa đạt đã quá hạn nộp' : 'Không có bài quá hạn',
      link: late.length ? { href: '/assignments.html', text: 'Xem bài quá hạn' } : null,
      tone: late.length ? 'danger' : '',
      source: 'hạn nộp của bài chưa đạt',
    }),
  );

  const upcoming = open
    .filter((a) => Number.isFinite(dueTime(a)))
    .sort((a, b) => dueTime(a) - dueTime(b))
    .slice(0, 5);
  const host = $('slot-due');
  if (upcoming.length === 0) {
    setSlot(host, el('p', { class: 'muted' }, 'Chưa có hạn nộp nào'), el('p', { class: 'dash-more' }, el('a', { href: '/assignments.html' }, 'Xem tất cả', icon('arrow-right'))));
    return;
  }
  setSlot(
    host,
    el(
      'ul',
      { class: 'dash-list' },
      upcoming.map((a) =>
        el(
          'li',
          { class: 'dash-list__item' },
          el(
            'div',
            { class: 'dash-list__main' },
            el('a', { href: `/problem.html?lesson=${encodeURIComponent(a.lessonId)}&problem=${encodeURIComponent(a.exerciseId)}` }, a.exerciseTitle),
            el('span', { class: 'muted' }, a.lessonTitle),
          ),
          el('div', { class: 'dash-list__side' }, el('span', { class: 'mono nowrap' }, formatDateTime(a.dueAt)), dueChip(a, now)),
        ),
      ),
    ),
    el('p', { class: 'dash-more' }, el('a', { href: '/assignments.html' }, 'Xem tất cả', icon('arrow-right'))),
  );
}

// ---------------- Điều phối ----------------
const board = $('board');
const emptyHost = $('empty-host');
const status = $('board-status');

const lessonSlots = [$('slot-ring'), $('slot-resume'), $('slot-lessons')];
const assignSlots = [$('slot-done'), $('slot-soon'), $('slot-late'), $('slot-due')];
const runLessons = createSource({ slots: lessonSlots, quiet: [$('slot-resume')], load: async () => (await api.lessons()).lessons || [], render: renderLessons });
const runAssignments = createSource({ slots: assignSlots, load: async () => (await api.myAssignments()).assignments || [], render: renderAssignments });

async function loadAll() {
  emptyHost.replaceChildren();
  board.hidden = false;
  board.setAttribute('aria-busy', 'true');
  status.textContent = 'Đang tải tổng quan…';
  const [lessons, assignments] = await Promise.all([runLessons(), runAssignments()]);
  board.setAttribute('aria-busy', 'false');
  status.textContent = '';
  if (lessons.ok && assignments.ok && lessons.data.length === 0 && assignments.data.length === 0) {
    board.hidden = true;
    emptyHost.append(stateBox({
      kind: 'empty',
      title: 'Chưa có bài học nào',
      message: 'Giáo viên chưa xuất bản bài học hoặc giao bài nào cho bạn.',
      actions: [el('button', { type: 'button', class: 'btn btn--primary', onclick: loadAll }, 'Tải lại')],
    }));
    return;
  }
}

revealOnView(board.querySelectorAll('.dash-section, .dash-kpis'));
enableCardEffects(board.querySelector('.dash-kpis'), '.dash-card');
loadAll();

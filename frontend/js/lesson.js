import { api, describeError } from '/js/api.js';
import { initPage } from '/js/auth.js';
import { el, clear, stateBox, skeleton, difficultyInfo, LESSON_SECTIONS as SECTIONS, lessonSections } from '/js/dom.js';
import { icon } from '/js/icons.js';
import { revealOnView, prefersReducedMotion } from '/js/fx.js';
import { formatDateTime, lateChip, overdueChip, statusChip, setTitle, stateChip } from '/js/ui.js';

const user = await initPage({ active: 'lessons' });
const isTeacher = user.role === 'Teacher';
const main = document.getElementById('main');
const root = document.getElementById('lesson-root');
const lessonId = new URLSearchParams(location.search).get('id') || '';
const homeHref = isTeacher ? '/teacher/index.html' : '/lessons.html';

// Thanh tiến độ đọc: CSS scroll-driven (animation-timeline) là chính; --read-step (0-4) là dự phòng theo mục (AC-226, AC-227).
const readProgress = el('div', { class: 'read-progress', 'aria-hidden': 'true' });
document.body.append(readProgress);
let sectionObserver = null;
let lockTimer = 0;

function stopObserving() {
  if (sectionObserver) sectionObserver.disconnect();
  sectionObserver = null;
  clearTimeout(lockTimer);
}
window.addEventListener('pagehide', stopObserving, { once: true });

if (isTeacher) {
  document.getElementById('preview-banner').append(el('div', { class: 'preview-banner', role: 'status' }, 'Chế độ xem trước – kết quả sẽ không được lưu'));
}

// Thứ tự và tiêu đề của 4 mục là CỐ ĐỊNH cho mọi bài học (SECTIONS nằm trong dom.js, dùng chung với khung xem trước).

function notFound() {
  clear(root);
  root.className = '';
  root.append(stateBox({
    kind: 'error',
    title: 'Không tìm thấy bài học',
    message: 'Mã bài học không hợp lệ hoặc bài học không còn tồn tại.',
    actions: [el('a', { class: 'btn btn--primary', href: homeHref }, 'Về danh sách bài học')],
  }));
}

function exerciseBadges(ex) {
  const d = difficultyInfo(ex.difficulty);
  const badges = [el('span', { class: 'badge ' + d.cls }, d.label)];
  if (isTeacher) {
    if (ex.isPublished === false) badges.push(stateChip('draft'));
    if (ex.isArchived) badges.push(stateChip('archived'));
    return badges;
  }
  const a = ex.assignment;
  if (a) {
    badges.push(statusChip(a.status));
    if (a.status === 'Passed' && a.passedLate) badges.push(lateChip('Nộp muộn'));
    else if (a.isOverdue) badges.push(overdueChip());
    if (a.hasViewedSolution) badges.push(el('span', { class: 'badge badge--seen' }, 'Đã xem đáp án'));
  }
  return badges;
}

function renderExercises(lesson) {
  const wrap = el('div');
  if (!lesson.exercises.length) {
    wrap.append(stateBox({ kind: 'empty', title: isTeacher ? 'Bài học này chưa có bài tập' : 'Giáo viên chưa giao bài tập nào trong bài này' }));
    return wrap;
  }
  const list = el('ul', { class: 'exercise-list' });
  for (const ex of lesson.exercises) {
    const href = `/problem.html?lesson=${encodeURIComponent(lesson.id)}&problem=${encodeURIComponent(ex.id)}`;
    const due = ex.assignment && ex.assignment.dueAt
      ? el('span', { class: 'small muted exercise-item__due' }, 'Hạn nộp: ', el('span', { class: 'mono' }, formatDateTime(ex.assignment.dueAt)))
      : null;
    list.append(el(
      'li',
      {},
      el(
        'a',
        { class: 'exercise-item', href },
        el('span', { class: 'exercise-item__num', 'aria-hidden': 'true' }, `${ex.order}.`),
        el('span', { class: 'exercise-item__title' }, ex.title, due ? el('br') : null, due),
        el('span', { class: 'exercise-item__badges' }, exerciseBadges(ex)),
        icon('arrow-right', 'exercise-item__arrow'),
      ),
    ));
  }
  wrap.append(list);
  return wrap;
}

/**
 * Mục đang đọc (AC-230): IntersectionObserver trên 4 mục; đúng một liên kết có aria-current="location".
 * Cũng đặt --read-step (0-4) cho thanh tiến độ khi trình duyệt thiếu animation-timeline (AC-227).
 * Hiện dần các mục khi cuộn (AC-232): mục đầu hiện ngay.
 */
function setupReading(toc, sections) {
  stopObserving();
  const links = Array.from(toc.querySelectorAll('a'));
  let current = -1;
  let locked = false;

  const setCurrent = (index) => {
    if (index === current) return;
    current = index;
    links.forEach((a, i) => {
      if (i === index) a.setAttribute('aria-current', 'location');
      else a.removeAttribute('aria-current');
    });
    readProgress.style.setProperty('--read-step', String(index + 1));
  };

  links.forEach((a, i) => {
    a.addEventListener('click', () => {
      setCurrent(i);
      locked = true;
      clearTimeout(lockTimer);
      lockTimer = setTimeout(() => { locked = false; }, prefersReducedMotion() ? 100 : 800);
    });
  });

  sections[0].classList.add('reveal', 'is-in', 'is-settled');
  revealOnView(sections.slice(1));

  if (!('IntersectionObserver' in window)) {
    setCurrent(0);
    return;
  }
  const inBand = new Set();
  sectionObserver = new IntersectionObserver((records) => {
    for (const r of records) {
      if (r.isIntersecting) inBand.add(r.target);
      else inBand.delete(r.target);
    }
    if (locked) return;
    const index = sections.findIndex((n) => inBand.has(n));
    if (index >= 0) setCurrent(index);
  }, { rootMargin: '-30% 0px -60% 0px' });
  sections.forEach((n) => sectionObserver.observe(n));
  setCurrent(0);
}

function renderLesson(lesson, neighbours) {
  setTitle(lesson.title);
  clear(root);
  root.className = 'lesson-layout';

  root.append(
    el('p', { class: 'breadcrumb' }, el('a', { class: 'back-link', href: homeHref }, '← Về danh sách bài học')),
    el('div', { class: 'lesson-hero' }, el('h1', {}, lesson.title), isTeacher && lesson.isPublished === false ? stateChip('draft') : null),
  );

  const toc = el(
    'nav',
    { class: 'toc', 'aria-label': 'Mục lục bài học' },
    el('ol', {}, SECTIONS.map((s) => el('li', {}, el('a', { href: '#' + s.id }, s.title)))),
  );
  root.append(toc);

  const sectionNodes = lessonSections(lesson, renderExercises(lesson));
  root.append(...sectionNodes);

  const pager = el('nav', { class: 'pager', 'aria-label': 'Chuyển bài học' });
  if (neighbours.prev) {
    pager.append(el('a', { class: 'btn', href: '/lesson.html?id=' + encodeURIComponent(neighbours.prev.id), rel: 'prev' }, '← Bài trước: ' + neighbours.prev.title));
  } else {
    pager.append(el('span'));
  }
  if (neighbours.next) {
    pager.append(el('a', { class: 'btn btn--primary', href: '/lesson.html?id=' + encodeURIComponent(neighbours.next.id), rel: 'next' }, 'Bài sau: ' + neighbours.next.title + ' →'));
  }
  root.append(pager);

  setupReading(toc, sectionNodes);

  // Hỗ trợ mở thẳng bằng neo (#bai-tap) sau khi nội dung được dựng
  if (location.hash) {
    const target = document.getElementById(decodeURIComponent(location.hash.slice(1)));
    if (target) target.scrollIntoView();
  }
}

async function load() {
  main.setAttribute('aria-busy', 'true');
  stopObserving();
  readProgress.style.setProperty('--read-step', '0');
  clear(root);
  root.className = '';
  root.append(el('div', { class: 'state state--loading' }, skeleton({ shape: 'lines', count: 3, label: 'Đang tải bài học…' }), skeleton({ shape: 'rows', count: 4 })));

  if (!lessonId) {
    notFound();
    main.setAttribute('aria-busy', 'false');
    return;
  }

  try {
    const lesson = await api.lesson(lessonId);

    // Bài trước / bài sau: lấy từ danh sách; nếu lỗi thì chỉ ẩn nút, không ảnh hưởng nội dung bài.
    let neighbours = { prev: null, next: null };
    try {
      const list = (await api.lessons()).lessons || [];
      const i = list.findIndex((l) => l.id === lesson.id);
      if (i >= 0) neighbours = { prev: list[i - 1] ?? null, next: list[i + 1] ?? null };
    } catch (e) { /* bỏ qua */ }

    renderLesson(lesson, neighbours);
  } catch (err) {
    const info = describeError(err, { notFound: 'Không tìm thấy bài học', invalidPrefix: 'Dữ liệu bài học bị lỗi' });
    clear(root);
    root.className = '';
    const actions = [];
    if (info.retry) actions.push(el('button', { type: 'button', class: 'btn btn--primary', onclick: load }, 'Thử lại'));
    actions.push(el('a', { class: 'btn', href: homeHref }, 'Về danh sách bài học'));
    root.append(stateBox({
      kind: 'error',
      title: info.title,
      message: err?.status === 404 ? 'Mã bài học không hợp lệ hoặc bài học không còn tồn tại.' : info.message,
      actions,
    }));
  } finally {
    main.setAttribute('aria-busy', 'false');
  }
}

load();

// Bài học của tôi: thẻ tổng quan, thanh công cụ (tìm, lọc trạng thái, Lưới/Danh sách) và thẻ bài học có độ hoàn thiện.
// Hành vi (xuất bản, lưu trữ, đổi thứ tự, xóa, xác nhận khi ẩn bài đang giao) giữ nguyên; API không đổi.
import { api } from '/js/api.js';
import { initPage } from '/js/auth.js';
import { el, clear, stateBox, skeleton } from '/js/dom.js';
import { icon } from '/js/icons.js';
import { confirmDialog } from '/js/modal.js';
import { uiPrefs } from '/js/storage.js';
import { debounce, formatDateTime, stateChip } from '/js/ui.js';
import { menuButton } from '/js/teacher/editor-kit.js';
import { errorText, finishLoading, pageContent, say } from '/js/teacher/common.js';

await initPage({ role: 'Teacher', active: 'lessons' });

const content = pageContent();
let lessons = [];
let tab = 'all';
let view = uiPrefs.getLessonView();

const TABS = [
  { key: 'all', label: 'Tất cả', test: () => true },
  { key: 'draft', label: 'Bản nháp', test: (l) => !l.isPublished && !l.isArchived },
  { key: 'published', label: 'Đã xuất bản', test: (l) => l.isPublished && !l.isArchived },
  { key: 'archived', label: 'Lưu trữ', test: (l) => l.isArchived },
];

// Ô tìm có biểu tượng; nhãn chỉ dành cho trình đọc màn hình (placeholder nói rõ công dụng).
const searchInput = el('input', { id: 'search', class: 'input', type: 'search', maxlength: 100, placeholder: 'Tìm theo tên bài học', autocomplete: 'off' });
const search = {
  root: el('label', { class: 'ed-search', for: 'search' }, icon('search'), el('span', { class: 'sr-only' }, 'Tìm theo tên bài học'), searchInput),
  input: searchInput,
  get value() { return searchInput.value; },
  set value(v) { searchInput.value = v; },
};

const kpis = el('div', { class: 'ed-kpis', 'aria-label': 'Tổng quan bài học', role: 'group' });
const list = el('div', { class: 'ed-lgrid', role: 'tabpanel', 'aria-live': 'polite', 'data-view': view });
const createBtn = el('a', { class: 'btn btn--primary ed-create', id: 'create-lesson', href: '/teacher/lesson-edit.html' }, icon('plus'), el('span', {}, 'Tạo bài học'));
const tabEls = TABS.map((t, i) => el('button', {
  type: 'button', role: 'tab', class: 'tab', id: 'tab-' + t.key, 'aria-selected': String(i === 0), tabindex: i === 0 ? '0' : '-1',
}, el('span', {}, t.label), el('span', { class: 'ed-tabs__n' }, '0')));
tabEls.forEach((b, i) => {
  b.addEventListener('click', () => selectTab(TABS[i].key));
  b.addEventListener('keydown', (e) => {
    const j = e.key === 'ArrowRight' ? (i + 1) % TABS.length : e.key === 'ArrowLeft' ? (i + TABS.length - 1) % TABS.length : e.key === 'Home' ? 0 : e.key === 'End' ? TABS.length - 1 : -1;
    if (j < 0) return;
    e.preventDefault();
    selectTab(TABS[j].key);
    tabEls[j].focus();
  });
});

const viewBtns = [['grid', 'Lưới', 'grid'], ['list', 'Danh sách', 'list']].map(([key, text, ic]) => {
  const b = el('button', { type: 'button', class: 'btn btn--small ed-viewbtn', 'aria-pressed': String(view === key), 'data-view': key }, icon(ic), el('span', {}, text));
  b.addEventListener('click', () => setView(key));
  return b;
});

content.append(
  el('div', { class: 'ed-listhead' },
    el('p', { class: 'lead' }, 'Soạn bài, xuất bản cho học sinh và giao bài tập.'),
    createBtn),
  kpis,
  el('div', { class: 'ed-listbar' },
    search.root,
    el('div', { class: 'tabs ed-tabs', role: 'tablist', 'aria-label': 'Lọc bài học theo trạng thái' }, tabEls),
    el('div', { class: 'ed-viewswitch', role: 'group', 'aria-label': 'Kiểu hiển thị' }, viewBtns)),
  list,
);

function selectTab(key) {
  tab = key;
  render();
}

function setView(key) {
  view = key;
  uiPrefs.setLessonView(key);
  list.dataset.view = key;
  viewBtns.forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.view === key)));
}

function visible() {
  const term = search.value.trim().toLowerCase();
  const test = TABS.find((t) => t.key === tab).test;
  return lessons.filter((l) => test(l) && (!term || l.title.toLowerCase().includes(term)));
}

async function reload(focusId = null) {
  try {
    lessons = (await api.teacherLessons(true)).lessons || [];
    render();
    if (focusId) document.querySelector(`[data-lesson="${CSS.escape(focusId)}"] h3 a`)?.focus();
  } catch (err) {
    clear(list);
    list.append(stateBox({ kind: 'error', title: errorText(err), actions: [el('button', { type: 'button', class: 'btn btn--primary', onclick: () => reload() }, 'Thử lại')] }));
  } finally {
    list.setAttribute('aria-busy', 'false');
    finishLoading();
  }
}

/** Đổi thứ tự trong danh sách ĐẦY ĐỦ (kể cả bài lưu trữ): hoán đổi với bài hiển thị kề bên. */
async function move(lesson, delta) {
  const shown = visible().map((l) => l.id);
  const at = shown.indexOf(lesson.id);
  const neighbour = shown[at + delta];
  if (!neighbour) return;
  const ids = lessons.map((l) => l.id);
  const a = ids.indexOf(lesson.id);
  const b = ids.indexOf(neighbour);
  [ids[a], ids[b]] = [ids[b], ids[a]];
  try {
    await api.reorderLessons(ids);
    say('ok', `Đã đổi thứ tự bài "${lesson.title}".`);
    await reload(lesson.id);
  } catch (err) {
    say('error', errorText(err));
  }
}

async function togglePublish(lesson, opener) {
  const publishing = !lesson.isPublished;
  if (!publishing && lesson.assignmentCount > 0) {
    let affected = null;
    try {
      affected = (await api.teacherLesson(lesson.id)).affectedStudents;
    } catch (err) { /* vẫn cho xác nhận mà không có số liệu */ }
    const ok = await confirmDialog({
      title: 'Ẩn bài học đang được giao?',
      message: `Bài "${lesson.title}" đã được giao (${lesson.assignmentCount} lượt giao bài)${affected === null ? '' : `, ảnh hưởng ${affected} học sinh`}. `
        + 'Sau khi ẩn, học sinh sẽ không thấy bài này nữa (bài nộp cũ vẫn được giữ).',
      confirmText: 'Ẩn bài học',
      opener,
      danger: true,
    });
    if (!ok) return;
  }
  try {
    await api.publishLesson(lesson.id, publishing);
    say('ok', publishing ? `Đã xuất bản bài "${lesson.title}".` : `Đã chuyển bài "${lesson.title}" về bản nháp.`);
    await reload(lesson.id);
  } catch (err) {
    say('error', errorText(err));
  }
}

async function toggleArchive(lesson, opener) {
  const archiving = !lesson.isArchived;
  const ok = await confirmDialog({
    title: archiving ? 'Lưu trữ bài học?' : 'Khôi phục bài học?',
    message: archiving
      ? `Bài "${lesson.title}" sẽ bị ẩn với học sinh và không giao mới được. Dữ liệu và bài nộp vẫn được giữ, có thể khôi phục sau.`
      : `Bài "${lesson.title}" sẽ trở lại trạng thái ${lesson.isPublished ? 'đã xuất bản' : 'bản nháp'} như trước khi lưu trữ.`,
    confirmText: archiving ? 'Lưu trữ' : 'Khôi phục',
    opener,
  });
  if (!ok) return;
  try {
    await api.archiveLesson(lesson.id, archiving);
    say('ok', archiving ? `Đã cất bài "${lesson.title}" đi (lưu trữ).` : `Đã khôi phục bài "${lesson.title}".`);
    await reload(lesson.id);
  } catch (err) {
    say('error', errorText(err));
  }
}

async function remove(lesson, opener) {
  const ok = await confirmDialog({
    title: 'Xóa bài học?',
    message: `Xóa bài "${lesson.title}" sẽ xóa luôn ${lesson.exerciseCount} bài tập và các bộ kiểm tra của bài. Thao tác này không hoàn tác được.`,
    confirmText: 'Xóa bài học',
    opener,
    danger: true,
  });
  if (!ok) return;
  try {
    await api.deleteLesson(lesson.id);
    say('ok', `Đã xóa bài "${lesson.title}".`);
    await reload();
    createBtn.focus();
  } catch (err) {
    say('error', errorText(err));
  }
}

function emptyState() {
  if (lessons.length) {
    return stateBox({
      kind: 'empty', title: 'Không có bài học phù hợp', message: 'Hãy đổi tab hoặc xóa từ khóa tìm kiếm.',
      actions: [el('button', { type: 'button', class: 'btn', onclick: () => { search.value = ''; selectTab('all'); } }, 'Xem tất cả')],
    });
  }
  const box = stateBox({ kind: 'empty', title: 'Bắt đầu soạn bài đầu tiên', actions: [el('a', { class: 'btn btn--primary', href: '/teacher/lesson-edit.html' }, icon('plus'), el('span', {}, 'Tạo bài học'))] });
  box.insertBefore(el('ol', { class: 'ed-steps3' }, [
    ['pencil', 'Tạo bài học và viết nội dung'],
    ['code', 'Thêm bài tập và bộ kiểm tra chấm bài'],
    ['send', 'Xuất bản rồi giao bài cho học sinh'],
  ].map(([ic, t], i) => el('li', {}, el('span', { class: 'ed-steps3__ic' }, icon(ic)), `${i + 1}. ${t}`))), box.querySelector('.state__actions'));
  return box;
}

// ---------------------------------------------------------------- Tổng quan
function renderKpis() {
  const live = lessons.filter((l) => !l.isArchived);
  const sum = (key) => live.reduce((n, l) => n + (l[key] || 0), 0);
  const tiles = [
    ['book', 'Bài học', live.length, lessons.length > live.length ? `${lessons.length - live.length} bài lưu trữ` : 'đang dùng'],
    ['published', 'Đã xuất bản', live.filter((l) => l.isPublished).length, 'hiện với học sinh'],
    ['pencil', 'Bản nháp', live.filter((l) => !l.isPublished).length, 'chỉ bạn thấy'],
    ['code', 'Bài tập', sum('exerciseCount'), `${sum('assignmentCount')} lượt giao bài`],
  ];
  kpis.replaceChildren(...tiles.map(([ic, label, value, sub]) => el('div', { class: 'ed-kpi' },
    el('span', { class: 'ed-kpi__ic' }, icon(ic)),
    el('div', { class: 'ed-kpi__body' },
      el('p', { class: 'ed-kpi__label' }, label),
      el('p', { class: 'ed-kpi__value mono' }, String(value)),
      el('p', { class: 'ed-kpi__sub' }, sub)))));
}

// ---------------------------------------------------------------- Độ hoàn thiện của một bài (từ dữ liệu danh sách)
function readiness(l) {
  const steps = [
    [!!(l.description || '').trim(), 'Viết mô tả cho bài'],
    [l.exerciseCount > 0, 'Thêm bài tập'],
    [l.publishedExerciseCount > 0, 'Xuất bản ít nhất một bài tập'],
    [l.isPublished, 'Xuất bản bài học'],
  ];
  const done = steps.filter(([ok]) => ok).length;
  const next = steps.find(([ok]) => !ok);
  return { steps, done, next: next ? 'Tiếp theo: ' + next[1].toLowerCase() : l.assignmentCount > 0 ? 'Sẵn sàng, đang được giao' : 'Sẵn sàng để giao bài' };
}

function meter(l) {
  const r = readiness(l);
  return el('div', { class: 'ed-meter' + (r.done === r.steps.length ? ' is-full' : '') },
    el('div', { class: 'ed-meter__head' },
      el('span', { class: 'ed-meter__label' }, 'Độ hoàn thiện'),
      el('span', { class: 'ed-meter__value mono' }, `${r.done}/${r.steps.length}`)),
    el('div', { class: 'ed-meter__track', role: 'img', 'aria-label': `Đã xong ${r.done} trên ${r.steps.length} bước` },
      r.steps.map(([ok]) => el('span', { class: 'ed-meter__seg' + (ok ? ' is-on' : '') }))),
    el('p', { class: 'ed-meter__next' }, icon(r.done === r.steps.length ? 'check' : 'arrow-right'), r.next));
}

function lessonState(l) {
  return l.isArchived ? 'archived' : l.isPublished ? 'published' : 'draft';
}

function render() {
  TABS.forEach((t, i) => {
    tabEls[i].querySelector('.ed-tabs__n').textContent = String(lessons.filter(t.test).length);
    tabEls[i].setAttribute('aria-selected', String(t.key === tab));
    tabEls[i].tabIndex = t.key === tab ? 0 : -1;
  });
  renderKpis();
  list.setAttribute('aria-labelledby', 'tab-' + tab);
  clear(list);
  const shown = visible();
  list.classList.toggle('is-empty', !shown.length);
  if (!shown.length) {
    list.append(emptyState());
    return;
  }

  shown.forEach((lesson, index) => {
    const menu = menuButton({
      text: 'Thêm', label: `Thêm hành động cho bài ${lesson.title}`, end: true,
      items: () => [
        { text: lesson.isPublished ? 'Chuyển về bản nháp' : 'Xuất bản cho học sinh', icon: 'published', onSelect: (b) => togglePublish(lesson, b) },
        { text: lesson.isArchived ? 'Khôi phục' : 'Cất đi (lưu trữ)', icon: 'archive', onSelect: (b) => toggleArchive(lesson, b) },
        { text: 'Đưa lên', icon: 'arrow-up', disabled: index === 0, onSelect: () => move(lesson, -1) },
        { text: 'Đưa xuống', icon: 'arrow-down', disabled: index === shown.length - 1, onSelect: () => move(lesson, 1) },
        { sep: true },
        { text: 'Xóa bài học', icon: 'trash', danger: true, onSelect: (b) => remove(lesson, b) },
      ],
    });
    const edit = '/teacher/lesson-edit.html?id=' + encodeURIComponent(lesson.id);
    list.append(el('article', { class: 'card ed-lcard', 'data-lesson': lesson.id, 'data-state': lessonState(lesson), title: `Tạo bởi ${lesson.createdByName || 'Hệ thống'}` },
      el('div', { class: 'ed-lcard__top' },
        el('span', { class: 'ed-lcard__num' }, el('span', { class: 'ed-lcard__numk' }, 'Bài'), el('span', { class: 'mono' }, String(lesson.order))),
        el('div', { class: 'ed-lcard__chips' }, lesson.isArchived ? stateChip('archived') : null, lesson.isPublished ? stateChip('published') : stateChip('draft'))),
      el('div', { class: 'ed-lcard__main' },
        el('h3', { class: 'ed-lcard__title' }, el('a', { href: edit }, lesson.title)),
        lesson.description ? el('p', { class: 'ed-lcard__desc ed-clamp' }, lesson.description) : el('p', { class: 'ed-lcard__desc muted' }, 'Chưa có mô tả.')),
      el('ul', { class: 'ed-lcard__stats' },
        el('li', {}, icon('code'), el('span', {}, el('strong', { class: 'mono' }, String(lesson.exerciseCount)), ' bài tập',
          lesson.exerciseCount ? el('span', { class: 'muted' }, ` (${lesson.publishedExerciseCount} đã xuất bản)`) : null)),
        el('li', {}, icon('send'), el('span', {}, el('strong', { class: 'mono' }, String(lesson.assignmentCount)), ' lượt giao bài')),
        lesson.updatedAt ? el('li', {}, icon('clock'), el('span', { class: 'muted' }, 'Cập nhật ', el('span', { class: 'mono' }, formatDateTime(lesson.updatedAt)))) : null),
      meter(lesson),
      el('div', { class: 'ed-lcard__actions' },
        el('a', { class: 'btn btn--small btn--primary ed-lcard__edit', href: edit }, icon('pencil'), el('span', {}, 'Soạn bài')),
        el('a', { class: 'btn btn--small', href: '/lesson.html?id=' + encodeURIComponent(lesson.id), 'aria-label': `Xem trước bài ${lesson.title}` }, icon('eye'), el('span', {}, 'Xem trước')),
        el('a', { class: 'btn btn--small', href: '/teacher/assignments.html?lesson=' + encodeURIComponent(lesson.id), 'aria-label': `Giao bài ${lesson.title}` }, icon('send'), el('span', {}, 'Giao bài')),
        menu.root)));
  });
}

kpis.append(...Array.from({ length: 4 }, () => el('div', { class: 'ed-kpi is-loading', 'aria-hidden': 'true' })));
list.setAttribute('aria-busy', 'true');
list.append(skeleton({ shape: 'rows', count: 4, label: 'Đang tải danh sách bài học…' }));
search.input.addEventListener('input', debounce(render, 150));
reload();

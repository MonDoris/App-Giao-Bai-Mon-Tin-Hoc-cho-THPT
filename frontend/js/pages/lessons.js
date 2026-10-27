// Danh sách bài học (học sinh; giáo viên xem như học sinh). Bảng điều khiển đã chuyển sang dashboard.js (QD-64).
import { api, describeError } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { el, clear, stateBox } from '/js/core/dom.js';
import { enableCardEffects, revealOnView } from '/js/core/fx.js';
import { count } from '/js/components/charts.js';
import { lessonCard } from '/js/components/dash-kit.js';

const user = await initPage({ active: 'lessons' });
const isTeacher = user.role === 'Teacher';
const container = document.getElementById('lessons');

if (isTeacher) {
  document.getElementById('preview-banner').append(el('div', { class: 'preview-banner', role: 'status' }, 'Chế độ xem trước – kết quả sẽ không được lưu'));
}

/** Bài nổi bật: bài đầu tiên (theo order) còn bài tập chưa đạt; không có thì không làm nổi bài nào. */
function featuredOf(sorted) {
  if (isTeacher) return sorted[0] || null;
  const total = sorted.reduce((sum, l) => sum + count(l.exerciseCount), 0);
  return sorted.find((l) => count(l.exerciseCount) > 0 && count(l.passedCount) < count(l.exerciseCount)) || (total === 0 ? sorted[0] : null) || null;
}

function renderLessons(lessons) {
  clear(container);
  if (lessons.length === 0) {
    container.append(stateBox({
      kind: 'empty',
      title: 'Chưa có bài học nào',
      message: isTeacher ? 'Hãy tạo bài học ở mục "Bài học của tôi".' : 'Giáo viên chưa xuất bản bài học nào.',
      actions: [
        isTeacher
          ? el('a', { class: 'btn btn--primary', href: '/teacher/index.html' }, 'Bài học của tôi')
          : el('button', { type: 'button', class: 'btn btn--primary', onclick: () => load(true) }, 'Tải lại'),
      ],
    }));
    return;
  }
  const sorted = [...lessons].sort((a, b) => count(a.order) - count(b.order));
  const featured = featuredOf(sorted);
  const grid = el('div', { class: 'lesson-grid' });
  const cards = sorted.map((lesson) => lessonCard(lesson, { featured: lesson === featured, teacher: isTeacher }));
  grid.append(...cards);
  container.append(grid);
  revealOnView(cards);
  enableCardEffects(grid);
}

async function load(refocus = false) {
  container.setAttribute('aria-busy', 'true');
  clear(container);
  container.append(stateBox({ kind: 'loading', title: 'Đang tải danh sách bài học…', shape: 'bento', count: 6 }));
  try {
    const data = await api.lessons();
    renderLessons(data.lessons || []);
  } catch (err) {
    const info = describeError(err);
    clear(container);
    const actions = info.retry ? [el('button', { type: 'button', class: 'btn btn--primary', onclick: () => load(true) }, 'Thử lại')] : [];
    container.append(stateBox({ kind: 'error', title: info.title, message: info.message, actions }));
  } finally {
    container.setAttribute('aria-busy', 'false');
    if (refocus) container.focus({ preventScroll: true });
  }
}

load();

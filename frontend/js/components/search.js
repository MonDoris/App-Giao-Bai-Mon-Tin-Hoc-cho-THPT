// Tìm nhanh (Ctrl+K): các đích điều hướng của role luôn có sẵn; tên bài học chỉ tải ở lần mở đầu tiên bằng API sẵn có
// (GET /api/lessons hoặc GET /api/teacher/lessons) rồi nhớ trong phiên trang. Mọi chữ chèn bằng textContent.
import { api } from '/js/core/api.js';
import { el, clear } from '/js/core/dom.js';
import { icon } from '/js/core/icons.js';
import { showDialog } from '/js/core/modal.js';
import { navGroups } from '/js/components/header.js';

let lessonsPromise = null;

/** Bỏ dấu tiếng Việt và hạ chữ thường để khớp "bai hoc" với "Bài học". */
function fold(text) {
  return String(text ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'd').toLowerCase();
}

function loadLessons(isTeacher) {
  if (!lessonsPromise) {
    const request = isTeacher ? api.teacherLessons(false) : api.lessons();
    lessonsPromise = request.then((data) => data.lessons || [], () => {
      lessonsPromise = null; // lỗi thì lần mở sau thử lại
      return null;
    });
  }
  return lessonsPromise;
}

export function openQuickSearch(user, opener) {
  const isTeacher = user.role === 'Teacher';
  const pages = navGroups(user.role).flatMap((g) => g.items).map((item) => ({ text: item.text, href: item.href, iconName: item.icon, kind: 'Trang' }));
  let lessons = [];
  let state = 'loading';
  let active = 0;
  let shown = [];

  const input = el('input', {
    id: 'quick-search',
    class: 'input',
    type: 'text',
    role: 'combobox',
    autocomplete: 'off',
    'aria-expanded': 'true',
    'aria-controls': 'quick-search-list',
    'aria-autocomplete': 'list',
  });
  const list = el('ul', { id: 'quick-search-list', class: 'app-results', role: 'listbox', 'aria-label': 'Kết quả tìm nhanh' });
  const note = el('p', { class: 'muted app-results__note', role: 'status' });

  function render() {
    const term = fold(input.value.trim());
    const lessonItems = lessons.map((l) => ({
      text: l.title,
      href: (isTeacher ? '/teacher/lesson-edit.html?id=' : '/lesson.html?id=') + encodeURIComponent(l.id),
      iconName: 'book',
      kind: isTeacher ? 'Soạn bài' : 'Bài học',
    }));
    shown = [...pages, ...lessonItems].filter((item) => !term || fold(item.text).includes(term));
    active = Math.min(active, Math.max(0, shown.length - 1));
    clear(list);
    shown.forEach((item, index) => {
      const link = el(
        'a',
        { id: 'qs-' + index, class: 'app-results__item', role: 'option', href: item.href, tabindex: '-1', 'aria-selected': String(index === active) },
        icon(item.iconName),
        el('span', { class: 'app-results__text' }, item.text),
        el('span', { class: 'app-results__kind' }, item.kind),
      );
      link.addEventListener('pointermove', () => setActive(index));
      list.append(el('li', { role: 'presentation' }, link));
    });
    input.setAttribute('aria-activedescendant', shown.length ? 'qs-' + active : '');
    note.textContent = state === 'loading' ? 'Đang tải danh sách bài học…' : state === 'error' ? 'Chưa tải được danh sách bài học' : shown.length ? '' : 'Không tìm thấy kết quả';
  }

  function setActive(index) {
    if (index === active || !shown.length) return;
    list.children[active]?.firstChild.setAttribute('aria-selected', 'false');
    active = index;
    const link = list.children[active].firstChild;
    link.setAttribute('aria-selected', 'true');
    input.setAttribute('aria-activedescendant', link.id);
    link.scrollIntoView({ block: 'nearest' });
  }

  input.addEventListener('input', () => {
    active = 0;
    render();
  });
  input.addEventListener('keydown', (event) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (shown.length) setActive((active + (event.key === 'ArrowDown' ? 1 : -1) + shown.length) % shown.length);
    } else if (event.key === 'Enter' && shown.length) {
      event.preventDefault();
      list.children[active].firstChild.click();
    }
  });

  render();
  loadLessons(isTeacher).then((data) => {
    if (data) lessons = data;
    state = data ? 'ready' : 'error';
    render();
  });

  return showDialog({
    title: 'Tìm nhanh',
    content: el('div', {}, el('label', { for: 'quick-search', class: 'field-label' }, 'Tìm trang hoặc bài học'), input, list, note),
    buttons: [{ text: 'Đóng', value: false }],
    opener,
    describe: false,
    focusFirst: () => {
      input.focus();
      return input;
    },
  });
}

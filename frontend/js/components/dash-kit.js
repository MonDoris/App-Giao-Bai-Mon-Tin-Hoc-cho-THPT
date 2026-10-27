// Thành phần dùng chung của hai trang Tổng quan và danh sách bài học: nguồn dữ liệu độc lập có skeleton/lỗi/thử lại,
// thẻ KPI, thẻ bài học. Mọi chữ từ API chèn bằng textContent.
import { describeError } from '/js/core/api.js';
import { el, clear, stateBox } from '/js/core/dom.js';
import { icon, stateChip } from '/js/core/ui.js';
import { count } from '/js/components/charts.js';

export function setSlot(slot, ...children) {
  clear(slot);
  slot.append(...children.flat().filter(Boolean));
}

/** Khung xương một thẻ (phần hình aria-hidden; thông báo đọc nằm ở vùng role=status của trang). */
export function skCard() {
  return el(
    'div',
    { class: 'dash-card dash-card--sk', 'aria-hidden': 'true' },
    el('span', { class: 'sk sk--pill' }),
    el('span', { class: 'sk sk--title' }),
    el('span', { class: 'sk sk--line' }),
  );
}

/**
 * Một nguồn dữ liệu độc lập cho nhiều khối (slots). run() hiện skeleton, gọi load(), rồi render(data);
 * lỗi thì mỗi khối hiện stateBox lỗi với nút "Thử lại" chỉ chạy lại nguồn này (không tải lại trang).
 * quiet: các khối chỉ ẩn khi lỗi (không hiện hộp lỗi riêng). Trả về Promise<{ ok, data }>.
 */
export function createSource({ slots, quiet = [], load, render }) {
  async function run() {
    slots.forEach((slot) => {
      slot.hidden = false;
      slot.setAttribute('aria-busy', 'true');
      setSlot(slot, skCard());
    });
    let result;
    try {
      const data = await load();
      slots.forEach((slot) => clear(slot));
      render(data);
      result = { ok: true, data };
    } catch (err) {
      const info = describeError(err);
      const actions = info.retry ? [el('button', { type: 'button', class: 'btn btn--primary', onclick: () => run() }, 'Thử lại')] : [];
      slots.forEach((slot) => {
        if (quiet.includes(slot)) {
          clear(slot);
          slot.hidden = true;
        } else {
          setSlot(slot, stateBox({ kind: 'error', title: info.title, message: info.message, actions }));
        }
      });
      result = { ok: false, data: null };
    }
    slots.forEach((slot) => slot.removeAttribute('aria-busy'));
    return result;
  }
  return run;
}

/**
 * Thẻ KPI: nhãn (h2), hình/số lớn, dòng phụ, liên kết tùy chọn và dòng "Tính từ: ..." cho biết số đến từ đâu.
 * tone: '' | 'danger' (tông cảnh báo kèm biểu tượng và chữ, không chỉ màu).
 */
export function kpiCard({ label, iconName, visual = null, value = null, sub = null, link = null, source, tone = '' }) {
  return el(
    'article',
    { class: 'dash-card dash-kpi' + (tone ? ' dash-card--' + tone : '') },
    el('h2', { class: 'dash-card__label' }, icon(iconName), label),
    value !== null ? el('p', { class: 'dash-kpi__value mono' }, String(value)) : null,
    visual,
    ...[].concat(sub || []).map((text) => (text instanceof Node ? text : el('p', { class: 'dash-kpi__sub' }, text))),
    link ? el('a', { class: 'dash-kpi__link', href: link.href }, link.text, icon('arrow-right')) : null,
    el('p', { class: 'dash-card__source' }, 'Tính từ: ' + source),
  );
}

/** Thẻ bài học (dùng lại .lesson-card). level: cấp tiêu đề của tên bài (h2 ở danh sách, h3 khi nằm dưới h2 của Tổng quan). */
export function lessonCard(lesson, { featured = false, teacher = false, level = 'h2' } = {}) {
  const exerciseCount = count(lesson.exerciseCount);
  const passedCount = count(lesson.passedCount);
  const completed = !teacher && exerciseCount > 0 && passedCount >= exerciseCount;
  const meta = teacher
    ? `${exerciseCount} bài tập`
    : exerciseCount > 0
      ? `${passedCount}/${exerciseCount} bài tập đã đạt`
      : 'Chưa có bài tập được giao';
  const bar = !teacher && exerciseCount > 0 ? el('span', { class: 'lesson-card__bar', 'aria-hidden': 'true' }, el('span')) : null;
  if (bar) bar.firstChild.style.setProperty('--p', String(Math.min(1, passedCount / exerciseCount)));
  return el(
    'a',
    { class: 'lesson-card' + (featured ? ' lesson-card--featured' : ''), href: '/lesson.html?id=' + encodeURIComponent(lesson.id) },
    el(
      'div',
      { class: 'lesson-card__top' },
      el('span', { class: 'lesson-card__num', 'aria-hidden': 'true' }, String(lesson.order ?? '')),
      completed ? el('span', { class: 'badge badge--done' }, icon('check'), 'Đã hoàn thành') : null,
      teacher && lesson.isPublished === false ? stateChip('draft') : null,
      teacher && lesson.isArchived ? stateChip('archived') : null,
    ),
    featured ? el('p', { class: 'lesson-card__eyebrow' }, 'Bài tiếp theo') : null,
    el(level, { class: 'lesson-card__title' }, lesson.title),
    el('p', { class: 'lesson-card__desc' }, lesson.description),
    el('span', { class: 'lesson-card__meta' }, meta),
    bar,
  );
}

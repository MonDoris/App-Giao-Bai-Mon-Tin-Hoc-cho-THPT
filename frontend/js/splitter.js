// Thanh kéo chia đôi khung đề bài / khung code (AC-239..243, QD-55).
// Chuột/cảm ứng: pointer capture trên chính thanh kéo (không listener window/document). Bàn phím: mũi tên, Home, End, Enter.
// Tỉ lệ (30-70, mặc định 50) ghi vào biến CSS --split của .workspace và lưu qua uiPrefs sau 300 ms.
import { el } from '/js/dom.js';
import { uiPrefs } from '/js/storage.js';

const MIN = 30;
const MAX = 70;
const DEFAULT = 50;
const SAVE_DELAY = 300;

const clamp = (n) => Math.min(MAX, Math.max(MIN, n));

/**
 * Tạo thanh kéo cho `workspace` (lưới 3 phần: khung đề bài | thanh kéo | khung code).
 * Trả về { element, destroy }; element phải được chèn giữa hai khung; gọi destroy() ở pagehide để lưu nốt tỉ lệ.
 */
export function createSplitter({ workspace }) {
  let value = clamp(uiPrefs.getSplit());
  let dragging = false;
  let startLeft = 0;
  let trackWidth = 1;
  let saveTimer = 0;

  const element = el('div', {
    class: 'splitter',
    role: 'separator',
    tabindex: '0',
    'aria-orientation': 'vertical',
    'aria-label': 'Kéo để đổi độ rộng khung đề bài và khung code',
    'aria-valuemin': String(MIN),
    'aria-valuemax': String(MAX),
  }, el('span', { class: 'splitter__grip', 'aria-hidden': 'true' }));

  function paint() {
    const whole = Math.round(value);
    workspace.style.setProperty('--split', String(value));
    element.setAttribute('aria-valuenow', String(whole));
    element.setAttribute('aria-valuetext', `Đề bài chiếm ${whole} phần trăm`);
  }

  function scheduleSave() {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(flush, SAVE_DELAY);
  }

  function flush() {
    if (!saveTimer) return;
    clearTimeout(saveTimer);
    saveTimer = 0;
    uiPrefs.setSplit(Math.round(value));
  }

  function setValue(next, persist) {
    value = clamp(next);
    paint();
    if (persist) scheduleSave();
  }

  function endDrag(event) {
    if (!dragging) return;
    dragging = false;
    workspace.classList.remove('is-dragging');
    if (event && event.pointerId !== undefined && element.hasPointerCapture && element.hasPointerCapture(event.pointerId)) {
      element.releasePointerCapture(event.pointerId);
    }
    value = Math.round(value);
    paint();
    scheduleSave();
  }

  element.addEventListener('pointerdown', (event) => {
    if (event.button !== undefined && event.button !== 0) return;
    const first = element.previousElementSibling;
    const second = element.nextElementSibling;
    if (!first || !second) return;
    const a = first.getBoundingClientRect();
    const b = second.getBoundingClientRect();
    const grip = element.getBoundingClientRect();
    startLeft = a.left;
    trackWidth = Math.max(1, b.right - a.left - grip.width);
    dragging = true;
    workspace.classList.add('is-dragging');
    element.setPointerCapture(event.pointerId);
    event.preventDefault();
  });

  element.addEventListener('pointermove', (event) => {
    if (!dragging) return;
    const grip = element.offsetWidth;
    const ratio = (event.clientX - startLeft - grip / 2) / trackWidth;
    setValue(ratio * 100, false);
  });

  element.addEventListener('pointerup', endDrag);
  element.addEventListener('pointercancel', endDrag);
  element.addEventListener('lostpointercapture', endDrag);

  element.addEventListener('keydown', (event) => {
    const step = event.shiftKey ? 10 : 2;
    let next = null;
    if (event.key === 'ArrowLeft') next = Math.round(value) - step;
    else if (event.key === 'ArrowRight') next = Math.round(value) + step;
    else if (event.key === 'Home') next = MIN;
    else if (event.key === 'End') next = MAX;
    else if (event.key === 'Enter') next = DEFAULT;
    if (next === null) return;
    event.preventDefault();
    setValue(next, true);
  });

  paint();

  function destroy() {
    flush();
    clearTimeout(saveTimer);
    dragging = false;
    workspace.classList.remove('is-dragging');
  }

  return { element, destroy };
}

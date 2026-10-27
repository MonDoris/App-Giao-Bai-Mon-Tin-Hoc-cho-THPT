import { el } from '/js/dom.js';

let counter = 0;
let openLayers = 0;

function reducedMotion() {
  return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Hộp thoại tổng quát, thân thiện bàn phím và trình đọc màn hình (AC-37, AC-147):
 * role="dialog", aria-modal, tiêu điểm vào phần tử đầu tiên được chọn (mặc định nút đầu) và trả về phần tử gọi khi đóng;
 * Tab quay vòng trong hộp thoại; Esc hoặc bấm nền ngoài = đóng với giá trị closeValue.
 *
 * buttons: [{ text, value, kind: 'primary'|'danger'|'', action?: () => (boolean|void|Promise<boolean|void>) }]
 *   action trả về false thì hộp thoại giữ nguyên (dùng để kiểm tra biểu mẫu).
 * Trả về Promise<value của nút được chọn | closeValue>.
 */
export function showDialog({ title, content, buttons, opener = null, wide = false, closeValue = false, focusFirst = null, describe = true }) {
  return new Promise((resolve) => {
    const id = ++counter;
    const returnFocusTo = opener || document.activeElement;
    const titleId = `modal-title-${id}`;
    const descId = `modal-desc-${id}`;

    const buttonEls = buttons.map((b) => el('button', {
      type: 'button',
      class: 'btn ' + (b.kind === 'primary' ? 'btn--primary' : b.kind === 'danger' ? 'btn--danger' : ''),
      'data-action': String(b.value),
    }, b.text));
    const body = el('div', { id: descId, class: 'modal__body' }, content);
    const dialog = el(
      'div',
      {
        class: 'modal' + (wide ? ' modal--wide' : ''),
        role: 'dialog',
        'aria-modal': 'true',
        'aria-labelledby': titleId,
        'aria-describedby': describe ? descId : null,
      },
      el('h2', { id: titleId }, title),
      body,
      el('div', { class: 'modal__actions' }, buttonEls),
    );
    const backdrop = el('div', { class: 'modal-backdrop' }, dialog);

    // Làm các phần còn lại của trang không tương tác được khi hộp thoại mở.
    const inerted = [];
    for (const child of Array.from(document.body.children)) {
      // Vùng toast không bị inert để thông báo vẫn được đọc (AC-274)
      if (child.id !== 'toast-region' && !child.hasAttribute('inert')) {
        child.setAttribute('inert', '');
        inerted.push(child);
      }
    }

    let closed = false;
    const close = (result) => {
      if (closed) return;
      closed = true;
      document.removeEventListener('keydown', onKeydown, true);
      // Hoạt ảnh đóng (<= 150 ms) chạy sau; Promise và tiêu điểm trả về ngay (AC-265)
      backdrop.classList.add('is-closing');
      setTimeout(() => backdrop.remove(), reducedMotion() ? 0 : 150);
      inerted.forEach((n) => n.removeAttribute('inert'));
      openLayers = Math.max(0, openLayers - 1);
      if (openLayers === 0) document.documentElement.classList.remove('has-modal');
      if (returnFocusTo && typeof returnFocusTo.focus === 'function') returnFocusTo.focus();
      resolve(result);
    };

    const focusables = () => Array.from(dialog.querySelectorAll(FOCUSABLE)).filter((n) => !n.hidden && n.offsetParent !== null);

    const onKeydown = (event) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        close(closeValue);
      } else if (event.key === 'Tab') {
        const list = focusables();
        if (!list.length) return;
        const index = list.indexOf(document.activeElement);
        event.preventDefault();
        if (event.shiftKey) list[index <= 0 ? list.length - 1 : index - 1].focus();
        else list[(index + 1) % list.length].focus();
      }
    };

    buttons.forEach((b, i) => {
      buttonEls[i].addEventListener('click', async () => {
        if (b.action) {
          buttonEls[i].disabled = true;
          let keep = false;
          try {
            keep = (await b.action()) === false;
          } finally {
            buttonEls[i].disabled = false;
          }
          if (keep) return;
        }
        close(b.value);
      });
    });
    backdrop.addEventListener('mousedown', (event) => {
      if (event.target === backdrop) close(closeValue);
    });

    document.body.append(backdrop);
    openLayers += 1;
    document.documentElement.classList.add('has-modal');
    document.addEventListener('keydown', onKeydown, true);
    (focusFirst ? focusFirst() : null) || buttonEls[0].focus();
  });
}

/**
 * Hộp thoại xác nhận. Trả về Promise<boolean>: true nếu chọn nút xác nhận; false nếu Hủy / Esc / bấm nền ngoài.
 * Tiêu điểm vào nút "Hủy" khi mở và trả về phần tử gọi (opener) khi đóng; Tab quay vòng trong hộp thoại.
 */
export function confirmDialog({ title, message, confirmText = 'Đồng ý', cancelText = 'Hủy', opener = null, danger = false }) {
  return showDialog({
    title,
    content: el('p', {}, message),
    buttons: [
      { text: cancelText, value: false },
      { text: confirmText, value: true, kind: danger ? 'danger' : 'primary' },
    ],
    opener,
    closeValue: false,
  });
}

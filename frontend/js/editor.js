import { el } from './dom.js';

/**
 * Trình soạn code nhẹ: textarea monospace + cột số dòng; phím Tab chèn 4 khoảng trắng.
 * Nhấn Esc rồi Tab để rời khỏi ô soạn thảo bằng bàn phím (đảm bảo thao tác được bằng bàn phím).
 */
export class CodeEditor {
  constructor({ label = 'Trình soạn code Python', onChange = () => {} } = {}) {
    this.onChange = onChange;
    this.releaseTab = false;

    this.gutter = el('div', { class: 'editor__gutter', 'aria-hidden': 'true' }, '1');
    this.textarea = el('textarea', {
      class: 'editor__input',
      spellcheck: 'false',
      autocapitalize: 'off',
      autocomplete: 'off',
      autocorrect: 'off',
      wrap: 'off',
      'aria-label': label,
      'aria-describedby': 'editor-hint',
    });
    // Thanh tiêu đề kiểu IDE: 3 chấm trang trí + tên tệp (AC-244)
    this.bar = el(
      'div',
      { class: 'editor__bar' },
      el('span', { class: 'editor__dots', 'aria-hidden': 'true' }, el('i'), el('i'), el('i')),
      el('span', { class: 'editor__file' }, 'main.py'),
    );
    this.root = el('div', { class: 'editor' }, this.bar, el('div', { class: 'editor__body' }, this.gutter, this.textarea));

    this.textarea.addEventListener('input', () => {
      this.#refreshGutter();
      this.onChange(this.value);
    });
    this.textarea.addEventListener('scroll', () => {
      this.gutter.scrollTop = this.textarea.scrollTop;
    });
    this.textarea.addEventListener('keydown', (e) => this.#onKeydown(e));
    this.textarea.addEventListener('blur', () => { this.releaseTab = false; });
  }

  get value() {
    return this.textarea.value;
  }

  set value(text) {
    this.textarea.value = text;
    this.#refreshGutter();
  }

  focus() {
    this.textarea.focus();
  }

  #refreshGutter() {
    const lines = this.textarea.value.split('\n').length;
    let text = '';
    for (let i = 1; i <= lines; i++) text += (i > 1 ? '\n' : '') + i;
    this.gutter.textContent = text;
    this.gutter.scrollTop = this.textarea.scrollTop;
  }

  #onKeydown(e) {
    if (e.key === 'Escape') {
      this.releaseTab = true;
      return;
    }
    if (e.key === 'Tab' && !e.ctrlKey && !e.altKey && !e.metaKey) {
      if (this.releaseTab) {
        this.releaseTab = false;
        return; // cho phép Tab chuyển tiêu điểm sau khi nhấn Esc
      }
      e.preventDefault();
      const ta = this.textarea;
      const { selectionStart: start, selectionEnd: end, value } = ta;
      if (e.shiftKey) {
        // Bỏ tối đa 4 khoảng trắng đầu dòng hiện tại
        const lineStart = value.lastIndexOf('\n', start - 1) + 1;
        const m = /^ {1,4}/.exec(value.slice(lineStart));
        if (m) {
          ta.setRangeText('', lineStart, lineStart + m[0].length, 'preserve');
          ta.selectionStart = Math.max(lineStart, start - m[0].length);
          ta.selectionEnd = Math.max(lineStart, end - m[0].length);
        }
      } else {
        ta.setRangeText('    ', start, end, 'end');
      }
      ta.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    }
    this.releaseTab = false;
  }
}

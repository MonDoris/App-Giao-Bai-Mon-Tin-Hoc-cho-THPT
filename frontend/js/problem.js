import { api, ApiError, describeError } from '/js/api.js';
import { initPage } from '/js/auth.js';
import { el, clear, stateBox, renderText, difficultyInfo, spinner, codeBlockEl } from '/js/dom.js';
import { icon } from '/js/icons.js';
import { createSplitter } from '/js/splitter.js';
import { drafts } from '/js/storage.js';
import { confirmDialog, showDialog } from '/js/modal.js';
import { CodeEditor } from '/js/editor.js';
import { PyRunner } from '/js/runner.js';
import { classify, STATUS } from '/js/grader.js';
import { formatDateTime, lateChip, overdueChip, setTitle, stateChip, statusChip } from '/js/ui.js';

const user = await initPage({ active: 'lessons' });
// Giáo viên mở ở chế độ XEM TRƯỚC: thấy cả bản nháp, chạy/nộp thử nhưng KHÔNG ghi kết quả lên máy chủ (QD-34).
const preview = user.role === 'Teacher';
const homeHref = preview ? '/teacher/index.html' : '/lessons.html';

const main = document.getElementById('main');
const root = document.getElementById('problem-root');
const params = new URLSearchParams(location.search);
const lessonId = params.get('lesson') || '';
const problemId = params.get('problem') || '';

let runner = null;

if (preview) {
  document.getElementById('preview-banner').append(
    el('div', { class: 'preview-banner', role: 'status' }, 'Chế độ xem trước – kết quả sẽ không được lưu'),
  );
}

function showFatal({ title, message, retry = null }) {
  clear(root);
  const actions = [];
  if (retry) actions.push(el('button', { type: 'button', class: 'btn btn--primary', onclick: retry }, 'Thử lại'));
  actions.push(el('a', { class: 'btn', href: homeHref }, 'Về danh sách bài học'));
  root.append(el('div', { class: 'container container--narrow' }, stateBox({ kind: 'error', title, message, actions })));
}

// ---------------------------------------------------------------- Đề bài
function exampleBlock(ex, index) {
  const block = el(
    'div',
    { class: 'example' },
    el(
      'div',
      { class: 'example__grid' },
      el('div', { class: 'example__col' }, el('div', { class: 'example__label' }, 'Input'), el('pre', {}, ex.input)),
      el('div', { class: 'example__col' }, el('div', { class: 'example__label' }, 'Output'), el('pre', {}, ex.output)),
    ),
  );
  if (ex.explanation) {
    block.append(el('div', { class: 'example__expl' }, el('strong', {}, `Giải thích ví dụ ${index + 1}: `), ex.explanation));
  }
  return block;
}

/** Trạng thái hiển thị cạnh tiêu đề đề bài: học sinh lấy từ máy chủ; giáo viên xem nhãn bản nháp/xuất bản. */
function buildStatusArea(ex, state) {
  const host = el('span', { id: 'status-badge', class: 'status-area' });
  const paint = () => {
    clear(host);
    if (preview) {
      host.append(ex.isPublished === false ? stateChip('draft') : stateChip('published'));
      return;
    }
    host.append(statusChip(state.status));
    if (state.viewedSolution) host.append(' ', el('span', { class: 'badge badge--seen' }, 'Đã xem đáp án'));
    if (state.status === 'Passed' && state.passedLate) host.append(' ', lateChip('Nộp muộn'));
    else if (state.isOverdue) host.append(' ', overdueChip());
  };
  paint();
  return { host, paint };
}

function buildStatementPane(ex, lesson, state, statusArea) {
  const d = difficultyInfo(ex.difficulty);
  const body = el('div', { class: 'pane__body statement', tabindex: '0', 'aria-label': 'Nội dung đề bài' });
  body.append(
    el('h3', { style: 'margin-top:0;font-size:1.4rem' }, ex.title),
    el('div', { class: 'meta' }, el('span', { class: 'badge ' + d.cls }, d.label), statusArea.host),
  );
  if (!preview && ex.assignment) {
    if (ex.assignment.dueAt) {
      body.append(el('p', { class: 'small' }, el('strong', {}, 'Hạn nộp: '), formatDateTime(ex.assignment.dueAt)));
    }
    if (state.isOverdue || (ex.assignment.dueAt && Date.parse(ex.assignment.dueAt) < Date.now() && state.status !== 'Passed')) {
      body.append(el('div', { class: 'alert alert--warn', role: 'status', style: 'margin:0 0 12px' }, 'Đã quá hạn nộp – bài vẫn được ghi nhận là nộp muộn'));
    }
    if (ex.assignment.note) {
      body.append(el('p', { class: 'small' }, el('strong', {}, 'Ghi chú của giáo viên: '), ex.assignment.note));
    }
  }
  body.append(
    renderText(ex.statement),
    el('h3', {}, 'Dữ liệu vào'),
    renderText(ex.inputFormat),
    el('h3', {}, 'Dữ liệu ra'),
    renderText(ex.outputFormat),
  );
  if (ex.constraints && ex.constraints.trim()) {
    body.append(el('h3', {}, 'Ràng buộc'), renderText(ex.constraints));
  }
  body.append(el('h3', {}, 'Ví dụ'), ...ex.examples.map(exampleBlock));
  return el('section', { class: 'pane', 'aria-labelledby': 'statement-heading' }, el('div', { class: 'pane__head' }, el('h2', { id: 'statement-heading', style: 'margin:0;font-size:1.05rem' }, 'Đề bài')), body);
}

// ---------------------------------------------------------------- Màn hình làm bài
function buildWorkspace(ex, lesson, state, statusArea) {
  const limitMs = ex.timeLimitMs || 5000;
  const limitSeconds = Math.round(limitMs / 1000);

  const editor = new CodeEditor({ onChange: () => scheduleSave() });
  const stdin = el('textarea', { class: 'stdin', id: 'stdin', spellcheck: 'false', rows: '3', placeholder: 'Mỗi dòng là một lần input() đọc được' });
  const runStatus = el('div', { class: 'runner-status', id: 'runner-status', role: 'status' });
  const results = el('div', { class: 'results', id: 'results', 'aria-live': 'polite' });
  const sendArea = el('div', { id: 'send-area', 'aria-live': 'polite' });
  const solutionArea = el('div', { id: 'solution-area' });
  const historyArea = el('div', { id: 'history-area' });

  const btnRun = el('button', { type: 'button', class: 'btn btn--primary', id: 'btn-run' }, 'Chạy');
  const btnSubmit = el('button', { type: 'button', class: 'btn btn--success', id: 'btn-submit' }, 'Nộp bài');
  const btnStop = el('button', { type: 'button', class: 'btn btn--danger', id: 'btn-stop' }, 'Dừng');
  const btnSolution = ex.hasSolution ? el('button', { type: 'button', class: 'btn', id: 'btn-solution' }, 'Đáp án') : null;
  const btnReset = el('button', { type: 'button', class: 'btn btn--ghost', id: 'btn-reset' }, 'Đặt lại code');

  const toolbar = el('div', { class: 'toolbar', role: 'group', 'aria-label': 'Thao tác với code' }, btnRun, btnSubmit, btnStop, btnSolution, btnReset);

  // Khung dưới: tab "Kết quả | Đầu vào" (AC-246, QD-54). Panel không chọn chỉ bị `hidden`, vẫn còn trong DOM.
  const tabResults = el('button', { type: 'button', class: 'tab', role: 'tab', id: 'tab-results', 'aria-selected': 'true', 'aria-controls': 'panel-results', tabindex: '0' }, 'Kết quả');
  const tabInput = el('button', { type: 'button', class: 'tab', role: 'tab', id: 'tab-input', 'aria-selected': 'false', 'aria-controls': 'panel-input', tabindex: '-1' }, 'Đầu vào');
  const panelResults = el('div', { role: 'tabpanel', id: 'panel-results', class: 'tabpanel', 'aria-labelledby': 'tab-results' },
    el('h3', { class: 'results__title' }, 'Kết quả'),
    results,
    sendArea,
    solutionArea,
    preview ? null : historyArea,
  );
  const panelInput = el('div', { role: 'tabpanel', id: 'panel-input', class: 'tabpanel', 'aria-labelledby': 'tab-input', hidden: true },
    el('label', { class: 'field-label', for: 'stdin' }, 'Dữ liệu vào (stdin)'),
    stdin,
  );
  // Khi đang xem tab "Đầu vào", tiến độ chấm vẫn được đọc qua vùng này (AC-247).
  const liveAnnounce = el('div', { class: 'sr-only', role: 'status', 'aria-live': 'polite' });
  const tabs = [
    { tab: tabResults, panel: panelResults },
    { tab: tabInput, panel: panelInput },
  ];
  function selectTab(name, focus = false) {
    tabs.forEach(({ tab, panel }) => {
      const on = tab === (name === 'input' ? tabInput : tabResults);
      tab.setAttribute('aria-selected', on ? 'true' : 'false');
      tab.tabIndex = on ? 0 : -1;
      panel.hidden = !on;
      if (on && focus) tab.focus();
    });
  }
  function announce(text) {
    liveAnnounce.textContent = panelResults.hidden ? text : '';
  }
  tabs.forEach(({ tab }, index) => {
    tab.addEventListener('click', () => selectTab(index === 0 ? 'results' : 'input'));
    tab.addEventListener('keydown', (event) => {
      let next = null;
      if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
      else if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
      else if (event.key === 'Home') next = 0;
      else if (event.key === 'End') next = tabs.length - 1;
      if (next === null) return;
      event.preventDefault();
      selectTab(next === 0 ? 'results' : 'input', true);
    });
  });

  const codeBody = el(
    'div',
    { class: 'pane__body' },
    runStatus,
    el('label', { class: 'field-label', style: 'margin-top:0' }, 'Mã Python'),
    editor.root,
    el('p', { class: 'hint', id: 'editor-hint' }, 'Phím Tab chèn 4 khoảng trắng. Nhấn Esc rồi Tab để rời khỏi ô soạn code.'),
    toolbar,
    el('div', { class: 'tabs', role: 'tablist', 'aria-label': 'Kết quả và dữ liệu vào' }, tabResults, tabInput),
    panelResults,
    panelInput,
    liveAnnounce,
  );
  const codePane = el(
    'section',
    { class: 'pane', 'aria-labelledby': 'code-heading' },
    el('div', { class: 'pane__head' }, el('h2', { id: 'code-heading', style: 'margin:0;font-size:1.05rem' }, 'Trình soạn code')),
    codeBody,
  );

  // ---------- Khôi phục / lưu bản nháp code (khóa theo userId)
  const saved = drafts.getCode(ex.id);
  editor.value = saved ? saved : ex.starterCode;
  let saveTimer = null;
  function saveNow() {
    clearTimeout(saveTimer);
    saveTimer = null;
    drafts.saveCode(ex.id, editor.value);
  }
  function scheduleSave() {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(saveNow, 800);
  }
  window.addEventListener('pagehide', () => { if (saveTimer) saveNow(); });
  document.addEventListener('visibilitychange', () => { if (document.hidden && saveTimer) saveNow(); });

  // ---------- Trạng thái nút
  const ui = { runner: 'loading', busy: null }; // busy: null | 'run' | 'submit'
  function refreshButtons() {
    const ready = ui.runner === 'ready';
    btnRun.disabled = !ready || ui.busy !== null;
    btnSubmit.disabled = !ready || ui.busy !== null;
    btnStop.disabled = ui.busy === null || ui.busy === 'sending';
    btnRun.textContent = ui.busy === 'run' ? 'Đang chạy…' : 'Chạy';
    btnSubmit.textContent = ui.busy === 'submit' ? 'Đang chấm…' : ui.busy === 'sending' ? 'Đang lưu…' : 'Nộp bài';
    if (ui.busy === 'run') btnRun.setAttribute('aria-busy', 'true');
    else btnRun.removeAttribute('aria-busy');
    if (ui.busy === 'submit' || ui.busy === 'sending') btnSubmit.setAttribute('aria-busy', 'true');
    else btnSubmit.removeAttribute('aria-busy');
  }

  function setRunnerStatus(status) {
    ui.runner = status;
    clear(runStatus);
    runStatus.classList.remove('runner-status--error');
    if (status === 'ready') {
      runStatus.hidden = true;
    } else if (status === 'error') {
      runStatus.hidden = false;
      runStatus.classList.add('runner-status--error');
      runStatus.append(
        el('span', {}, 'Không tải được trình chạy Python. Hãy kiểm tra kết nối Internet.'),
        el('button', { type: 'button', class: 'btn', onclick: () => runner.start() }, 'Thử lại'),
      );
    } else {
      runStatus.hidden = false;
      runStatus.append(spinner(), el('span', {}, status === 'restarting' ? 'Đang khởi động lại trình chạy Python…' : 'Đang tải trình chạy Python…'));
    }
    refreshButtons();
  }

  runner = new PyRunner({ onState: setRunnerStatus });

  // ---------- Hiển thị kết quả
  function errorText(err) {
    if (err.type === 'EOFError') return 'Chương trình cần thêm dữ liệu vào';
    const where = err.line ? `Lỗi ở dòng ${err.line}: ` : 'Lỗi: ';
    return `${where}${err.type}: ${err.message}`;
  }

  function renderRun(res) {
    clear(results);
    if (res.stopped) {
      results.append(el('div', { class: 'alert alert--info' }, 'Đã dừng chương trình.'));
      return;
    }
    if (res.timedOut) {
      results.append(el('div', { class: 'alert alert--error' }, `Quá thời gian (${limitSeconds} giây)`));
      return;
    }
    if (res.output) results.append(el('pre', { class: 'output' }, res.output));
    else if (!res.error) results.append(el('pre', { class: 'output output--empty' }, '(Chương trình không in ra gì)'));
    if (res.error) results.append(el('div', { class: 'alert alert--error', role: 'alert' }, icon('alert', 'alert__icon'), ' ' + errorText(res.error)));
    else results.append(el('p', { class: 'hint' }, `Chạy xong trong ${res.ms} ms.`));
  }

  async function guardedRun(code, input) {
    try {
      return await runner.run(code, input, limitMs);
    } catch (e) {
      return null; // runner không khả dụng; trạng thái đã được báo qua setRunnerStatus
    }
  }

  // Nút Chạy KHÔNG BAO GIỜ gửi gì lên máy chủ (AC-136).
  async function onRun() {
    if (ui.busy) return;
    const code = editor.value;
    if (!code.trim()) {
      selectTab('results');
      clear(results);
      results.append(el('div', { class: 'alert alert--warn' }, 'Bạn chưa viết code'));
      return;
    }
    selectTab('results');
    ui.busy = 'run';
    refreshButtons();
    clear(results);
    results.append(el('p', { class: 'muted' }, spinner(), ' Đang chạy…'));
    const res = await guardedRun(code, stdin.value);
    ui.busy = null;
    refreshButtons();
    if (res) renderRun(res);
    else clear(results);
  }

  function testRow(index, tc) {
    const name = el('span', { class: 'testitem__name' }, `Test ${index + 1}${tc.isHidden ? ' (ẩn)' : ''}`);
    const status = el('span', { class: 'status status--pending' }, icon('circle'), 'Đang chờ');
    const time = el('span', { class: 'testitem__time' }, '');
    const li = el('li', { class: 'testitem' }, el('div', { class: 'testitem__row' }, name, status, time));
    li.style.setProperty('--i', String(Math.min(index, 9)));
    return { li, status, time };
  }

  function detailBlock(label, text) {
    return el('div', {}, el('h4', {}, label), el('pre', {}, text === '' ? '(trống)' : text));
  }

  const STATUS_ICONS = { pass: 'check', wrong: 'x', error: 'alert', timeout: 'clock' };

  function fillRow(row, tc, status, res) {
    row.status.className = 'status status--' + status.code;
    clear(row.status);
    row.status.append(icon(STATUS_ICONS[status.code] || 'circle'), status.label);
    row.time.textContent = status.code === 'timeout' ? '' : `${res.ms} ms`;
    // Test ẩn: chỉ báo trạng thái, không lộ input/đầu ra mong đợi (kể cả trong chế độ xem trước của giáo viên, giống học sinh).
    if (tc.isHidden) return;
    if (status.code === 'wrong') {
      row.li.append(el('div', { class: 'testitem__detail' },
        detailBlock('Input', tc.input),
        detailBlock('Đầu ra mong đợi', tc.expectedOutput),
        detailBlock('Đầu ra của bạn', res.output),
      ));
    } else if (status.code === 'error' && res.error) {
      row.li.append(el('div', { class: 'testitem__detail' },
        detailBlock('Input', tc.input),
        el('div', { class: 'alert alert--error', style: 'margin:0' }, icon('alert', 'alert__icon'), ' ' + errorText(res.error)),
      ));
    }
  }

  /** Huy hiệu kết luận: dấu tích vẽ bằng stroke-dashoffset (Đạt) hoặc dấu x (Sai), trang trí (AC-248, AC-249). */
  function verdictBadge(name) {
    const svg = icon(name, 'verdict__mark');
    svg.querySelectorAll('path').forEach((p) => p.setAttribute('pathLength', '1'));
    return el('span', { class: 'verdict__badge', 'aria-hidden': 'true' }, svg);
  }

  // ---------- Gửi kết quả lên máy chủ (chỉ khi Nộp bài, chỉ học sinh)
  const RESULT_NAMES = { pass: 'Passed', wrong: 'Wrong', error: 'Error', timeout: 'Timeout' };
  let pending = null; // kết quả đã chấm nhưng chưa gửi được: giữ trong bộ nhớ trang để "Gửi lại" không phải chấm lại

  function sendMessage(kind, text, button = null) {
    clear(sendArea);
    sendArea.append(el('div', { class: 'alert alert--' + kind, role: kind === 'error' ? 'alert' : 'status' }, text, button ? ' ' : null, button));
  }

  function applyServerResult(r) {
    state.attempts = (state.attempts || 0) + 1;
    state.bestPassedTests = Math.max(state.bestPassedTests || 0, pending ? pending.passedTests : 0);
    if (r.isPassed) {
      state.status = 'Passed';
      if (r.isLate) state.passedLate = !state.passedOnTime;
      else { state.passedLate = false; state.passedOnTime = true; }
    } else if (state.status === 'NotStarted') {
      state.status = 'InProgress';
    }
    statusArea.paint();
  }

  async function sendResult() {
    if (!pending) return;
    ui.busy = 'sending';
    refreshButtons();
    sendMessage('info', 'Đang lưu kết quả lên hệ thống…');
    try {
      const r = await api.submit(ex.id, pending);
      sendMessage('ok', `Đã lưu kết quả lên hệ thống (lần nộp thứ ${r.attemptNo})${r.isLate ? ' – ghi nhận nộp muộn' : ''}.`);
      applyServerResult(r);
      pending = null;
      refreshHistory();
    } catch (err) {
      const retry = el('button', { type: 'button', class: 'btn btn--small', onclick: () => sendResult() }, 'Gửi lại kết quả');
      if (err instanceof ApiError && err.status === 401) {
        sendMessage('error', 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại. Code của bạn vẫn được giữ; hãy nộp lại sau khi đăng nhập.');
      } else if (err instanceof ApiError && err.code === 'EXERCISE_CHANGED') {
        pending = null;
        sendMessage('error', 'Đề bài vừa được cập nhật, hãy tải lại trang.', el('button', { type: 'button', class: 'btn btn--small', onclick: () => location.reload() }, 'Tải lại trang'));
      } else if (err instanceof ApiError && err.status === 429) {
        sendMessage('error', 'Bạn nộp quá nhanh, thử lại sau vài giây.', retry);
      } else if (err instanceof ApiError && err.status === 422) {
        pending = null;
        sendMessage('error', err.message || 'Kết quả nộp không hợp lệ.');
      } else if (err instanceof ApiError && err.status === 404) {
        pending = null;
        sendMessage('error', 'Bài tập này không còn được giao cho bạn.');
      } else {
        sendMessage('error', 'Chưa lưu được kết quả.', retry);
      }
    } finally {
      ui.busy = null;
      refreshButtons();
    }
  }

  async function onSubmit() {
    if (ui.busy) return;
    const code = editor.value;
    if (!code.trim()) {
      selectTab('results');
      clear(results);
      clear(sendArea);
      results.append(el('div', { class: 'alert alert--warn', role: 'alert' }, 'Bạn chưa viết code'));
      return;
    }
    saveNow();
    ui.busy = 'submit';
    refreshButtons();
    clear(results);
    clear(sendArea);
    pending = null;

    selectTab('results');
    const total = ex.testCases.length;
    const verdict = el('div', { class: 'verdict verdict--info', role: 'status' }, `Đang chấm… 0/${total}`);
    const barFill = el('span', { class: 'run-progress__bar' });
    const progress = el('div', {
      class: 'run-progress',
      role: 'progressbar',
      'aria-label': 'Tiến độ chấm bài',
      'aria-valuemin': '0',
      'aria-valuemax': String(total),
      'aria-valuenow': '0',
    }, barFill);
    const list = el('ol', { class: 'testlist' });
    const rows = ex.testCases.map((tc, i) => testRow(i, tc));
    rows.forEach((r) => list.append(r.li));
    results.append(progress, verdict, list);
    const setProgress = (done) => {
      progress.setAttribute('aria-valuenow', String(done));
      barFill.style.setProperty('--p', String(total ? done / total : 1));
    };
    const finishProgress = () => {
      progress.classList.add('is-done');
      setTimeout(() => progress.remove(), 700);
    };
    announce(`Đang chấm… 0/${total}`);

    let passedCount = 0;
    let aborted = false;
    const perTest = [];
    for (let i = 0; i < total; i++) {
      const tc = ex.testCases[i];
      const res = await guardedRun(code, tc.input);
      if (!res) { aborted = true; break; }
      if (res.stopped) { aborted = true; break; }
      const status = classify(res, tc.expectedOutput);
      if (status === STATUS.pass) passedCount++;
      perTest.push({ status: RESULT_NAMES[status.code], timeMs: Math.max(0, Math.round(res.ms || 0)) });
      fillRow(rows[i], tc, status, res);
      verdict.textContent = `Đang chấm… ${i + 1}/${total}`;
      setProgress(i + 1);
      announce(verdict.textContent);
    }

    ui.busy = null;
    refreshButtons();

    if (aborted) {
      progress.remove();
      verdict.className = 'verdict verdict--info';
      verdict.textContent = 'Đã dừng chấm bài.';
      announce(verdict.textContent);
      return;
    }
    // Kết quả chấm cục bộ hiển thị ngay, không chờ máy chủ.
    finishProgress();
    clear(verdict);
    if (passedCount === total) {
      verdict.className = 'verdict verdict--ok';
      verdict.append(verdictBadge('check'), el('span', { class: 'verdict__text' }, `Chấp nhận – ${passedCount}/${total} test đạt`));
      results.insertBefore(el('div', { class: 'alert alert--ok', role: 'status', style: 'margin:0 0 10px' }, icon('star', 'alert__icon'), ' Chúc mừng! Bạn đã hoàn thành bài tập này.'), list);
    } else {
      verdict.className = 'verdict verdict--fail';
      verdict.append(verdictBadge('x'), el('span', { class: 'verdict__text' }, `Chưa đạt – ${passedCount}/${total} test đạt`));
    }
    announce(verdict.textContent);

    if (preview) {
      sendMessage('info', 'Chế độ xem trước – kết quả không được lưu.');
      return;
    }
    if (ex.assignment && ex.assignment.dueAt && Date.now() > Date.parse(ex.assignment.dueAt)) {
      sendMessage('warn', 'Đã quá hạn nộp – bài vẫn được ghi nhận là nộp muộn.');
    }
    pending = { code, passedTests: passedCount, totalTests: total, results: perTest };
    await sendResult();
  }

  // ---------- Lịch sử nộp (chỉ học sinh)
  async function refreshHistory() {
    if (preview) return;
    try {
      const data = await api.mySubmissions(ex.id);
      renderHistory(data.submissions || []);
    } catch (err) {
      clear(historyArea);
    }
  }

  async function viewSubmission(item, opener) {
    try {
      const detail = await api.mySubmission(item.id);
      const choice = await showDialog({
        title: `Lần nộp thứ ${detail.attemptNo} – ${detail.passedTests}/${detail.totalTests} test đạt`,
        content: el('div', {},
          el('p', { class: 'muted small' }, formatDateTime(detail.submittedAt) + (detail.isLate ? ' · Nộp muộn' : '')),
          el('pre', { class: 'dialog-pre code-view', tabindex: '0', 'aria-label': 'Mã đã nộp (chỉ đọc)' }, detail.code)),
        buttons: [{ text: 'Đóng', value: 'close' }, { text: 'Nạp vào trình soạn', value: 'load', kind: 'primary' }],
        opener,
        wide: true,
        closeValue: 'close',
      });
      if (choice === 'load') {
        const ok = await confirmDialog({
          title: 'Nạp mã đã nộp vào trình soạn?',
          message: 'Code hiện tại trong trình soạn sẽ bị thay bằng mã của lần nộp này.',
          confirmText: 'Nạp vào trình soạn',
          cancelText: 'Giữ code hiện tại',
          opener,
          danger: true,
        });
        if (ok) {
          editor.value = detail.code;
          saveNow();
          editor.focus();
        }
      }
    } catch (err) {
      sendMessage('error', describeError(err, { notFound: 'Không tìm thấy bài nộp' }).title);
    }
  }

  function renderHistory(items) {
    clear(historyArea);
    const list = el('ul', { class: 'exercise-list', style: 'margin-top:8px' });
    if (!items.length) list.append(el('li', { class: 'muted' }, 'Bạn chưa nộp bài nào.'));
    for (const item of items) {
      const open = el('button', { type: 'button', class: 'btn btn--small' }, 'Xem mã');
      open.addEventListener('click', () => viewSubmission(item, open));
      list.append(el('li', { class: 'exercise-item', style: 'min-height:0' },
        el('span', { class: 'exercise-item__num' }, `#${item.attemptNo}`),
        el('span', { class: 'exercise-item__title' }, formatDateTime(item.submittedAt), ` · ${item.passedTests}/${item.totalTests} test đạt`),
        el('span', { class: 'exercise-item__badges' },
          el('span', { class: 'badge ' + (item.isPassed ? 'badge--done' : 'badge--todo') }, item.isPassed ? 'Đạt' : 'Chưa đạt'),
          item.isLate ? lateChip('Muộn') : null),
        open));
    }
    historyArea.append(el('section', { class: 'solution solution--history', 'aria-labelledby': 'history-heading' },
      el('h3', { id: 'history-heading' }, 'Lịch sử nộp'), list));
  }

  // ---------- Đáp án (chỉ tải sau khi xác nhận; "đã xem" lấy từ máy chủ)
  let solutionData = null;
  let solutionVisible = false;
  let viewed = state.viewedSolution;

  async function fetchSolution() {
    if (solutionData) return solutionData;
    solutionData = await api.solution(ex.id);
    return solutionData;
  }

  function hideSolution() {
    clear(solutionArea);
    solutionVisible = false;
    if (btnSolution) btnSolution.focus();
  }

  async function copySolution() {
    if (editor.value !== ex.starterCode) {
      const ok = await confirmDialog({
        title: 'Ghi đè code hiện tại?',
        message: 'Code bạn đã viết sẽ bị thay bằng đáp án tham khảo. Bạn có chắc chắn không?',
        confirmText: 'Ghi đè',
        cancelText: 'Giữ code của tôi',
        opener: document.getElementById('btn-copy-solution'),
        danger: true,
      });
      if (!ok) return;
    }
    editor.value = solutionData.code;
    saveNow();
    editor.focus();
  }

  function renderSolution() {
    clear(solutionArea);
    solutionVisible = true;
    solutionArea.append(el(
      'div',
      { class: 'solution', role: 'region', 'aria-label': 'Đáp án tham khảo' },
      el('h3', {}, 'Đáp án tham khảo'),
      codeBlockEl(solutionData.code, 'python'),
      solutionData.explanation ? el('p', {}, el('strong', {}, 'Giải thích: '), solutionData.explanation) : null,
      el('div', { class: 'solution__actions' },
        el('button', { type: 'button', class: 'btn btn--primary', id: 'btn-copy-solution', onclick: copySolution }, 'Chép vào trình soạn'),
        el('button', { type: 'button', class: 'btn', onclick: hideSolution }, 'Ẩn đáp án'),
      ),
    ));
  }

  async function revealSolution() {
    btnSolution.disabled = true;
    try {
      await fetchSolution();
      viewed = true;
      state.viewedSolution = true;
      statusArea.paint();
      renderSolution();
      solutionArea.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    } catch (err) {
      const info = describeError(err, { notFound: 'Không tìm thấy đáp án' });
      clear(solutionArea);
      solutionArea.append(el('div', { class: 'alert alert--error', role: 'alert' }, info.title));
    } finally {
      btnSolution.disabled = false;
    }
  }

  async function onSolution() {
    if (solutionVisible) { solutionArea.scrollIntoView({ block: 'nearest' }); return; }
    // Chỉ bỏ qua xác nhận khi người dùng đã xem đáp án ngay trong trang này (AC-36).
    const skipConfirm = !ex.confirmEveryTime && solutionData !== null;
    if (!skipConfirm) {
      const ok = await confirmDialog({
        title: 'Xem đáp án?',
        message: 'Bạn đã thử hết cách chưa? Xem đáp án sẽ không còn cơ hội tự suy nghĩ.',
        confirmText: 'Xem đáp án',
        cancelText: 'Hủy',
        opener: btnSolution,
      });
      if (!ok) return;
    }
    await revealSolution();
  }

  async function onReset() {
    const ok = await confirmDialog({
      title: 'Đặt lại code?',
      message: 'Code bạn đã viết sẽ bị thay bằng mã khởi tạo của bài tập.',
      confirmText: 'Đặt lại',
      cancelText: 'Hủy',
      opener: btnReset,
      danger: true,
    });
    if (!ok) return;
    editor.value = ex.starterCode;
    drafts.clearCode(ex.id);
    clear(results);
    clear(sendArea);
    editor.focus();
  }

  btnRun.addEventListener('click', onRun);
  btnSubmit.addEventListener('click', onSubmit);
  btnStop.addEventListener('click', () => runner.stop());
  btnReset.addEventListener('click', onReset);
  if (btnSolution) btnSolution.addEventListener('click', onSolution);
  refreshButtons();

  // Đáp án KHÔNG tự tải khi mở trang (AC-32, AC-62): chỉ huy hiệu "Đã xem đáp án" lấy từ máy chủ.
  refreshHistory();

  runner.start();
  return codePane;
}

function render(lesson, ex) {
  setTitle(ex.title);
  clear(root);
  root.append(el(
    'div',
    { class: 'problem-bar' },
    el(
      'div',
      { class: 'problem-bar__inner' },
      el('a', { class: 'back-link', href: '/lesson.html?id=' + encodeURIComponent(lesson.id) + '#bai-tap' }, '← Về bài học: ' + lesson.title),
      el('h1', { class: 'problem-bar__title' }, ex.title),
    ),
  ));

  const a = ex.assignment;
  const state = {
    status: a ? a.status : 'NotStarted',
    isOverdue: a ? a.isOverdue : false,
    passedLate: a ? a.passedLate : false,
    passedOnTime: a ? a.status === 'Passed' && !a.passedLate : false,
    viewedSolution: !!(a && a.hasViewedSolution),
    attempts: a ? a.attempts : 0,
    bestPassedTests: a ? a.bestPassedTests : 0,
  };
  const statusArea = buildStatusArea(ex, state);
  const workspace = el('div', { class: 'workspace' });
  const splitter = createSplitter({ workspace });
  workspace.append(buildStatementPane(ex, lesson, state, statusArea), splitter.element, buildWorkspace(ex, lesson, state, statusArea));
  window.addEventListener('pagehide', splitter.destroy, { once: true });
  root.append(workspace);
}

async function load() {
  main.setAttribute('aria-busy', 'true');
  clear(root);
  root.append(el('div', { class: 'container container--wide' }, stateBox({ kind: 'loading', title: 'Đang tải bài tập…', shape: 'panes' })));

  if (!lessonId || !problemId) {
    showFatal({ title: 'Không tìm thấy bài tập', message: 'Liên kết thiếu mã bài học hoặc mã bài tập.' });
    main.setAttribute('aria-busy', 'false');
    return;
  }

  try {
    const lesson = await api.lesson(lessonId);
    const ex = lesson.exercises.find((e) => e.id === problemId);
    if (!ex) {
      showFatal({ title: 'Không tìm thấy bài tập', message: 'Bài tập không tồn tại trong bài học này hoặc chưa được giao cho bạn.' });
      return;
    }
    render(lesson, ex);
  } catch (err) {
    const info = describeError(err, { notFound: 'Không tìm thấy bài học', invalidPrefix: 'Dữ liệu bài học bị lỗi' });
    showFatal({ title: info.title, message: info.message, retry: info.retry ? load : null });
  } finally {
    main.setAttribute('aria-busy', 'false');
  }
}

load();

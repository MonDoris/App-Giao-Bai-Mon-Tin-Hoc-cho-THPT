// Trình soạn bài học 4 bước (QD-72..QD-78): thanh bước dính, xem trước hai cột, thanh lưu dính, danh sách kiểm tra.
// Dữ liệu gửi lên máy chủ và API không đổi so với bản cũ (AC-418).
import { api, ApiError } from '/js/core/api.js';
import { initPage } from '/js/core/auth.js';
import { el, clear, stateBox, difficultyInfo, lessonSections } from '/js/core/dom.js';
import { icon } from '/js/core/icons.js';
import { confirmDialog, showDialog } from '/js/core/modal.js';
import { formDraft, uiPrefs } from '/js/core/storage.js';
import { toast } from '/js/core/toast.js';
import { banner, chip, debounce, field, guardUnsaved, setTitle, stateChip, withBusy } from '/js/core/ui.js';
import { blockList } from '/js/teacher/blocks.js';
import { backLink, difficultyLabel, errorText, finishLoading, pageContent, say } from '/js/teacher/common.js';
import { checklist, errorSummary, fillSample, iconBtn, menuButton, onSave, purposeBox, saveBar, splitLayout, stepHead, wizard } from '/js/teacher/editor-kit.js';
import { EXAMPLE, SAMPLE } from '/js/teacher/samples.js';

await initPage({ role: 'Teacher', active: 'lessons' });

const content = pageContent();
const params = new URLSearchParams(location.search);
const lessonId = params.get('id');
const isEdit = !!lessonId;
const draftName = 'lesson:' + (lessonId || 'new');

let lesson = null; // bản trên máy chủ (khi sửa)
let dirty = false;
let saving = false;
let bar = null;
let wiz = null;

const markDirty = () => {
  dirty = true;
  bar.set('dirty');
  saveDraft();
  refresh();
};
const saveDraft = debounce(() => {
  formDraft.save(draftName, snapshot());
  if (dirty && !saving) bar.set('draft');
}, 500);
const refresh = debounce(() => update(), 120);
guardUnsaved(() => dirty);

// ---------------------------------------------------------------- Ô nhập
const titleF = field({ id: 'title', label: 'Tên bài học', maxlength: 200, counter: true, hint: 'Học sinh thấy tên này ở đầu trang bài.' });
const descF = field({ id: 'description', label: 'Mô tả ngắn', type: 'textarea', rows: 2, maxlength: 500, counter: true, hint: 'Một hai câu cho biết bài nói về gì; hiện dưới tên bài ở danh sách.' });
const idF = field({ id: 'lesson-id', label: 'Địa chỉ bài (không bắt buộc)', maxlength: 100, hint: 'Hệ thống tự tạo từ tên bài. Chỉ đặt tay khi cần; dùng chữ thường không dấu, số và dấu gạch ngang; không đổi được sau khi tạo.' });
titleF.input.placeholder = 'Ví dụ: ' + EXAMPLE.title;
descF.input.placeholder = 'Ví dụ: ' + EXAMPLE.description;
idF.input.placeholder = 'Ví dụ: vong-lap-for';
[titleF, descF].forEach((f) => f.input.setAttribute('aria-required', 'true'));

const sampleBtn = (list, key, name) => iconBtn('Điền mẫu', 'sparkle', (e) => fillSample({
  opener: e.currentTarget,
  hasText: () => list.filled.length > 0,
  capture: () => list.values,
  apply: () => { list.values = SAMPLE[key]; markDirty(); },
  restore: (v) => { list.values = v; markDirty(); },
}), { label: `Điền mẫu cho ${name}` });

const listHint = 'Mỗi đoạn hiển thị thành một đoạn riêng trong bài. Dùng các nút phía trên ô để định dạng.';
const summary = blockList({ id: 'summary', label: 'Tóm tắt', hint: listHint, onChange: markDirty, placeholder: 'Ví dụ: ' + EXAMPLE.summary });
const notes = blockList({ id: 'notes', label: 'Những điểm cần lưu ý', hint: 'Mỗi mục là một ý ngắn, hiện thành dạng gạch đầu dòng. Cần ít nhất một mục.', onChange: markDirty, forceList: true, placeholder: 'Ví dụ: ' + EXAMPLE.notes });
const conclusion = blockList({ id: 'conclusion', label: 'Tổng kết', hint: listHint, onChange: markDirty, placeholder: 'Ví dụ: ' + EXAMPLE.conclusion });
const lists = [[summary, 'Tóm tắt', 'summary'], [notes, 'Những điểm cần lưu ý', 'notes'], [conclusion, 'Tổng kết', 'conclusion']];
const purposes = {
  summary: 'Nêu ý chính của bài bằng vài đoạn ngắn. Bạn có thể thêm gạch đầu dòng và đoạn mã.',
  notes: 'Ghi những điều học sinh dễ nhầm hoặc cần nhớ khi làm bài.',
  conclusion: 'Nhắc lại điều học sinh cần nhớ sau khi học xong bài.',
};
lists.forEach(([list, name, key]) => {
  const head = list.root.querySelector('legend');
  const [purpose, example] = purposeBox(purposes[key], EXAMPLE[key]);
  head.after(...[el('div', { class: 'ed-sechead' }, purpose, sampleBtn(list, key, name)), example].filter(Boolean));
});

const idDetails = el('details', { class: 'ed-more' }, el('summary', {}, 'Địa chỉ bài (không bắt buộc)'), idF.root);
const warnBox = el('div');
const draftBox = el('div');
const errors = errorSummary();
const previewBody = el('div', { class: 'ed-pv' });
const exercisesBox = el('div');
const checkBox = el('div');
const stateHosts = [el('span', { class: 'ed-top__state' }), el('span')]; // đầu trình soạn và bước 4
const readyHost = el('span');
const checkMeter = el('div', { class: 'ed-ready' });
const statusNote = el('p', { class: 'muted' }, 'Bản nháp: chỉ giáo viên thấy. Đã xuất bản: học sinh được giao bài mới thấy.');
const publishBtn = el('button', { type: 'button', class: 'btn btn--accent', id: 'btn-publish' }, 'Xuất bản cho học sinh');
const saveBtn = el('button', { type: 'button', class: 'btn btn--primary', id: 'btn-save' }, 'Lưu bài học');

for (const f of [titleF, descF, idF]) f.input.addEventListener('input', markDirty);

function snapshot() {
  return { title: titleF.value, description: descF.value, summary: summary.values, notes: notes.values, conclusion: conclusion.values, id: idF.value };
}

function applySnapshot(s) {
  titleF.value = s.title || '';
  descF.value = s.description || '';
  idF.value = s.id || '';
  summary.values = s.summary || [];
  notes.values = s.notes || [];
  conclusion.values = s.conclusion || [];
}

// ---------------------------------------------------------------- Xem trước (cùng hàm dựng với lesson.html)
function renderPreview() {
  const saved = lesson ? lesson.exercises : [];
  const exercises = saved.length
    ? el('ul', { class: 'exercise-list' }, saved.map((ex, i) => el('li', {},
      el('div', { class: 'exercise-item' },
        el('span', { class: 'exercise-item__num', 'aria-hidden': 'true' }, `${i + 1}.`),
        el('span', { class: 'exercise-item__title' }, ex.title),
        el('span', { class: 'exercise-item__badges' }, el('span', { class: 'badge ' + difficultyInfo(String(ex.difficulty).toLowerCase()).cls }, difficultyLabel(ex.difficulty)))))))
    : el('p', { class: 'muted' }, 'Chưa có bài tập');
  clear(previewBody);
  previewBody.append(
    el('p', { class: 'ed-pv__title' }, titleF.value.trim() || '(chưa có tiêu đề)'),
    ...(descF.value.trim() ? [el('p', { class: 'lead' }, descF.value.trim())] : []),
    ...lessonSections({ summary: summary.filled, notes: notes.filled, conclusion: conclusion.filled }, exercises));
}

// ---------------------------------------------------------------- Kiểm tra trước khi xuất bản và chỉ báo từng bước
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const toStep = (step, fn) => () => { wiz.go(step); if (fn) fn(); };
const focusList = (l) => toStep(1, () => l.focusItem(l.blanks[0] ?? 0));

function checkItems() {
  const exs = lesson ? lesson.exercises : [];
  const samples = [];
  if (titleF.value === SAMPLE.lesson.title && descF.value === SAMPLE.lesson.description) samples.push('Thông tin bài');
  lists.forEach(([l, name, key]) => { if (same(l.values, SAMPLE[key])) samples.push(name); });
  return [
    { text: 'Có tên bài học', ok: !!titleF.value.trim(), go: toStep(0, () => titleF.input.focus()) },
    { text: 'Có mô tả ngắn', ok: !!descF.value.trim(), go: toStep(0, () => descF.input.focus()) },
    { text: 'Phần Tóm tắt có ít nhất một đoạn có chữ', ok: summary.filled.length > 0, go: focusList(summary) },
    { text: 'Phần Những điểm cần lưu ý có ít nhất một mục có chữ', ok: notes.filled.length > 0, go: focusList(notes) },
    { text: 'Phần Tổng kết có ít nhất một đoạn có chữ', ok: conclusion.filled.length > 0, go: focusList(conclusion) },
    { text: 'Có ít nhất một bài tập', ok: exs.length > 0, go: toStep(2, () => exercisesBox.querySelector('a.btn--primary, button')?.focus()) },
    { text: 'Mỗi bài tập có ít nhất 3 bộ kiểm tra', ok: exs.length > 0 && exs.every((e) => e.testCases.length >= 3), go: toStep(2) },
    { text: samples.length ? `Phần ${samples.join(', ')} vẫn là bản mẫu` : 'Không phần nào còn là bản mẫu', ok: samples.length === 0, go: toStep(samples[0] === 'Thông tin bài' ? 0 : 1) },
    { text: 'Có ít nhất một bài tập đã xuất bản', ok: exs.some((e) => e.isPublished), hint: true, go: toStep(2) },
  ];
}

function paintState() {
  stateHosts.forEach((h) => h.replaceChildren(!lesson ? chip('chip--draft', 'pencil', 'Bài mới, chưa lưu') : lesson.isPublished ? stateChip('published') : stateChip('draft')));
}

/** Thanh "Đạt x/y mục" ở bước 4 (chỉ tính mục bắt buộc, không tính gợi ý). */
function renderReady(items) {
  const must = items.filter((i) => !i.hint);
  const ok = must.filter((i) => i.ok).length;
  checkMeter.replaceChildren(
    el('div', { class: 'ed-ready__head' },
      el('strong', {}, ok === must.length ? 'Bài học đã đủ điều kiện xuất bản' : `Đạt ${ok}/${must.length} mục cần có`),
      el('span', { class: 'mono' }, `${Math.round((ok / must.length) * 100)}%`)),
    el('div', { class: 'ed-meter__track', role: 'img', 'aria-label': `Đạt ${ok} trên ${must.length} mục` },
      must.map((i) => el('span', { class: 'ed-meter__seg' + (i.ok ? ' is-on' : '') }))));
  checkMeter.classList.toggle('is-full', ok === must.length);
}

function update() {
  const items = checkItems();
  const missing = items.filter((i) => !i.ok && !i.hint).length;
  const has = (k) => content.querySelectorAll('[data-step-panel]')[k].querySelector('[aria-invalid="true"]');
  const done = [
    !!titleF.value.trim() && !!descF.value.trim(),
    lists.every(([l]) => l.blanks.length === 0),
    !!lesson && lesson.exercises.length > 0,
    missing === 0,
  ];
  done.forEach((ok, k) => wiz.setStatus(k, has(k) ? 'error' : ok ? 'done' : 'todo'));
  renderPreview();
  renderReady(items);
  checkBox.replaceChildren(checklist(items));
  const published = !!lesson && lesson.isPublished;
  publishBtn.textContent = published ? 'Chuyển về bản nháp' : dirty && isEdit ? 'Lưu và xuất bản' : 'Xuất bản cho học sinh';
  publishBtn.className = 'btn ' + (published ? '' : 'btn--accent');
  publishBtn.disabled = !isEdit;
  readyHost.replaceChildren(published || !isEdit ? '' : missing === 0 ? chip('chip--published', 'check', 'Đủ điều kiện') : chip('chip--draft', 'alert', `Còn ${missing} mục chưa đủ`));
}

// ---------------------------------------------------------------- Kiểm tra, lỗi, lưu
function clearErrors() {
  [titleF, descF, idF].forEach((f) => f.clearError());
  lists.forEach(([l]) => l.clearErrors());
  errors.clear();
}

function showErrors(list) {
  const blank = lists.some(([l]) => l.blanks.length);
  const extras = blank && list.some((p) => p.blank) ? [iconBtn('Xóa các khối trống', 'trash', () => {
    lists.forEach(([l]) => l.removeBlanks());
    markDirty();
    validate();
  }, { cls: 'btn btn--small' })] : [];
  errors.show(list, extras);
  update();
}

/** Kiểm tra phía trình trình duyệt; máy chủ vẫn là nguồn xác thực chính. */
function validate() {
  clearErrors();
  const out = [];
  if (!titleF.value.trim()) {
    titleF.setError('Hãy nhập tên bài học.');
    out.push({ label: 'Bước 1, Tên bài học', message: 'Hãy nhập tên bài học.', go: toStep(0, () => titleF.input.focus()) });
  }
  if (!descF.value.trim()) {
    descF.setError('Hãy nhập mô tả ngắn.');
    out.push({ label: 'Bước 1, Mô tả ngắn', message: 'Hãy nhập mô tả ngắn.', go: toStep(0, () => descF.input.focus()) });
  }
  if (idF.value.trim() && !/^[a-z0-9-]{1,100}$/.test(idF.value.trim())) {
    const m = 'Địa chỉ bài chỉ gồm chữ thường không dấu, số và dấu gạch ngang.';
    idF.setError(m);
    idDetails.open = true;
    out.push({ label: 'Bước 1, Địa chỉ bài', message: m, go: toStep(0, () => idF.input.focus()) });
  }
  for (const [l, name] of lists) {
    l.blanks.forEach((i) => {
      const m = `Khối ${i + 1} của ${name} đang để trống. Hãy nhập nội dung hoặc xóa khối này.`;
      l.setError(i, m);
      out.push({ label: `Bước 2, ${name}, khối ${i + 1}`, message: m, blank: true, go: toStep(1, () => l.focusItem(i)) });
    });
  }
  showErrors(out);
  return out.length === 0;
}

function mapServerErrors(err) {
  clearErrors();
  const out = [];
  const where = { summary: 'Tóm tắt', notes: 'Những điểm cần lưu ý', conclusion: 'Tổng kết' };
  const byKey = { summary, notes, conclusion };
  for (const e of err.errors && err.errors.length ? err.errors : [{ field: err.field, message: err.message }]) {
    const f = String(e.field || '');
    const m = /^(summary|notes|conclusion)(?:\[(\d+)\])?$/.exec(f);
    const simple = { title: [titleF, 'Tên bài học'], description: [descF, 'Mô tả ngắn'], id: [idF, 'Địa chỉ bài'] }[f];
    if (simple) {
      simple[0].setError(e.message);
      if (f === 'id') idDetails.open = true;
      out.push({ label: 'Bước 1, ' + simple[1], message: e.message, go: toStep(0, () => simple[0].input.focus()) });
    } else if (m) {
      const idx = m[2] === undefined ? null : Number(m[2]);
      byKey[m[1]].setError(idx, e.message);
      out.push({ label: `Bước 2, ${where[m[1]]}${idx === null ? '' : `, khối ${idx + 1}`}`, message: e.message, go: toStep(1, () => (idx === null ? byKey[m[1]].focusFirstError() : byKey[m[1]].focusItem(idx))) });
    } else {
      out.push({ label: 'Lưu bài học', message: e.message, go: () => {} });
    }
  }
  showErrors(out);
}

function payload() {
  return {
    id: isEdit ? undefined : (idF.value.trim() || undefined),
    title: titleF.value,
    description: descF.value,
    summary: summary.values,
    notes: notes.values,
    conclusion: conclusion.values,
    version: isEdit ? lesson.version : undefined,
  };
}

function dependencyWarning() {
  if (!lesson || (lesson.assignmentCount === 0 && lesson.submissionCount === 0)) return null;
  return `Bài này đã được giao (${lesson.assignmentCount} lượt giao bài) và đã có ${lesson.submissionCount} bài nộp. Thay đổi đề hoặc bộ kiểm tra có thể làm kết quả cũ không còn khớp.`;
}

/** Cảnh báo bài đã giao: một dòng gọn, bấm để đọc chi tiết (không chiếm nửa màn hình ở mọi bước). */
function renderWarn() {
  warnBox.replaceChildren();
  const w = dependencyWarning();
  if (!w) return;
  warnBox.append(el('details', { class: 'ed-warn', role: 'status' },
    el('summary', {}, icon('alert'), el('span', {}, `Bài đang được giao: ${lesson.assignmentCount} lượt giao, ${lesson.submissionCount} bài nộp`), el('span', { class: 'ed-warn__more' }, 'Chi tiết')),
    el('p', {}, w)));
}

async function onConflict() {
  const choice = await showDialog({
    title: 'Bài học vừa được lưu ở nơi khác',
    content: el('p', {}, 'Có người, hoặc một tab khác của bạn, vừa lưu bản mới. Bạn muốn làm gì?'),
    buttons: [
      { text: 'Tải lại bản mới (bỏ thay đổi của tôi)', value: 'reload' },
      { text: 'Giữ bản của tôi', value: 'keep', kind: 'primary' },
    ],
    closeValue: 'cancel',
  });
  if (choice === 'reload') {
    dirty = false;
    formDraft.clear(draftName);
    location.reload();
  } else if (choice === 'keep') {
    try {
      lesson = await api.teacherLesson(lessonId);
      toast('info', 'Đã lấy bản mới nhất. Bấm "Lưu bài học" lần nữa để ghi đè.');
    } catch (err) {
      say('error', errorText(err));
    }
  }
}

/** Lưu (một yêu cầu POST/PUT như cũ). Trả về true khi lưu thành công. step (1..4): bước mở sau khi tạo mới. */
async function save(button, { step = null } = {}) {
  if (saving || !validate()) return false;
  const warning = dependencyWarning();
  if (warning) {
    const ok = await confirmDialog({ title: 'Lưu thay đổi vào bài đã giao?', message: warning, confirmText: 'Vẫn lưu', opener: button });
    if (!ok) return false;
  }
  saving = true;
  bar.set('saving');
  let done = false;
  await withBusy(button, 'Đang lưu…', async () => {
    try {
      if (isEdit) {
        lesson = await api.updateLesson(lessonId, payload());
        dirty = false;
        formDraft.clear(draftName);
        bar.set('saved');
        say('ok', 'Đã lưu bài học.');
        document.getElementById('page-title').textContent = lesson.title;
        renderWarn();
        renderExercises();
      } else {
        const created = await api.createLesson(payload());
        dirty = false;
        formDraft.clear(draftName);
        bar.set('saved');
        location.replace('/teacher/lesson-edit.html?id=' + encodeURIComponent(created.id) + '&created=1&step=' + (step || wiz.index + 1));
      }
      done = true;
    } catch (err) {
      bar.set('fail');
      if (err instanceof ApiError && err.code === 'CONFLICT') await onConflict();
      else if (err instanceof ApiError && err.status === 422) mapServerErrors(err);
      else if (err instanceof ApiError && err.status === 409) {
        idF.setError(err.message);
        idDetails.open = true;
        errors.show([{ label: 'Bước 1, Địa chỉ bài', message: err.message, go: toStep(0, () => idF.input.focus()) }]);
      } else if (!(err instanceof ApiError && err.status === 401)) {
        say('error', 'Chưa lưu được: ' + errorText(err) + ' Nội dung bạn soạn vẫn còn trên biểu mẫu, hãy bấm "Lưu bài học" để thử lại.');
      }
    } finally {
      saving = false;
    }
  });
  update();
  return done;
}

saveBtn.addEventListener('click', () => save(saveBtn));
onSave(() => save(saveBtn));

// ---------------------------------------------------------------- Xuất bản
async function togglePublish(button) {
  const publishing = !lesson.isPublished;
  if (publishing) {
    const miss = checkItems().filter((i) => !i.ok && !i.hint);
    if (miss.length) {
      const go = await showDialog({
        title: 'Vẫn xuất bản?',
        content: el('div', {}, el('p', {}, 'Bài học chưa đạt các mục sau. Học sinh có thể thấy một bài chưa hoàn chỉnh.'), el('ul', {}, miss.map((m) => el('li', {}, m.text)))),
        buttons: [{ text: 'Quay lại sửa', value: false }, { text: 'Vẫn xuất bản', value: true, kind: 'primary' }],
        opener: button,
        closeValue: false,
      });
      if (!go) return;
    }
    if (dirty && !(await save(saveBtn))) return;
  } else if (lesson.assignmentCount > 0) {
    const ok = await confirmDialog({
      title: 'Ẩn bài học đang được giao?',
      message: `Bài này đã được giao (${lesson.assignmentCount} lượt giao bài), ảnh hưởng ${lesson.affectedStudents} học sinh. Sau khi ẩn, học sinh không thấy bài nữa (bài nộp cũ vẫn giữ).`,
      confirmText: 'Ẩn bài học',
      opener: button,
      danger: true,
    });
    if (!ok) return;
  }
  try {
    lesson = await api.publishLesson(lessonId, publishing);
    say('ok', publishing ? 'Đã xuất bản bài học.' : 'Đã chuyển bài học về bản nháp.');
    paintState();
    update();
  } catch (err) {
    say('error', errorText(err));
  }
}
publishBtn.addEventListener('click', () => togglePublish(publishBtn));

// ---------------------------------------------------------------- Bước 3: danh sách bài tập
async function exerciseAction(task, okText, focusId) {
  try {
    await task();
    lesson = await api.teacherLesson(lessonId);
    say('ok', okText);
    renderExercises();
    renderWarn();
    update();
    if (focusId) exercisesBox.querySelector(`[data-ex="${CSS.escape(focusId)}"] h3 a`)?.focus();
  } catch (err) {
    say('error', errorText(err));
  }
}

function renderExercises() {
  clear(exercisesBox);
  if (!isEdit) {
    const b = iconBtn('Lưu bài học và thêm bài tập', 'plus', () => save(b, { step: 3 }), { cls: 'btn btn--primary' });
    exercisesBox.append(el('p', {}, 'Hãy lưu bài học trước, sau đó bạn mới thêm được bài tập.'), b);
    return;
  }
  const ids = lesson.exercises.map((e) => e.id);
  const pub = lesson.exercises.filter((e) => e.isPublished).length;
  exercisesBox.append(el('div', { class: 'ed-exhead' },
    el('p', { class: 'ed-exhead__sum' }, el('strong', { class: 'mono' }, String(lesson.exercises.length)), ' bài tập',
      lesson.exercises.length ? el('span', { class: 'muted' }, `, ${pub} đã xuất bản`) : null),
    el('a', { class: 'btn btn--primary', href: '/teacher/exercise-edit.html?lesson=' + encodeURIComponent(lessonId) }, icon('plus'), el('span', {}, 'Thêm bài tập'))));
  if (!lesson.exercises.length) {
    exercisesBox.append(stateBox({ kind: 'empty', title: 'Chưa có bài tập', message: 'Bài học chưa có bài tập. Mỗi bài tập gồm đề bài, ví dụ và bộ kiểm tra chấm bài.' }));
    return;
  }
  const list = el('ol', { class: 'ed-exlist' });
  lesson.exercises.forEach((ex, i) => {
    const swap = (delta) => {
      const next = ids.slice();
      [next[i], next[i + delta]] = [next[i + delta], next[i]];
      exerciseAction(() => api.reorderExercises(lessonId, next), 'Đã đổi thứ tự bài tập.', ex.id);
    };
    const menu = menuButton({
      text: 'Thêm', label: `Thêm hành động cho bài tập ${ex.title}`, end: true,
      items: () => [
        { text: ex.isPublished ? 'Chuyển về bản nháp' : 'Xuất bản cho học sinh', icon: 'published', onSelect: () => exerciseAction(() => api.publishExercise(ex.id, !ex.isPublished), ex.isPublished ? 'Đã chuyển bài tập về bản nháp.' : 'Đã xuất bản bài tập.', ex.id) },
        { text: ex.isArchived ? 'Khôi phục' : 'Cất đi (lưu trữ)', icon: 'archive', onSelect: () => exerciseAction(() => api.archiveExercise(ex.id, !ex.isArchived), ex.isArchived ? 'Đã khôi phục bài tập.' : 'Đã cất bài tập đi (lưu trữ).', ex.id) },
        { text: 'Lên', icon: 'arrow-up', disabled: i === 0, onSelect: () => swap(-1) },
        { text: 'Xuống', icon: 'arrow-down', disabled: i === ids.length - 1, onSelect: () => swap(1) },
        { sep: true },
        {
          text: 'Xóa', icon: 'trash', danger: true,
          onSelect: async (opener) => {
            const ok = await confirmDialog({ title: 'Xóa bài tập?', message: `Xóa bài tập "${ex.title}" và ${ex.testCases.length} bộ kiểm tra của nó. Không hoàn tác được.`, confirmText: 'Xóa bài tập', opener, danger: true });
            if (ok) exerciseAction(() => api.deleteExercise(ex.id), 'Đã xóa bài tập.');
          },
        },
      ],
    });
    const enough = ex.testCases.length >= 3;
    const editEx = '/teacher/exercise-edit.html?id=' + encodeURIComponent(ex.id);
    list.append(el('li', { class: 'ed-exrow' + (ex.isArchived ? ' is-archived' : ''), 'data-ex': ex.id },
      el('span', { class: 'ed-exrow__num mono', 'aria-hidden': 'true' }, String(i + 1)),
      el('div', { class: 'ed-exrow__main' },
        el('h3', { class: 'ed-exrow__title' }, el('span', { class: 'sr-only' }, `${i + 1}. `), el('a', { href: editEx }, ex.title)),
        el('div', { class: 'ed-exrow__meta' },
          el('span', { class: 'badge ' + difficultyInfo(String(ex.difficulty).toLowerCase()).cls }, difficultyLabel(ex.difficulty)),
          ex.isArchived ? stateChip('archived') : null,
          ex.isPublished ? stateChip('published') : stateChip('draft'),
          chip(enough ? 'chip--published' : 'chip--draft', enough ? 'check' : 'alert', `${ex.testCases.length} bộ kiểm tra (${ex.testCases.filter((t) => t.isHidden).length} ẩn)`),
          el('span', { class: 'muted' }, `${ex.assignmentCount} lượt giao, ${ex.submissionCount} bài nộp`))),
      el('div', { class: 'ed-exrow__actions' },
        el('a', { class: 'btn btn--small btn--primary', href: editEx }, icon('pencil'), el('span', {}, 'Soạn')),
        el('a', { class: 'btn btn--small', href: `/problem.html?lesson=${encodeURIComponent(lessonId)}&problem=${encodeURIComponent(ex.id)}`, 'aria-label': `Xem trước bài tập ${ex.title}` }, icon('eye'), el('span', {}, 'Xem trước')),
        menu.root)));
  });
  exercisesBox.append(list);
}

// ---------------------------------------------------------------- Dựng trang
async function build() {
  if (isEdit) {
    try {
      lesson = await api.teacherLesson(lessonId);
    } catch (err) {
      content.append(backLink('/teacher/index.html', '← Về danh sách bài học'), stateBox({ kind: 'error', title: err && err.status === 404 ? 'Không tìm thấy bài học' : errorText(err) }));
      finishLoading();
      return;
    }
    setTitle('Soạn bài học');
    document.getElementById('page-title').textContent = lesson.title;
    applySnapshot({ title: lesson.title, description: lesson.description, summary: lesson.summary, notes: lesson.notes, conclusion: lesson.conclusion });
  } else {
    document.getElementById('page-title').textContent = 'Tạo bài học mới';
    applySnapshot({});
  }
  paintState();

  const panel = (id, ...nodes) => el('section', { class: 'ed-panel', 'data-step-panel': '', 'aria-labelledby': id }, ...nodes);
  const infoSample = iconBtn('Điền mẫu', 'sparkle', (e) => fillSample({
    opener: e.currentTarget,
    hasText: () => !!(titleF.value.trim() || descF.value.trim()),
    capture: () => [titleF.value, descF.value],
    apply: () => { titleF.value = SAMPLE.lesson.title; descF.value = SAMPLE.lesson.description; markDirty(); },
    restore: ([t, d]) => { titleF.value = t; descF.value = d; markDirty(); },
  }), { label: 'Điền mẫu cho thông tin bài' });
  const panels = [
    panel('step-1',
      stepHead('step-1', 'Thông tin bài', 'Đặt tên và viết mô tả để học sinh biết bài nói về gì.', `${EXAMPLE.title}. ${EXAMPLE.description}`, [infoSample]),
      isEdit ? null : el('p', { class: 'banner banner--info' }, 'Bạn đang tạo bài học mới. Điền tên và mô tả, rồi sang bước 2 để viết nội dung.'),
      el('div', { class: 'ed-card' }, titleF.root, descF.root, isEdit ? null : idDetails)),
    panel('step-2',
      stepHead('step-2', 'Nội dung bài', 'Viết nội dung chính của bài, chia thành ba phần: tóm tắt, điểm cần lưu ý và tổng kết.'),
      lists.map(([l]) => l.root)),
    panel('step-3',
      stepHead('step-3', 'Bài tập', 'Thêm các bài tập để học sinh luyện tập sau khi đọc bài.', EXAMPLE.exercises),
      exercisesBox),
    panel('step-4',
      stepHead('step-4', 'Xem lại và xuất bản', 'Xem bài còn thiếu gì, rồi xuất bản để học sinh được giao bài có thể thấy.'),
      el('div', { class: 'ed-pubcard' },
        el('div', { class: 'ed-pubcard__info' }, el('div', { class: 'row' }, el('strong', {}, 'Trạng thái:'), stateHosts[1]), statusNote),
        el('div', { class: 'row ed-publish' }, readyHost, publishBtn),
        isEdit ? null : el('p', { class: 'muted', id: 'publish-note' }, 'Lưu bài học trước, rồi mới xuất bản được.')),
      el('h3', { class: 'ed-sub__title' }, 'Kiểm tra trước khi xuất bản'),
      el('p', { class: 'muted' }, 'Danh sách này chỉ để nhắc bạn. Bạn vẫn có thể xuất bản khi còn thiếu mục.'),
      checkMeter,
      checkBox),
  ];
  wiz = wizard({ label: 'Các bước soạn bài', names: ['Thông tin bài', 'Nội dung bài', 'Bài tập', 'Xem lại và xuất bản'], panels });
  bar = saveBar({ saveBtn, wiz, noun: 'bài học' });
  if (!isEdit) publishBtn.setAttribute('aria-describedby', 'publish-note');

  const edit = el('div', { class: 'ed-edit' },
    draftBox, warnBox, errors.root,
    el('form', { class: 'ed-form', novalidate: true, onsubmit: (e) => { e.preventDefault(); save(saveBtn); } }, panels));
  const preview = el('aside', { class: 'ed-preview', 'aria-labelledby': 'preview-title' },
    el('div', { class: 'ed-preview__head' },
      el('h2', { id: 'preview-title' }, 'Xem trước'),
      el('span', { class: 'chip chip--info' }, icon('eye'), 'Như học sinh thấy')),
    el('div', { class: 'ed-pv__frame' },
      el('div', { class: 'ed-pv__bar', 'aria-hidden': 'true' }, el('span', { class: 'ed-pv__dot' }), el('span', { class: 'ed-pv__dot' }), el('span', { class: 'ed-pv__dot' }), el('span', { class: 'ed-pv__url' }, 'Trang học sinh')),
      previewBody),
    isEdit ? el('p', { class: 'ed-preview__foot' }, el('a', { class: 'btn btn--small', href: '/lesson.html?id=' + encodeURIComponent(lessonId), target: '_blank', rel: 'noopener' }, 'Mở trang học sinh trong tab mới', el('span', { class: 'chip chip--info' }, 'Bản đã lưu'))) : null);
  const split = splitLayout({ main: edit, preview });

  // Màn rộng: ẩn/hiện khung xem trước để có chỗ soạn rộng hơn (nhớ theo người dùng).
  const pvBtn = el('button', { type: 'button', class: 'btn btn--small ed-pvtoggle', 'aria-pressed': 'true' }, icon('eye'), el('span', {}, 'Xem trước'));
  const setPreview = (on) => {
    split.root.dataset.preview = on ? 'on' : 'off';
    pvBtn.setAttribute('aria-pressed', String(on));
    pvBtn.replaceChildren(icon(on ? 'eye' : 'eye-off'), el('span', {}, on ? 'Xem trước' : 'Xem trước (đang ẩn)'));
  };
  pvBtn.addEventListener('click', () => {
    const on = split.root.dataset.preview === 'off';
    uiPrefs.setEditorPreview(on);
    setPreview(on);
  });
  setPreview(uiPrefs.getEditorPreview());

  const top = el('div', { class: 'ed-top' },
    el('div', { class: 'ed-top__main' },
      el('a', { class: 'ed-top__back', href: '/teacher/index.html' }, icon('arrow-left'), el('span', {}, 'Bài học của tôi')),
      el('div', { class: 'ed-top__title' }, document.getElementById('page-title'), stateHosts[0])),
    el('div', { class: 'ed-top__actions' }, pvBtn));

  const saved = formDraft.load(draftName);
  if (saved) {
    const restore = el('button', { type: 'button', class: 'btn btn--small' }, 'Khôi phục');
    const discard = el('button', { type: 'button', class: 'btn btn--small' }, 'Bỏ qua');
    restore.addEventListener('click', () => { applySnapshot(saved); dirty = true; bar.set('dirty'); clear(draftBox); update(); toast('ok', 'Đã khôi phục bản soạn dở.'); });
    discard.addEventListener('click', () => { formDraft.clear(draftName); clear(draftBox); });
    draftBox.append(banner('info', 'Có bản soạn dở chưa lưu từ lần trước. Bạn muốn khôi phục không? ', [restore, ' ', discard]));
  }
  if (params.get('created') === '1') say('ok', 'Đã tạo bài học (bản nháp). Hãy thêm bài tập rồi xuất bản.');

  content.append(top, document.getElementById('status'), wiz.nav, split.tablist, split.root, bar.root);
  renderWarn();
  renderExercises();
  update();
  wiz.go(Math.min(3, Math.max(0, (Number.parseInt(params.get('step'), 10) || 1) - 1)));
  finishLoading();
}

build();

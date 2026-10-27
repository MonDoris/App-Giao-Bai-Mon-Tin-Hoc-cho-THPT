// Trình soạn bài tập 5 bước (AC-409..AC-420). API, dữ liệu gửi đi, version/xung đột, nháp và cảnh báo bài đã giao không đổi.
import { api, ApiError } from '/js/api.js';
import { initPage } from '/js/auth.js';
import { el, clear, stateBox } from '/js/dom.js';
import { confirmDialog, showDialog } from '/js/modal.js';
import { formDraft } from '/js/storage.js';
import { toast } from '/js/toast.js';
import { banner, chip, debounce, field, guardUnsaved, setTitle, stateChip, withBusy } from '/js/ui.js';
import { recordList } from '/js/teacher/blocks.js';
import { DIFFICULTIES, backLink, errorText, finishLoading, pageContent, say } from '/js/teacher/common.js';
import { checklist, errorSummary, fillSample, iconBtn, onSave, roving, saveBar, stepHead, switchEl, toggleBulletLines, toggleInlineCode, wizard } from '/js/teacher/editor-kit.js';
import { EXAMPLE, SAMPLE } from '/js/teacher/samples.js';
import { classify, STATUS } from '/js/grader.js';

await initPage({ role: 'Teacher', active: 'lessons' });

const content = pageContent();
const params = new URLSearchParams(location.search);
const exerciseId = params.get('id');
const isEdit = !!exerciseId;
let lessonId = params.get('lesson');
const draftName = 'exercise:' + (exerciseId || 'new:' + lessonId);

let exercise = null;
let dirty = false;
let saving = false;
let lastRun = null; // { all: boolean } sau khi bấm "Chạy thử với đáp án" trong phiên
let bar = null;
let wiz = null;
let panels = [];

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
const mk = (opts, example, aria = false) => {
  const f = field(opts);
  if (example) f.input.placeholder = 'Ví dụ: ' + example;
  if (aria) f.input.setAttribute('aria-required', 'true');
  return f;
};
const ex = SAMPLE.exercise;
const titleF = mk({ id: 'title', label: 'Tên bài tập', maxlength: 200, counter: true, hint: 'Học sinh thấy tên này ở đầu trang làm bài.' }, ex.title, true);
const difficultyF = field({ id: 'difficulty', label: 'Độ khó', type: 'select', value: 'Easy', options: DIFFICULTIES, hint: 'Chọn mức để học sinh biết bài dễ hay khó.' });
const statementF = mk({ id: 'statement', label: 'Nội dung đề bài', type: 'textarea', rows: 8, maxlength: 20000, counter: true, hint: 'Cách đoạn bằng một dòng trống. Dùng các nút phía trên ô để định dạng.' }, EXAMPLE.statement, true);
const inputFormatF = mk({ id: 'inputFormat', label: 'Mô tả dữ liệu vào', type: 'textarea', rows: 3, maxlength: 5000, counter: true, hint: 'Cho biết dữ liệu vào gồm những gì và nằm trên mấy dòng.' }, ex.inputFormat, true);
const outputFormatF = mk({ id: 'outputFormat', label: 'Mô tả kết quả cần in ra', type: 'textarea', rows: 3, maxlength: 5000, counter: true, hint: 'Cho biết chương trình cần in ra những gì và theo thứ tự nào.' }, ex.outputFormat, true);
const constraintsF = mk({ id: 'constraints', label: 'Ràng buộc (không bắt buộc)', type: 'textarea', rows: 2, maxlength: 5000, counter: true, hint: 'Giới hạn của dữ liệu vào, nếu có.' }, ex.constraints);
const starterF = mk({ id: 'starterCode', label: 'Khung mã cho học sinh (không bắt buộc)', type: 'textarea', rows: 6, maxlength: 20000, code: true, counter: true, hint: 'Đoạn mã có sẵn trong ô soạn code khi học sinh mở bài.' });
const solutionF = mk({ id: 'solutionCode', label: 'Đáp án mẫu (không bắt buộc)', type: 'textarea', rows: 8, maxlength: 20000, code: true, counter: true, hint: 'Để trống nếu không muốn học sinh có nút Đáp án.' });
const explanationF = mk({ id: 'solutionExplanation', label: 'Giải thích đáp án', type: 'textarea', rows: 3, maxlength: 20000, counter: true, hint: 'Nên viết ít nhất một câu nếu có đáp án mẫu.' }, ex.solutionExplanation);
const timeF = field({ id: 'timeLimitMs', label: 'Thời gian tối đa cho mỗi bộ kiểm tra (mili giây)', type: 'number', value: '5000', inputmode: 'numeric', hint: '5000 mili giây bằng 5 giây; chọn từ 500 đến 5000.' });
const idF = mk({ id: 'exercise-id', label: 'Địa chỉ bài tập (không bắt buộc)', maxlength: 100, hint: 'Hệ thống tự tạo từ tên bài. Chỉ đặt tay khi cần; duy nhất toàn hệ thống và không đổi được sau khi tạo.' }, 'in-binh-phuong');
const confirmSw = switchEl({ id: 'confirmEveryTime', text: (on) => `Luôn hỏi lại trước khi học sinh xem đáp án: ${on ? 'bật' : 'tắt'}`, onChange: () => markDirty() });
const confirmEvery = confirmSw.input;

const examples = recordList({
  id: 'examples',
  label: 'Ví dụ mẫu (1 đến 10)',
  itemLabel: 'Ví dụ',
  min: 1,
  max: 10,
  makeEmpty: () => ({ input: '', output: '', explanation: '' }),
  columns: [
    { key: 'input', label: 'Vào', maxlength: 5000 },
    { key: 'output', label: 'Ra', maxlength: 5000 },
    { key: 'explanation', label: 'Giải thích (không bắt buộc)', maxlength: 5000, wide: true, mono: false },
  ],
  extraActions: (data) => [iconBtn('Thêm ví dụ này vào bộ kiểm tra', 'plus', () => {
    const ok = tests.add({ input: data.input, expectedOutput: data.output, isHidden: false });
    toast(ok ? 'ok' : 'warn', ok ? 'Đã thêm vào bộ kiểm tra chấm bài.' : 'Đã đủ 50 bộ kiểm tra, hãy xóa bớt trước khi thêm.');
  }, { cls: 'btn btn--small' })],
  onChange: markDirty,
});
const tests = recordList({
  id: 'testCases',
  label: 'Bộ kiểm tra chấm bài (3 đến 50)',
  caption: 'Bộ kiểm tra chấm bài',
  itemLabel: 'Bộ kiểm tra',
  table: true,
  duplicate: true,
  min: 3,
  max: 50,
  makeEmpty: () => ({ input: '', expectedOutput: '', isHidden: false }),
  columns: [
    { key: 'input', label: 'Dữ liệu vào (học sinh nhập)', maxlength: 10000 },
    { key: 'expectedOutput', label: 'Kết quả đúng (in ra)', maxlength: 10000 },
    { key: 'isHidden', label: 'Kiểm tra ẩn', type: 'switch', text: (on) => (on ? 'Ẩn: học sinh không thấy' : 'Học sinh thấy'), iconFor: (on) => (on ? 'eye-off' : 'eye') },
  ],
  onChange: markDirty,
});

const warnBox = el('div');
const draftBox = el('div');
const errors = errorSummary();
const testWarn = el('div', { 'aria-live': 'polite' });
const testCount = el('p', { class: 'ed-count' });
const checkBox = el('div', { 'aria-live': 'polite' });
const exPreview = el('div');
const listBox = el('div');
const stateHost = el('span');
const readyHost = el('span');
const adv = el('details', { class: 'ed-more' });
const publishBtn = el('button', { type: 'button', class: 'btn btn--accent', id: 'btn-publish' }, 'Xuất bản cho học sinh');
const saveBtn = el('button', { type: 'button', class: 'btn btn--primary', id: 'btn-save' }, 'Lưu bài tập');
const checkBtn = el('button', { type: 'button', class: 'btn', id: 'btn-check' }, 'Chạy thử với đáp án');
const textFields = [titleF, difficultyF, statementF, inputFormatF, outputFormatF, constraintsF, starterF, solutionF, explanationF, timeF, idF];

for (const f of textFields) {
  f.input.addEventListener('input', markDirty);
  f.input.addEventListener('change', markDirty);
}

function snapshot() {
  return {
    title: titleF.value, difficulty: difficultyF.value, statement: statementF.value, inputFormat: inputFormatF.value,
    outputFormat: outputFormatF.value, constraints: constraintsF.value, starterCode: starterF.value, solutionCode: solutionF.value,
    solutionExplanation: explanationF.value, timeLimitMs: timeF.value, id: idF.value, confirmEveryTime: confirmEvery.checked,
    examples: examples.values, testCases: tests.values,
  };
}

function applySnapshot(s) {
  titleF.value = s.title || '';
  difficultyF.value = s.difficulty || 'Easy';
  statementF.value = s.statement || '';
  inputFormatF.value = s.inputFormat || '';
  outputFormatF.value = s.outputFormat || '';
  constraintsF.value = s.constraints || '';
  starterF.value = s.starterCode || '';
  solutionF.value = s.solutionCode || '';
  explanationF.value = s.solutionExplanation || '';
  timeF.value = String(s.timeLimitMs ?? 5000);
  idF.value = s.id || '';
  confirmEvery.checked = !!s.confirmEveryTime;
  confirmSw.paint();
  examples.values = s.examples || [];
  tests.values = s.testCases || [];
}

// ---------------------------------------------------------------- Chỉ báo từng bước và danh sách kiểm tra
const toStep = (step, fn) => () => { if (step === 4 && fn) adv.open = true; wiz.go(step); if (fn) fn(); };
const filled = (v) => String(v ?? '').trim() !== '';

function checkItems() {
  const t = tests.values;
  const time = Number.parseInt(timeF.value, 10);
  const timeOk = Number.isFinite(time) && time >= 500 && time <= 5000;
  const solOk = filled(solutionF.value) === filled(explanationF.value);
  const f = (field) => () => field.input.focus();
  return [
    { text: 'Có tên bài tập', ok: filled(titleF.value), go: toStep(0, f(titleF)) },
    { text: 'Có nội dung đề bài', ok: filled(statementF.value), go: toStep(0, f(statementF)) },
    { text: 'Có mô tả dữ liệu vào', ok: filled(inputFormatF.value), go: toStep(0, f(inputFormatF)) },
    { text: 'Có mô tả kết quả cần in ra', ok: filled(outputFormatF.value), go: toStep(0, f(outputFormatF)) },
    { text: 'Có ít nhất 1 ví dụ có cả dữ liệu vào và kết quả ra', ok: examples.values.some((e) => filled(e.input) && filled(e.output)), go: toStep(1, () => examples.focusFirst()) },
    { text: 'Có ít nhất 3 bộ kiểm tra, mỗi bộ có kết quả đúng', ok: t.length >= 3 && t.every((x) => filled(x.expectedOutput)), go: toStep(2, () => tests.focusFirst()) },
    { text: 'Thời gian tối đa nằm trong khoảng 500 đến 5000 mili giây', ok: timeOk, go: toStep(4, f(timeF)) },
    { text: 'Có ít nhất 1 bộ kiểm tra ẩn', ok: t.some((x) => x.isHidden), hint: true, go: toStep(2, () => tests.focusFirst()) },
    { text: 'Đáp án mẫu đi kèm lời giải thích', ok: solOk, hint: true, go: toStep(3, f(filled(solutionF.value) ? explanationF : solutionF)) },
    { text: 'Đáp án mẫu chạy đạt mọi bộ kiểm tra', ok: !!lastRun && lastRun.all, hint: true, pending: lastRun ? null : 'Chưa kiểm tra', go: toStep(2, () => checkBtn.focus()) },
  ];
}

function exampleCard(e, i) {
  const card = el('div', { class: 'example' }, el('div', { class: 'example__grid' },
    el('div', { class: 'example__col' }, el('div', { class: 'example__label' }, 'Vào'), el('pre', {}, e.input)),
    el('div', { class: 'example__col' }, el('div', { class: 'example__label' }, 'Ra'), el('pre', {}, e.output))));
  if (e.explanation) card.append(el('div', { class: 'example__expl' }, el('strong', {}, `Giải thích ví dụ ${i + 1}: `), e.explanation));
  return card;
}

function update() {
  const items = checkItems();
  const missing = items.filter((i) => !i.ok && !i.hint).length;
  const t = tests.values;
  const hidden = t.filter((x) => x.isHidden).length;
  const has = (k) => panels[k].querySelector('[aria-invalid="true"]');
  const done = [items[0].ok && items[1].ok && items[2].ok && items[3].ok, items[4].ok, items[5].ok, items[8].ok, items[6].ok];
  done.forEach((ok, k) => wiz.setStatus(k, has(k) ? 'error' : ok ? 'done' : 'todo'));

  testCount.textContent = `${t.length} bộ kiểm tra (cần 3 đến 50), ${hidden} ẩn`;
  const warns = [];
  if (!hidden) warns.push('Chưa có bộ kiểm tra nào ẩn: học sinh sẽ thấy toàn bộ. Nên ẩn vài bộ, nhất là trường hợp đặc biệt.');
  if (t.length < 5) warns.push('Bộ kiểm tra còn ít (dưới 5 bộ): hãy thêm trường hợp đặc biệt như giá trị nhỏ nhất, lớn nhất, số 0, số âm.');
  testWarn.replaceChildren(...(warns.length ? [el('div', { class: 'banner banner--warn' }, el('ul', {}, warns.map((w) => el('li', {}, w))))] : []));

  const exs = examples.values;
  exPreview.replaceChildren(...exs.filter((e) => filled(e.input) || filled(e.output)).map(exampleCard));
  if (!exPreview.firstChild) exPreview.append(el('p', { class: 'muted' }, 'Ví dụ bạn nhập sẽ hiện ở đây.'));

  listBox.replaceChildren(checklist(items));
  const published = !!exercise && exercise.isPublished;
  publishBtn.textContent = published ? 'Chuyển về bản nháp' : dirty && isEdit ? 'Lưu và xuất bản' : 'Xuất bản cho học sinh';
  publishBtn.className = 'btn ' + (published ? '' : 'btn--accent');
  publishBtn.disabled = !isEdit;
  readyHost.replaceChildren(published || !isEdit ? '' : missing === 0 ? chip('chip--published', 'check', 'Đủ điều kiện') : chip('chip--draft', 'alert', `Còn ${missing} mục chưa đủ`));
}

// ---------------------------------------------------------------- Kiểm tra, lỗi, lưu
function clearErrors() {
  textFields.forEach((f) => f.clearError());
  examples.clearErrors();
  tests.clearErrors();
  errors.clear();
}

function showErrors(list) {
  errors.show(list);
  update();
}

function validate() {
  clearErrors();
  const out = [];
  const need = (f, message, name) => {
    if (filled(f.value)) return;
    f.setError(message);
    out.push({ label: 'Bước 1, ' + name, message, go: toStep(0, () => f.input.focus()) });
  };
  need(titleF, 'Hãy nhập tên bài tập.', 'Tên bài tập');
  need(statementF, 'Hãy nhập nội dung đề bài.', 'Nội dung đề bài');
  need(inputFormatF, 'Hãy cho biết dữ liệu vào gồm những gì.', 'Mô tả dữ liệu vào');
  need(outputFormatF, 'Hãy cho biết kết quả cần in ra.', 'Mô tả kết quả cần in ra');
  if (examples.values.length < 1) {
    examples.setError(null, null, 'Cần ít nhất 1 ví dụ.');
    out.push({ label: 'Bước 2, Ví dụ', message: 'Cần ít nhất 1 ví dụ.', go: toStep(1, () => examples.focusFirst()) });
  }
  const n = tests.values.length;
  if (n < 3 || n > 50) {
    const m = n < 3 ? 'Cần ít nhất 3 bộ kiểm tra.' : 'Tối đa 50 bộ kiểm tra.';
    tests.setError(null, null, m);
    out.push({ label: 'Bước 3, Bộ kiểm tra', message: m, go: toStep(2, () => tests.focusFirst()) });
  }
  const time = Number.parseInt(timeF.value, 10);
  if (!Number.isFinite(time) || time < 500 || time > 5000) {
    adv.open = true;
    timeF.setError('Thời gian phải từ 500 đến 5000 mili giây.');
    out.push({ label: 'Bước 5, Thời gian tối đa', message: 'Thời gian phải từ 500 đến 5000 mili giây.', go: toStep(4, () => timeF.input.focus()) });
  }
  showErrors(out);
  return out.length === 0;
}

const FIELD_META = {
  title: [0, 'Tên bài tập', titleF], difficulty: [0, 'Độ khó', difficultyF], statement: [0, 'Nội dung đề bài', statementF],
  inputFormat: [0, 'Mô tả dữ liệu vào', inputFormatF], outputFormat: [0, 'Mô tả kết quả cần in ra', outputFormatF], constraints: [0, 'Ràng buộc', constraintsF],
  starterCode: [3, 'Khung mã cho học sinh', starterF], solutionCode: [3, 'Đáp án mẫu', solutionF], solutionExplanation: [3, 'Giải thích đáp án', explanationF],
  timeLimitMs: [4, 'Thời gian tối đa', timeF], id: [4, 'Địa chỉ bài tập', idF],
};

function mapServerErrors(err) {
  clearErrors();
  const out = [];
  for (const e of err.errors && err.errors.length ? err.errors : [{ field: err.field, message: err.message }]) {
    const f = String(e.field || '');
    const meta = FIELD_META[f];
    const exm = /^examples(?:\[(\d+)\](?:\.(\w+))?)?$/.exec(f);
    const tcm = /^testCases(?:\[(\d+)\](?:\.(\w+))?)?$/.exec(f);
    if (meta) {
      meta[2].setError(e.message);
      if (meta[0] === 4) adv.open = true;
      out.push({ label: `Bước ${meta[0] + 1}, ${meta[1]}`, message: e.message, go: toStep(meta[0], () => meta[2].input.focus()) });
    } else if (exm || tcm) {
      const [m, list, step, name] = exm ? [exm, examples, 1, 'Ví dụ'] : [tcm, tests, 2, 'Bộ kiểm tra'];
      const idx = m[1] === undefined ? null : Number(m[1]);
      list.setError(idx, m[2], e.message);
      out.push({ label: `Bước ${step + 1}, ${name}${idx === null ? '' : ' ' + (idx + 1)}`, message: e.message, go: toStep(step, () => (idx === null ? list.focusFirstError() : list.focusFirst())) });
    } else {
      out.push({ label: 'Lưu bài tập', message: e.message, go: () => {} });
    }
  }
  showErrors(out);
}

function payload() {
  const num = Number.parseInt(timeF.value, 10);
  return {
    id: isEdit ? undefined : (idF.value.trim() || undefined),
    title: titleF.value,
    difficulty: difficultyF.value,
    statement: statementF.value,
    inputFormat: inputFormatF.value,
    outputFormat: outputFormatF.value,
    constraints: constraintsF.value,
    examples: examples.values.map((e) => ({ input: e.input, output: e.output, explanation: e.explanation || null })),
    starterCode: starterF.value,
    solutionCode: solutionF.value,
    solutionExplanation: explanationF.value,
    confirmEveryTime: confirmEvery.checked,
    timeLimitMs: Number.isFinite(num) ? num : null,
    testCases: tests.values.map((t) => ({ input: t.input, expectedOutput: t.expectedOutput, isHidden: !!t.isHidden })),
    version: isEdit ? exercise.version : undefined,
  };
}

function dependencyWarning() {
  if (!exercise || (exercise.assignmentCount === 0 && exercise.submissionCount === 0)) return null;
  return `Bài này đã được giao (${exercise.assignmentCount} lượt giao bài) và đã có ${exercise.submissionCount} bài nộp. Thay đổi đề hoặc bộ kiểm tra có thể làm kết quả cũ không còn khớp. Học sinh đang mở bản cũ sẽ phải tải lại trang khi nộp.`;
}

function renderWarn() {
  warnBox.replaceChildren();
  const w = dependencyWarning();
  if (w) warnBox.append(banner('warn', w));
}

async function onConflict() {
  const choice = await showDialog({
    title: 'Bài tập vừa được lưu ở nơi khác',
    content: el('p', {}, 'Có người, hoặc một tab khác của bạn, vừa lưu bản mới. Bạn muốn làm gì?'),
    buttons: [{ text: 'Tải lại bản mới (bỏ thay đổi của tôi)', value: 'reload' }, { text: 'Giữ bản của tôi', value: 'keep', kind: 'primary' }],
    closeValue: 'cancel',
  });
  if (choice === 'reload') {
    dirty = false;
    formDraft.clear(draftName);
    location.reload();
  } else if (choice === 'keep') {
    try {
      exercise = await api.teacherExercise(exerciseId);
      toast('info', 'Đã lấy bản mới nhất. Bấm "Lưu bài tập" lần nữa để ghi đè.');
    } catch (err) {
      say('error', errorText(err));
    }
  }
}

async function save(button) {
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
        exercise = await api.updateExercise(exerciseId, payload());
        dirty = false;
        formDraft.clear(draftName);
        bar.set('saved');
        say('ok', 'Đã lưu bài tập.');
        document.getElementById('page-title').textContent = 'Soạn bài tập: ' + exercise.title;
        renderWarn();
      } else {
        const created = await api.createExercise(lessonId, payload());
        dirty = false;
        formDraft.clear(draftName);
        bar.set('saved');
        location.replace('/teacher/exercise-edit.html?id=' + encodeURIComponent(created.id) + '&created=1&step=' + (wiz.index + 1));
      }
      done = true;
    } catch (err) {
      bar.set('fail');
      if (err instanceof ApiError && err.code === 'CONFLICT') await onConflict();
      else if (err instanceof ApiError && err.status === 422) mapServerErrors(err);
      else if (err instanceof ApiError && err.code === 'DUPLICATE_ID') {
        adv.open = true;
        idF.setError(err.message);
        errors.show([{ label: 'Bước 5, Địa chỉ bài tập', message: err.message, go: toStep(4, () => idF.input.focus()) }]);
      } else if (!(err instanceof ApiError && err.status === 401)) {
        say('error', 'Chưa lưu được: ' + errorText(err) + ' Nội dung bạn soạn vẫn còn trên biểu mẫu, hãy bấm "Lưu bài tập" để thử lại.');
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

// ---------------------------------------------------------------- Chạy thử với đáp án (chạy trong trình duyệt, không gửi gì lên máy chủ)
let runner = null;

async function checkSolution(button) {
  const code = solutionF.value;
  if (!code.trim()) {
    checkBox.replaceChildren(el('div', { class: 'banner banner--warn' }, 'Hãy nhập Đáp án mẫu ở bước 4 trước. ', iconBtn('Đi tới bước 4', 'arrow-right', () => wiz.go(3, solutionF.input), { after: true })));
    wiz.go(2);
    return;
  }
  const list = tests.values;
  await withBusy(button, 'Đang chạy thử…', async () => {
    checkBox.replaceChildren(banner('info', 'Đang nạp trình chạy Python (lần đầu có thể mất vài giây)…'));
    if (!runner) {
      try {
        const { PyRunner } = await import('/js/runner.js'); // nạp lười: chỉ khi giáo viên bấm chạy thử
        runner = new PyRunner({ onState: () => {} });
        await runner.start();
      } catch (e) {
        runner = null;
        checkBox.replaceChildren(banner('error', 'Không tải được trình chạy Python. Hãy kiểm tra kết nối Internet rồi thử lại.'));
        return;
      }
    }
    const limit = Math.min(5000, Math.max(500, Number.parseInt(timeF.value, 10) || 5000));
    const rows = [];
    let passed = 0;
    for (let i = 0; i < list.length; i++) {
      let res;
      try {
        res = await runner.run(code, list[i].input, limit);
      } catch (e) {
        checkBox.replaceChildren(banner('error', 'Không chạy được trình Python. Hãy kiểm tra kết nối Internet rồi thử lại.'));
        return;
      }
      const status = classify(res, list[i].expectedOutput);
      if (status === STATUS.pass) passed++;
      rows.push(el('li', { class: 'testitem' },
        el('div', { class: 'testitem__row' },
          el('span', { class: 'testitem__name' }, `Bộ kiểm tra ${i + 1}${list[i].isHidden ? ' (ẩn)' : ''}`),
          el('span', { class: 'status status--' + status.code }, status.label),
          el('span', { class: 'testitem__time' }, status.code === 'timeout' ? '' : `${res.ms} ms`)),
        status === STATUS.pass ? null : el('div', { class: 'testitem__detail' },
          el('div', {}, el('h4', {}, 'Kết quả đúng'), el('pre', {}, list[i].expectedOutput || '(trống)')),
          el('div', {}, el('h4', {}, 'Kết quả của đáp án'), el('pre', {}, res.error ? `${res.error.type}: ${res.error.message}` : (res.output || '(trống)'))))));
    }
    const all = passed === list.length;
    lastRun = { all };
    checkBox.replaceChildren(
      el('p', { class: 'ed-runsummary' }, chip(all ? 'chip--passed' : 'chip--draft', all ? 'check' : 'alert', `Đạt ${passed}/${list.length} bộ kiểm tra`), all ? '' : ' Việc này không chặn lưu, nhưng nên xem lại đáp án mẫu hoặc kết quả đúng.'),
      el('ol', { class: 'testlist' }, rows));
    update();
  });
}
checkBtn.addEventListener('click', () => checkSolution(checkBtn));

// ---------------------------------------------------------------- Xuất bản
async function togglePublish(button) {
  const publishing = !exercise.isPublished;
  if (publishing) {
    const miss = checkItems().filter((i) => !i.ok && !i.hint);
    if (miss.length) {
      const go = await showDialog({
        title: 'Vẫn xuất bản?',
        content: el('div', {}, el('p', {}, 'Bài tập chưa đạt các mục sau. Học sinh có thể thấy một bài chưa hoàn chỉnh.'), el('ul', {}, miss.map((m) => el('li', {}, m.text)))),
        buttons: [{ text: 'Quay lại sửa', value: false }, { text: 'Vẫn xuất bản', value: true, kind: 'primary' }],
        opener: button,
        closeValue: false,
      });
      if (!go) return;
    }
    if (dirty && !(await save(saveBtn))) return;
  } else if (exercise.assignmentCount > 0) {
    const ok = await confirmDialog({
      title: 'Ẩn bài tập đang được giao?',
      message: `Bài tập này đã được giao (${exercise.assignmentCount} lượt giao bài), ảnh hưởng ${exercise.affectedStudents} học sinh. Sau khi ẩn, học sinh không thấy bài nữa (bài nộp cũ vẫn giữ).`,
      confirmText: 'Ẩn bài tập',
      opener: button,
      danger: true,
    });
    if (!ok) return;
  }
  try {
    const next = await api.publishExercise(exerciseId, publishing);
    exercise = { ...exercise, ...next };
    stateHost.replaceChildren(exercise.isPublished ? stateChip('published') : stateChip('draft'));
    say('ok', publishing ? 'Đã xuất bản bài tập.' : 'Đã chuyển bài tập về bản nháp.');
    update();
  } catch (err) {
    say('error', errorText(err));
  }
}
publishBtn.addEventListener('click', () => togglePublish(publishBtn));

// ---------------------------------------------------------------- Dựng trang
const sampleBtn = (name, hasText, capture, apply, restore) => iconBtn('Điền mẫu', 'sparkle', (e) => fillSample({ opener: e.currentTarget, hasText, capture, apply, restore }), { label: `Điền mẫu cho ${name}` });

function statementToolbar() {
  const bar2 = el('div', { class: 'ed-toolbar', role: 'toolbar', 'aria-label': 'Định dạng nội dung đề bài' },
    iconBtn('Mã trong dòng', 'braces', () => toggleInlineCode(statementF.input), { label: 'Mã trong dòng, nội dung đề bài' }),
    iconBtn('Gạch đầu dòng', 'list', () => toggleBulletLines(statementF.input), { label: 'Gạch đầu dòng, nội dung đề bài' }));
  roving(bar2);
  statementF.input.before(bar2);
}

async function build() {
  if (isEdit) {
    try {
      exercise = await api.teacherExercise(exerciseId);
    } catch (err) {
      content.append(backLink('/teacher/index.html', '← Về danh sách bài học'), stateBox({ kind: 'error', title: err && err.status === 404 ? 'Không tìm thấy bài tập' : errorText(err) }));
      finishLoading();
      return;
    }
    lessonId = exercise.lessonId;
    document.getElementById('page-title').textContent = 'Soạn bài tập: ' + exercise.title;
    applySnapshot({
      title: exercise.title, difficulty: exercise.difficulty, statement: exercise.statement, inputFormat: exercise.inputFormat,
      outputFormat: exercise.outputFormat, constraints: exercise.constraints, starterCode: exercise.starterCode,
      solutionCode: exercise.solutionCode, solutionExplanation: exercise.solutionExplanation, timeLimitMs: exercise.timeLimitMs,
      confirmEveryTime: exercise.confirmEveryTime, examples: exercise.examples.map((e) => ({ input: e.input, output: e.output, explanation: e.explanation || '' })),
      testCases: exercise.testCases,
    });
    stateHost.append(exercise.isPublished ? stateChip('published') : stateChip('draft'));
  } else {
    if (!lessonId) {
      content.append(stateBox({ kind: 'error', title: 'Thiếu thông tin bài học', message: 'Hãy tạo bài tập từ trang soạn bài học.', actions: [el('a', { class: 'btn btn--primary', href: '/teacher/index.html' }, 'Về danh sách bài học')] }));
      finishLoading();
      return;
    }
    document.getElementById('page-title').textContent = 'Tạo bài tập mới';
    applySnapshot({ testCases: [{}, {}, {}], examples: [{}] });
  }
  setTitle('Soạn bài tập');
  statementToolbar();
  adv.append(el('summary', {}, 'Cấu hình nâng cao'), timeF.root, el('div', { class: 'form-field' }, confirmSw.root), isEdit ? null : idF.root);

  const panel = (id, ...nodes) => el('section', { class: 'ed-panel', 'data-step-panel': '', 'aria-labelledby': id }, ...nodes);
  panels = [
    panel('step-1',
      stepHead('step-1', 'Đề bài', 'Viết đề bài để học sinh biết cần làm gì, dữ liệu vào và kết quả ra như thế nào.', EXAMPLE.statement),
      el('div', { class: 'ed-sample' }, sampleBtn('đề bài',
        () => [titleF, statementF, inputFormatF, outputFormatF, constraintsF].some((f) => filled(f.value)),
        () => [titleF.value, difficultyF.value, statementF.value, inputFormatF.value, outputFormatF.value, constraintsF.value],
        () => { titleF.value = ex.title; difficultyF.value = ex.difficulty; statementF.value = ex.statement; inputFormatF.value = ex.inputFormat; outputFormatF.value = ex.outputFormat; constraintsF.value = ex.constraints; markDirty(); },
        (v) => { [titleF.value, difficultyF.value, statementF.value, inputFormatF.value, outputFormatF.value, constraintsF.value] = v; markDirty(); })),
      titleF.root, difficultyF.root, statementF.root, inputFormatF.root, outputFormatF.root, constraintsF.root),
    panel('step-2',
      stepHead('step-2', 'Ví dụ', 'Cho học sinh xem vài ví dụ để hiểu đề. Ví dụ không dùng để chấm bài.', EXAMPLE.examples),
      examples.root,
      el('h3', {}, 'Học sinh sẽ thấy'),
      exPreview),
    panel('step-3',
      stepHead('step-3', 'Bộ kiểm tra chấm bài', 'Mỗi bộ gồm dữ liệu vào và kết quả đúng. Hệ thống chấm bài học sinh bằng cách so kết quả; cần từ 3 đến 50 bộ.', EXAMPLE.tests),
      el('p', { class: 'hint' }, 'Khi so, hệ thống bỏ qua khoảng trắng thừa ở cuối mỗi dòng và các dòng trống ở cuối kết quả, nhưng phân biệt chữ hoa và chữ thường.'),
      el('p', { class: 'hint' }, 'Kiểm tra ẩn: học sinh chỉ thấy bài đạt hay chưa, không thấy dữ liệu. Nên ẩn vài bộ, nhất là trường hợp đặc biệt (số 0, số âm, giá trị lớn nhất).'),
      el('div', { class: 'row row--between ed-testbar' }, testCount, checkBtn),
      el('p', { class: 'hint' }, 'Chạy đáp án mẫu qua mọi bộ kiểm tra ngay trên máy bạn; không gửi gì lên máy chủ.'),
      checkBox,
      tests.root,
      testWarn),
    panel('step-4',
      stepHead('step-4', 'Đáp án và mã khởi tạo', 'Cho học sinh khung mã để bắt đầu và lưu đáp án mẫu để tự kiểm tra bộ chấm bài.', EXAMPLE.solution),
      el('div', { class: 'ed-sample' }, sampleBtn('đáp án',
        () => [starterF, solutionF, explanationF].some((f) => filled(f.value)),
        () => [starterF.value, solutionF.value, explanationF.value],
        () => { starterF.value = ex.starterCode; solutionF.value = ex.solutionCode; explanationF.value = ex.solutionExplanation; markDirty(); },
        (v) => { [starterF.value, solutionF.value, explanationF.value] = v; markDirty(); })),
      starterF.root, solutionF.root, explanationF.root,
      el('p', { class: 'hint' }, 'Học sinh chỉ thấy đáp án sau khi bấm nút Đáp án và xác nhận.'),
      iconBtn('Chạy thử với đáp án', 'code', () => checkSolution(checkBtn), { cls: 'btn' })),
    panel('step-5',
      stepHead('step-5', 'Nâng cao và xuất bản', 'Chỉnh thời gian chạy nếu cần, xem bài còn thiếu gì, rồi xuất bản để giao cho học sinh.'),
      adv,
      el('div', { class: 'row' }, stateHost),
      el('p', { class: 'muted' }, 'Bản nháp: chỉ giáo viên thấy. Đã xuất bản: học sinh được giao bài mới thấy.'),
      el('h3', {}, 'Kiểm tra trước khi xuất bản'),
      el('p', { class: 'muted' }, 'Danh sách này chỉ để nhắc bạn. Bạn vẫn có thể xuất bản khi còn thiếu mục.'),
      listBox,
      el('div', { class: 'row ed-publish' }, readyHost, publishBtn),
      isEdit ? null : el('p', { class: 'muted', id: 'publish-note' }, 'Lưu bài tập trước, rồi mới xuất bản được.')),
  ];
  wiz = wizard({ label: 'Các bước soạn bài tập', names: ['Đề bài', 'Ví dụ', 'Bộ kiểm tra chấm bài', 'Đáp án và mã khởi tạo', 'Nâng cao và xuất bản'], panels });
  bar = saveBar({ saveBtn, wiz, noun: 'bài tập' });
  if (!isEdit) publishBtn.setAttribute('aria-describedby', 'publish-note');

  const saved = formDraft.load(draftName);
  if (saved) {
    const restore = el('button', { type: 'button', class: 'btn btn--small' }, 'Khôi phục');
    const discard = el('button', { type: 'button', class: 'btn btn--small' }, 'Bỏ qua');
    restore.addEventListener('click', () => { applySnapshot(saved); dirty = true; bar.set('dirty'); clear(draftBox); update(); toast('ok', 'Đã khôi phục bản soạn dở.'); });
    discard.addEventListener('click', () => { formDraft.clear(draftName); clear(draftBox); });
    draftBox.append(banner('info', 'Có bản soạn dở chưa lưu từ lần trước. Bạn muốn khôi phục không? ', [restore, ' ', discard]));
  }
  if (params.get('created') === '1') say('ok', 'Đã tạo bài tập (bản nháp). Chạy thử với đáp án rồi xuất bản để có thể giao cho học sinh.');

  content.append(
    backLink('/teacher/lesson-edit.html?id=' + encodeURIComponent(lessonId) + '&step=3', '← Về bài học chứa bài tập này'),
    isEdit ? el('p', {}, el('a', { class: 'btn btn--small', href: `/problem.html?lesson=${encodeURIComponent(lessonId)}&problem=${encodeURIComponent(exerciseId)}`, target: '_blank', rel: 'noopener' }, 'Xem trang học sinh (bản đã lưu)')) : null,
    wiz.nav,
    el('div', { class: 'ed-edit' }, draftBox, warnBox, errors.root,
      el('form', { class: 'ed-form', novalidate: true, onsubmit: (e) => { e.preventDefault(); save(saveBtn); } }, panels)),
    bar.root);
  renderWarn();
  update();
  wiz.go(Math.min(4, Math.max(0, (Number.parseInt(params.get('step'), 10) || 1) - 1)));
  finishLoading();
}

build();

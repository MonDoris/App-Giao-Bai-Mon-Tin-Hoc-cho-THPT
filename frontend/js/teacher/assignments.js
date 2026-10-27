// Giao bài: biểu mẫu theo bước (bài học, bài tập, đối tượng, hạn nộp) với thẻ tóm tắt dính bên phải,
// và danh sách lượt giao dạng thẻ có tiến độ nộp bài. API và quy tắc kiểm tra giữ nguyên như bản cũ.
import { api, ApiError } from '/js/api.js';
import { initPage } from '/js/auth.js';
import { el, clear, stateBox, skeleton, difficultyInfo } from '/js/dom.js';
import { icon } from '/js/icons.js';
import { confirmDialog, showDialog } from '/js/modal.js';
import { banner, chip, debounce, field, formatDateTime, fromLocalInput, stateChip, timeZoneLabel, toLocalInputValue, withBusy } from '/js/ui.js';
import { difficultyLabel, errorText, finishLoading, pageContent, say } from '/js/teacher/common.js';

await initPage({ role: 'Teacher', active: 'assign' });

const content = pageContent();
let lessons = [];
let classes = [];
let detail = null;

// ---------------------------------------------------------------- Biểu mẫu giao bài
const lessonSelect = field({ id: 'lesson', label: 'Bài học', type: 'select', options: [{ value: '', label: 'Chọn bài học' }] });
const exerciseList = el('div', { class: 'as-exgrid' });
const exerciseCount = el('span', { class: 'as-count', 'aria-live': 'polite' });
const selectAll = el('input', { type: 'checkbox', id: 'select-all' });
const targetAll = el('input', { type: 'radio', name: 'target', id: 'target-all', value: 'all', checked: true });
const targetClasses = el('input', { type: 'radio', name: 'target', id: 'target-classes', value: 'classes' });
const classList = el('div', { class: 'as-pills' });
const dueF = field({ id: 'due', label: `Hạn nộp (giờ địa phương: ${timeZoneLabel()})`, type: 'datetime-local' });
const noteF = field({ id: 'note', label: 'Ghi chú cho học sinh', type: 'textarea', rows: 2, maxlength: 500, counter: true });
noteF.input.placeholder = 'Ví dụ: Làm xong trước buổi học tới, nhớ đọc lại phần Lưu ý.';
const formAlert = el('div', { role: 'alert' });
const submitBtn = el('button', { type: 'submit', class: 'btn btn--primary as-submit' }, icon('send'), el('span', {}, 'Giao bài'));
const summary = el('dl', { class: 'as-sum__list' });

/** Một bước của biểu mẫu: vòng số, tiêu đề, câu gợi ý ngắn và phần nội dung. */
function step(n, title, hint, ...body) {
  const id = 'as-step-' + n;
  return el('section', { class: 'as-step', 'aria-labelledby': id },
    el('div', { class: 'as-step__head' },
      el('span', { class: 'as-step__n', 'aria-hidden': 'true' }, String(n)),
      el('div', {}, el('h3', { id, class: 'as-step__title' }, title), hint ? el('p', { class: 'as-step__hint' }, hint) : null)),
    el('div', { class: 'as-step__body' }, ...body));
}

// Hạn nộp nhanh: đặt giá trị cho ô datetime-local (giờ địa phương), vẫn sửa tay được.
function quickDue(label, make) {
  const b = el('button', { type: 'button', class: 'btn btn--small as-quick' }, label);
  b.addEventListener('click', () => {
    const d = make();
    dueF.value = d ? toLocalInputValue(d.toISOString()) : '';
    dueF.clearError();
    renderSummary();
  });
  return b;
}
const endOfDay = (days) => {
  const d = new Date();
  d.setDate(d.getDate() + days);
  d.setHours(23, 59, 0, 0);
  return d;
};

const form = el('form', { class: 'as-form', novalidate: true },
  formAlert,
  step(1, 'Chọn bài học', 'Chỉ bài học đã xuất bản mới hiện với học sinh.', lessonSelect.root),
  step(2, 'Chọn bài tập', 'Bấm vào thẻ để chọn. Bài tập chưa xuất bản hoặc đã lưu trữ không giao được.',
    el('div', { class: 'as-exbar' }, el('label', { class: 'check', for: 'select-all' }, selectAll, 'Chọn tất cả bài tập của bài'), exerciseCount),
    exerciseList),
  step(3, 'Giao cho', null,
    el('div', { class: 'as-seg', role: 'radiogroup', 'aria-label': 'Giao cho' },
      el('label', { class: 'as-seg__opt', for: 'target-all' }, targetAll, icon('users'), el('span', {}, el('strong', {}, 'Tất cả học sinh'), el('span', { class: 'as-seg__sub' }, 'Mọi học sinh trong hệ thống'))),
      el('label', { class: 'as-seg__opt', for: 'target-classes' }, targetClasses, icon('list'), el('span', {}, el('strong', {}, 'Chọn lớp'), el('span', { class: 'as-seg__sub' }, 'Chỉ các lớp bạn đánh dấu')))),
    classList),
  step(4, 'Hạn nộp và ghi chú', 'Không bắt buộc. Bài nộp sau hạn vẫn được nhận nhưng ghi là nộp muộn.',
    dueF.root,
    el('div', { class: 'as-quickrow', role: 'group', 'aria-label': 'Chọn nhanh hạn nộp' },
      quickDue('Không có hạn', () => null),
      quickDue('Hết hôm nay', () => endOfDay(0)),
      quickDue('Ngày mai', () => endOfDay(1)),
      quickDue('3 ngày', () => endOfDay(3)),
      quickDue('1 tuần', () => endOfDay(7))),
    noteF.root));

const aside = el('aside', { class: 'as-sum', 'aria-labelledby': 'as-sum-title' },
  el('h2', { id: 'as-sum-title', class: 'as-sum__title' }, icon('clipboard'), 'Tóm tắt lượt giao'),
  summary,
  submitBtn,
  el('p', { class: 'as-sum__note' }, 'Học sinh thấy bài ngay sau khi giao. Bạn có thể sửa hạn nộp hoặc hủy giao ở danh sách bên dưới.'));
// Nút Giao bài nằm ngoài <form> (trong thẻ tóm tắt) nên gắn vào form bằng thuộc tính form.
form.id = 'assign-form';
submitBtn.setAttribute('form', 'assign-form');

function exerciseChecks() {
  return Array.from(exerciseList.querySelectorAll('input[type=checkbox]:not(:disabled)'));
}

function selectedClasses() {
  return classes.filter((c) => classList.querySelector(`#cls-${c.id}`)?.checked);
}

function renderSummary() {
  const picked = exerciseChecks().filter((c) => c.checked);
  const cls = selectedClasses();
  const due = fromLocalInput(dueF.value);
  const row = (ic, label, value, ok) => [
    el('dt', {}, icon(ic), label),
    el('dd', { class: ok ? '' : 'is-missing' }, value),
  ];
  const target = targetAll.checked ? 'Tất cả học sinh'
    : cls.length ? cls.map((c) => c.name).join(', ') : 'Chưa chọn lớp';
  summary.replaceChildren(
    ...row('book', 'Bài học', detail ? detail.title : 'Chưa chọn', !!detail),
    ...row('code', 'Bài tập', picked.length ? `${picked.length} bài tập` : 'Chưa chọn', picked.length > 0),
    ...row('users', 'Giao cho', target, targetAll.checked || cls.length > 0),
    ...row('calendar', 'Hạn nộp', due ? formatDateTime(due) : 'Không có hạn', true));
  exerciseCount.textContent = detail && detail.exercises.length ? `Đã chọn ${picked.length}/${exerciseChecks().length}` : '';
}

function renderExercises() {
  clear(exerciseList);
  selectAll.checked = false;
  if (!detail) {
    exerciseList.append(el('p', { class: 'as-empty' }, icon('book'), 'Hãy chọn một bài học ở bước 1.'));
    renderSummary();
    return;
  }
  if (!detail.exercises.length) {
    exerciseList.append(el('p', { class: 'as-empty' }, icon('inbox'), 'Bài học này chưa có bài tập.'));
    renderSummary();
    return;
  }
  if (!detail.isPublished) exerciseList.append(banner('warn', 'Bài học này đang là bản nháp: hãy xuất bản bài học để học sinh nhìn thấy bài tập được giao.'));
  detail.exercises.forEach((ex, i) => {
    const ok = ex.isPublished && !ex.isArchived && detail.isPublished && !detail.isArchived;
    const box = el('input', { type: 'checkbox', id: 'ex-' + ex.id, value: ex.id, disabled: !ok, class: 'as-ex__input' });
    box.addEventListener('change', renderSummary);
    exerciseList.append(el('label', { class: 'as-ex' + (ok ? '' : ' is-disabled'), for: 'ex-' + ex.id },
      box,
      el('span', { class: 'as-ex__tick', 'aria-hidden': 'true' }, icon('check')),
      el('span', { class: 'as-ex__body' },
        el('span', { class: 'as-ex__title' }, `${i + 1}. ${ex.title}`),
        el('span', { class: 'as-ex__meta' },
          el('span', { class: 'badge ' + difficultyInfo(String(ex.difficulty).toLowerCase()).cls }, difficultyLabel(ex.difficulty)),
          el('span', { class: 'muted' }, `${ex.testCases.length} bộ kiểm tra`)),
        ok ? null : el('span', { class: 'as-ex__why' }, icon('alert'), ex.isArchived ? 'Đã lưu trữ, không giao được' : 'Chưa xuất bản, hãy xuất bản trước khi giao'))));
  });
  renderSummary();
}

selectAll.addEventListener('change', () => {
  exerciseChecks().forEach((c) => { c.checked = selectAll.checked; });
  renderSummary();
});

async function loadLesson(id) {
  detail = null;
  if (id) {
    try {
      detail = await api.teacherLesson(id);
    } catch (err) {
      say('error', errorText(err));
    }
  }
  renderExercises();
}

lessonSelect.input.addEventListener('change', () => loadLesson(lessonSelect.value));

function setClassesEnabled() {
  classList.querySelectorAll('input').forEach((c) => { c.disabled = !targetClasses.checked; });
  classList.classList.toggle('is-off', !targetClasses.checked);
  renderSummary();
}
targetAll.addEventListener('change', setClassesEnabled);
targetClasses.addEventListener('change', setClassesEnabled);
dueF.input.addEventListener('input', debounce(renderSummary, 150));

function renderClassChoices() {
  clear(classList);
  if (!classes.length) {
    classList.append(el('span', { class: 'muted' }, 'Chưa có lớp nào. Hãy tạo lớp ở mục "Học sinh".'));
    targetClasses.disabled = true;
    return;
  }
  for (const c of classes) {
    const box = el('input', { type: 'checkbox', id: 'cls-' + c.id, value: String(c.id), disabled: true });
    box.addEventListener('change', renderSummary);
    classList.append(el('label', { class: 'as-pill', for: 'cls-' + c.id }, box, el('span', {}, c.name), el('span', { class: 'as-pill__n mono' }, String(c.memberCount))));
  }
  classList.classList.add('is-off');
}

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  clear(formAlert);
  dueF.clearError();
  const exerciseIds = exerciseChecks().filter((c) => c.checked).map((c) => c.value);
  const classIds = targetClasses.checked ? Array.from(classList.querySelectorAll('input:checked')).map((c) => Number(c.value)) : [];
  if (!exerciseIds.length) return formAlert.append(banner('error', 'Hãy chọn ít nhất một bài tập để giao.'));
  if (targetClasses.checked && !classIds.length) return formAlert.append(banner('error', 'Hãy chọn ít nhất một lớp hoặc chọn "Tất cả học sinh".'));
  const dueAt = fromLocalInput(dueF.value);
  if (dueAt && Date.parse(dueAt) <= Date.now()) {
    dueF.setError('Hạn nộp phải sau thời điểm hiện tại.');
    return dueF.input.focus();
  }

  await withBusy(submitBtn, 'Đang giao…', async () => {
    try {
      const result = await api.assign({
        exerciseIds,
        target: { all: targetAll.checked, classIds },
        dueAt,
        note: noteF.value.trim() || null,
      });
      say('ok', `Đã giao ${result.assignments.length} bài tập. ${result.studentCount} học sinh sẽ nhìn thấy các bài này.`);
      exerciseChecks().forEach((c) => { c.checked = false; });
      selectAll.checked = false;
      renderSummary();
      await loadTable();
    } catch (err) {
      if (err instanceof ApiError && (err.status === 422 || err.status === 404)) {
        formAlert.append(banner('error', err.message || 'Dữ liệu giao bài không hợp lệ.'));
      } else if (!(err instanceof ApiError && err.status === 401)) {
        formAlert.append(banner('error', 'Chưa giao được: ' + errorText(err) + ' Bạn có thể bấm "Giao bài" để thử lại.'));
      }
    }
  });
});

// ---------------------------------------------------------------- Danh sách lượt giao
const filterLesson = field({ id: 'f-lesson', label: 'Bài học', type: 'select', options: [{ value: '', label: 'Tất cả bài học' }] });
const filterClass = field({ id: 'f-class', label: 'Lớp', type: 'select', options: [{ value: '', label: 'Tất cả' }] });
const cancelledBox = el('input', { type: 'checkbox', id: 'f-cancelled' });
const listHost = el('div', { 'aria-live': 'polite' });
const listCount = el('span', { class: 'as-count' });

function targetText(a) {
  return a.assignedToAll ? 'Tất cả học sinh' : a.classNames.join(', ') || '(chưa có lớp)';
}

/** Chip hạn nộp: còn bao lâu, đã hết hạn, hoặc không có hạn. */
function dueChip(a) {
  if (a.cancelledAt) return chip('chip--archived', 'archive', 'Đã hủy');
  if (!a.dueAt) return chip('chip--info', 'clock', 'Không có hạn');
  const ms = Date.parse(a.dueAt) - Date.now();
  if (ms <= 0) return chip('chip--late', 'alert', 'Đã hết hạn');
  const hours = Math.floor(ms / 3600000);
  return chip(hours < 48 ? 'chip--draft' : 'chip--published', 'clock', hours < 24 ? `Còn ${Math.max(1, hours)} giờ` : `Còn ${Math.floor(hours / 24)} ngày`);
}

/** Thanh tiến độ: đạt / đã nộp nhưng chưa đạt / chưa nộp (kèm chữ số, không chỉ màu). */
function progress(a) {
  const total = Math.max(0, a.assignedStudents);
  const pct = (n) => (total ? (n / total) * 100 : 0);
  const passed = Math.min(a.passedStudents, total);
  const tried = Math.max(0, Math.min(a.submittedStudents, total) - passed);
  const bar = el('div', { class: 'as-bar', role: 'img', 'aria-label': `${a.submittedStudents} trên ${total} học sinh đã nộp, ${a.passedStudents} đạt` },
    el('span', { class: 'as-bar__seg as-bar__seg--pass' }), el('span', { class: 'as-bar__seg as-bar__seg--try' }));
  bar.firstChild.style.width = pct(passed) + '%';
  bar.lastChild.style.width = pct(tried) + '%';
  return el('div', { class: 'as-prog' },
    bar,
    el('div', { class: 'as-prog__nums' },
      el('span', {}, el('strong', { class: 'mono' }, `${a.submittedStudents}/${total}`), ' đã nộp'),
      el('span', { class: 'as-prog__pass' }, el('strong', { class: 'mono' }, String(a.passedStudents)), ' đạt')));
}

async function editAssignment(a, opener) {
  const due = field({ id: 'e-due', label: `Hạn nộp (giờ địa phương: ${timeZoneLabel()})`, type: 'datetime-local', value: toLocalInputValue(a.dueAt) });
  const note = field({ id: 'e-note', label: 'Ghi chú', type: 'textarea', rows: 2, maxlength: 500, value: a.note || '', counter: true });
  const all = el('input', { type: 'radio', name: 'e-target', id: 'e-all', checked: a.assignedToAll });
  const some = el('input', { type: 'radio', name: 'e-target', id: 'e-some', checked: !a.assignedToAll });
  const boxes = classes.map((c) => ({ c, box: el('input', { type: 'checkbox', id: 'e-cls-' + c.id, checked: a.classIds.includes(c.id) }) }));
  const err = el('div', { role: 'alert' });
  const warnPast = el('p', { class: 'hint' }, 'Khi sửa, bạn được phép đặt hạn nộp về quá khứ; bài nộp từ lúc đó sẽ bị ghi nhận là nộp muộn.');
  const choice = await showDialog({
    title: `Sửa lượt giao: ${a.exerciseTitle}`,
    wide: true,
    opener,
    closeValue: false,
    content: el('div', {}, err, due.root, warnPast, note.root,
      el('fieldset', {}, el('legend', {}, 'Giao cho'),
        el('div', { class: 'row' }, el('label', { class: 'check', for: 'e-all' }, all, 'Tất cả học sinh'), el('label', { class: 'check', for: 'e-some' }, some, 'Chọn lớp')),
        el('div', { class: 'as-pills' }, boxes.map(({ c, box }) => el('label', { class: 'as-pill', for: box.id }, box, el('span', {}, c.name)))))),
    buttons: [
      { text: 'Hủy', value: false },
      {
        text: 'Lưu',
        value: true,
        kind: 'primary',
        action: async () => {
          clear(err);
          const classIds = all.checked ? [] : boxes.filter((b) => b.box.checked).map((b) => b.c.id);
          if (!all.checked && !classIds.length) { err.append(banner('error', 'Hãy chọn ít nhất một lớp hoặc "Tất cả học sinh".')); return false; }
          try {
            await api.updateAssignment(a.id, { target: { all: all.checked, classIds }, dueAt: fromLocalInput(due.value), note: note.value.trim() || null });
          } catch (e) {
            err.append(banner('error', errorText(e)));
            return false;
          }
          return true;
        },
      },
    ],
  });
  if (choice) {
    say('ok', 'Đã cập nhật lượt giao bài.');
    await loadTable();
  }
}

async function cancelAssignment(a, opener) {
  const ok = await confirmDialog({
    title: 'Hủy giao bài?',
    message: `Hủy giao "${a.exerciseTitle}" cho ${targetText(a)}. Nếu đã có bài nộp, lượt giao chỉ bị hủy mềm: học sinh mất quyền truy cập nhưng bài nộp và báo cáo của bạn vẫn được giữ.`,
    confirmText: 'Hủy giao',
    opener,
    danger: true,
  });
  if (!ok) return;
  try {
    await api.deleteAssignment(a.id);
    say('ok', 'Đã hủy giao bài.');
    await loadTable();
  } catch (err) {
    say('error', errorText(err));
  }
}

function assignmentCard(a) {
  const mk = (text, ic, handler, kind = '') => {
    const b = el('button', { type: 'button', class: 'btn btn--small ' + kind, 'aria-label': `${text}: ${a.exerciseTitle}` }, icon(ic), el('span', {}, text));
    b.addEventListener('click', () => handler(a, b));
    return b;
  };
  return el('li', { class: 'as-item' + (a.cancelledAt ? ' is-cancelled' : '') },
    el('div', { class: 'as-item__main' },
      el('p', { class: 'as-item__lesson' }, icon('book'), a.lessonTitle),
      el('h3', { class: 'as-item__title' }, a.exerciseTitle),
      el('div', { class: 'as-item__chips' },
        dueChip(a),
        chip('chip--info', a.assignedToAll ? 'users' : 'list', targetText(a)),
        a.dueAt ? el('span', { class: 'muted' }, 'Hạn ', el('span', { class: 'mono' }, formatDateTime(a.dueAt))) : null),
      a.note ? el('p', { class: 'as-item__note' }, a.note) : null),
    progress(a),
    a.cancelledAt ? el('div', { class: 'as-item__actions' }, stateChip('archived'))
      : el('div', { class: 'as-item__actions' }, mk('Sửa', 'pencil', editAssignment), mk('Hủy giao', 'x', cancelAssignment, 'as-cancel')));
}

async function loadTable() {
  if (!listHost.firstChild) listHost.append(skeleton({ shape: 'rows', count: 3, label: 'Đang tải các lượt giao bài…' }));
  try {
    const data = await api.assignments({ lessonId: filterLesson.value, classId: filterClass.value, includeCancelled: cancelledBox.checked });
    clear(listHost);
    const list = data.assignments || [];
    listCount.textContent = list.length ? `${list.length} lượt` : '';
    if (!list.length) {
      listHost.append(stateBox({ kind: 'empty', title: 'Chưa có lượt giao bài nào', message: 'Hãy chọn bài học và bài tập ở biểu mẫu phía trên rồi bấm "Giao bài".' }));
      return;
    }
    listHost.append(el('ul', { class: 'as-list' }, list.map(assignmentCard)));
    listHost.append(el('p', { class: 'hint' }, 'Chấm tại trình duyệt học sinh – chưa xác minh.'));
  } catch (err) {
    clear(listHost);
    listHost.append(stateBox({ kind: 'error', title: errorText(err), actions: [el('button', { type: 'button', class: 'btn btn--primary', onclick: loadTable }, 'Thử lại')] }));
  }
}

async function build() {
  try {
    [lessons, classes] = await Promise.all([
      api.teacherLessons(false).then((d) => d.lessons || []),
      api.classes().then((d) => d.classes || []),
    ]);
  } catch (err) {
    content.append(stateBox({ kind: 'error', title: errorText(err) }));
    finishLoading();
    return;
  }
  for (const l of lessons) {
    const label = `${l.order}. ${l.title}${l.isPublished ? '' : ' (bản nháp)'}`;
    lessonSelect.input.append(el('option', { value: l.id }, label));
    filterLesson.input.append(el('option', { value: l.id }, label));
  }
  for (const c of classes) filterClass.input.append(el('option', { value: String(c.id) }, c.name));
  renderClassChoices();
  renderExercises();

  const preset = new URLSearchParams(location.search).get('lesson');
  if (preset && lessons.some((l) => l.id === preset)) {
    lessonSelect.value = preset;
    await loadLesson(preset);
  }

  const toolbar = el('div', { class: 'as-filters' }, filterLesson.root, filterClass.root, el('label', { class: 'check', for: 'f-cancelled' }, cancelledBox, 'Hiện cả lượt giao đã hủy'));
  for (const f of [filterLesson.input, filterClass.input, cancelledBox]) f.addEventListener('change', loadTable);
  content.append(
    el('p', { class: 'lead as-lead' }, 'Chọn bài tập, chọn lớp và đặt hạn nộp. Học sinh thấy bài ngay sau khi giao.'),
    el('section', { class: 'as-new', 'aria-labelledby': 'as-new-title' },
      el('h2', { id: 'as-new-title', class: 'as-h2' }, 'Giao bài mới'),
      el('div', { class: 'as-layout' }, form, aside)),
    el('section', { class: 'as-section', 'aria-labelledby': 'list-title' },
      el('div', { class: 'as-section__head' }, el('h2', { id: 'list-title', class: 'as-h2' }, 'Các lượt giao bài'), listCount),
      toolbar, listHost));
  await loadTable();
  finishLoading();
}

build();

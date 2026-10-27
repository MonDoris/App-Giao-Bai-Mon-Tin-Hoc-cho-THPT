// Cổng ra DUY NHẤT tới backend (QD-88, AC-487): giao diện chỉ lấy/gửi dữ liệu qua /api/..., địa chỉ lấy từ API_BASE (js/config.js).
// Cookie phiên là HttpOnly (mã JavaScript không đọc được) nên mọi fetch dùng credentials: 'include';
// mọi yêu cầu ghi gửi kèm token CSRF trong header X-CSRF-TOKEN.
import { API_BASE } from '/js/config.js';
import { homeFor, joinApiUrl } from '/js/guard.js';
import { clearSignedIn, hasSignedInHint, markSignedIn } from '/js/storage.js';

export { homeFor };

/** URL đầy đủ của một đường dẫn API: API_BASE (đã cắt dấu / cuối) + path. API_BASE rỗng thì ra URL tương đối. */
function apiUrl(path) {
  return joinApiUrl(API_BASE, path);
}

export class ApiError extends Error {
  constructor({ kind, status = 0, code = '', message = '', field = '', errors = [], retryAfter = 0 }) {
    super(message || code || kind);
    this.kind = kind;        // 'network' | 'http'
    this.status = status;
    this.code = code;        // NOT_FOUND | VALIDATION_FAILED | CONFLICT | ...
    this.field = field;
    this.errors = errors;    // [{ field, message }]
    this.retryAfter = retryAfter;
  }
}

let csrfToken = null;
let csrfHeader = 'X-CSRF-TOKEN';
let pageUserId = null;
let redirecting = false;

/** Trang gọi sau khi biết người dùng hiện tại để phát hiện đổi tài khoản ở tab khác (AC-100). */
export function setPageUserId(id) {
  pageUserId = id;
}

/**
 * Chuyển tới trang đăng nhập (G1, AC-516): không kèm returnUrl khi đang ở landing hoặc đăng nhập;
 * chỉ thêm expired=1 khi trước đó thật sự đã đăng nhập (có cờ gợi ý); luôn xóa cờ; chỉ chạy một lần.
 */
export function goToLogin({ expired = false } = {}) {
  if (redirecting) return;
  redirecting = true;
  const wasSignedIn = hasSignedInHint();
  clearSignedIn();
  const path = location.pathname.toLowerCase();
  const params = new URLSearchParams();
  if (!/^\/(index\.html)?$/.test(path) && !path.startsWith('/login.html')) params.set('returnUrl', location.pathname + location.search);
  if (expired && wasSignedIn) params.set('expired', '1');
  const query = params.toString();
  location.href = '/login.html' + (query ? '?' + query : '');
}

async function readBody(response) {
  if (response.status === 204) return null;
  try {
    return await response.json();
  } catch (e) {
    return null;
  }
}

async function loadCsrf() {
  const response = await fetch(apiUrl('/api/auth/csrf'), { headers: { Accept: 'application/json' }, credentials: 'include', cache: 'no-store' });
  const body = await readBody(response);
  if (!response.ok || !body || !body.token) throw new ApiError({ kind: 'network', message: 'Không lấy được mã bảo vệ từ máy chủ' });
  csrfToken = body.token;
  csrfHeader = body.headerName || csrfHeader;
}

async function currentToken() {
  if (!csrfToken) await loadCsrf();
  return csrfToken;
}

/** Sau khi đăng nhập/đăng xuất/đổi mật khẩu, token cũ không còn hợp lệ (token gắn với danh tính). */
export function forgetCsrf() {
  csrfToken = null;
}

async function identityChanged() {
  try {
    const response = await fetch(apiUrl('/api/auth/me'), { headers: { Accept: 'application/json' }, credentials: 'include', cache: 'no-store' });
    if (response.status === 401) return true;
    const me = await readBody(response);
    return !!me && pageUserId !== null && me.id !== pageUserId;
  } catch (e) {
    return false;
  }
}

/**
 * Gửi một yêu cầu JSON.
 * opts.anonymous: không chuyển hướng sang đăng nhập khi gặp 401 (dùng ở trang đăng nhập).
 */
async function call(method, path, body, opts = {}) {
  const unsafe = method !== 'GET';
  let attempt = 0;
  for (;;) {
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (unsafe) {
      try {
        headers[csrfHeader] = await currentToken();
      } catch (e) {
        throw e instanceof ApiError ? e : new ApiError({ kind: 'network', message: 'Không kết nối được máy chủ' });
      }
    }

    let response;
    try {
      response = await fetch(apiUrl(path), {
        method,
        headers,
        credentials: 'include',
        cache: 'no-store',
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch (e) {
      throw new ApiError({ kind: 'network', message: 'Không kết nối được máy chủ' });
    }

    const data = await readBody(response);
    if (response.ok) {
      if (data === null && response.status !== 204) {
        throw new ApiError({ kind: 'network', message: 'Không đọc được dữ liệu từ máy chủ' });
      }
      return data;
    }

    const error = new ApiError({
      kind: 'http',
      status: response.status,
      code: data?.error ?? '',
      message: data?.message ?? '',
      field: data?.field ?? '',
      errors: Array.isArray(data?.errors) ? data.errors : [],
      retryAfter: Number(response.headers.get('Retry-After')) || 0,
    });

    if (error.code === 'CSRF_INVALID' && attempt === 0) {
      // Token hết hạn hoặc đã đổi tài khoản: nếu tài khoản khác thì tải lại trang, nếu không thì thử lại một lần với token mới.
      attempt += 1;
      forgetCsrf();
      if (await identityChanged()) {
        if (pageUserId !== null) location.reload();
        throw error;
      }
      continue;
    }
    if (error.status === 401) {
      // Phiên không còn hiệu lực: bỏ gợi ý "đã từng đăng nhập" (AC-512). goToLogin đọc cờ để biết có thêm expired=1 rồi mới xóa.
      if (opts.anonymous) clearSignedIn();
      else goToLogin({ expired: true });
    } else if (error.status === 403 && error.code === 'PASSWORD_CHANGE_REQUIRED') {
      location.href = '/change-password.html';
    }
    throw error;
  }
}

const enc = encodeURIComponent;
const query = (params) => {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') q.set(k, String(v));
  const s = q.toString();
  return s ? '?' + s : '';
};

export const api = {
  // ---- xác thực
  me: (opts) => call('GET', '/api/auth/me', undefined, opts),
  login: async (body) => {
    const result = await call('POST', '/api/auth/login', body, { anonymous: true });
    forgetCsrf();
    markSignedIn();
    return result;
  },
  logout: async () => {
    try {
      await call('POST', '/api/auth/logout', undefined, { anonymous: true });
    } finally {
      forgetCsrf();
      clearSignedIn(); // đúng cả khi API đăng xuất lỗi (AC-512)
    }
  },
  changePassword: async (body) => {
    await call('POST', '/api/auth/change-password', body);
    forgetCsrf();
  },

  // ---- nội dung dùng chung (học sinh: đã lọc theo bài được giao)
  lessons: () => call('GET', '/api/lessons'),
  lesson: (id) => call('GET', '/api/lessons/' + enc(id)),
  solution: (exerciseId) => call('GET', '/api/exercises/' + enc(exerciseId) + '/solution'),

  // ---- học sinh
  myAssignments: () => call('GET', '/api/me/assignments'),
  submit: (exerciseId, body) => call('POST', '/api/exercises/' + enc(exerciseId) + '/submissions', body),
  mySubmissions: (exerciseId) => call('GET', '/api/me/exercises/' + enc(exerciseId) + '/submissions'),
  mySubmission: (id) => call('GET', '/api/me/submissions/' + enc(id)),

  // ---- giáo viên: soạn bài
  teacherLessons: (includeArchived = true) => call('GET', '/api/teacher/lessons' + query({ includeArchived })),
  teacherLesson: (id) => call('GET', '/api/teacher/lessons/' + enc(id)),
  createLesson: (body) => call('POST', '/api/teacher/lessons', body),
  updateLesson: (id, body) => call('PUT', '/api/teacher/lessons/' + enc(id), body),
  deleteLesson: (id) => call('DELETE', '/api/teacher/lessons/' + enc(id)),
  publishLesson: (id, isPublished) => call('PATCH', '/api/teacher/lessons/' + enc(id) + '/publish', { isPublished }),
  archiveLesson: (id, isArchived) => call('PATCH', '/api/teacher/lessons/' + enc(id) + '/archive', { isArchived }),
  reorderLessons: (ids) => call('POST', '/api/teacher/lessons/reorder', { ids }),
  teacherExercise: (id) => call('GET', '/api/teacher/exercises/' + enc(id)),
  createExercise: (lessonId, body) => call('POST', '/api/teacher/lessons/' + enc(lessonId) + '/exercises', body),
  updateExercise: (id, body) => call('PUT', '/api/teacher/exercises/' + enc(id), body),
  deleteExercise: (id) => call('DELETE', '/api/teacher/exercises/' + enc(id)),
  publishExercise: (id, isPublished) => call('PATCH', '/api/teacher/exercises/' + enc(id) + '/publish', { isPublished }),
  archiveExercise: (id, isArchived) => call('PATCH', '/api/teacher/exercises/' + enc(id) + '/archive', { isArchived }),
  reorderExercises: (lessonId, ids) => call('POST', '/api/teacher/lessons/' + enc(lessonId) + '/exercises/reorder', { ids }),

  // ---- giáo viên: giao bài, tiến độ, học sinh, lớp
  assignments: (params = {}) => call('GET', '/api/teacher/assignments' + query(params)),
  assign: (body) => call('POST', '/api/teacher/assignments', body),
  updateAssignment: (id, body) => call('PUT', '/api/teacher/assignments/' + enc(id), body),
  deleteAssignment: (id) => call('DELETE', '/api/teacher/assignments/' + enc(id)),
  progress: (params) => call('GET', '/api/teacher/progress' + query(params)),
  studentSubmissions: (studentId, exerciseId) =>
    call('GET', '/api/teacher/progress/students/' + enc(studentId) + '/exercises/' + enc(exerciseId) + '/submissions'),
  students: (params = {}) => call('GET', '/api/teacher/students' + query(params)),
  createStudent: (body) => call('POST', '/api/teacher/students', body),
  importStudents: (body) => call('POST', '/api/teacher/students/import', body),
  updateStudent: (id, body) => call('PATCH', '/api/teacher/students/' + enc(id), body),
  resetPassword: (id) => call('POST', '/api/teacher/students/' + enc(id) + '/reset-password'),
  unlockStudent: (id) => call('POST', '/api/teacher/students/' + enc(id) + '/unlock'),
  classes: () => call('GET', '/api/teacher/classes'),
  createClass: (name) => call('POST', '/api/teacher/classes', { name }),
  renameClass: (id, name) => call('PUT', '/api/teacher/classes/' + enc(id), { name }),
  deleteClass: (id) => call('DELETE', '/api/teacher/classes/' + enc(id)),
};

/** Thông điệp thân thiện (tiếng Việt) cho từng loại lỗi. */
export function describeError(err, { notFound = 'Không tìm thấy', invalidPrefix = 'Dữ liệu bị lỗi' } = {}) {
  if (err instanceof ApiError) {
    if (err.kind === 'network') return { title: 'Không kết nối được máy chủ', message: 'Hãy kiểm tra kết nối rồi thử lại.', retry: true };
    if (err.status === 401) return { title: 'Phiên đăng nhập đã hết hạn', message: 'Vui lòng đăng nhập lại.', retry: false };
    if (err.status === 403) return { title: 'Bạn không có quyền thực hiện thao tác này', message: '', retry: false };
    if (err.status === 404) return { title: notFound, message: '', retry: false };
    if (err.status === 409) return { title: err.message || 'Dữ liệu đã thay đổi, hãy tải lại', message: '', retry: false };
    if (err.status === 429) return { title: 'Bạn thao tác quá nhanh, hãy thử lại sau ít phút', message: '', retry: true };
    if (err.code === 'DATA_INVALID') return { title: `${invalidPrefix}: ${err.field || 'không xác định'}`, message: err.message, retry: false };
    if (err.status === 422) return { title: err.message || 'Dữ liệu không hợp lệ', message: '', retry: false };
    if (err.status === 503) return { title: 'Máy chủ dữ liệu đang bận, thử lại sau', message: '', retry: true };
    return { title: 'Đã xảy ra lỗi trên máy chủ', message: 'Vui lòng thử lại sau.', retry: true };
  }
  return { title: 'Đã xảy ra lỗi không mong muốn', message: 'Vui lòng tải lại trang.', retry: true };
}

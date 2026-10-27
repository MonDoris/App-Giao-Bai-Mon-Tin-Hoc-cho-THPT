// Chỉ lưu BẢN NHÁP code trong localStorage, khóa theo userId: th12.<userId>.code.<exerciseId> (QD-40).
// Trạng thái học tập (đã đạt, đã xem đáp án...) lấy từ máy chủ. Không bao giờ lưu mật khẩu hoặc token.
// Mọi thao tác có try/catch; khi localStorage bị chặn thì dùng bộ nhớ tạm của trang (không báo lỗi).

const memory = new Map();
let userId = null;

/** Gọi sau khi biết người dùng hiện tại; chưa biết thì không đọc/ghi gì. */
export function setStorageUser(id) {
  userId = id;
}

const fullKey = (key) => `th12.${userId}.${key}`;

function read(key) {
  if (userId === null) return null;
  const k = fullKey(key);
  try {
    const v = window.localStorage.getItem(k);
    return v === null ? (memory.get(k) ?? null) : v;
  } catch (e) {
    return memory.get(k) ?? null;
  }
}

function write(key, value) {
  if (userId === null) return;
  const k = fullKey(key);
  memory.set(k, value);
  try {
    window.localStorage.setItem(k, value);
  } catch (e) {
    /* bị chặn hoặc đầy: bỏ qua */
  }
}

function remove(key) {
  if (userId === null) return;
  const k = fullKey(key);
  memory.delete(k);
  try {
    window.localStorage.removeItem(k);
  } catch (e) {
    /* bỏ qua */
  }
}

export const drafts = {
  getCode(exerciseId) {
    return read('code.' + exerciseId);
  },
  saveCode(exerciseId, code) {
    write('code.' + exerciseId, code);
  },
  clearCode(exerciseId) {
    remove('code.' + exerciseId);
  },
};

/**
 * Bản nháp biểu mẫu của giáo viên giữ tạm trong sessionStorage (AC-126): khôi phục sau khi đăng nhập lại
 * chỉ khi cùng userId. Là nội dung bài soạn, không chứa mật khẩu.
 */
export const formDraft = {
  save(name, data) {
    if (userId === null) return;
    try {
      window.sessionStorage.setItem(fullKey('form.' + name), JSON.stringify(data));
    } catch (e) {
      /* bỏ qua */
    }
  },
  load(name) {
    if (userId === null) return null;
    try {
      const raw = window.sessionStorage.getItem(fullKey('form.' + name));
      return raw ? JSON.parse(raw) : null;
    } catch (e) {
      return null;
    }
  },
  clear(name) {
    if (userId === null) return;
    try {
      window.sessionStorage.removeItem(fullKey('form.' + name));
    } catch (e) {
      /* bỏ qua */
    }
  },
};

// ---- Gợi ý "đã từng đăng nhập" (QD-94, AC-512) ---------------------------------------------------
// Cờ không nhạy cảm th12.signedin = "1" (không chứa id, vai trò hay tên): chỉ để landing/đăng nhập biết có nên hỏi
// /api/auth/me hay không. Không phải bằng chứng đăng nhập; phiên thật nằm ở cookie HttpOnly do máy chủ cấp.
const SIGNEDIN_KEY = 'th12.signedin';

export function markSignedIn() {
  try {
    window.localStorage.setItem(SIGNEDIN_KEY, '1');
  } catch (e) {
    /* bị chặn: coi như không có cờ */
  }
}

export function clearSignedIn() {
  try {
    window.localStorage.removeItem(SIGNEDIN_KEY);
  } catch (e) {
    /* bỏ qua */
  }
}

export function hasSignedInHint() {
  try {
    return window.localStorage.getItem(SIGNEDIN_KEY) === '1';
  } catch (e) {
    return false;
  }
}

// ---- Tùy chọn giao diện -------------------------------------------------------------------------
// Giao diện sáng/tối: khóa th12.theme KHÔNG gắn userId (dùng được cả ở trang đăng nhập); không có khóa = Tự động.
const THEME_KEY = 'th12.theme';
let themeMemory = null;
const SIDEBAR_KEY = 'th12.sidebar';
let sidebarMemory = null;

export const uiPrefs = {
  /** 'light' | 'dark' | null (Tự động). Giá trị lạ coi như Tự động. */
  getTheme() {
    let value = themeMemory;
    try {
      const saved = window.localStorage.getItem(THEME_KEY);
      if (saved !== null) value = saved;
    } catch (e) {
      /* bị chặn: dùng bộ nhớ tạm */
    }
    return value === 'light' || value === 'dark' ? value : null;
  },
  /** value: 'light' | 'dark' | null (Tự động = xóa khóa). */
  setTheme(value) {
    themeMemory = value === 'light' || value === 'dark' ? value : null;
    try {
      if (themeMemory) window.localStorage.setItem(THEME_KEY, themeMemory);
      else window.localStorage.removeItem(THEME_KEY);
    } catch (e) {
      /* bỏ qua */
    }
  },
  /** 'collapsed' | 'expanded' (thanh bên; khóa th12.sidebar không gắn userId, giá trị lạ coi là expanded). */
  getSidebar() {
    let value = sidebarMemory;
    try {
      const saved = window.localStorage.getItem(SIDEBAR_KEY);
      if (saved !== null) value = saved;
    } catch (e) {
      /* bị chặn: dùng bộ nhớ tạm */
    }
    return value === 'collapsed' ? 'collapsed' : 'expanded';
  },
  setSidebar(value) {
    sidebarMemory = value === 'collapsed' ? 'collapsed' : 'expanded';
    try {
      window.localStorage.setItem(SIDEBAR_KEY, sidebarMemory);
    } catch (e) {
      /* bỏ qua */
    }
  },
  /** Tỉ lệ khung đề bài (30-70, mặc định 50), theo userId (QD-55). */
  getSplit() {
    const n = Number(read('split'));
    return Number.isInteger(n) && n >= 30 && n <= 70 ? n : 50;
  },
  setSplit(value) {
    const n = Math.round(Number(value));
    if (Number.isFinite(n) && n >= 30 && n <= 70) write('split', String(n));
  },
  /** Kiểu xem danh sách bài học của giáo viên: 'grid' (mặc định) | 'list', theo userId. */
  getLessonView() {
    return read('lessonView') === 'list' ? 'list' : 'grid';
  },
  setLessonView(value) {
    write('lessonView', value === 'list' ? 'list' : 'grid');
  },
  /** Khung xem trước của trình soạn bài (màn rộng): true = hiện (mặc định), theo userId. */
  getEditorPreview() {
    return read('edPreview') !== 'off';
  },
  setEditorPreview(on) {
    write('edPreview', on ? 'on' : 'off');
  },
};

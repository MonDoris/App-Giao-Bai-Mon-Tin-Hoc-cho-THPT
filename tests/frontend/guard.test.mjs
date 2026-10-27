// Kiểm thử guard phía trình duyệt (AC-507, AC-517) bằng Node thuần, không cần gói ngoài:
//   node --test tests/frontend
// Cùng bảng véc-tơ với tests/Application.Tests/ReturnUrlPolicyTests.cs (mục 4.1 của đặc tả v6) để máy chủ và client luôn khớp.
// Cần Node >= 20.19 hoặc >= 22.7 (tự nhận ES module cho tệp .js không có package.json).
import test from 'node:test';
import assert from 'node:assert/strict';
import { homeFor, isSafeRedirect, joinApiUrl, sanitizeReturnUrl, signedInTarget } from '../../frontend/js/guard.js';

const BOTH_NULL = [
  '/', '/index.html', '/index.html?x=1', '/index.html#a', '/INDEX.HTML', '/landing.html', '/landing.html?x=1',
  '/login.html', '/login.html?returnUrl=%2F', '/api/lessons', '/API/me',
  '//evil.com', '/\\evil.com', 'https://evil.com', 'javascript:alert(1)', 'data:text/html,x', '/a\r\nb', '/a\u007fb', '',
  '/' + 'a'.repeat(400), null, undefined,
];

const SHARED_OK = ['/lessons.html', '/lesson.html?id=x', '/problem.html?lesson=a&problem=b', '/change-password.html'];
const STUDENT_PAGES = ['/dashboard.html', '/dashboard.html?x=1', '/assignments.html'];
const TEACHER_AREA_FOR_STUDENT_NULL = ['/teacher', '/teacher/', '/teacher/index.html', '/TEACHER/dashboard.html'];
const TEACHER_OK = ['/teacher/index.html', '/teacher/', '/teacher/dashboard.html'];

for (const role of ['Student', 'Teacher']) {
  test(`sanitizeReturnUrl trả null cho đích không hợp lệ (${role})`, () => {
    for (const url of BOTH_NULL) assert.equal(sanitizeReturnUrl(url, role), null, JSON.stringify(url));
  });

  test(`sanitizeReturnUrl giữ nguyên trang dùng chung (${role})`, () => {
    for (const url of SHARED_OK) assert.equal(sanitizeReturnUrl(url, role), url);
  });
}

test('độ dài tối đa là 300 ký tự', () => {
  assert.equal(sanitizeReturnUrl('/' + 'a'.repeat(299), 'Student'), '/' + 'a'.repeat(299));
  assert.equal(sanitizeReturnUrl('/' + 'a'.repeat(300), 'Student'), null);
});

test('học sinh được vào trang của học sinh, giáo viên thì không', () => {
  for (const url of STUDENT_PAGES) {
    assert.equal(sanitizeReturnUrl(url, 'Student'), url);
    assert.equal(sanitizeReturnUrl(url, 'Teacher'), null);
  }
});

test('khu giáo viên bị chặn với học sinh và hợp lệ với giáo viên (không nhầm với landing)', () => {
  for (const url of TEACHER_AREA_FOR_STUDENT_NULL) assert.equal(sanitizeReturnUrl(url, 'Student'), null, url);
  for (const url of TEACHER_OK) assert.equal(sanitizeReturnUrl(url, 'Teacher'), url, url);
});

test('homeFor theo vai trò', () => {
  assert.equal(homeFor({ role: 'Teacher' }), '/teacher/dashboard.html');
  assert.equal(homeFor({ role: 'Student' }), '/dashboard.html');
  assert.equal(homeFor(null), '/dashboard.html');
});

test('signedInTarget: đổi mật khẩu trước, rồi returnUrl hợp lệ, cuối cùng trang chủ', () => {
  assert.equal(signedInTarget({ role: 'Student', mustChangePassword: true }, '/lessons.html'), '/change-password.html');
  assert.equal(signedInTarget({ role: 'Student', mustChangePassword: false }, '/lessons.html'), '/lessons.html');
  assert.equal(signedInTarget({ role: 'Student', mustChangePassword: false }, '//evil.com'), '/dashboard.html');
  assert.equal(signedInTarget({ role: 'Teacher', mustChangePassword: false }, null), '/teacher/dashboard.html');
  assert.equal(signedInTarget({ role: 'Teacher', mustChangePassword: false }, '/dashboard.html'), '/teacher/dashboard.html');
});

test('isSafeRedirect chỉ nhận đường dẫn tương đối một dấu /', () => {
  for (const ok of ['/dashboard.html', '/teacher/dashboard.html', '/change-password.html']) assert.equal(isSafeRedirect(ok), true, ok);
  for (const bad of ['//evil.com', '/\\evil.com', 'https://evil.com', 'dashboard.html', '', null, undefined, 42, '/a\nb']) {
    assert.equal(isSafeRedirect(bad), false, String(bad));
  }
});

test('joinApiUrl ghép API_BASE với đường dẫn (AC-485)', () => {
  assert.equal(joinApiUrl('', '/api/auth/me'), '/api/auth/me');
  assert.equal(joinApiUrl('http://localhost:5000', '/api/auth/me'), 'http://localhost:5000/api/auth/me');
  assert.equal(joinApiUrl('http://localhost:5000/', '/api/auth/me'), 'http://localhost:5000/api/auth/me');
  assert.equal(joinApiUrl('http://localhost:5000///', '/api/x'), 'http://localhost:5000/api/x');
});

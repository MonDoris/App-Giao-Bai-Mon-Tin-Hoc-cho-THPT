// Quy tắc chấm (AC-26): đổi CRLF về LF, bỏ khoảng trắng thừa ở cuối mỗi dòng và cuối toàn bộ đầu ra,
// sau đó so sánh chuỗi chính xác (phân biệt hoa/thường). Hàm thuần, không phụ thuộc DOM.

export function normalizeOutput(text) {
  const unified = String(text ?? '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  return unified
    .split('\n')
    .map((line) => line.replace(/[ \t\f\v]+$/g, ''))
    .join('\n')
    .replace(/\s+$/g, '');
}

export function outputsMatch(actual, expected) {
  return normalizeOutput(actual) === normalizeOutput(expected);
}

export const STATUS = Object.freeze({
  pass: { code: 'pass', label: 'Đạt' },
  wrong: { code: 'wrong', label: 'Sai kết quả' },
  error: { code: 'error', label: 'Lỗi chạy' },
  timeout: { code: 'timeout', label: 'Quá thời gian' },
});

/** Phân loại kết quả một test từ kết quả chạy của runner. */
export function classify(runResult, expectedOutput) {
  if (runResult.timedOut) return STATUS.timeout;
  if (runResult.error) return STATUS.error;
  return outputsMatch(runResult.output, expectedOutput) ? STATUS.pass : STATUS.wrong;
}

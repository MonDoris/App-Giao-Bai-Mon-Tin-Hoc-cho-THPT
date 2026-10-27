/* Web Worker chạy Python bằng Pyodide (phiên bản cố định, nạp từ CDN). Code học sinh chỉ chạy tại đây, trong trình duyệt. */
'use strict';

const PYODIDE_VERSION = 'v0.26.4';
const PYODIDE_BASE = `https://cdn.jsdelivr.net/pyodide/${PYODIDE_VERSION}/full/`;

const HARNESS = `
import sys, io, json, builtins

class _Limited:
    """Ghi đầu ra nhưng chỉ giữ tối đa 'limit' ký tự để không tốn bộ nhớ khi in vô hạn."""
    def __init__(self, limit):
        self.parts = []
        self.size = 0
        self.limit = limit
        self.truncated = False
    def write(self, s):
        if not isinstance(s, str):
            s = str(s)
        n = len(s)
        if self.size >= self.limit:
            if n:
                self.truncated = True
            return n
        room = self.limit - self.size
        if n > room:
            self.parts.append(s[:room])
            self.size += room
            self.truncated = True
        else:
            self.parts.append(s)
            self.size += n
        return n
    def flush(self):
        pass
    def isatty(self):
        return False
    def getvalue(self):
        return ''.join(self.parts)

def __run_student(code, stdin_text, limit):
    out = _Limited(limit)
    saved = (sys.stdin, sys.stdout, sys.stderr)
    sys.stdin = io.StringIO(stdin_text)
    sys.stdout = out
    sys.stderr = out
    error = None
    try:
        try:
            compiled = compile(code, '<hocsinh>', 'exec')
        except SyntaxError as e:
            error = {'type': type(e).__name__, 'line': e.lineno, 'message': e.msg or str(e)}
        else:
            # Mỗi lần chạy dùng không gian tên MỚI: biến toàn cục không rò rỉ giữa các test.
            env = {'__name__': '__main__', '__builtins__': dict(vars(builtins))}
            try:
                exec(compiled, env)
            except SystemExit:
                pass
            except BaseException as e:
                tb = e.__traceback__
                line = None
                while tb is not None:
                    if tb.tb_frame.f_code.co_filename == '<hocsinh>':
                        line = tb.tb_lineno
                    tb = tb.tb_next
                error = {'type': type(e).__name__, 'line': line, 'message': str(e)}
    finally:
        sys.stdin, sys.stdout, sys.stderr = saved
    return json.dumps({'output': out.getvalue(), 'truncated': out.truncated, 'error': error})
`;

let pyodide = null;
let runFn = null;

async function init() {
  try {
    self.postMessage({ type: 'loading' });
    importScripts(PYODIDE_BASE + 'pyodide.js');
    pyodide = await self.loadPyodide({ indexURL: PYODIDE_BASE });
    pyodide.runPython(HARNESS);
    runFn = pyodide.globals.get('__run_student');
    self.postMessage({ type: 'ready' });
  } catch (e) {
    self.postMessage({ type: 'init-error', message: String((e && e.message) || e) });
  }
}

self.onmessage = (event) => {
  const msg = event.data;
  if (!msg || msg.type !== 'run' || !runFn) return;
  const started = performance.now();
  let payload;
  try {
    payload = JSON.parse(runFn(msg.code, msg.stdin, msg.outputLimit));
  } catch (e) {
    payload = { output: '', truncated: false, error: { type: 'InternalError', line: null, message: String((e && e.message) || e) } };
  }
  self.postMessage({ type: 'result', id: msg.id, ms: Math.round(performance.now() - started), ...payload });
};

init();

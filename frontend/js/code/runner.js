// Điều khiển Web Worker chạy Python. Hủy / quá thời gian = terminate worker rồi tạo worker mới,
// nên trang không bao giờ bị treo bởi code của học sinh.

export const OUTPUT_LIMIT = 10000;
export const TRUNCATE_NOTE = '… (đã cắt bớt)';

export class PyRunner {
  /**
   * @param {{onState?: (state: 'loading'|'ready'|'error'|'restarting', info?: string) => void}} options
   */
  constructor({ onState = () => {} } = {}) {
    this.onState = onState;
    this.worker = null;
    this.ready = null;       // Promise đã sẵn sàng
    this.state = 'idle';
    this.current = null;     // lần chạy đang diễn ra
    this.nextId = 1;
  }

  /** Khởi động (hoặc khởi động lại) worker. Trả về Promise hoàn tất khi Pyodide sẵn sàng. */
  start(restarting = false) {
    this.dispose();
    this.state = restarting ? 'restarting' : 'loading';
    this.onState(this.state);

    this.ready = new Promise((resolve, reject) => {
      let worker;
      try {
        worker = new Worker('/js/code/py-worker.js');
      } catch (e) {
        this.state = 'error';
        this.onState('error', 'Trình duyệt không hỗ trợ Web Worker.');
        reject(e);
        return;
      }
      this.worker = worker;

      worker.onmessage = (event) => {
        const msg = event.data;
        if (msg.type === 'ready') {
          this.state = 'ready';
          this.onState('ready');
          resolve();
        } else if (msg.type === 'init-error') {
          this.state = 'error';
          this.onState('error', msg.message);
          reject(new Error(msg.message));
        } else if (msg.type === 'result' && this.current && this.current.id === msg.id) {
          const cur = this.current;
          this.current = null;
          clearTimeout(cur.timer);
          cur.resolve(this.#toResult(msg));
        }
      };
      worker.onerror = (event) => {
        if (this.state !== 'ready') {
          this.state = 'error';
          this.onState('error', event.message || 'Không tải được trình chạy Python.');
          reject(new Error(event.message || 'worker error'));
        }
      };
    });
    // tránh cảnh báo "unhandled rejection" – lỗi đã được báo qua onState
    this.ready.catch(() => {});
    return this.ready;
  }

  dispose() {
    if (this.worker) {
      this.worker.terminate();
      this.worker = null;
    }
  }

  #toResult(msg) {
    let output = msg.output || '';
    if (msg.truncated || output.length > OUTPUT_LIMIT) {
      output = output.slice(0, OUTPUT_LIMIT) + '\n' + TRUNCATE_NOTE;
    }
    let error = null;
    let needInput = false;
    if (msg.error) {
      if (msg.error.type === 'EOFError') {
        needInput = true;
        error = { type: 'EOFError', line: msg.error.line, message: 'Chương trình cần thêm dữ liệu vào' };
      } else {
        error = msg.error;
      }
    }
    return { output, error, needInput, timedOut: false, stopped: false, ms: msg.ms || 0 };
  }

  /**
   * Chạy một đoạn code với stdin cho trước.
   * @returns {Promise<{output:string,error:?{type:string,line:?number,message:string},needInput:boolean,timedOut:boolean,stopped:boolean,ms:number}>}
   */
  async run(code, stdin, limitMs = 5000) {
    try {
      await (this.ready ?? this.start());
    } catch (e) {
      throw new Error('PYODIDE_UNAVAILABLE');
    }
    if (!this.worker) {
      await this.start(true);
    }

    return new Promise((resolve) => {
      const id = this.nextId++;
      const started = performance.now();
      const timer = setTimeout(() => {
        // Quá thời gian: dừng worker, báo kết quả, khởi động lại worker nền.
        this.current = null;
        resolve({ output: '', error: null, needInput: false, timedOut: true, stopped: false, ms: Math.round(performance.now() - started) });
        this.start(true);
      }, limitMs);
      this.current = { id, timer, resolve, started };
      this.worker.postMessage({ type: 'run', id, code, stdin: String(stdin ?? ''), outputLimit: OUTPUT_LIMIT + 1 });
    });
  }

  /** Dừng lần chạy hiện tại (nút "Dừng"). */
  stop() {
    const cur = this.current;
    if (!cur) return false;
    clearTimeout(cur.timer);
    this.current = null;
    cur.resolve({ output: '', error: null, needInput: false, timedOut: false, stopped: true, ms: Math.round(performance.now() - cur.started) });
    this.start(true);
    return true;
  }
}

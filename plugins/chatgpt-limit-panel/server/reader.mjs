import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

export class RateLimitReader {
  constructor({ command = process.env.QUOTA_CODEX_BIN || 'codex', args = ['app-server'], timeoutMs = 15000 } = {}) {
    this.command = command; this.args = args; this.timeoutMs = timeoutMs;
    this.pending = new Map(); this.sequence = 0;
  }

  async start() {
    if (this.starting) return this.starting;
    const starting = this.starting = (async () => {
      const child = spawn(this.command, this.args, { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
      this.child = child;
      const lines = createInterface({ input: child.stdout });
      child.stderr.resume(); // Never expose backend diagnostics or credentials through MCP.
      child.on('error', () => { if (this.child === child) this.fail(new Error('Codex App Server is unavailable')); });
      child.on('exit', () => { if (this.child === child) this.fail(new Error('Codex App Server disconnected')); });
      child.stdin.on('error', () => { if (this.child === child) this.fail(new Error('Codex App Server disconnected')); });
      lines.on('line', line => {
        let message; try { message = JSON.parse(line); } catch { return; }
        const request = this.pending.get(message.id);
        if (!request) return;
        this.pending.delete(message.id); clearTimeout(request.timer);
        message.error ? request.reject(new Error('Codex quota request was rejected')) : request.resolve(message.result);
      });
      await this.request('initialize', { clientInfo: { name: 'chatgpt_limit_panel', title: 'ChatGPT Limit Panel', version: '1.4.5-beta.2' } });
      child.stdin.write(JSON.stringify({ method: 'initialized' }) + '\n');
    })();
    try { await starting; } catch (error) { if (this.starting === starting) this.close(); throw error; }
  }

  request(method, params = {}) {
    return new Promise((resolve, reject) => {
      const id = ++this.sequence;
      const timer = setTimeout(() => {
        this.pending.delete(id); reject(new Error('Codex quota request timeout')); this.close();
      }, this.timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      this.child.stdin.write(JSON.stringify({ method, id, params }) + '\n');
    });
  }

  async read() {
    if (this.reading) return this.reading;
    this.reading = (async () => { await this.start(); return await this.request('account/rateLimits/read'); })();
    try { return await this.reading; } finally { this.reading = null; }
  }

  fail(error) {
    for (const request of this.pending.values()) { clearTimeout(request.timer); request.reject(error); }
    this.pending.clear(); this.starting = null;
    const child = this.child; this.child = null;
    if (child && !child.killed) child.kill();
  }

  close() { this.fail(new Error('Codex quota reader closed')); }
}

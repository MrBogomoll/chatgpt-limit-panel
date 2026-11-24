import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

const failureMessages = {
  unavailable: 'Codex App Server is unavailable',
  disconnected: 'Codex App Server disconnected',
  rejected: 'Codex quota request was rejected',
  timeout: 'Codex quota request timeout',
  closed: 'Codex quota reader closed',
};

export class RateLimitReadError extends Error {
  constructor(reason, stage, startedAt) {
    super(failureMessages[reason]);
    this.reason = reason;
    this.stage = stage;
    this.durationMs = Math.round(performance.now() - startedAt);
  }
}

export class RateLimitReader {
  constructor({ command = process.env.QUOTA_CODEX_BIN || 'codex', args = ['app-server'], timeoutMs = 15000 } = {}) {
    this.command = command; this.args = args; this.timeoutMs = timeoutMs;
    this.pending = new Map(); this.sequence = 0;
  }

  async start() {
    if (this.starting) return this.starting;
    const starting = this.starting = (async () => {
      const launchStartedAt = performance.now();
      let child;
      try {
        child = spawn(this.command, this.args, { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
      } catch {
        throw new RateLimitReadError('unavailable', 'launch', launchStartedAt);
      }
      this.child = child;
      const lines = createInterface({ input: child.stdout });
      child.stderr.resume(); // Never expose backend diagnostics or credentials through MCP.
      const connectionLost = () => {
        if (this.child !== child) return;
        if (child.pid === undefined) this.fail('unavailable', 'launch', launchStartedAt);
        else this.fail('disconnected');
      };
      child.on('error', connectionLost);
      child.on('exit', connectionLost);
      child.stdin.on('error', connectionLost);
      lines.on('line', line => {
        if (this.child !== child) return;
        let message; try { message = JSON.parse(line); } catch { return; }
        const request = this.pending.get(message.id);
        if (!request) return;
        this.pending.delete(message.id); clearTimeout(request.timer);
        if (message.error) request.reject(new RateLimitReadError('rejected', request.stage, request.startedAt));
        else request.resolve(message.result);
      });
      await this.request('initialize', { clientInfo: { name: 'chatgpt_limit_panel', title: 'ChatGPT Limit Panel', version: '1.8.3' } });
      child.stdin.write(JSON.stringify({ method: 'initialized' }) + '\n');
    })();
    try { await starting; } catch (error) { if (this.starting === starting) this.close(); throw error; }
  }

  request(method, params = {}) {
    return new Promise((resolve, reject) => {
      const id = ++this.sequence;
      const stage = method === 'initialize' ? 'initialize' : 'read-limits';
      const startedAt = performance.now();
      const timer = setTimeout(() => {
        this.pending.delete(id); reject(new RateLimitReadError('timeout', stage, startedAt)); this.close();
      }, this.timeoutMs);
      this.pending.set(id, { resolve, reject, timer, stage, startedAt });
      this.child.stdin.write(JSON.stringify({ method, id, params }) + '\n');
    });
  }

  async read() {
    if (this.reading) return this.reading;
    this.reading = (async () => { await this.start(); return await this.request('account/rateLimits/read'); })();
    try { return await this.reading; } finally { this.reading = null; }
  }

  fail(reason, stage, startedAt) {
    for (const request of this.pending.values()) {
      clearTimeout(request.timer);
      request.reject(new RateLimitReadError(reason, stage ?? request.stage, startedAt ?? request.startedAt));
    }
    this.pending.clear(); this.starting = null;
    const child = this.child; this.child = null;
    if (child && !child.killed) child.kill();
  }

  close() { this.fail('closed'); }
}

import { createInterface } from 'node:readline';
import { createOverlayController } from './overlay-controller.mjs';
import { createMcpHandler } from './mcp.mjs';
import { buildUi } from './ui.mjs';

const handle = createMcpHandler({ controller: createOverlayController(), readUi: buildUi });
const input = createInterface({ input: process.stdin, crlfDelay: Infinity });
input.on('line', async line => {
  let request;
  try { request = JSON.parse(line); }
  catch { process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: null, error: { code: -32700, message: 'Parse error' } }) + '\n'); return; }
  try {
    const response = await handle(request);
    if (response) process.stdout.write(JSON.stringify(response) + '\n');
  } catch {
    if (request?.id !== undefined) process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: request.id, error: { code: -32603, message: 'Quota panel request failed' } }) + '\n');
  }
});
// The native monitor owns its lifetime. Closing this transport never stops it.
for (const signal of ['SIGINT', 'SIGTERM']) process.once(signal, () => { input.close(); process.exit(0); });

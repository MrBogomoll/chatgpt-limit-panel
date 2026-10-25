const readline = require('node:readline');
const rl = readline.createInterface({ input: process.stdin });
rl.on('line', line => {
  const request = JSON.parse(line);
  if (request.method === 'initialize') {
    process.stdout.write(JSON.stringify({ id: request.id, result: { userAgent: 'test' } }) + '\n');
  } else if (request.method === 'account/rateLimits/read') {
    if (process.argv.includes('--hang')) return;
    process.stdout.write(JSON.stringify({ id: request.id, result: { rateLimits: { limitId: 'codex', primary: { usedPercent: 62, windowDurationMins: 300, resetsAt: 1792774530 }, secondary: null } } }) + '\n');
  } else if (request.id !== undefined) process.stdout.write(JSON.stringify({ id: request.id, error: { code: -32601, message: 'Forbidden in fixture' } }) + '\n');
});

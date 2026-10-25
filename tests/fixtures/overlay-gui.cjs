const fs = require('node:fs');
const readline = require('node:readline');

const statePath = process.argv[2];
const configPath = statePath + '.fixture.json';
const config = fs.existsSync(configPath) ? JSON.parse(fs.readFileSync(configPath, 'utf8')) : {};
const eventsPath = statePath + '.events.ndjson';
const commandsPath = statePath + '.commands.ndjson';
const record = event => fs.appendFileSync(eventsPath, JSON.stringify(event) + '\n');
const command = value => process.stdout.write(JSON.stringify({ command: value }) + '\n');
record({ event: 'started', args: process.argv.slice(2) });
let commandCount = 0;
const controls = setInterval(() => {
  let lines;
  try { lines = fs.readFileSync(commandsPath, 'utf8').split('\n').filter(Boolean); }
  catch { return; }
  for (const line of lines.slice(commandCount)) {
    commandCount++;
    const message = JSON.parse(line);
    if (message.command === 'crash') process.exit(7);
    command(message.command);
    record({ event: 'command', command: message.command });
  }
}, 10);
const lines = readline.createInterface({ input: process.stdin });
lines.on('line', line => {
  try { record({ event: 'view', view: JSON.parse(line) }); }
  catch { record({ event: 'invalid-view', line }); }
});
lines.on('close', () => {
  record({ event: 'eof' });
  if (!config.ignoreEof) {
    clearInterval(controls);
    process.exitCode = 0;
  }
});
if (config.autoReady !== false) command('ready');

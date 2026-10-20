const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { test } = require('node:test');
const { createHash } = require('node:crypto');

const repo = path.resolve(__dirname, '../..');
const linux = { skip: process.platform !== 'linux' };
function fixture(t, uid = '0') {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'linux-server-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const bin = path.join(root, 'bin');
  const pkg = path.join(root, 'package');
  fs.mkdirSync(bin); fs.mkdirSync(pkg);
  const calls = path.join(root, 'calls');
  function executable(name, body) { fs.writeFileSync(path.join(bin, name), `#!/bin/bash\nset -e\n${body}\n`, { mode: 0o755 }); }
  executable('id', `if [[ $1 == -u ]]; then echo ${uid}; else echo clanker; fi`);
  executable('getent', 'exit 2');
  for (const name of ['chown', 'useradd', 'systemctl'])
    executable(name, `printf '%s\\n' '${name}' "$@" >> "$LINUX_TEST_CALLS"`);
  executable('install', `printf '%s\\n' install "$@" >> "$LINUX_TEST_CALLS"
args=()
while (( $# )); do
  case "$1" in -o|-g) shift 2;; *) args+=("$1"); shift;; esac
done
exec /usr/bin/install "\u0024{args[@]}"`);
  executable('curl', `printf '%s\\n' "$@" > "$LINUX_TEST_CURL_ARGS"
cat > "$LINUX_TEST_CURL_BODY"
exit "\u0024{LINUX_TEST_CURL_EXIT:-0}"`);
  const env = { ...process.env, PATH: `${bin}:${process.env.PATH}`, LINUX_TEST_CALLS: calls,
    LINUX_TEST_CURL_ARGS: path.join(root, 'curl-args'), LINUX_TEST_CURL_BODY: path.join(root, 'curl-body') };
  function stage(file, target) {
    let text = fs.readFileSync(path.join(repo, file), 'utf8');
    // Relocate installation destinations only; the script's operations remain real.
    for (const dir of ['/opt/clankerworld-server', '/var/lib/clankerworld-server', '/etc/clankerworld-server',
      '/etc/systemd/system/clankerworld-server.service', '/usr/local/bin/clankerworld-server'])
      text = text.replaceAll(dir, path.join(root, dir));
    fs.writeFileSync(path.join(pkg, target), text, { mode: 0o755 });
  }
  stage('deploy/install-server.sh', 'install-server.sh');
  stage('deploy/clankerworld-server', 'clankerworld-server');
  fs.copyFileSync(path.join(repo, 'deploy/clankerworld-server.service'), path.join(pkg, 'clankerworld-server.service'));
  fs.writeFileSync(path.join(pkg, 'ClankerWorld.Server'), '#!/bin/sh\nexit 0\n', { mode: 0o755 });
  const manifest = fs.readdirSync(pkg).map(file => `${createHash('sha256').update(fs.readFileSync(path.join(pkg, file))).digest('hex')} *${file}\n`).join('');
  fs.writeFileSync(path.join(pkg, 'manifest.sha256'), manifest);
  fs.mkdirSync(path.join(root, 'etc/systemd/system'), { recursive: true });
  fs.mkdirSync(path.join(root, 'usr/local/bin'), { recursive: true });
  return { root, pkg, env, run: (file, args = [], input) => spawnSync('bash', [path.join(pkg, file), ...args], { env, input, encoding: 'utf8' }),
    calls: () => fs.existsSync(calls) ? fs.readFileSync(calls, 'utf8') : '' };
}

test('fresh install creates private state and enables the exact packaged service', linux, t => {
  const f = fixture(t);
  const result = f.run('install-server.sh');
  assert.equal(result.status, 0, result.stderr);
  const state = path.join(f.root, 'var/lib/clankerworld-server');
  assert.equal(fs.statSync(state).mode & 0o777, 0o700);
  assert.deepEqual(fs.readFileSync(path.join(f.root, 'opt/clankerworld-server/ClankerWorld.Server')),
    fs.readFileSync(path.join(f.pkg, 'ClankerWorld.Server')));
  assert.match(f.calls(), /useradd\n--system\n--user-group/);
  assert.match(f.calls(), /-o\nclanker\n-g\nclanker/);
  assert.match(f.calls(), /systemctl\ndaemon-reload\nsystemctl\nenable\n--now\nclankerworld-server.service/);
  const unit = fs.readFileSync(path.join(f.root, 'etc/systemd/system/clankerworld-server.service'), 'utf8');
  assert.match(unit, /ExecStart=\/opt\/clankerworld-server\/ClankerWorld.Server\n/);
  assert.match(unit, /ClankerWorld__Hosting__Kind=terminal/);
  assert.match(unit, /StateDirectoryMode=0700/);
  assert.match(unit, /ClankerWorld__Providers__UsagePath=\/var\/lib\/clankerworld-server\/provider-usage.json/);
  // A second installation must preserve all state and never stop or restart it.
  fs.writeFileSync(path.join(state, 'existing-world'), 'retained');
  const before = f.calls();
  assert.notEqual(f.run('install-server.sh').status, 0);
  assert.equal(fs.readFileSync(path.join(state, 'existing-world'), 'utf8'), 'retained');
  assert.equal(f.calls(), before);
});

test('bad package, an existing account, and a non-root install make no changes', linux, t => {
  for (const problem of ['tampered', 'account', 'unprivileged']) {
    const f = fixture(t, problem === 'unprivileged' ? '1001' : '0');
    if (problem === 'tampered') fs.appendFileSync(path.join(f.pkg, 'ClankerWorld.Server'), 'changed');
    if (problem === 'account') fs.writeFileSync(path.join(f.root, 'bin/getent'), '#!/bin/sh\nexit 0\n');
    assert.notEqual(f.run('install-server.sh').status, 0, problem);
    assert.equal(f.calls(), '', problem);
    assert.equal(fs.existsSync(path.join(f.root, 'var/lib/clankerworld-server')), false);
  }
});

test('approval sends a hidden code on stdin, reads the private header by path and bounds its request', linux, t => {
  const f = fixture(t, '1001');
  const code = '123456'; // Synthetic comparison value; never a real device or credential.
  const result = f.run('clankerworld-server', ['approve'], `${code}\n`);
  assert.equal(result.status, 0, result.stderr);
  const args = fs.readFileSync(f.env.LINUX_TEST_CURL_ARGS, 'utf8');
  assert.match(args, /--json\n@-\nhttp:\/\/127.0.0.1:5189\/api\/v1\/approval\/pairings\/approve/);
  assert.match(args, /--connect-timeout\n3\n--max-time\n15/);
  assert.match(args, /--header\n@.*approval-header/);
  assert.deepEqual(JSON.parse(fs.readFileSync(f.env.LINUX_TEST_CURL_BODY, 'utf8')), { pairingCode: code });
  for (const text of [args, result.stdout, result.stderr]) assert.ok(!text.includes(code));
  f.env.LINUX_TEST_CURL_EXIT = '22';
  assert.notEqual(f.run('clankerworld-server', ['approve'], `${code}\n`).status, 0);
  fs.rmSync(f.env.LINUX_TEST_CURL_ARGS);
  assert.notEqual(f.run('clankerworld-server', ['approve'], 'bad\n').status, 0);
  assert.equal(fs.existsSync(f.env.LINUX_TEST_CURL_ARGS), false);
});

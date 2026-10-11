const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { test } = require('node:test');

const workflow = fs.readFileSync(path.join(__dirname, '../workflows/nightly.yml'), 'utf8');
const sha = '1234567890123456789012345678901234567890';

function script(name) {
  const lines = workflow.split('\n');
  const start = lines.findIndex(line => line === `      - name: ${name}`);
  assert.ok(start >= 0, name);
  const run = lines.findIndex((line, index) => index > start && line === '        run: |');
  assert.ok(run > start, name);
  const body = [];
  for (const line of lines.slice(run + 1)) {
    if (line && !line.startsWith('          ')) break;
    body.push(line.slice(10));
  }
  return body.join('\n').replaceAll('${{ steps.decide.outputs.sha }}', sha);
}

function sandbox(t, pages = []) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nightly-workflow-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const bin = path.join(root, 'bin');
  fs.mkdirSync(bin);
  fs.writeFileSync(path.join(root, 'pages.json'), JSON.stringify(pages));
  fs.writeFileSync(path.join(bin, 'git'), '#!/bin/bash\n[[ "$*" == "rev-parse HEAD" ]] || exit 2\nprintf "%s\\n" "$NIGHTLY_TEST_SHA"\n', { mode: 0o755 });
  fs.writeFileSync(path.join(bin, 'gh'), `#!/usr/bin/env node
const fs = require('node:fs');
const { spawnSync } = require('node:child_process');
const args = process.argv.slice(2);
fs.appendFileSync(process.env.NIGHTLY_TEST_CALLS, JSON.stringify(args) + '\\n');
if (args[0] === 'api') {
  if (process.env.NIGHTLY_TEST_API_FAIL === 'true') process.exit(1);
  const pages = JSON.parse(fs.readFileSync(process.env.NIGHTLY_TEST_PAGES, 'utf8'));
  const data = args.includes('--paginate') && args.includes('--slurp') ? pages : (pages[0] || []);
  const result = spawnSync('jq', ['-r', args[args.indexOf('--jq') + 1]], { input: JSON.stringify(data), encoding: 'utf8' });
  process.stdout.write(result.stdout || ''); process.stderr.write(result.stderr || '');
  process.exit(result.status ?? 1);
}
if (args[0] === 'run') { console.log(process.env.NIGHTLY_TEST_CI); process.exit(0); }
if (args[0] === 'release') {
  if (args[1] === 'delete' && args[2] === process.env.NIGHTLY_TEST_DELETE_FAIL) process.exit(1);
  process.exit(0);
}
process.exit(2);
`, { mode: 0o755 });
  const env = {
    ...process.env, PATH: `${bin}${path.delimiter}${process.env.PATH}`,
    GH_TOKEN: 'offline-test', GH_REPO: 'ClankerWorldOrg/ClankerWorld', KEEP_NIGHTLIES: '7',
    GITHUB_OUTPUT: path.join(root, 'output'), NIGHTLY_TEST_SHA: sha,
    NIGHTLY_TEST_PAGES: path.join(root, 'pages.json'), NIGHTLY_TEST_CALLS: path.join(root, 'calls'),
    NIGHTLY_TEST_CI: 'success', NIGHTLY_TEST_API_FAIL: 'false', NIGHTLY_TEST_DELETE_FAIL: '',
  };
  function run(name) {
    return spawnSync('bash', ['-e', '-o', 'pipefail', '-c', script(name)], { cwd: root, env, encoding: 'utf8' });
  }
  function calls() {
    return fs.existsSync(env.NIGHTLY_TEST_CALLS) ? fs.readFileSync(env.NIGHTLY_TEST_CALLS, 'utf8').trim().split('\n').map(JSON.parse) : [];
  }
  return { root, env, run, calls };
}

const alphaPage = Array.from({ length: 100 }, (_, index) => ({ tag_name: `v0.1.0-alpha.${index}`, created_at: '2026-10-11T01:00:00Z', target_commitish: sha }));
const nightlies = Array.from({ length: 9 }, (_, index) => ({ tag_name: `v0.1.0-nightly.20261010.${index}`, created_at: `2026-10-10T0${index}:00:00Z`, target_commitish: index === 8 ? sha : 'older' }));

test('the newest nightly on a later API page prevents publishing the same commit again', t => {
  const fixture = sandbox(t, [alphaPage, nightlies]);
  const result = fixture.run('Decide whether main needs a new nightly build');
  assert.equal(result.status, 0, result.stderr);
  assert.match(fs.readFileSync(fixture.env.GITHUB_OUTPUT, 'utf8'), /build=false/);
  assert.equal(fixture.calls().filter(call => call[0] === 'run').length, 0);
});

test('only a green push run for the exact checked-out commit allows a new nightly', t => {
  for (const conclusion of ['', 'in_progress', 'failure', 'success']) {
    const fixture = sandbox(t);
    fixture.env.NIGHTLY_TEST_CI = conclusion;
    const result = fixture.run('Decide whether main needs a new nightly build');
    assert.equal(result.status, 0, result.stderr);
    assert.match(fs.readFileSync(fixture.env.GITHUB_OUTPUT, 'utf8'), conclusion === 'success' ? /build=true/ : /build=false/);
    const call = fixture.calls().find(call => call[0] === 'run');
    assert.equal(call[call.indexOf('--commit') + 1], sha);
    assert.equal(call[call.indexOf('--event') + 1], 'push');
  }
});

test('publication targets the checked commit and uploads both versioned assets as a pre-release', t => {
  const fixture = sandbox(t);
  fs.mkdirSync(path.join(fixture.root, 'export/package'), { recursive: true });
  const zip = 'export/package/ClankerWorld-0.1.0-nightly.20261011.42-windows-x64.zip';
  fs.writeFileSync(path.join(fixture.root, zip), 'synthetic package');
  fs.writeFileSync(path.join(fixture.root, `${zip}.sha256`), 'synthetic hash');
  const result = fixture.run('Publish the nightly pre-release');
  assert.equal(result.status, 0, result.stderr);
  const call = fixture.calls().find(call => call[0] === 'release');
  assert.deepEqual(call.slice(0, 3), ['release', 'create', 'v0.1.0-nightly.20261011.42']);
  assert.ok(call.includes('--prerelease') && call.includes('--latest=false'));
  assert.equal(call[call.indexOf('--target') + 1], sha);
  assert.deepEqual(call.slice(-2), [zip, `${zip}.sha256`]);
});

test('retention finds later API pages and removes only nightlies older than the newest seven', t => {
  const unrelated = [{ tag_name: 'v0.1.0-nightlyx.1' }, { tag_name: 'launcher-v1.0.0-nightly.1' }];
  const fixture = sandbox(t, [[...alphaPage, ...unrelated], nightlies]);
  const result = fixture.run('Remove older nightly builds');
  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(fixture.calls().filter(call => call[1] === 'delete'), [
    ['release', 'delete', nightlies[1].tag_name, '--yes', '--cleanup-tag'],
    ['release', 'delete', nightlies[0].tag_name, '--yes', '--cleanup-tag'],
  ]);
});

test('API errors fail both decision and cleanup without attempting a deletion', t => {
  assert.match(workflow, /defaults:\s+run:\s+shell: bash/);
  for (const name of ['Decide whether main needs a new nightly build', 'Remove older nightly builds']) {
    const fixture = sandbox(t, [nightlies]);
    fixture.env.NIGHTLY_TEST_API_FAIL = 'true';
    assert.notEqual(fixture.run(name).status, 0);
    assert.equal(fixture.calls().filter(call => call[1] === 'delete').length, 0);
  }
});

test('a failed deletion stops retention before deleting another tag', t => {
  const fixture = sandbox(t, [nightlies]);
  fixture.env.NIGHTLY_TEST_DELETE_FAIL = nightlies[1].tag_name;
  assert.notEqual(fixture.run('Remove older nightly builds').status, 0);
  assert.deepEqual(fixture.calls().filter(call => call[1] === 'delete').map(call => call[2]), [nightlies[1].tag_name]);
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFile, spawnSync } = require('node:child_process');
const { createServer } = require('node:http');
const { createHash } = require('node:crypto');
const { promisify } = require('node:util');
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
  return body.join('\n').replaceAll('${{ needs.nightly.outputs.sha }}', sha);
}

function sandbox(t, pages = []) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nightly-workflow-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const bin = path.join(root, 'bin');
  fs.mkdirSync(bin);
  fs.writeFileSync(path.join(root, 'pages.json'), JSON.stringify(pages));
  fs.writeFileSync(path.join(bin, 'git'), `#!/bin/bash
case "$*" in
  'rev-parse HEAD') printf '%s\\n' "$NIGHTLY_TEST_SHA" ;;
  'cat-file -t refs/tags/'*) printf '%s\\n' "${'$'}{NIGHTLY_TEST_TAG_TYPE:-tag}" ;;
  'rev-parse refs/tags/'*) printf '%s\\n' "${'$'}{NIGHTLY_TEST_TAG_SHA:-$NIGHTLY_TEST_SHA}" ;;
  'merge-base --is-ancestor '*) [[ "${'$'}{NIGHTLY_TEST_ON_MAIN:-true}" == true ]] ;;
  *) exit 2 ;;
esac
`, { mode: 0o755 });
  fs.writeFileSync(path.join(bin, 'gh'), `#!/usr/bin/env node
const fs = require('node:fs');
const { spawnSync } = require('node:child_process');
const args = process.argv.slice(2);
fs.appendFileSync(process.env.NIGHTLY_TEST_CALLS, JSON.stringify(args) + '\\n');
if (args[0] === 'api') {
  if (args.includes('--slurp') && args.includes('--jq')) {
    console.error('the --slurp option is not supported with --jq or --template');
    process.exit(1);
  }
  if (process.env.NIGHTLY_TEST_API_FAIL === 'true') process.exit(1);
  if (args[1].includes('/git/ref/tags/')) {
    console.log(JSON.stringify({ type: process.env.NIGHTLY_TEST_TAG_TYPE || 'tag', sha: 'tag-object' }));
    process.exit(0);
  }
  if (args[1].includes('/git/tags/')) {
    console.log(JSON.stringify({ type: 'commit', sha: process.env.NIGHTLY_TEST_TAG_SHA || process.env.NIGHTLY_TEST_SHA }));
    process.exit(0);
  }
  const pages = JSON.parse(fs.readFileSync(process.env.NIGHTLY_TEST_PAGES, 'utf8'));
  if (process.env.NIGHTLY_TEST_API_FAIL === 'partial') {
    process.stdout.write(JSON.stringify(pages[0] || []) + '\\n');
    process.exit(1);
  }
  if (!args.includes('--jq')) {
    const output = args.includes('--slurp') ? JSON.stringify(pages)
      : args.includes('--paginate') ? (pages.length ? pages : [[]]).map(page => JSON.stringify(page)).join('\\n')
      : JSON.stringify(pages[0] || []);
    process.stdout.write(output + '\\n');
    process.exit(0);
  }
  const data = args.includes('--paginate') && args.includes('--slurp') ? pages : (pages[0] || []);
  const result = spawnSync('jq', ['-r', args[args.indexOf('--jq') + 1]], { input: JSON.stringify(data), encoding: 'utf8' });
  process.stdout.write(result.stdout || ''); process.stderr.write(result.stderr || '');
  process.exit(result.status ?? 1);
}
if (args[0] === 'run') { console.log(process.env.NIGHTLY_TEST_CI); process.exit(0); }
if (args[0] === 'release') {
  if (args[1] === process.env.NIGHTLY_TEST_RELEASE_FAIL) process.exit(1);
  if (args[1] === 'delete' && args[2] === process.env.NIGHTLY_TEST_DELETE_FAIL) process.exit(1);
  if (args[1] === 'delete' && args[2] === process.env.NIGHTLY_TEST_UNTAGGED_DRAFT && args.includes('--cleanup-tag')) process.exit(1);
  process.exit(0);
}
process.exit(2);
`, { mode: 0o755 });
  const env = {
    ...process.env, PATH: `${bin}${path.delimiter}${process.env.PATH}`,
    GH_TOKEN: 'offline-test', GH_REPO: 'ClankerWorldOrg/ClankerWorld', KEEP_NIGHTLIES: '7',
    RELEASE_CHANNEL: 'nightly', RELEASE_TAG: '',
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

test('the newest nightly draft on a later API page prevents rebuilding the same commit', t => {
  const fixture = sandbox(t, [alphaPage, nightlies.map(release => ({ ...release, draft: release.target_commitish === sha }))]);
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

const releaseAssets = [
  'export/package/ClankerWorld-0.1.0-nightly.20261011.42-windows-x64.zip',
  'export/package/ClankerWorld-0.1.0-nightly.20261011.42-windows-x64.zip.sha256',
  'export/package/ClankerWorld-0.1.0-nightly.20261011.42-windows-x64.zip.sig',
  'export/setup/ClankerWorld-Setup-0.1.0-nightly.20261011.42-windows-x64.exe',
  'export/setup/ClankerWorld-Setup-0.1.0-nightly.20261011.42-windows-x64.exe.sha256',
  'export/setup/ClankerWorld-Setup-0.1.0-nightly.20261011.42-windows-x64.exe.sig',
  'export/launcher/ClankerWorld-Launcher-1.0.0-windows-x64.zip',
  'export/launcher/ClankerWorld-Launcher-1.0.0-windows-x64.zip.sha256',
  'export/launcher/ClankerWorld-Launcher-1.0.0-windows-x64.zip.sig',
];

function writeReleaseAssets(root, missing) {
  for (const asset of releaseAssets.filter(asset => asset !== missing)) {
    const file = path.join(root, asset);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const contents = 'synthetic release asset';
    fs.writeFileSync(file, asset.endsWith('.sha256')
      ? `${createHash('sha256').update(contents).digest('hex')} *${path.basename(asset, '.sha256')}\n`
      : contents);
  }
}

test('publishing uploads all approved artifacts before making the draft public', t => {
  const fixture = sandbox(t);
  writeReleaseAssets(fixture.root);
  const result = fixture.run('Publish the automatically approved release');
  assert.equal(result.status, 0, result.stderr);
  const call = fixture.calls().find(call => call[0] === 'release');
  assert.deepEqual(call.slice(0, 3), ['release', 'create', 'v0.1.0-nightly.20261011.42']);
  assert.ok(call.includes('--prerelease') && call.includes('--latest=false'));
  assert.ok(call.includes('--draft'), 'attach every artifact before publication');
  assert.deepEqual(fixture.calls().filter(call => call[0] === 'release').map(call => call[1]), ['create', 'edit']);
  assert.ok(fixture.calls().find(call => call[1] === 'edit').includes('--draft=false'));
  assert.match(call[call.indexOf('--notes') + 1], /Automatically approved nightly/);
  assert.equal(call[call.indexOf('--target') + 1], sha);
  assert.deepEqual(call.slice(-releaseAssets.length), releaseAssets);
});

test('a missing game, setup or checksum prevents staging', t => {
  for (const missing of releaseAssets) {
    const fixture = sandbox(t);
    writeReleaseAssets(fixture.root, missing);
    const result = fixture.run('Publish the automatically approved release');
    assert.notEqual(result.status, 0, missing);
    assert.equal(fixture.calls().filter(call => call[0] === 'release').length, 0, missing);
  }
});

test('a failed draft upload cannot publish a partial release', t => {
  const fixture = sandbox(t);
  writeReleaseAssets(fixture.root);
  fixture.env.NIGHTLY_TEST_RELEASE_FAIL = 'create';
  assert.notEqual(fixture.run('Publish the automatically approved release').status, 0);
  assert.deepEqual(fixture.calls().filter(call => call[0] === 'release').map(call => call[1]), ['create']);
});

test('prepared releases require an annotated tag at the exact green main commit', t => {
  for (const overrides of [{}, { NIGHTLY_TEST_TAG_TYPE: 'commit' }, { NIGHTLY_TEST_TAG_SHA: 'different' },
    { NIGHTLY_TEST_ON_MAIN: 'false' }, { RELEASE_TAG: 'main' }, { NIGHTLY_TEST_CI: 'failure' }]) {
    const fixture = sandbox(t);
    Object.assign(fixture.env, { RELEASE_CHANNEL: 'release', RELEASE_TAG: 'v0.1.0-alpha.2' }, overrides);
    const result = fixture.run('Decide whether main needs a new nightly build');
    if (Object.keys(overrides).length === 0) {
      assert.equal(result.status, 0, result.stderr);
      assert.match(fs.readFileSync(fixture.env.GITHUB_OUTPUT, 'utf8'), /build=true/);
    } else {
      const output = fs.readFileSync(fixture.env.GITHUB_OUTPUT, 'utf8');
      assert.ok(result.status !== 0 || !output.includes('build=true'), JSON.stringify(overrides));
    }
  }
});

test('publication refuses a prepared tag that moved after the build gate', t => {
  for (const tagSha of [sha, 'different']) {
    const fixture = sandbox(t);
    writeReleaseAssets(fixture.root);
    Object.assign(fixture.env, { RELEASE_CHANNEL: 'release', RELEASE_TAG: 'v0.1.0-nightly.20261011.42', NIGHTLY_TEST_TAG_SHA: tagSha });
    const result = fixture.run('Publish the automatically approved release');
    assert.equal(result.status === 0, tagSha === sha, result.stderr);
    assert.equal(fixture.calls().some(call => call[1] === 'create'), tagSha === sha);
  }
});

test('only main signing jobs obtain an identity token and the installer waits for approval', () => {
  const jobs = workflow.split('\njobs:\n')[1].split(/\n(?=  [\w-]+:\n)/);
  const signers = jobs.filter(job => /id-token: write/.test(job));
  assert.deepEqual(signers.map(job => job.match(/^  ([\w-]+):/)[1]), ['sign-game', 'sign-setup']);
  for (const signer of signers) {
    assert.match(signer, /github\.repository == 'ClankerWorldOrg\/ClankerWorld' && github\.ref == 'refs\/heads\/main'/);
    assert.match(signer, /ref: \$\{\{ needs\.nightly\.outputs\.sha \}\}/);
    assert.ok(signer.indexOf('scripts/sign-release-package.py sign') < signer.indexOf('actions/upload-artifact@'));
  }
  const installer = jobs.find(job => job.startsWith('  windows-installer:'));
  assert.match(installer, /needs: \[nightly, sign-game\]/);
  assert.match(installer, /test -f "\$\{game_zip\}\.sig"/);
});

test('changed package bytes or checksums prevent draft staging', t => {
  for (const asset of releaseAssets.filter(asset => !asset.endsWith('.sig'))) {
    const fixture = sandbox(t);
    writeReleaseAssets(fixture.root);
    fs.appendFileSync(path.join(fixture.root, asset), 'changed after building');
    assert.notEqual(fixture.run('Publish the automatically approved release').status, 0, asset);
    assert.equal(fixture.calls().filter(call => call[0] === 'release').length, 0, asset);
  }
});

test('draft staging preserves the built asset bytes and their matching checksums', t => {
  const fixture = sandbox(t);
  writeReleaseAssets(fixture.root);
  const before = releaseAssets.map(asset => fs.readFileSync(path.join(fixture.root, asset)));
  assert.equal(fixture.run('Publish the automatically approved release').status, 0);
  const uploaded = fixture.calls().find(call => call[1] === 'create').slice(-releaseAssets.length);
  assert.deepEqual(uploaded.map(asset => fs.readFileSync(path.join(fixture.root, asset))), before);
});

test('read-only builds hand artifacts to a main-only publisher without repository execution', () => {
  const jobs = workflow.split('\njobs:\n')[1].split(/\n(?=  [\w-]+:\n)/);
  const publisher = jobs.find(job => job.startsWith('  publish:\n'));
  assert.match(publisher, /needs: \[nightly, sign-setup\]/);
  assert.match(publisher, /if: github\.ref == 'refs\/heads\/main' && needs\.nightly\.outputs\.build == 'true'/);
  assert.doesNotMatch(publisher, /actions\/checkout@|\b(?:bash|node|python|dotnet)\b|scripts\//);
  assert.equal((publisher.match(/actions\/download-artifact@/g) || []).length, 3);
  for (const job of jobs.filter(job => !job.startsWith('  publish:\n'))) {
    assert.doesNotMatch(job, /\b(?:contents|actions): write\b/);
  }
  assert.match(workflow.split('\njobs:\n')[0], /permissions:\n  contents: read/);
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

test('retention removes untagged drafts and still cleans published nightly tags', t => {
  const oldDraft = { ...nightlies[1], draft: true };
  const fixture = sandbox(t, [nightlies.map(release => release.tag_name === oldDraft.tag_name ? oldDraft : release)]);
  fixture.env.NIGHTLY_TEST_UNTAGGED_DRAFT = oldDraft.tag_name;
  const result = fixture.run('Remove older nightly builds');
  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(fixture.calls().filter(call => call[1] === 'delete'), [
    ['release', 'delete', oldDraft.tag_name, '--yes'],
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

test('only the steps that call gh get the token, and checkout keeps no credentials', () => {
  const [head, jobs] = workflow.split('\njobs:\n');
  assert.doesNotMatch(head, /^\s*GH_TOKEN:/m);
  const writers = jobs.split(/\n(?=  [\w-]+:\n)/)
    .filter(job => /^      contents: write$/m.test(job))
    .map(job => job.match(/^  ([\w-]+):$/m)?.[1]);
  assert.deepEqual(writers, ['publish']);
  const steps = jobs.split(/\n(?=  [\w-]+:\n)/).flatMap(job => {
    const [settings, body] = job.split('\n    steps:\n');
    assert.doesNotMatch(settings, /^\s*GH_TOKEN:/m);
    assert.ok(body, 'Each nightly job has steps');
    return body.split(/\n(?=      - )/);
  });
  const withToken = steps.filter(step => /^\s*GH_TOKEN:/m.test(step)).map(step => step.match(/^      - name: (.+)$/m)?.[1]);
  assert.deepEqual(withToken, ['Decide whether main needs a new nightly build', 'Publish the automatically approved release', 'Remove older nightly builds']);
  for (const step of steps.filter(step => /\bgh (api|run|release) /.test(step))) assert.match(step, /\n        env:\n          GH_TOKEN: \$\{\{ github\.token \}\}\n/);
  for (const step of steps.filter(step => step.includes('actions/checkout@')))
    assert.match(step, /\n          persist-credentials: false(\n|$)/);
});

test('a failed deletion stops retention before deleting another tag', t => {
  const fixture = sandbox(t, [nightlies]);
  fixture.env.NIGHTLY_TEST_DELETE_FAIL = nightlies[1].tag_name;
  assert.notEqual(fixture.run('Remove older nightly builds').status, 0);
  assert.deepEqual(fixture.calls().filter(call => call[1] === 'delete').map(call => call[2]), [nightlies[1].tag_name]);
});


test('a later API page failure stops decision and cleanup before using partial releases', t => {
  for (const name of ['Decide whether main needs a new nightly build', 'Remove older nightly builds']) {
    const fixture = sandbox(t, [nightlies, alphaPage]);
    fixture.env.NIGHTLY_TEST_API_FAIL = 'partial';
    const result = fixture.run(name);
    assert.notEqual(result.status, 0);
    assert.equal(fixture.calls().filter(call => call[1] === 'delete').length, 0);
    if (fs.existsSync(fixture.env.GITHUB_OUTPUT))
      assert.doesNotMatch(fs.readFileSync(fixture.env.GITHUB_OUTPUT, 'utf8'), /build=true/);
  }
});


test('the workflow API flags paginate with the installed gh binary', async t => {
  const fixture = sandbox(t, [alphaPage, nightlies]);
  const result = fixture.run('Decide whether main needs a new nightly build');
  assert.equal(result.status, 0, result.stderr);
  const flags = fixture.calls().find(call => call[0] === 'api').slice(2);
  const located = spawnSync('bash', ['-c', 'command -v gh'], { encoding: 'utf8' });
  assert.equal(located.status, 0, located.stderr);
  const gh = located.stdout.trim();
  t.diagnostic(spawnSync(gh, ['--version'], { encoding: 'utf8' }).stdout.split('\n')[0]);
  const requested = [];
  let endpoint;
  const server = createServer((request, response) => {
    requested.push(request.url);
    const second = new URL(request.url, endpoint).searchParams.get('page') === '2';
    response.setHeader('content-type', 'application/json');
    if (!second) response.setHeader('link', `<${endpoint}?page=2>; rel="next"`);
    response.end(JSON.stringify(second ? nightlies : alphaPage));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  endpoint = `http://127.0.0.1:${server.address().port}/releases`;
  const actual = await promisify(execFile)(gh, ['api', endpoint, ...flags], {
    env: { ...process.env, GH_TOKEN: 'offline-test', GH_ENTERPRISE_TOKEN: 'offline-test', GH_CONFIG_DIR: fixture.root },
    timeout: 5_000,
  });
  const collected = spawnSync('jq', ['-s', '.'], { input: actual.stdout, encoding: 'utf8' });
  assert.equal(collected.status, 0, collected.stderr);
  assert.deepEqual(JSON.parse(collected.stdout).flat(), [...alphaPage, ...nightlies]);
  assert.deepEqual(requested.map(value => {
    const request = new URL(value, endpoint);
    return [request.pathname, request.searchParams.get('page') || '1'];
  }), [['/releases', '1'], ['/releases', '2']]);
});

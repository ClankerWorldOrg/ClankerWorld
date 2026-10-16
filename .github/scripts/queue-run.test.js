const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { test } = require('node:test');
const findQueueRun = require('./queue-run.js');

const sha = '40811eda51c2b4874954e8dc939488c679546f4a';
const context = { repo: { owner: 'ClankerWorldOrg', repo: 'ClankerWorld' }, sha };
const required = ['verify', 'windows-documentation', 'windows-provider-storage'];
const queueRun = (extra = {}) => ({
  id: 30, event: 'merge_group', head_sha: sha, run_attempt: 2,
  status: 'in_progress', conclusion: null, ...extra,
});
const greenJobs = () => required.map(name => ({ name, head_sha: sha, status: 'completed', conclusion: 'success' }));

async function lookup({ runs = [queueRun()], jobs = greenJobs(), errorAt } = {}) {
  const calls = [], outputs = {}, warnings = [], messages = [];
  const github = {
    rest: { actions: { listWorkflowRuns: 'runs', listJobsForWorkflowRunAttempt: 'jobs' } },
    // paginate's result is the combined array, including jobs past the first page.
    paginate: async (endpoint, params) => {
      calls.push({ endpoint, params });
      if (endpoint === errorAt) throw new Error('API unavailable');
      return endpoint === 'runs' ? runs : jobs;
    },
  };
  const core = {
    setOutput: (name, value) => { outputs[name] = value; },
    warning: message => warnings.push(message), info: message => messages.push(message),
  };
  await findQueueRun({ github, context, core });
  return { calls, outputs, warnings, messages };
}

test('main reuses all green required jobs while the queue workflow is still finishing', async () => {
  const result = await lookup({ jobs: [
    ...greenJobs(), { name: 'test-times', status: 'in_progress', conclusion: null },
  ] });
  assert.equal(result.outputs.run, '30');
  assert.deepEqual(result.calls, [
    { endpoint: 'runs', params: { ...context.repo, workflow_id: 'ci.yml', event: 'merge_group', head_sha: sha, per_page: 100 } },
    { endpoint: 'jobs', params: { ...context.repo, run_id: 30, attempt_number: 2, per_page: 100 } },
  ]);
  assert.equal(result.warnings.length, 0);
  assert.match(result.messages[0], /required jobs passed in run 30/);
});

test('a completed green queue workflow also reuses its required jobs', async () => {
  assert.equal((await lookup({ runs: [queueRun({ status: 'completed', conclusion: 'success' })] })).outputs.run, '30');
});

test('every required job must be present exactly once and completed successfully on this SHA', async () => {
  for (const name of required) {
    for (const change of [
      { status: 'queued', conclusion: null }, { status: 'in_progress', conclusion: null },
      { conclusion: null }, { conclusion: 'failure' }, { conclusion: 'cancelled' },
      { conclusion: 'skipped' }, { conclusion: 'neutral' }, { conclusion: 'timed_out' },
      { conclusion: 'action_required' }, { conclusion: 'stale' }, { head_sha: 'other' },
      { head_sha: undefined },
    ]) {
      const jobs = greenJobs().map(job => job.name === name ? { ...job, ...change } : job);
      assert.equal((await lookup({ jobs })).outputs.run, '', `${name}: ${JSON.stringify(change)}`);
    }
    assert.equal((await lookup({ jobs: greenJobs().filter(job => job.name !== name) })).outputs.run, '', name);
    assert.equal((await lookup({ jobs: [...greenJobs(), greenJobs().find(job => job.name === name)] })).outputs.run, '', name);
  }
});

test('overall workflow success cannot stand in for required-job evidence', async () => {
  assert.equal((await lookup({ runs: [queueRun({ status: 'completed', conclusion: 'success' })], jobs: [] })).outputs.run, '');
});

test('missing, other-SHA and non-queue runs never suppress main verification', async () => {
  for (const runs of [[], [queueRun({ head_sha: 'other' })], [queueRun({ event: 'pull_request' })], [queueRun({ event: 'push' })]]) {
    const result = await lookup({ runs });
    assert.equal(result.outputs.run, '');
    assert.equal(result.calls.length, 1);
  }
});

test('use only the latest exact-SHA queue run and its current attempt, never older green evidence', async () => {
  const result = await lookup({
    runs: [queueRun({ id: 10, run_attempt: 1, conclusion: 'success' }), queueRun(), queueRun({ id: 50, head_sha: 'other' })],
    jobs: greenJobs().map(job => ({ ...job, conclusion: 'failure' })),
  });
  assert.equal(result.outputs.run, '');
  assert.equal(result.calls[1].params.run_id, 30);
  assert.equal(result.calls[1].params.attempt_number, 2);
  for (const run_attempt of [undefined, 0, -1]) {
    assert.equal((await lookup({ runs: [queueRun({ run_attempt })] })).outputs.run, '');
  }
});

test('pagination includes required jobs after the first hundred jobs', async () => {
  const jobs = [...Array.from({ length: 100 }, (_, i) => ({ name: `other-${i}` })), ...greenJobs()];
  assert.equal((await lookup({ jobs })).outputs.run, '30');
});

test('API failures at either lookup leave full main verification enabled', async () => {
  for (const errorAt of ['runs', 'jobs']) {
    const result = await lookup({ errorAt });
    assert.equal(result.outputs.run, '');
    assert.match(result.warnings[0], /API unavailable/);
    assert.match(result.messages[0], /everything runs/);
  }
});

test('main aggregate still refuses scope failures and unverified skipped or failed jobs', () => {
  const workflow = fs.readFileSync(path.join(__dirname, '..', 'workflows', 'ci.yml'), 'utf8');
  const verify = workflow.split('\n  verify:\n')[1].split('\n  windows-documentation:')[0];
  const script = verify.split('        run: |\n')[1].split('\n').map(line => line.slice(10)).join('\n');
  const pass = { CODE: 'true', TESTS_NEEDED: 'true', SCOPE: 'success', CHECKS: 'success', TESTS: 'success', INSTALLER: 'success', QUEUE_RUN: '' };
  const execute = extra => spawnSync('bash', ['-e', '-c', script], { env: { ...process.env, ...pass, ...extra } }).status;
  assert.equal(execute({}), 0);
  assert.equal(execute({ QUEUE_RUN: '30', CHECKS: 'skipped', TESTS: 'skipped', INSTALLER: 'skipped' }), 0);
  for (const SCOPE of ['failure', 'cancelled', 'skipped', '']) {
    assert.notEqual(execute({ SCOPE, QUEUE_RUN: '30' }), 0);
  }
  for (const name of ['CHECKS', 'TESTS', 'INSTALLER']) {
    for (const value of ['failure', 'cancelled', 'skipped', '']) assert.notEqual(execute({ [name]: value }), 0);
  }
  assert.notEqual(execute({ TESTS_NEEDED: '', TESTS: 'skipped' }), 0);
  assert.equal(execute({ CODE: 'false', TESTS_NEEDED: 'false', TESTS: 'skipped', INSTALLER: 'skipped' }), 0);
});

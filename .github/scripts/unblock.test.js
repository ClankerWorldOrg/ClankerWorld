const assert = require('node:assert/strict');
const { test } = require('node:test');
const unblockFinishedWork = require('./unblock.js');
const { readBlockers, blockerState, plan, readyPrCloses } = unblockFinishedWork;

const Repo = 'compoodment/ClankerWorld';

// Blocker states as blockerState reports them, by number.
const merged = number => blockerState(number, { state: 'closed', pull_request: { merged_at: '2026-10-01T12:00:00Z' } });
const unmerged = number => blockerState(number, { state: 'closed', pull_request: { merged_at: null } });
const completed = number => blockerState(number, { state: 'closed', state_reason: 'completed' });
const notPlanned = number => blockerState(number, { state: 'closed', state_reason: 'not_planned' });
const open = number => blockerState(number, { state: 'open' });
const states = (...list) => new Map(list.map(state => [state.number, state]));
const blocked = (body, labels = [], more = {}) => ({ number: 50, state: 'open', body, labels: ['status:blocked', ...labels], ...more });

test('blockers are read from Blocked by and Waits on, alone or in lists', () => {
  assert.deepEqual(readBlockers('Blocked by #12').numbers, [12]);
  assert.deepEqual(readBlockers('Blocked by #12, #13').numbers, [12, 13]);
  assert.deepEqual(readBlockers('Blocked by #12 and #13').numbers, [12, 13]);
  assert.deepEqual(readBlockers('Blocked by #14, #12, and #13.').numbers, [12, 13, 14]);
  assert.deepEqual(readBlockers('- Other open PRs that overlap: Waits on #7').numbers, [7]);
  assert.deepEqual(readBlockers('This waits on #7 & #8; it is also blocked by #9.').numbers, [7, 8, 9]);
  assert.deepEqual(readBlockers('**Blocked by:** #3\nWaits on: #4').numbers, [3, 4]);
  assert.deepEqual(readBlockers(`Blocked by ${Repo}#5 and other/repo#6`, Repo).numbers, [5]);
  // The first word that is not another number ends the list.
  assert.deepEqual(readBlockers('Blocked by #1704, as required by the owner order in #1600.').numbers, [1704]);
});

test('descriptions with no numbered blocker, or a blocker only in ignored text, name none', () => {
  for (const body of [
    '', null, 'Blocked by: the repository having no open pull requests.', 'Fixes the bug from #12.',
    '<!-- Blocked by #12 -->', 'Write `Blocked by #12` in the description.', '```\nWaits on #12\n```',
    '~~Blocked by #12~~', 'Unblocked by #12 last week.',
  ]) {
    assert.deepEqual(readBlockers(body).numbers, [], String(body));
  }
});

test('a line naming an outside event next to a numbered blocker leaves the item alone', () => {
  const both = readBlockers('Blocked by #12\nBlocked by: the owner announcing a merge day');
  assert.deepEqual(both, { numbers: [12], outside: true });
  assert.equal(readBlockers('Blocked by #12.\n- Waits on #13 and #14').outside, false);
  // A sentence that only mentions being blocked is not a blocker line.
  assert.equal(readBlockers('Blocked by #12\nSteps 2 to 10 are blocked by the open pull requests.').outside, false);
  assert.deepEqual(plan(blocked('Blocked by #12\nWaits on the Windows signing key'), states(merged(12))),
    { action: 'skip', reason: 'also waits on something with no number' });
});

test('each blocker reports how it finished', () => {
  assert.deepEqual([merged(1), completed(2), unmerged(3), notPlanned(4), open(5)].map(state => [state.kind, state.how]), [
    ['done', 'merged'], ['done', 'closed as completed'], ['dropped', 'closed without merging'],
    ['dropped', 'closed as not planned'], ['open', 'is still open'],
  ]);
  assert.equal(blockerState(6, { state: 'closed', state_reason: 'duplicate' }).how, 'closed as a duplicate');
  assert.equal(blockerState(7, null).how, 'could not be found');
});

test('an issue whose single blocker merged is unblocked and queued', () => {
  const step = plan(blocked('Blocked by #12', ['type:feature', 'priority:p2']), states(merged(12)), { repoName: Repo });
  assert.equal(step.action, 'unblock');
  assert.equal(step.queue, true);
  assert.match(step.comment, /^<!-- unblock-check: unblocked #12 -->\nUnblocked automatically: #12 merged\. This issue is back in the queue\.$/);
});

test('every one of several blockers must be done', () => {
  const step = plan(blocked('Blocked by #12, #13 and #14'), states(merged(12), completed(13), completed(14)));
  assert.equal(step.action, 'unblock');
  assert.match(step.comment, /Unblocked automatically: #12 merged, #13 closed as completed, #14 closed as completed\./);
  assert.deepEqual(plan(blocked('Blocked by #12 and #13'), states(merged(12), open(13))),
    { action: 'skip', reason: 'waits on #13' });
});

test('an item naming no blocker, or waiting on the owner, is left alone', () => {
  assert.deepEqual(plan(blocked('Blocked by a fresh failure with diagnostics.'), states()),
    { action: 'skip', reason: 'names no blocker by number' });
  assert.deepEqual(plan(blocked('Blocked by #12', ['status:needs-decision']), states(merged(12))),
    { action: 'skip', reason: 'waits on the owner' });
  assert.deepEqual(plan(blocked('Blocked by #50'), states()), { action: 'skip', reason: 'names no blocker by number' });
  assert.equal(plan({ ...blocked('Blocked by #12'), labels: [] }, states(merged(12))).action, 'skip');
});

test('a blocker closed as not planned, or a pull request closed without merging, asks for new blockers', () => {
  for (const [dropped, how] of [[notPlanned(13), 'closed as not planned'], [unmerged(13), 'closed without merging']]) {
    const step = plan(blocked('Blocked by #12 and #13'), states(merged(12), dropped));
    assert.equal(step.action, 'repoint', how);
    assert.match(step.comment, new RegExp(`^<!-- unblock-check: not done #13 -->\nNot unblocked automatically: #13 ${how}\\.`));
    assert.match(step.comment, /`Blocked by` or `Waits on` line/);
  }
  // While another blocker is still open, nothing is said yet.
  assert.equal(plan(blocked('Blocked by #12 and #13'), states(open(12), notPlanned(13))).action, 'skip');
});

test('claimed, decision, owner-task, parked and ready-PR issues are unblocked without joining the queue', () => {
  for (const label of ['status:in-progress', 'type:decision', 'owner-task', 'status:parked']) {
    const step = plan(blocked('Blocked by #12', [label]), states(merged(12)));
    assert.equal(step.action, 'unblock', label);
    assert.equal(step.queue, false, label);
    assert.doesNotMatch(step.comment, /queue/, label);
  }
  assert.equal(plan(blocked('Blocked by #12'), states(merged(12)), { readyPr: true }).queue, false);
  // A pull request is unblocked, but only issues join the queue.
  assert.equal(plan(blocked('Waits on #12', [], { pull_request: {} }), states(merged(12))).queue, false);
  const prs = [{ draft: true, body: 'Closes #50' }, { draft: false, body: 'Refs #50' }];
  assert.equal(readyPrCloses(prs, 50, Repo), false);
  assert.equal(readyPrCloses([...prs, { draft: false, body: 'Fixes #50' }], 50, Repo), true);
});

test('the comment marker prevents posting the same note twice', () => {
  const item = blocked('Blocked by #12 and #13');
  const repoint = plan(item, states(merged(12), notPlanned(13)));
  assert.equal(plan(item, states(merged(12), notPlanned(13)), { comments: [{ body: repoint.comment }] }).action, 'skip');
  // A different dropped blocker gets its own note.
  assert.equal(plan(item, states(notPlanned(12), notPlanned(13)), { comments: [{ body: repoint.comment }] }).action, 'repoint');
  // status:blocked added back by hand for the same finished blockers stays on.
  const done = plan(item, states(merged(12), completed(13)));
  assert.deepEqual(plan(item, states(merged(12), completed(13)), { comments: [{ body: done.comment }] }),
    { action: 'skip', reason: 'status:blocked was added back after an earlier unblock' });
  // A marker for #1 does not match #12.
  assert.equal(plan(blocked('Blocked by #12'), states(merged(12)), { comments: [{ body: '<!-- unblock-check: unblocked #1 -->' }] }).action, 'unblock');
});

// A small fake of the GitHub API: issues and pull requests share numbers, as on GitHub.
function world({ issues = [], prs = [], comments = {}, beforeGet = () => {}, failComment = false }) {
  const records = new Map();
  for (const issue of issues) records.set(issue.number, { state: 'open', body: '', labels: [], ...issue });
  for (const pr of prs) {
    records.set(pr.number, { state: 'open', draft: false, body: '', labels: [], ...pr, pull_request: { merged_at: pr.merged_at ?? null } });
  }
  const posted = [];
  const calls = [];
  const github = {
    rest: {
      pulls: { list: 'pulls.list' },
      issues: {
        listForRepo: 'issues.listForRepo',
        listComments: 'issues.listComments',
        get: async ({ issue_number }) => {
          calls.push(['get', issue_number]);
          beforeGet({ issue_number, records });
          if (!records.has(issue_number)) throw Object.assign(new Error('Not Found'), { status: 404 });
          return { data: structuredClone(records.get(issue_number)) };
        },
        addLabels: async ({ issue_number, labels }) => {
          const record = records.get(issue_number);
          record.labels = [...new Set([...record.labels, ...labels])];
        },
        removeLabel: async ({ issue_number, name }) => {
          const record = records.get(issue_number);
          if (!record.labels.includes(name)) throw Object.assign(new Error('missing'), { status: 404 });
          record.labels = record.labels.filter(label => label !== name);
        },
        createComment: async ({ issue_number, body }) => {
          if (failComment) throw Object.assign(new Error('server error'), { status: 500 });
          posted.push({ number: issue_number, body });
          (comments[issue_number] ??= []).push({ body });
        },
      },
    },
    paginate: async (method, params) => {
      calls.push([method]);
      if (method === 'issues.listForRepo') {
        return structuredClone([...records.values()].filter(r => r.state === 'open' && r.labels.includes(params.labels)));
      }
      if (method === 'pulls.list') return structuredClone([...records.values()].filter(r => r.pull_request && r.state === 'open'));
      if (method === 'issues.listComments') return structuredClone(comments[params.issue_number] ?? []);
      throw new Error(`unexpected ${method}`);
    },
  };
  const logs = [];
  const run = (dryRun = false) => unblockFinishedWork({
    github, context: { repo: { owner: 'compoodment', repo: 'ClankerWorld' } },
    core: { info: message => logs.push(message), warning: message => logs.push(message) }, dryRun,
  });
  return { records, posted, calls, logs, run };
}

test('a run unblocks finished work once, queues the issue and leaves the rest', async () => {
  const state = world({
    issues: [
      { number: 1, labels: ['type:feature', 'status:blocked'], body: 'Blocked by #8 and #2' },
      { number: 2, state: 'closed', state_reason: 'completed' },
      { number: 3, labels: ['type:feature', 'status:blocked'], body: 'Blocked by #9' },
      { number: 4, labels: ['type:bug', 'status:blocked'], body: 'Blocked by a fresh failure.' },
    ],
    prs: [
      { number: 8, state: 'closed', merged_at: '2026-10-01T12:00:00Z' },
      { number: 9 },
      { number: 10, labels: ['status:blocked', 'status:needs-review'], body: '- Other open PRs that overlap: Waits on #8' },
    ],
  });
  await state.run();
  assert.deepEqual(state.records.get(1).labels, ['type:feature', 'status:needs-pr']);
  assert.deepEqual(state.records.get(3).labels, ['type:feature', 'status:blocked']);
  assert.deepEqual(state.records.get(4).labels, ['type:bug', 'status:blocked']);
  assert.deepEqual(state.records.get(10).labels, ['status:needs-review']);
  assert.deepEqual(state.posted.map(comment => comment.number), [1, 10]);
  assert.match(state.posted[0].body, /Unblocked automatically: #2 closed as completed, #8 merged\. This issue is back in the queue\./);
  assert.match(state.posted[1].body, /Unblocked automatically: #8 merged\.$/);
  // Each blocker is read once, and no open pull request list is needed for the pull request.
  assert.equal(state.calls.filter(([kind, number]) => kind === 'get' && number === 8).length, 1);
  await state.run();
  assert.equal(state.posted.length, 2);
});

test('an issue a ready pull request closes is unblocked without joining the queue', async () => {
  const state = world({
    issues: [{ number: 1, labels: ['status:blocked', 'status:has-pr'], body: 'Blocked by #8' }],
    prs: [{ number: 8, state: 'closed', merged_at: '2026-10-01T12:00:00Z' }, { number: 9, body: 'Closes #1' }],
  });
  await state.run();
  assert.deepEqual(state.records.get(1).labels, ['status:has-pr']);
  assert.doesNotMatch(state.posted[0].body, /queue/);
});

test('a blocker closed as not planned gets one note across runs, and the item stays blocked', async () => {
  const state = world({
    issues: [
      { number: 1, labels: ['status:blocked'], body: 'Blocked by #2' },
      { number: 2, state: 'closed', state_reason: 'not_planned' },
    ],
  });
  await state.run();
  await state.run();
  assert.deepEqual(state.records.get(1).labels, ['status:blocked']);
  assert.equal(state.posted.length, 1);
  assert.match(state.posted[0].body, /#2 closed as not planned/);
});

test('a dry run changes nothing but says what it would do', async () => {
  const state = world({
    issues: [
      { number: 1, labels: ['status:blocked'], body: 'Blocked by #2' },
      { number: 2, state: 'closed', state_reason: 'completed' },
    ],
  });
  await state.run(true);
  assert.deepEqual(state.records.get(1).labels, ['status:blocked']);
  assert.equal(state.posted.length, 0);
  assert.ok(state.logs.some(line => /Unblocking issue #1 and adding status:needs-pr: #2 closed as completed\. \(dry run\)/.test(line)));
});

test('a claim or owner wait added after the list was read is respected', async () => {
  for (const [label, labels, posts] of [['status:in-progress', ['status:in-progress'], 1], ['status:needs-decision', ['status:blocked', 'status:needs-decision'], 0]]) {
    let added = false;
    const state = world({
      issues: [
        { number: 1, labels: ['status:blocked'], body: 'Blocked by #2' },
        { number: 2, state: 'closed', state_reason: 'completed' },
      ],
      beforeGet: ({ issue_number, records }) => {
        if (issue_number === 1 && !added) records.get(1).labels.push(label);
        if (issue_number === 1) added = true;
      },
    });
    await state.run();
    assert.deepEqual(state.records.get(1).labels, labels, label);
    assert.equal(state.posted.length, posts, label);
  }
});

test('a failure after removing the label puts it back for the next run', async () => {
  const state = world({
    issues: [
      { number: 1, labels: ['status:blocked'], body: 'Blocked by #2' },
      { number: 2, state: 'closed', state_reason: 'completed' },
    ],
    failComment: true,
  });
  await assert.rejects(state.run(), /server error/);
  assert.ok(state.records.get(1).labels.includes('status:blocked'));
});

test('the workflow runs on closes, on a schedule and by hand from main, using main\'s script', () => {
  const { readFileSync } = require('node:fs');
  const workflow = readFileSync(`${__dirname}/../workflows/unblock.yml`, 'utf8');
  assert.match(workflow, /issues:\s*\n\s+types: \[closed\]/);
  assert.match(workflow, /pull_request_target:\s*\n\s+types: \[closed\]/);
  assert.match(workflow, /schedule:\s*\n\s+- cron: '\d+,\d+ \* \* \* \*'/);
  assert.match(workflow, /workflow_dispatch:\s*\n\s+inputs:\s*\n\s+dry_run:/);
  assert.match(workflow, /if: github\.event_name != 'workflow_dispatch' \|\| github\.ref == 'refs\/heads\/main'/);
  assert.match(workflow, /uses: actions\/checkout@v5\s*\n\s+with:\s*\n\s+ref: main\s*\n\s+persist-credentials: false/);
});

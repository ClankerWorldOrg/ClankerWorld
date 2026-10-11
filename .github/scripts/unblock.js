// Removes status:blocked once everything an item waits on is done, so blocked
// work goes back to the queue without anyone having to remember it.
// Run by .github/workflows/unblock.yml through actions/github-script, when an
// issue or pull request closes and every 30 minutes as a catch-up.
// It reads only GitHub data; it never checks out or runs pull request code.
//
// For each open issue or pull request with status:blocked:
// - Its description names its blockers as "Blocked by #N" or "Waits on #N",
//   including lists such as "Blocked by #N, #M and #K". Text in HTML
//   comments, code and ~~struck-out~~ text is ignored, and so are other
//   repositories' issues. Each number may be an issue or a pull request.
// - It is left alone when it names no blocker by number, when a line starting
//   with "Blocked by" or "Waits on" names something else (an outside event),
//   when it waits on the owner (status:needs-decision), or while any blocker
//   is still open.
// - A blocker is done when it is a pull request that merged or an issue closed
//   as completed. When every blocker is done, status:blocked comes off, and an
//   issue gets status:needs-pr unless someone holds it (status:in-progress),
//   it is parked, a decision or an owner task, or a ready pull request closes
//   it. One comment says how each blocker finished.
// - When every blocker is closed but one closed without being done (an issue
//   closed as not planned or as a duplicate, or a pull request closed without
//   merging), the item stays blocked and gets one comment asking for it to
//   name what it waits on now.
// Comments carry a marker naming the blockers, so a run never posts the same
// one twice. If someone adds status:blocked back after this script removed it
// for the same blockers, it is left alone: they meant it.

// pr-labels.js reads closing keywords, so every script finds the same pull
// requests closing an issue.
const { closingIssueNumbers } = require('./pr-labels.js');

const Blocked = 'status:blocked';
const NeedsPr = 'status:needs-pr';
const NeedsDecision = 'status:needs-decision';
const InProgress = 'status:in-progress';
const Parked = 'status:parked';
// Someone holds these, or they are never pull request work (CONTRIBUTING
// Labels; pr-labels.js and stale-claims.js), so they never get status:needs-pr.
const NotQueued = [InProgress, Parked, 'type:decision', 'owner-task'];
const Marker = '<!-- unblock-check';

// The same list grammar as "Refs" in pr-labels.js: "#7", "#7, #8",
// "#7, #8 and #9" or "#7 & #8", each optionally as owner/repo#N.
const IssueRef = String.raw`(?:[\w.-]+\/[\w.-]+)?#\d+\b`;
const RefList = String.raw`${IssueRef}(?:\s*(?:,\s*(?:and\s+)?|and\s+|&\s*)${IssueRef})*`;
// "Blocked by #7", "Waits on #7", "**Blocked by:** #7".
const Phrase = String.raw`\b(?:blocked\s+by|waits\s+on)\b`;
const BlockerPattern = new RegExp(String.raw`${Phrase}[*_]*:?[*_]*\s*[*_]*(${RefList})`, 'gi');
// A line that starts with the phrase and goes on to name something other than
// a number, such as "Blocked by: the owner's merge day", waits on an outside event.
const LinePattern = new RegExp(String.raw`^\s*(?:[-*+>]\s+)*[*_]*${Phrase}(.*)$`, 'gim');
const StartsWithRef = new RegExp(String.raw`^${IssueRef}`);
const IssueRefPattern = /(?<![\w./-])(?:([\w.-]+\/[\w.-]+))?#(\d+)\b/g;

function labelNames(labels) {
  return (labels ?? []).map(label => (typeof label === 'string' ? label : label.name));
}

function withoutIgnoredText(body) {
  return (body ?? '')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/`[^`\n]*`/g, ' ')
    .replace(/~~[^~\n]*~~/g, ' ');
}

// The blockers a description names, in number order, and whether it also
// waits on something that has no number.
function readBlockers(body, repoName = '') {
  const text = withoutIgnoredText(body);
  const thisRepo = repoName.toLowerCase();
  const numbers = new Set();
  for (const [, list] of text.matchAll(BlockerPattern)) {
    for (const [, otherRepo, number] of list.matchAll(IssueRefPattern)) {
      if (!otherRepo || otherRepo.toLowerCase() === thisRepo) numbers.add(Number(number));
    }
  }
  const outside = [...text.matchAll(LinePattern)].some(([, rest]) => {
    const named = rest.replace(/^[\s*_:]+/, '');
    return named !== '' && !StartsWithRef.test(named);
  });
  return { numbers: [...numbers].sort((a, b) => a - b), outside };
}

// How one blocker stands, from its issues.get record (null when it does not
// exist). The issues API reports a pull request's merge as pull_request.merged_at.
function blockerState(number, record) {
  const state = (kind, how) => ({ number, kind, how });
  if (!record) return state('dropped', 'could not be found');
  if (record.state === 'open') return state('open', 'is still open');
  if (record.pull_request) {
    return record.pull_request.merged_at ? state('done', 'merged') : state('dropped', 'closed without merging');
  }
  if (record.state_reason === 'completed') return state('done', 'closed as completed');
  if (record.state_reason === 'not_planned') return state('dropped', 'closed as not planned');
  if (record.state_reason === 'duplicate') return state('dropped', 'closed as a duplicate');
  return state('dropped', 'closed without being completed');
}

// The blockers to look up for an item, or why it is left alone whatever they are.
function waitsOn(item, repoName = '') {
  const labels = labelNames(item.labels);
  if (item.state && item.state !== 'open') return { skip: 'is closed' };
  if (!labels.includes(Blocked)) return { skip: `does not have ${Blocked}` };
  if (labels.includes(NeedsDecision)) return { skip: 'waits on the owner' };
  const { numbers, outside } = readBlockers(item.body, repoName);
  const blockers = numbers.filter(number => number !== item.number);
  if (blockers.length === 0) return { skip: 'names no blocker by number' };
  if (outside) return { skip: 'also waits on something with no number' };
  return { numbers: blockers };
}

const list = blockers => blockers.map(blocker => `#${blocker.number} ${blocker.how}`).join(', ');
const markerFor = (kind, blockers) => `${Marker}: ${kind} ${blockers.map(blocker => `#${blocker.number}`).join(' ')} -->`;

function alreadyPosted(comments, marker) {
  return comments.some(comment => (comment.body ?? '').includes(marker));
}

// Whether an open, ready pull request closes the issue, so it is with a reviewer.
function readyPrCloses(openPrs, number, repoName = '') {
  return openPrs.some(pr => !pr.draft && closingIssueNumbers(pr.body, repoName).has(number));
}

// What to do with one item, given the state of each of its blockers (a Map from
// number to blockerState). Comments are the item's comments, checked for markers.
function plan(item, states, { repoName = '', comments = [], readyPr = false } = {}) {
  const waits = waitsOn(item, repoName);
  if (waits.skip) return { action: 'skip', reason: waits.skip };
  const blockers = waits.numbers.map(number => states.get(number));
  const open = blockers.filter(blocker => blocker.kind === 'open');
  if (open.length > 0) return { action: 'skip', reason: `waits on ${open.map(blocker => `#${blocker.number}`).join(', ')}` };
  const dropped = blockers.filter(blocker => blocker.kind === 'dropped');
  if (dropped.length > 0) {
    const marker = markerFor('not done', dropped);
    if (alreadyPosted(comments, marker)) return { action: 'skip', reason: `already asked to name new blockers for ${list(dropped)}` };
    return {
      action: 'repoint', blockers,
      comment: `${marker}\nNot unblocked automatically: ${list(dropped)}. ` +
        'Name what this waits on now in its `Blocked by` or `Waits on` line, or remove `status:blocked` if nothing is left.',
    };
  }
  const marker = markerFor('unblocked', blockers);
  // This script removed the label for these blockers before; someone put it back.
  if (alreadyPosted(comments, marker)) return { action: 'skip', reason: `${Blocked} was added back after an earlier unblock` };
  const labels = labelNames(item.labels);
  const queue = !item.pull_request && !labels.some(name => NotQueued.includes(name)) && !readyPr;
  return {
    action: 'unblock', blockers, queue,
    comment: `${marker}\nUnblocked automatically: ${list(blockers)}.${queue ? ' This issue is back in the queue.' : ''}`,
  };
}

async function removeLabel(github, repo, number, name) {
  try {
    await github.rest.issues.removeLabel({ ...repo, issue_number: number, name });
    return true;
  } catch (error) {
    if (error.status !== 404) throw error;
    return false;
  }
}

async function readItem(github, repo, number) {
  try {
    return (await github.rest.issues.get({ ...repo, issue_number: number })).data;
  } catch (error) {
    if (error.status === 404 || error.status === 410) return null;
    throw error;
  }
}

// One read per blocker: the issues API also answers for pull requests. Only if
// it leaves out whether a closed pull request merged is the pull request read.
async function readBlocker(github, repo, number) {
  const record = await readItem(github, repo, number);
  if (record?.pull_request && record.state !== 'open' && !('merged_at' in record.pull_request)) {
    const { data: pr } = await github.rest.pulls.get({ ...repo, pull_number: number });
    record.pull_request = { ...record.pull_request, merged_at: pr.merged ? (pr.merged_at ?? 'merged') : null };
  }
  return blockerState(number, record);
}

// Stop before the token runs low. Checking the rate limit costs nothing
// against it.
const MinRemaining = 100;
async function enoughBudget(github, core) {
  if (!github.rest.rateLimit?.get) return true;
  const { data } = await github.rest.rateLimit.get();
  const remaining = data.resources?.core?.remaining ?? data.rate?.remaining ?? Infinity;
  if (remaining >= MinRemaining) return true;
  core.warning?.(`Stopping early: only ${remaining} API requests left this hour.`);
  return false;
}

async function unblockFinishedWork({ github, context, core, dryRun = false }) {
  const repo = context.repo;
  const repoName = `${repo.owner}/${repo.repo}`;
  const items = await github.paginate(github.rest.issues.listForRepo, {
    ...repo, state: 'open', labels: Blocked, per_page: 100,
  });
  // Each blocker is read once per run, and open pull requests only when an
  // unblocked issue might go back to the queue.
  const states = new Map();
  let openPrs = null;
  const readyPr = async number => readyPrCloses(openPrs ??= await github.paginate(github.rest.pulls.list, {
    ...repo, state: 'open', per_page: 100,
  }), number, repoName);

  const lookUp = async item => {
    for (const number of waitsOn(item, repoName).numbers ?? []) {
      if (!states.has(number)) states.set(number, await readBlocker(github, repo, number));
    }
  };

  for (const listed of items) {
    const kind = listed.pull_request ? 'Pull request' : 'Issue';
    const stays = reason => core.info(`${kind} #${listed.number} stays blocked: it ${reason}.`);
    if (!waitsOn(listed, repoName).skip && !await enoughBudget(github, core)) return;
    await lookUp(listed);
    const first = plan(listed, states, { repoName });
    if (first.action === 'skip') {
      stays(first.reason);
      continue;
    }

    // Read the item again, so a claim, owner wait or new blocker added since
    // the list was read is respected, and check its comments for markers.
    const item = await readItem(github, repo, listed.number);
    if (!item) continue;
    await lookUp(item);
    const comments = await github.paginate(github.rest.issues.listComments, { ...repo, issue_number: item.number, per_page: 100 });
    let step = plan(item, states, { repoName, comments });
    if (step.action === 'unblock' && step.queue && await readyPr(item.number)) {
      step = plan(item, states, { repoName, comments, readyPr: true });
    }
    if (step.action === 'skip') {
      stays(step.reason);
      continue;
    }
    const finished = list(step.blockers);
    if (step.action === 'repoint') {
      core.info(`${kind} #${item.number} stays blocked and gets a note: ${finished}.${dryRun ? ' (dry run)' : ''}`);
      if (!dryRun) await github.rest.issues.createComment({ ...repo, issue_number: item.number, body: step.comment });
      continue;
    }
    core.info(`Unblocking ${kind.toLowerCase()} #${item.number}${step.queue ? ` and adding ${NeedsPr}` : ''}: ${finished}.${dryRun ? ' (dry run)' : ''}`);
    if (dryRun) continue;
    if (!await removeLabel(github, repo, item.number, Blocked)) continue;
    try {
      if (step.queue && !labelNames(item.labels).includes(NeedsPr)) {
        await github.rest.issues.addLabels({ ...repo, issue_number: item.number, labels: [NeedsPr] });
      }
      await github.rest.issues.createComment({ ...repo, issue_number: item.number, body: step.comment });
    } catch (error) {
      // Never leave an issue unblocked but out of the queue with no note: put
      // the label back so the next run tries again, then fail this run visibly.
      await github.rest.issues.addLabels({ ...repo, issue_number: item.number, labels: [Blocked] }).catch(() => {});
      throw error;
    }
  }
}

module.exports = unblockFinishedWork;
module.exports.readBlockers = readBlockers;
module.exports.blockerState = blockerState;
module.exports.waitsOn = waitsOn;
module.exports.readyPrCloses = readyPrCloses;
module.exports.plan = plan;

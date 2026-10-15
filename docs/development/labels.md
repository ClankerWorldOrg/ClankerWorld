---
title: Labels
type: development-reference
status: active
updated: 2026-10-11
---

# Labels

[CONTRIBUTING](../../CONTRIBUTING.md#labels) says what each label means and
which ones you add by hand. This page explains how the label list is kept and
what the label workflows do on their own, for anyone changing them or puzzled
by a label that moved.

## The label list

[`.github/labels.json`](../../.github/labels.json) is the only source of
labels. A workflow updates the repository from it, and retired labels, such as
`status:needs-playtest`, are deleted again every six hours, so never create a
label by hand. To rename one, change its name and add the old name to its
`aliases`, so issues keep it, and in the same pull request update the issue
forms, `.github/scripts/` and the searches in CONTRIBUTING, AGENTS.md and the
skills that name it. The workflow-script tests check that every label the
forms, scripts and documented searches use is active.

Release labels split the open issues when more is open than one release can
take: `release:alpha-1` marks what the release being prepared needs and
`release:alpha-2` what waits for the release after it
([Release scope](../../CONTRIBUTING.md#release-scope)). They are added by hand.
When a release is published its label is retired and the next one becomes the
current release's label
([Releasing](releasing.md#release-scope-and-merge-day)).

## What the workflows do on issues

- Issue forms filed on GitHub's website set the type and `priority:p2`, and the
  Decision form also adds `status:needs-decision`. An issue created through the
  API or an agent's GitHub tools gets no labels.
- An issue a pull request closes (`Closes`, `Fixes` or `Resolves` directly
  before its number) gets `status:has-pr` in place of `status:needs-pr`, and
  loses `status:in-progress` when that pull request is marked ready. While the
  claimed pull request is a draft, `status:in-progress` stays next to
  `status:has-pr`; while the claim waits on the owner, it stays next to
  `status:needs-decision`.
- If the pull request goes back to draft, nobody holds the issue and the pull
  request has no `status:needs-decision` or `status:blocked`, the issue gets
  `status:needs-pr` back next to `status:has-pr`.
- When the last such pull request closes, `status:has-pr` comes off; if none
  merged, the issue goes back to `status:needs-pr`, unless it is claimed,
  blocked, waiting on the owner or parked, or is a decision or owner task.
- `Refs` changes no issue labels.
- Quiet claims are released by **Release stale claims**
  ([what counts](../../skills/fix-issue/SKILL.md#claim-an-issue)).
- **Unblock waiting work** removes `status:blocked` from an open issue or pull
  request once every item its description names as `Blocked by #N` or
  `Waits on #N` (also `#N, #M and #K`) has merged or closed as completed, and
  comments with how each one finished. An issue also gets `status:needs-pr`,
  unless it is claimed, parked, a decision or an owner task, or a ready pull
  request closes it. It leaves alone an item waiting on the owner
  (`status:needs-decision`), one that names no blocker by number or has a
  `Blocked by` line naming an outside event, and one with a blocker still
  open. If a blocker closed as not planned, or a pull request closed without
  merging, the item stays blocked and gets one comment asking it to name what
  it waits on now. If someone adds `status:blocked` back for the same
  blockers, it stays. The workflow runs when an issue or pull request closes
  and every 30 minutes; to preview it, run it from Actions on `main` with its
  dry-run option.

## What the workflows do on pull requests

- **Priority:** the highest of the open issues it closes; if none has one, of
  the open issues it refers to; otherwise P2; and P0 if it changes
  [how we work](../../CONTRIBUTING.md#priorities). A pull request shows one
  priority: one added by hand takes its place when it is higher, and a lower
  one added by hand comes off.
- **Type:** one, from the first ticked **Type of change** box, or `type:docs`
  when none is ticked and only documentation changes.
- **Areas:** at most two, from the files it changes, set when it opens,
  reopens, is marked ready, is pushed to or changes base, unless someone has
  changed its areas by hand.
- **Status:** `status:needs-review` while it is ready. `status:needs-review`
  and `status:reviewing` come off when it closes or goes back to draft.
- A pull request stacked on another's branch is judged by what it would change
  on main.

The scripts are [`pr-labels.js`](../../.github/scripts/pr-labels.js),
[`stale-claims.js`](../../.github/scripts/stale-claims.js),
[`unblock.js`](../../.github/scripts/unblock.js) and
[`sync-labels.js`](../../.github/scripts/sync-labels.js), each with its tests
beside it.

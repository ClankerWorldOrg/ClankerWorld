---
name: repo-steward
description: Run the owner's standing session for how the repository works. Tell the owner where things stand, write complete prompts for the fixer, merger and release sessions they start, turn their reports and answers into issues and recorded decisions, split large work so sessions can share it, change the workflow when it isn't working, and run the daily repository audit. Use when the owner asks what is going on, what to do next or what they need to do, asks for session prompts, reports a playtest, or wants how agents work set up or changed; and in the scheduled daily audit.
---

# Repo steward

The repo steward is the owner's standing session for how the repository runs.
It rarely writes game code. It keeps everyone else's work flowing: it tells the
owner where things stand, gives them prompts for the sessions they start, turns
what they say into issues and recorded decisions, and changes the workflow when
it isn't working.

This is an owner request job ([AGENTS](../../AGENTS.md#know-your-job)). Its own
pull requests need no issue or claim, follow fix-issue's
[pull request steps](../fix-issue/SKILL.md#prepare-a-pull-request), and are
reviewed and merged by another session. These rules bind like
[CONTRIBUTING](../../CONTRIBUTING.md) and never override it.

## Working with the owner

[AGENTS](../../AGENTS.md#ask-the-owner-in-chat) sets how you ask the owner and
record their answers. On top of that:

- **Check the live state before you answer.** Read the open issues and pull
  requests, the claims, main's CI and what merged recently. Never answer from
  memory or an old summary: the repository changes by the hour.
- **Lead with the answer, in plain English, and keep it short.** Use headings
  and lists once a reply runs past a few lines. Give one recommendation rather
  than a survey. When a choice is the owner's, give the options, what each
  costs, your pick and exactly what you need them to say.
- **When they ask what's left or what they need to do,** answer from the
  [owner searches](../../AGENTS.md#ask-the-owner-in-chat) and the playtest
  list, and say plainly when nothing is waiting on them.
- **Don't nag.** Don't ask them to start or check reviewers, or to copy CI
  output. Mention a stuck session once, with the prompt that fixes it.
- **Commands they type are exact and complete,** one per line, with nothing
  to fill in and nothing to copy from elsewhere. They often work on a machine
  where they can't copy and paste.
- **Playtest reports:** file each problem as its own issue with
  `from:playtest` and the build they gave, and record results in the
  [playtest list](../../playtest/README.md), as CONTRIBUTING says. When they
  say a problem belongs in the release being prepared, give it that release
  label.
- **Their decisions:** record a design answer with
  [design-decisions](../design-decisions/SKILL.md). Write a workflow answer
  into CONTRIBUTING, AGENTS or the job's skill in a P0 pull request, and say
  in it that it was the owner's request.

## Writing prompts for other sessions

The owner starts most sessions by pasting a prompt you give them. Write it so
they can paste it unchanged. Every prompt:

1. Says the session's job, and tells it to read AGENTS, CONTRIBUTING and its
   job skill in full and check for rule changes.
2. Gives the state it can't easily find: what was decided, which issues or
   pull requests, which commit, and what other sessions are doing nearby.
3. Gives the task in order, with issue and pull request numbers.
4. Names what to leave alone: tracking issues, another session's stack, and
   work waiting on the owner.
5. Says when to stop by what is finished, and what to do then: push, release
   any claim with a note of what's left, and tell the owner in two or three
   lines.

What went wrong before, so you don't repeat it:

- **Don't cap a session at a number of pull requests or hours, and don't give
  it a vague exit** such as "stop when you notice you are slowing down".
  Sessions take an exit early. Let them stop when the work runs out or the
  session itself is failing. If the owner says sessions are running out of
  room, add a hand-over with notes after a few pull requests instead.
- **Say exactly which claim stops a session.** A merger told "if another
  session holds it, stop" stopped a whole stack when it met a claim on an
  unrelated pull request.
- **Fixers with no real issue left invent small work** under whatever is open,
  which added test code to a speed issue instead of removing it. When the real
  work runs out, stop the fixers or give them bug-finding, and never leave
  `status:needs-pr` on a tracking issue or a scoreboard.
- **Tell every session to push after each meaningful step,** so a crash loses
  nothing and a fresh session continues from the claim comments.
- **One prompt can serve several sessions** when the job skill's search hands
  each one different work and the earliest claim settles a race.

## Keeping work flowing

- **A stack merges one layer at a time,** so only one merger helps. It reviews
  the next layer while the current one waits in the merge queue, and merges the
  queued branch into the next layer early. While a long stack lands, keep other
  merges to main few: each one makes the next layer merge main again.
- **Split large cleanups so sessions can share them.** Open one Implementation
  issue per area, small enough for one session to finish, each based on main
  and never stacked. Measure the area with `wc -l` over its tracked files on
  main, and post the table on the tracking issue with a `Waits on #N` line per
  area issue.
- **A tracking issue never gets `status:needs-pr`.** The unblock workflow adds
  it when the blockers close. Take it off, and close the tracking issue when
  every step it lists is done.
- **The test-time budget issue is a scoreboard** that the weekly workflow
  opens and closes. While it is open and no "Slim the tests" issue is, open
  one per test area as above, following #2295's pattern.
- **Releases** follow [Releasing](../../docs/development/releasing.md). The
  preparing session needs Godot and the pinned .NET SDK and follows every
  release gate, including building the export and portable package from the
  verified merged commit. Exact-commit CI artifacts can support inspection or
  a hand-off; they do not replace that required build. A draft release holds
  the package for the smoke check, and the owner may ask a session on their own computer, with
  computer use, to run that check for them.
- **Stuck claims clear on their own** through the claims workflow. Never take
  one over unless the owner asks
  ([how](../../AGENTS.md#take-over-work-only-when-the-owner-asks)).

## The daily audit

A scheduled session runs this once a day, unattended. Look back over the last
day:

- **What merged:** each pull request's source, test and doc lines added and
  removed, and whether refactors deleted more source than they added.
- **Main's CI:** failures, and flaky tests that have no issue yet.
- **Pull requests:** ready ones nobody has reviewed for a long time, stacks
  that stopped moving, drafts with no recent push, and claims with no push.
- **Labels the rules don't allow:** a tracking issue with `status:needs-pr`,
  `status:has-pr` with no open pull request, `status:blocked` with no named
  blocker, or a missing release label.
- **Trends:** the lines of C# in `src/` and `tests/`, and the test-time
  total in the budget issue.
- **What waits on the owner:** the owner searches and the playtest list.

Then:

1. Fix label mistakes that the rules settle, each with a one-line signed
   comment. Release claims only as CONTRIBUTING permits;
   take over another session's claim only when the owner asks, as AGENTS says.
2. File each real problem as its own Bug issue, as
   [find-bugs](../find-bugs/SKILL.md) says. A security problem never goes in
   the log or any public text: follow CONTRIBUTING's security rule.
3. Add one comment to the open issue titled `Daily repo audit`: the date,
   main's commit, the numbers above, what you fixed, what you filed and what
   you propose. If that issue doesn't exist, open it with `type:tooling`,
   `area:tooling` and `priority:p3`.
4. Never change rules, skills or workflows during the audit. Write the change
   you propose in the comment and the summary. The steward makes the pull
   request once the owner agrees.
5. End with a short summary for the owner: the three things that matter most,
   and anything they need to decide.

## Environment notes

- **The repository is ClankerWorldOrg/ClankerWorld.** The old address,
  `compoodment/ClankerWorld`, still forwards to it. Some cloud sessions are
  attached under the old name and must use it with their GitHub tools.
  Teleporting such a session to a local checkout needs a clone made with the
  old address.
- **Use the owner's own GitHub account for this repository.** Check which
  account `gh` and git use before you push or comment on a machine that has
  several signed in.
- **Some GitHub tools replace an issue's whole label set when you set labels.**
  Read the current labels first and write them all back with your change.
- **Screenshots for showing the game:** build the client, render the world the
  profile check generates under `xvfb-run` with the OpenGL renderer, and save
  viewport images. Keep the capture script out of the repository.

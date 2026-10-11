---
name: find-bugs
description: Find problems in ClankerWorld, in the game or in code and tests, confirm each one on current main and file it as its own Bug issue with the right labels, or report a security problem privately to the owner. Use when your job is Find bugs, when asked to look for bugs, test a feature for problems or audit an area of the game.
---

# Find bugs

These are the rules for the Find bugs job. They bind like
[CONTRIBUTING](../../CONTRIBUTING.md), which holds the shared rules for issues,
labels and priorities. [AGENTS](../../AGENTS.md) adds signing and asking the
owner.

Your job ends when each problem you found has its own Bug issue, or, for a
security problem, the owner has it in chat and its placeholder issue is open.
Fix a problem only if you are asked to; filing it is the job.

## Hunt in normal play

Look for problems a player would meet. Play ordinary new worlds from the menu,
or run seeded worlds through the normal private-world path, and watch what the
agents actually do. A hand-built setup is fine for confirming and repeating a
problem you saw, but not for inventing one. A problem that only a setup a
normal game cannot produce can trigger, such as goods placed where no agent
could put them or a state only a test fixture can create, is P3
([Priorities](../../CONTRIBUTING.md#priorities)). In the issue, say how a
normal game reaches it.

## Confirm it on current main

- Fetch main and reproduce the problem there first, in the game or with a test.
  A problem you saw only on an older build or a branch may already be fixed.
- Say how you saw it: in the game, or in code or tests. A problem found by
  reading code is real when you can point to the input that breaks it; a
  failing test that shows it is the strongest evidence.
- Search open and closed issues and open pull requests by topic, not only by
  number, and by cause: the function or code path at fault. If a closed issue's fix did not work, reopen it with the new
  evidence. If an open issue or pull request already covers it, add what you
  found there instead of filing again.

## File one issue per problem

- Use the **Bug** template's sections: what happened, what you expected, how to
  repeat it and the build (the main commit you tested). An issue filed through
  the API or your GitHub tools gets no labels, so add them yourself: `type:bug`,
  one or two areas, a priority and `status:needs-pr` when nothing is left to
  decide ([Labels](../../CONTRIBUTING.md#labels)).
- Set the priority by the [bug rules](../../CONTRIBUTING.md#priorities),
  including the P0 and P1 caps, and add `regression` to something that used
  to work.
- While a release is being prepared, give a new P0 or P1 bug its label, now
  `release:alpha-1` ([Release scope](../../CONTRIBUTING.md#release-scope)).
- Give the title in ordinary words about the effect, such as "Returning a jug
  can halt the world". Keep one problem per issue. When several symptoms
  come from the same cause, so one fix covers them all, file one issue that
  lists each symptom, or add a new symptom to the open issue for that cause.
  Link related problems that need separate fixes instead of combining them.
- If fixing it would settle an open game choice, it is a Decision, not a Bug:
  file a Decision issue and ask the owner
  ([how](../design-decisions/SKILL.md)).
- Tell the owner in chat about every P0 you file.

## Security problems

Follow CONTRIBUTING's [security rule](../../CONTRIBUTING.md#priorities): the
repository is public, so tell the owner in chat what you found, including how
to reproduce it ([how](../../AGENTS.md#ask-the-owner-in-chat)), and open only
the placeholder issue it describes, with no details. Sign the placeholder with
your session ID and the time you told the owner, so the owner can find your
report; a placeholder nobody can trace can't be acted on.

## Report

List the issues you filed, highest priority first, with how you confirmed each
one, and anything you suspect but could not confirm.

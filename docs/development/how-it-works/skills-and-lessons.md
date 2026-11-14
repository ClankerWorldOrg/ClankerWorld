---
title: Skills and practical lessons
type: architecture
status: active
updated: 2026-11-14
---

# Skills and practical lessons

This page describes learned skills and practical lessons.

## Skills and practical lessons

Agents start with no skills, including added adults and newborn children.
Finishing construction first records building; finishing a Farmhouse recipe
records farming, a Blacksmith recipe records smithing, and other production
records crafting. The record keeps the first learning time. Learning by work
has no teacher; completing an accepted lesson records the teacher's agent ID.

Lesson candidates use saved skills and the existing food/warmth readiness
rules, independently of work roles. A mentor cannot be working on an active
project, carrying out an operative Must Do order, handling another social
decision, or reserved for another lesson. Cancelling or finishing the order
allows lesson requests and replies again; a pending request keeps its normal
consent and expiry rules. Suggestion guidance does not reserve the teacher.
Both participants must be able to reach the common lesson site when a request
is offered or executed and when the teacher accepts. Mentor selection keeps
its stable identity order but skips adults without a physical route, so an
isolated teacher cannot hide another available teacher. Ordinary route checks
include occupied tiles; they do not grant travel through disconnected land.
Request, refusal, acceptance, cancellation and pause/reload retain their normal
flow. A completed lesson changes neither the agent's role nor work proficiency.
Its gratitude memory keeps the complete learner and teacher references, skill,
text and learning time. New memory keys longer than 128 characters use a SHA-256
digest of the complete key after the `lesson-gratitude:` prefix; shorter keys
stay unchanged. The prefix still supplies durable trust and protects the memory
from ordinary archival. Personal recall keeps its canonical 128-character key
boundary and owner filter.
Matching skills use one provisional 20% work reduction. Building speeds ordinary
construction, paid Town projects and expansions; farming speeds field work and
Farmhouse production; smithing speeds Blacksmith production; crafting speeds
other production and personal equipment repairs. Fixed work timers round down
to whole ticks, with a minimum of one. Recipe timing applies the existing knife
bonus first. Field growth, travel, gathering and lessons keep their own timers.
Project progress stays in its existing full-work units: skilled progress maps
those units back to elapsed work with a ceiling, adds the current tool/practice
work, then maps forward at the reduced rate. Finished projects therefore keep
the same full progress and exact order/payment receipts. Shared Town work
credits the acting worker's skill, without reducing the whole project's budget.
Ordinary action access still uses its existing recipe, tool and ownership rules.
The agent card and Event Log describe the record. Each ordinary action request includes
only its actor's sorted learned skill names in self context; that context also
contributes to the observation digest.
The owner observation keeps its existing JSON `role` field for the lesson's
skill name so older clients can still display it; new code calls it `Skill`.
This is separate from the saved lesson record, which now stores a skill.

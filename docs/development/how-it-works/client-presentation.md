---
title: Client presentation
type: architecture
status: active
updated: 2026-11-14
---

# Client presentation

This page describes client roster presentation and panel layout.

## Client roster presentation

The Godot client compares the sorted, non-draft Agents list presentation on
each accepted observation: IDs, names, living/deceased state, readable activity,
fullness label, Cold/Ill tags and portrait life stage. Unchanged presentation
retains both the selection text list and native cards and portrait textures,
including while the panel is hidden. Selection changes update the existing
rows separately, preserving browsing position unless another agent is selected.

Actual presentation changes refresh immediately, so opening a hidden list shows
the latest accepted information. World identity, a theme change or resetting the
displayed world invalidates reuse. This cache belongs only to client presentation;
it changes no observation cadence, simulation state, saved data or history.

## Client panel layout

Resize-driven text and Profile fitting is coalesced in the next process frame.
Changing a text panel's height can change its wrapping and the Profile's scroll
area; those notifications must not schedule another fit in the same idle flush.
Profile activity, orders, details, People notes and messages share one outer
scroll. `AddProfileText` gives text its container's width and wrapped height;
People stacks rows vertically instead of flowing independently sized labels.
The history layout check renders long trade and message histories at 1280×720
and 1920×1080, checks text widths and section bounds, and reaches the controls
and final message through the scroll.
The fresh-process Profile check opens every agent in a generated, three-day
hard world, including a born child, and measures both the button handler and
the following layout frames. The normal UI smoke runs separately so its earlier
profile opens cannot hide a first-open stall.
The real first Profile open in a rendered game logs `agent_profile_open` at `begin`,
`controls_ready` and `first_draw`, with elapsed times and no agent data, so a
platform-specific stall can be separated into control setup and first drawing.

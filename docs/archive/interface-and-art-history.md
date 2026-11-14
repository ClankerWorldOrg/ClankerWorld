---
title: Interface and art history
type: game-design
status: history
updated: 2026-11-14
---

# Interface and art history

This page records superseded interface choices.

## September 30 UI Scale

- **Agreed after the September 30 playtest:** UI Scale magnifies the whole
  interface in whole-number steps (100%, 200%, 300%, 400%) instead of
  enlarging text alone. Panels, padding, buttons, icons, confirmations,
  drop-down lists, tooltips and names on the map grow together, while the map
  keeps its own zoom. Top-bar panels open under the button that opened them,
  except the Event Log, which sits at the right edge of the screen, and the
  mouse wheel over a panel never zooms the map behind it. The current
  default, **Automatic**, keeps the interface about 720 pixels tall: 200% at
  1080p and 1440p and 300% at 4K. computment found 100% far too small at
  1440p. A step that would leave less than 960 × 540 interface pixels is
  unavailable, and on a narrow layout the top bar shows icons only.

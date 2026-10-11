# Live action refusals

Windows hands-on check after merge; pending.

- [ ] Open a newly created world, enable Developer mode and press F12. The
  readout **Refused actions** starts at zero and remains readable at 720p.
- [ ] During normal play, if an action is refused, check that time keeps moving,
  the Event Log names the agent and explains that they waited because of a game
  error, and their current order shows its cancellation reason. Record the
  world and action so the underlying bug can be fixed.
- [ ] Save, quit and reload that world. The refusal remains in its Event Log
  and the counter keeps its value. A different world has its own counter.

No error injection control is added to the player UI. Automated native controls
exercise the refused-action and checkpoint-failure boundaries separately.

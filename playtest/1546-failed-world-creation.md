- On a disposable Windows world, pause and preview a new Small world. Make only
  the active checkpoint path unwritable while keeping its world catalog writable.
  Try creating the world: check that creation reports failure, Load World has no
  extra entry, and the previous world and settings remain selected.
  ([#1546](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1546))
- Restore write access and retry the same preview. Check that exactly one new
  world appears and opens paused; save and reopen it, then reopen the old world.
  ([#1546](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1546))

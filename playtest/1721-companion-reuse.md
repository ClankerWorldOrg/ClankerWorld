# Reuse the game's running world server

- Build: pending this PR's merge and Windows package.
- Status: pending routine Windows playtest.

1. Start the portable game and let its world server open a world.
2. Relaunch the same game build while that server remains running. Confirm the
   game reconnects, the world stays available, and only one world server runs.
3. Repeat while the PC is busy. A brief delay should allow the game to connect.
   Confirm it reconnects without reporting that another program owns the port
   and without starting a second server.
4. Try a different game build against the running server. Confirm it refuses
   the incompatible server and leaves the original world running.

Automated real-host tests cover timed-out discovery/proof requests, same-build
reuse, secret refusal, cancellation, timeout, pairing and shutdown. This file
records the separate Windows check; it has not been performed.

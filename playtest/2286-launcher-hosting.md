# Launcher hosting

Pending Windows hands-on checks for #2286. Use a disposable world, matching
game/server builds and two Windows devices on a private Tailnet. Record the
Windows versions, exact builds, date and screen sizes. Keep comparison codes,
keys, private saves and model replies out of reports.

- [ ] Set up Tailscale and HTTPS, select an installed version, start hosting
  and use its address to join from the other computer. Compare codes and
  approve; verify signed play works. Decline a different request and verify
  it cannot activate.
- [ ] Quit the playing game. Confirm the world is saved and paused and the
  paid-attempt count stays unchanged. Reconnect to that same world.
- [ ] Close the host window; it minimizes and hosting remains available.
  Stop hosting from its screen and confirm the private Serve route stops.
  Local play afterwards must keep the same worlds, keys and usage total.
- [ ] Enable stop-on-quit before starting. Quit the last connected game,
  confirm hosting stops, and start it again from the host screen.
- [ ] Restart Windows and sign in. With automatic restart on, hosting resumes
  the saved version and data folder minimized. Turn it off and repeat;
  hosting must stay stopped. Repeat with Developer test data and confirm
  usual worlds and usage are untouched.
- [ ] With another Serve setup or public Funnel already configured, starting
  hosting refuses without replacing that configuration. No approval listener
  is exposed. If disk space prevents saving, Stop hosting leaves the server
  paused and available for retry; after freeing space it saves and stops.

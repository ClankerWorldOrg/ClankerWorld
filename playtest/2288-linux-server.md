# Linux server hosting

Pending hands-on checks for #2288. Use a disposable world and matching Linux
server and Windows game from the same commit. Record the OS, builds and dates;
keep keys, comparison codes, private saves and model replies out of reports.

- [ ] On a fresh systemd Linux x64 machine, follow `docs/hosting-linux.md` to
  verify the archive and install the service; no .NET SDK is needed.
- [ ] Join through private Tailscale Serve HTTPS. Run the approval command
  on the host, enter the Windows comparison code privately, then create and play
  a disposable world. Later launches reconnect without pairing again.
- [ ] Quit the game, confirm the saved world stops advancing and paid attempts
  stay unchanged, then reconnect to the same world.
- [ ] Restart the host and reconnect. Stop the service and check the launcher's
  conditional `sudo systemctl start clankerworld-server.service` hint.
- [ ] Disable automatic startup and reboot; confirm the service stays stopped.
  Re-enable it afterwards. Rerunning the installer refuses and preserves state.

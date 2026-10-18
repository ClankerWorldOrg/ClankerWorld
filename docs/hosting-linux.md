---
title: Host a world on Linux
type: player-guide
status: active
updated: 2026-10-18
---

# Host a world on Linux

Use a spare Linux x64 machine or VPS to keep your world available while your
Windows computer is off. The server saves and waits when nobody is playing;
it does not advance the world or call models without a connected game.
You need a terminal, administrator access and systemd. The download includes
its own .NET runtime. It contains no Linux desktop game.
Use an x64 distribution supported by [.NET 10 on Linux](https://learn.microsoft.com/dotnet/core/install/linux),
such as Ubuntu 24.04, with that distribution's native .NET dependencies installed.
You do not need the .NET SDK or managed runtime package.

Keep this server on a private network. The steps below use Tailscale Serve
for trusted HTTPS inside your own Tailnet. Do not use Tailscale Funnel, expose
either server port to the internet, or forward the approval port. Network
access alone does not approve a device.

## Download and install

1. Install Tailscale on the host and Windows computer, join your private
   Tailnet, and enable HTTPS certificates for it. Follow
   [Tailscale's Serve guide](https://tailscale.com/kb/1312/serve) for that setup.
2. Download `ClankerWorld-Server-<version>-linux-x64.tar.gz` and its `.sha256`
   from the same [ClankerWorld release](https://github.com/ClankerWorldOrg/ClankerWorld/releases)
   as your Windows game. In the download folder, check the archive, replacing
   `<version>` with the downloaded version:

   ```bash
   sha256sum --check ClankerWorld-Server-<version>-linux-x64.tar.gz.sha256
   tar -xzf ClankerWorld-Server-<version>-linux-x64.tar.gz
   cd ClankerWorld-Server-<version>-linux-x64
   sudo bash install-server.sh
   ```

   The install command requires `curl`, GNU core utilities, `runuser` and
   `useradd`, normally available on systemd Linux distributions. It is for a
   fresh installation: it refuses existing files or a `clanker` account,
   preserving any existing world. Do not delete those files to bypass a refusal.
3. Forward only the ordinary loopback listener into your private Tailnet:

   ```bash
   sudo tailscale serve --bg http://127.0.0.1:5188
   sudo systemctl status clankerworld-server.service
   ```

   Keep the HTTPS address Tailscale reports for the joining computer. The
   approval listener at `127.0.0.1:5189` must stay on the Linux machine.

## Approve and play

Start joining from the Windows launcher with that HTTPS address. On the Linux
host, run:

```bash
sudo clankerworld-server approve
```

Enter the six-digit comparison code shown on the joining computer. Input is
hidden and the command keeps it out of command arguments and shell history.
Approve only the device you are joining. If pairing expires, start it again.
If two requests share a code, neither is approved; start pairing again.
The device then proves ownership of its own key before it can play.

Choose models and keys in the game on Windows. Keys stay in this installation's
private storage, outside world saves. Do not put them in a terminal command,
service unit, report or issue.

## Stop, restart and update

The service starts again after a reboot. Quitting the connected game saves and
pauses the world; the service waits for the next session. To stop it yourself:

```bash
sudo systemctl stop clankerworld-server.service
```

Start it again with `sudo systemctl start clankerworld-server.service`.
To turn off starting after reboot, use
`sudo systemctl disable clankerworld-server.service`; `enable` turns it on again.
A connection failure can also mean a network problem: check Tailscale and the
service before assuming the host stopped.

Programs live in `/opt/clankerworld-server`. Worlds, device authority, keys
and usage accounting live in `/var/lib/clankerworld-server`, private to the
`clanker` account. The approval command reads the account-private proof there
without showing it. Preserve this entire directory. Settings live in
`/etc/clankerworld-server`; neither folder belongs inside the download.

Updates are manual during the alpha. Follow the
[remote-server update checklist](development/remote-server-deployment.md)
to pause, stop and back up the installation before switching binaries. The
installer deliberately refuses to update an existing installation. During
alpha an older world may need its matching game version; a refused save must
be kept, never deleted to make an update start.

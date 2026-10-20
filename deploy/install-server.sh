#!/usr/bin/env bash
set -euo pipefail

# Fresh installations only. Existing worlds use the documented paused update path.
if [[ $# != 0 || "$(id -u)" != 0 ]]; then
    printf 'Run sudo bash install-server.sh on a fresh Linux x64 system with systemd.\n' >&2
    exit 1
fi
for command in systemctl useradd getent install cp sha256sum curl runuser; do
    command -v "${command}" >/dev/null
done
[[ "$(uname -sm)" == 'Linux x86_64' ]]
for path in /opt/clankerworld-server /var/lib/clankerworld-server /etc/clankerworld-server \
    /etc/systemd/system/clankerworld-server.service /usr/local/bin/clankerworld-server; do
    if [[ -e "${path}" || -L "${path}" ]]; then
        printf 'An installation already exists at %s. Nothing was replaced; use the update guide.\n' "${path}" >&2
        exit 1
    fi
done
if getent passwd clanker >/dev/null || getent group clanker >/dev/null; then
    printf 'The clanker account already exists. Nothing was replaced; use the update guide.\n' >&2
    exit 1
fi
package="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
(cd "${package}" && sha256sum --check --quiet --strict manifest.sha256)
[[ -x "${package}/ClankerWorld.Server" ]]
useradd --system --user-group --home-dir /var/lib/clankerworld-server --no-create-home --shell /usr/sbin/nologin clanker
install -d -m 0755 /opt/clankerworld-server /etc/clankerworld-server
cp -a -- "${package}/." /opt/clankerworld-server/
chown -R root:root /opt/clankerworld-server
chmod -R go-w /opt/clankerworld-server
install -d -m 0700 -o clanker -g clanker /var/lib/clankerworld-server
install -m 0644 "${package}/clankerworld-server.service" /etc/systemd/system/clankerworld-server.service
install -m 0755 "${package}/clankerworld-server" /usr/local/bin/clankerworld-server
systemctl daemon-reload
systemctl enable --now clankerworld-server.service
printf 'Installed. Configure private HTTPS access as the Linux hosting guide describes, then pair your device.\n'

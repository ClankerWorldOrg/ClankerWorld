#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_dir="${1:-${repo_root}/export/linux-server}"
[[ "${output_dir}" == /* ]] || output_dir="${repo_root}/${output_dir}"
revision="$(git -C "${repo_root}" rev-parse HEAD)"
scratch="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/clankerworld-linux-server.XXXXXX")"
trap 'rm -rf -- "${scratch}"' EXIT
mkdir -p "${scratch}/source"
git -C "${repo_root}" archive "${revision}" global.json Directory.Build.props \
    src/ClankerWorld.Server src/ClankerWorld.Simulation src/ClankerWorld.Shared deploy docs/hosting-linux.md |
    tar -C "${scratch}/source" -xf -
project="${scratch}/source/src/ClankerWorld.Server/ClankerWorld.Server.csproj"
version="$(dotnet msbuild "${project}" -nologo -getProperty:Version)"
[[ "${version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]
name="ClankerWorld-Server-${version}-linux-x64"
stage="${scratch}/${name}"
SourceRevisionId="${revision}" dotnet publish "${project}" -c Release -r linux-x64 \
    --self-contained -p:RestoreLockedMode=true --output "${stage}" --nologo
[[ -x "${stage}/ClankerWorld.Server" ]]
file "${stage}/ClankerWorld.Server" | grep -q 'ELF 64-bit.*x86-64'
grep -a -F -q -- "${version}+${revision}" "${stage}/ClankerWorld.Server.dll"
install -m 0755 "${scratch}/source/deploy/install-server.sh" "${stage}/install-server.sh"
install -m 0755 "${scratch}/source/deploy/clankerworld-server" "${stage}/clankerworld-server"
install -m 0644 "${scratch}/source/deploy/clankerworld-server.service" "${stage}/clankerworld-server.service"
cp "${scratch}/source/docs/hosting-linux.md" "${stage}/README.md"
(
    cd "${stage}"
    printf '# %s\n# Source commit: %s\n' "${name}" "${revision}" > manifest.sha256
    find . -type f ! -name manifest.sha256 -print0 | sort -z | xargs -0 sha256sum --binary >> manifest.sha256
    sha256sum --check --quiet --strict manifest.sha256
)
mkdir -p "${output_dir}"
tar -C "${scratch}" -czf "${output_dir}/${name}.tar.gz" "${name}"
(cd "${output_dir}" && sha256sum --binary "${name}.tar.gz" > "${name}.tar.gz.sha256")
printf 'Linux x64 server package ready: %s/%s.tar.gz\n' "${output_dir}" "${name}"

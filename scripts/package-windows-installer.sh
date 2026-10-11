#!/usr/bin/env bash
set -euo pipefail

# Build an unsigned per-user setup from same-commit verified portable packages.
# Run in Windows Git Bash after package-windows.sh (game and --launcher).
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${repo_root}/scripts/tool-cache.sh"
readonly INNO_VERSION="6.5.4"
readonly INNO_SHA256="fa73bf47a4da250d185d07561c2bfda387e5e20db77e4570004cf6a133cc10b1"
readonly INNO_URL="https://github.com/jrsoftware/issrc/releases/download/is-6_5_4/innosetup-6.5.4.exe"

if ! command -v cygpath >/dev/null || [[ "$(uname -s)" != MINGW* ]]; then
    printf 'Build the setup in Windows Git Bash. Native Windows CI runs this script.\n' >&2
    exit 1
fi
game_package="${1:?Pass the portable game zip}"
launcher_package="${2:?Pass the launcher zip}"
output_dir="${3:-${repo_root}/export/setup}"
for path_var in game_package launcher_package output_dir; do
    printf -v "${path_var}" '%s' "$(cygpath -u "${!path_var}")"
    if [[ "${!path_var}" != /* ]]; then printf -v "${path_var}" '%s/%s' "${repo_root}" "${!path_var}"; fi
done
revision="$(git -C "${repo_root}" rev-parse HEAD)"
launcher_version="$(git -C "${repo_root}" show "${revision}:src/ClankerWorld.GodotClient/Launcher/Launcher.cs" |
    sed -n 's/^ *public const string Version = "\([^"]*\)";$/\1/p')"
game_name="$(basename "${game_package}" .zip)"
game_version="${game_name#ClankerWorld-}"
game_version="${game_version%-windows-x64}"
for version in "${launcher_version}" "${game_version}"; do
    if [[ ! "${version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
        printf 'Invalid package version: %s\n' "${version}" >&2
        exit 1
    fi
done
launcher_name="ClankerWorld-Launcher-${launcher_version}-windows-x64"
if [[ "$(basename "${launcher_package}")" != "${launcher_name}.zip" || "${game_name}" != "ClankerWorld-${game_version}-windows-x64" ]]; then
    printf 'Package names do not match the committed launcher and game versions.\n' >&2
    exit 1
fi
for package in "${game_package}" "${launcher_package}"; do
    checksum="$(cat "${package}.sha256")"
    checksum="${checksum%$'\r'}"
    if [[ ! "${checksum}" =~ ^([[:xdigit:]]{64})[[:space:]][*[:space:]](.+)$ || "${BASH_REMATCH[2]}" != "${package##*/}" ]]; then
        printf 'The package checksum does not name %s.\n' "${package##*/}" >&2
        exit 1
    fi
    (cd "${package%/*}" && sha256sum --check --quiet --strict "${package##*/}.sha256")
done

setup_temp_root="$(cygpath -u "${RUNNER_TEMP:-${TMPDIR:-/tmp}}")"
scratch_root="$(mktemp -d "${setup_temp_root}/clankerworld-setup.XXXXXX")"
trap 'rm -rf -- "${scratch_root}"' EXIT
python - "${game_package}" "${launcher_package}" "${scratch_root}" <<'PY'
import pathlib, sys, zipfile
root = pathlib.Path(sys.argv[3])
for kind, package in zip(("game", "launcher"), sys.argv[1:3]):
    expected = pathlib.Path(package).stem
    destination = root / kind
    with zipfile.ZipFile(package) as archive:
        for entry in archive.infolist():
            parts = entry.filename.replace("\\", "/").split("/")
            if parts[0] != expected or any(part in ("..", ".") or ":" in part for part in parts):
                raise ValueError("Unsafe or unexpected package path: " + entry.filename)
        archive.extractall(destination)
PY
game_files="${scratch_root}/game/${game_name}"
launcher_files="${scratch_root}/launcher/${launcher_name}"
for files in "${game_files}" "${launcher_files}"; do
    if ! grep -q -x -F "# Source commit: ${revision}" "${files}/manifest.sha256"; then
        printf 'A package was not built from %s.\n' "${revision}" >&2
        exit 1
    fi
    (cd "${files}" && sha256sum --check --quiet --strict manifest.sha256)
done
test -f "${game_files}/ClankerWorld.exe"
test -f "${game_files}/host/ClankerWorld.Viewer.exe"
test -f "${launcher_files}/ClankerWorldLauncher.exe"
staged_repo_root="${scratch_root}/repository"
mkdir -p "${staged_repo_root}"
git -C "${repo_root}" archive "${revision}" scripts/windows-installer.iss src/ClankerWorld.GodotClient/icon.ico |
    tar -C "${staged_repo_root}" -xf -

compiler_dir="$(mktemp -d "${setup_temp_root}/clankerworld-inno.XXXXXX")"
compiler_install="$(tool_cache_download "${INNO_URL}" "${INNO_SHA256}" "innosetup-${INNO_VERSION}.exe")"
chmod u+x "${compiler_install}"
# Inno's documented portable mode creates no tool shortcuts or uninstaller.
MSYS2_ARG_CONV_EXCL='*' "${compiler_install}" /PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- \
    "/DIR=$(cygpath -w "${compiler_dir}")"
compiler="${compiler_dir}/ISCC.exe"
test -f "${compiler}"
mkdir -p "${output_dir}"
MSYS2_ARG_CONV_EXCL='*' "${compiler}" "/O$(cygpath -w "${output_dir}")" \
    "/DLauncherVersion=${launcher_version}" "/DLauncherFileVersion=${launcher_version%%-*}.0" \
    "/DGameVersion=${game_version}" "/DSourceRevision=${revision}" \
    "/DLauncherFiles=$(cygpath -w "${launcher_files}")" "/DGameFiles=$(cygpath -w "${game_files}")" \
    "$(cygpath -w "${staged_repo_root}/scripts/windows-installer.iss")"
setup_name="ClankerWorld-Setup-${game_version}-windows-x64.exe"
(cd "${output_dir}" && sha256sum --binary "${setup_name}" > "${setup_name}.sha256")
python - "${output_dir}" "${revision}" "${game_version}" "${launcher_version}" "${INNO_SHA256}" <<'PY'
import json, pathlib, sys
path = pathlib.Path(sys.argv[1]) / "build.json"
path.write_text(json.dumps(dict(sourceRevision=sys.argv[2], gameVersion=sys.argv[3],
    launcherVersion=sys.argv[4], compilerVersion="6.5.4", compilerSha256=sys.argv[5]), indent=2) + "\n")
PY
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    printf 'compiler=%s\n' "$(cygpath -w "${compiler}")" >> "${GITHUB_OUTPUT}"
fi
printf 'Setup ready: %s\n' "${output_dir}/${setup_name}"

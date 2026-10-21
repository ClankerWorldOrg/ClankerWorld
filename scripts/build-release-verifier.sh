#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${repo_root}/scripts/tool-cache.sh"
readonly GO_VERSION="1.27.2"
case "$(uname -s)" in
    Linux*) platform=linux-amd64; extension=tar.gz; checksum=ecbadb99091a3f46e31f5f934b068b1864eafa7995211b39eaddf76996045fe5 ;;
    MINGW*|MSYS*) platform=windows-amd64; extension=zip; checksum=1314008898bd40df77af4b014f777f08873dbdfbcd3d92308728ee03304fe04f ;;
    *) printf 'Build the release verifier on Linux x64 or Windows x64.\n' >&2; exit 1 ;;
esac
archive="$(tool_cache_download "https://go.dev/dl/go${GO_VERSION}.${platform}.${extension}" "${checksum}" "go${GO_VERSION}.${platform}.${extension}")"
runtime="$(tool_cache_unzip "${archive}" "${checksum}")"
output="${1:?Pass the verifier output file}"
target="${2:-windows}"
[[ "${target}" == windows || "${target}" == linux ]] || exit 1
if [[ "${output}" != /* ]]; then output="${PWD}/${output}"; fi
mkdir -p "${output%/*}"
scratch="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/clankerworld-verifier.XXXXXX")"
trap 'rm -rf -- "${scratch}"' EXIT
revision="$(git -C "${repo_root}" rev-parse HEAD)"
git -C "${repo_root}" archive "${revision}" tools/release-verifier | tar -C "${scratch}" -xf -
export GOTOOLCHAIN=local CGO_ENABLED=0 GOOS="${target}" GOARCH=amd64
if [[ "${target}" == linux ]]; then
    "${runtime}/go/bin/go" -C "${scratch}/tools/release-verifier" test -mod=readonly ./...
fi
"${runtime}/go/bin/go" -C "${scratch}/tools/release-verifier" build -mod=readonly -trimpath -ldflags='-s -w' -o "${output}" .
licenses="${output}.licenses"
mkdir -p "${licenses}"
cp "${runtime}/go/LICENSE" "${licenses}/Go-LICENSE"
"${runtime}/go/bin/go" -C "${scratch}/tools/release-verifier" list -mod=readonly -deps \
    -f '{{if .Module}}{{if not .Module.Main}}{{.Module.Path}}{{"\t"}}{{.Module.Dir}}{{end}}{{end}}' . > "${scratch}/modules"
sort -u "${scratch}/modules" > "${scratch}/unique-modules"
while IFS=$'\t' read -r module directory; do
    [[ -n "${module}" ]] || continue
    if command -v cygpath >/dev/null; then directory="$(cygpath -u "${directory}")"; fi
    destination="${licenses}/${module//\//_}"
    mkdir -p "${destination}"
    found=false
    for notice in "${directory}"/LICENSE* "${directory}"/LICENCE* "${directory}"/COPYING* "${directory}"/NOTICE*; do
        [[ -f "${notice}" ]] || continue
        cp "${notice}" "${destination}/"
        found=true
    done
    if [[ "${found}" != true ]]; then printf 'Missing third-party license for %s\n' "${module}" >&2; exit 1; fi
done < "${scratch}/unique-modules"

#!/usr/bin/env bash
# Compile one unchanged public-API probe against both checkouts.
set -euo pipefail
if [[ $# != 3 ]]; then
    printf 'Usage: bash docs/development/shared-read-equivalence/run.sh BASE CANDIDATE OUTPUT\n' >&2
    exit 2
fi
script_root=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
base_root=$(cd "$1" && pwd)
candidate_root=$(cd "$2" && pwd)
mkdir -p "$3"
output_root=$(cd "$3" && pwd)
if [[ -e "${output_root}/base" || -e "${output_root}/candidate" ]]; then
    printf 'Use a fresh output directory.\n' >&2
    exit 2
fi
probe_root=$(mktemp -d)
trap 'rm -rf "${probe_root}"' EXIT
cp "${script_root}/Probe.cs" "${output_root}/probe.cs"
for variant in base candidate; do
    target_root=${base_root}
    if [[ "${variant}" == candidate ]]; then target_root=${candidate_root}; fi
    project_root="${probe_root}/${variant}"
    (
        cd "${target_root}"
        dotnet new console --name SharedReadEquivalence --output "${project_root}" --framework net10.0 --no-restore >/dev/null
        dotnet add "${project_root}/SharedReadEquivalence.csproj" reference "${target_root}/src/ClankerWorld.Simulation/ClankerWorld.Simulation.csproj" >/dev/null
        cp "${output_root}/probe.cs" "${project_root}/Program.cs"
        dotnet run --project "${project_root}/SharedReadEquivalence.csproj" --configuration Release -- \
            "${output_root}/${variant}" "$(git rev-parse HEAD)" "$(git status --porcelain)"
    ) >"${output_root}/${variant}.log" 2>&1 || { cat "${output_root}/${variant}.log" >&2; exit 2; }
done
python3 "${script_root}/../../../scripts/compare-tick-equivalence.py" "${output_root}/base" "${output_root}/candidate"

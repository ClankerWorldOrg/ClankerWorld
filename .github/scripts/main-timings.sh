#!/usr/bin/env bash
# Downloads the test timings of main's latest green Verify runs, one folder per run, for
# ci-plan.js to read with readMainTimings. These are the merge queue's runs: each tests main with
# the pull requests ahead of it, and main then moves to that very commit, so pushes to main skip
# the tests the queue already ran. A successful queue retry may never have merged, so
# accept only commits on main, pinned once through the API (checkouts may be shallow):
#   bash .github/scripts/main-timings.sh <dir> <count>
# Needs GH_TOKEN with actions: read and GITHUB_REPOSITORY. A run whose timings can't be
# downloaded, for example because they have expired, is left out.
set -euo pipefail

dir="$1"
count="$2"
mkdir -p "$dir"
main_sha="$(gh api "repos/${GITHUB_REPOSITORY}/commits/main" --jq '.sha')"
selected=0
page=1
while (( selected < count )); do
  runs="$(gh api "repos/${GITHUB_REPOSITORY}/actions/workflows/ci.yml/runs?event=merge_group&status=success&per_page=100&page=${page}" \
    --jq '.workflow_runs[] | [.id, .head_sha] | @tsv')"
  [[ -n "$runs" ]] || break
  while IFS=$'\t' read -r run_id head_sha; do
    status="$(gh api "repos/${GITHUB_REPOSITORY}/compare/${main_sha}...${head_sha}" --jq '.status')"
    if [[ "$status" != behind && "$status" != identical ]]; then
      echo "Skipping run ${run_id}: ${head_sha} is not on main ${main_sha} (${status})"
      continue
    fi
    echo "Selected run ${run_id}: ${head_sha} is on main ${main_sha}"
    (
      gh run download "$run_id" --repo "$GITHUB_REPOSITORY" --pattern 'test-timings-*' --dir "$dir/$run_id" \
        && echo "Timings from run ${run_id}" \
        || { echo "Could not download the timings of run ${run_id}"; rm -rf -- "${dir:?}/${run_id}"; }
    ) &
    selected=$((selected + 1))
    (( selected < count )) || break
  done <<< "$runs"
  page=$((page + 1))
done
wait

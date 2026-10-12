#!/usr/bin/env bash
# Stage 6 of the #1364 rename: delete the old persistence spike that nothing
# uses since #1738 removed the fixture world. Runs after camp.sh.
set -euo pipefail

spike=src/ClankerWorld.Simulation/Persistence
if [ ! -d "$spike" ]; then
  echo "dead-code: $spike is already gone"
  exit 0
fi

# Refuse if anything outside the folder still uses one of its types.
types='CanonicalPersistenceCodec|PersistenceContracts|MiniatureWorldState|PersistedWorld|SaveCompatibility|PersistenceEvent|PersistenceEventKind|WorldReplay|WorldIdentity'
users=$(git grep -lwE "$types" -- src tests scripts | grep -v "^$spike/" || true)
# The game client has its own unrelated WorldSnapshot; WorldIdentity is checked
# by namespace below, so only report files that also import the spike.
real=""
for f in $users; do
  if grep -q 'ClankerWorld\.Simulation\.Persistence' "$f"; then real="$real $f"; fi
done
if [ -n "$real" ]; then
  echo "dead-code: still used by:$real" >&2
  exit 1
fi

git rm -rq "$spike"

# Drop the now-empty imports.
for f in $(git grep -lE '^using ClankerWorld\.Simulation\.Persistence;' -- src tests || true); do
  perl -0pi -e 's/^using ClankerWorld\.Simulation\.Persistence;\r?\n//mg' "$f"
done

# Drop its label path.
perl -0pi -e "s/^    'src\/ClankerWorld\.Simulation\/Persistence\/',\r?\n//mg" .github/scripts/pr-labels.js

if git grep -nE 'ClankerWorld\.Simulation\.Persistence|Simulation/Persistence' -- src tests .github docs skills; then
  echo "dead-code: references to the spike remain" >&2
  exit 1
fi
echo "dead-code: removed $spike"

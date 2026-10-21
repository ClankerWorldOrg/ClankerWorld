import json, sys
from pathlib import Path
sys.path.insert(0, str(Path("skills/test-audit/scripts").resolve()))
from unique_coverage import Universe, Coverage, read_report
from per_test_coverage import completed_report, slug
base = Path("/workspace/ClankerWorld-2560-base/.evidence/2560-refresh/conversations-per-test-before")
proof = Path(".evidence/2560-refresh/per-test-retained")
old_names = json.loads((base / "methods.json").read_text())
proof_names = json.loads((proof / "methods.json").read_text())
removed = set(Path(".evidence/2560-refresh/removed-methods.txt").read_text().splitlines())
universe, old, new = Universe(), {}, {}
for name in old_names:
    old[name] = read_report(completed_report(base / slug(name), name), universe)
    if name not in removed and name not in proof_names:
        new[name] = old[name]
for name in proof_names:
    new[name] = read_report(completed_report(proof / slug(name), name), universe)
before, after = Coverage(universe, old), Coverage(universe, new)
lost = [dict(assembly=key[0], file=key[1], line=key[2]) for index, key in enumerate(universe.keys) if before.counts[index] and not after.counts[index]]
reduced = [dict(assembly=key[0], file=key[1], line=key[2], before=max(before.branch_levels[index], default=0), after=max(after.branch_levels[index], default=0)) for index, key in enumerate(universe.keys) if max(before.branch_levels[index], default=0) > max(after.branch_levels[index], default=0)]
print(json.dumps(dict(before=before.totals(), after=after.totals(), lost_lines=lost, reduced_branch_lower_bounds=reduced, note="Mapped branch unions are lower bounds; full-suite collector totals remain authoritative."), indent=2))

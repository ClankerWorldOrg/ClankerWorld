---
title: Shared read-service equivalence
type: developer-guide
status: active
updated: 2026-10-11
---

# Check event and resident-service equivalence

The extraction in [#1380](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1380)
preserves event order, IDs, positions, resident queries and checkpoint bytes.
Use the general [tick comparison](../../../scripts/compare-tick-equivalence.sh)
and the additional [native probe](Probe.cs). The probe compiles unchanged against
both versions and uses their existing public APIs.

```bash
bash scripts/compare-tick-equivalence.sh /path/to/base /path/to/candidate .evidence/keep/1380/standard
bash docs/development/shared-read-equivalence/run.sh /path/to/base /path/to/candidate .evidence/keep/1380/services
```

Use fresh output directories. The service probe starts two generated Small
Towns through the normal four-founder flow and advances ten native ticks each.
It records complete ordered candidate lists alongside observation digests,
events and compressed canonical checkpoints. It checks strict reload at every
capture, refuses a proposed tick before every committed tick, and refuses
persistence of a real developer goods grant before retrying successfully.
A player rename then checks the live resident identity and position path.
No model service or external simulator is involved. The comparator checks
every checkpoint byte and every ordered candidate/event record.

The focused tests also check longest-prefix living/deceased event positions,
explicit positions, retained history IDs, read-only events and separate log
versions. A native-born resident grows to adulthood through the existing family
fixture; every ID in that Town is compared with a linear scan before and after
a player rename and strict reload. Cache verification recomputes on every hit
in the test assembly, including a control that detects mutation behind an
unchanged snapshot key. These are correctness checks; they make no whole-tick
speedup or hands-on Windows playtest claim.

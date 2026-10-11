# Equipment and ornament query comparison

Compile the unchanged [Probe.cs](Probe.cs) in an external .NET 10 console
project referencing each checkout's `ClankerWorld.Simulation.csproj`. Pass a
separate output directory for each checkout. Compare decompressed checkpoint
bytes and the events and complete observation/candidate JSON at every frame.

The probe runs twelve native scenarios: household equipment, a full-load
upgrade retaining the broken aid, personal equipment stack splitting, reserved
and borrowed equipment, a walk to equipment, household ornaments, personally
carried ornaments with full cargo, partially reserved ornament stock, full-load
and borrowed ornament refusal, and a walk to ornaments. Each captures three
frames, a refused tick with unchanged state, and strict checkpoint reloads.
Fresh personal decisions select the real offered actions. Refused property
checks allow only the inventory processing clock to advance.

Use `scripts/compare-tick-equivalence.sh` for the general generated/legacy
comparison. These feature fixtures add stock and positions before strict
restore; they exercise native candidates, admission, movement and inventory
actions. They are automated checks, not a graphical playtest.

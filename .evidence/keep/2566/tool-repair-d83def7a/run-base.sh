#!/usr/bin/env bash
set -euo pipefail
export PATH=/workspace/tools/dotnet:$PATH DOTNET_PROCESSOR_COUNT=4
rm tests/ClankerWorld.Tests/.editorconfig
python - <<'PIN'
from pathlib import Path
import shutil,json,hashlib
out=Path('.evidence/2566-tool-repair'); binary=Path('tests/ClankerWorld.Tests/bin/Release/net10.0'); frozen=binary.with_name('net10.0-tool-repair-base-source'); assert not frozen.exists(); shutil.copytree(binary,frozen)
names=['ClankerWorld.Tests.dll','ClankerWorld.Tests.pdb','ClankerWorld.Simulation.dll','ClankerWorld.Simulation.pdb','ClankerWorld.Server.dll','ClankerWorld.Server.pdb']
(out/'base-pins.json').write_text(json.dumps({n:hashlib.sha256((binary/n).read_bytes()).hexdigest() for n in names},indent=2))
PIN
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --list-tests > .evidence/2566-tool-repair/before-tests.txt 2>&1
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ClankerWorld.Tests.ToolProgressionRuntimeTests.' --settings skills/test-audit/references/coverage.runsettings --collect 'XPlat Code Coverage;Format=cobertura,json' --results-directory .evidence/2566-tool-repair/before --logger 'trx;LogFileName=tests.trx' -- xUnit.MaxParallelThreads=4 > .evidence/2566-tool-repair/before.log 2>&1
python skills/test-audit/scripts/trx_summary.py .evidence/2566-tool-repair/before > .evidence/2566-tool-repair/before-summary.json
python skills/test-audit/scripts/per_test_coverage.py --assembly tests/ClankerWorld.Tests/bin/Release/net10.0-tool-repair-base-source/ClankerWorld.Tests.dll --settings skills/test-audit/references/coverage.runsettings --methods .evidence/2566-tool-repair/original-methods.txt --out .evidence/2566-tool-repair/per-method-before --workers 1 --timeout 600 --adapter-path /home/agent/.nuget/packages/coverlet.collector/6.0.4/build/netstandard2.0 --cwd "$PWD" > .evidence/2566-tool-repair/per-method-before.log 2>&1

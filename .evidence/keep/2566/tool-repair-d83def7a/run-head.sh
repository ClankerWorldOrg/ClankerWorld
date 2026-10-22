#!/usr/bin/env bash
set -euo pipefail
export PATH=/workspace/tools/dotnet:$PATH DOTNET_PROCESSOR_COUNT=4
python - <<'PY'
from pathlib import Path
p=Path('tests/ClankerWorld.Tests/.editorconfig');assert not p.exists();p.write_text('[*.cs]\ndotnet_diagnostic.IDE0051.severity = warning\ndotnet_diagnostic.IDE0052.severity = warning\ndotnet_diagnostic.IDE0060.severity = warning\ndotnet_diagnostic.IDE0005.severity = warning\n')
PY
trap 'rm -f tests/ClankerWorld.Tests/.editorconfig' EXIT
dotnet build tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-restore --no-incremental -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -p:EnforceCodeStyleInBuild=true -p:GenerateDocumentationFile=true -p:TreatWarningsAsErrors=false > .evidence/2566-tool-repair/build-after.log 2>&1
rm tests/ClankerWorld.Tests/.editorconfig
trap - EXIT
python - <<'PY'
from pathlib import Path
import json,hashlib,shutil,re,gzip
out=Path('.evidence/2566-tool-repair');binary=Path('tests/ClankerWorld.Tests/bin/Release/net10.0');old=json.loads((out/'base-pins.json').read_text());pins={n:hashlib.sha256((binary/n).read_bytes()).hexdigest() for n in old}
for n,h in old.items():
 if not n.startswith('ClankerWorld.Tests.'): assert pins[n]==h,n
frozen=binary.with_name('net10.0-tool-repair-head-source');assert not frozen.exists();shutil.copytree(binary,frozen)
(out/'head-pins.json').write_text(json.dumps(pins,indent=2))
def warnings(text):
 found=set()
 for line in text.splitlines():
  m=re.match(r'(.+?\.cs)\(\d+,\d+\): warning (IDE\d+): (.*?) \[',line)
  if m:
   p,c,msg=m.groups();found.add((p.split('/ClankerWorld-2566-tool-repair/',1)[-1],c,msg))
 return found
before={tuple(x) for x in json.loads((out/'unused-before.json').read_text())};after=warnings((out/'build-after.log').read_text());assert before and after
comparison={'before':len(before),'after':len(after),'new':sorted(after-before),'removed':sorted(before-after)};(out/'unused-comparison.json').write_text(json.dumps(comparison,indent=2));assert not comparison['new'],comparison
raw=out/'raw-build';raw.mkdir()
for label,folder in [('base',binary.with_name('net10.0-tool-repair-base-source')),('head',frozen)]:
 for n in old:
  (raw/(label+'-'+n+'.gz')).write_bytes(gzip.compress((folder/n).read_bytes(),mtime=0))
PY
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --list-tests > .evidence/2566-tool-repair/after-tests.txt 2>&1
python skills/test-audit/scripts/check_ledger.py .evidence/2566-tool-repair/ledger.json --tests .evidence/2566-tool-repair/after-tests.txt > .evidence/2566-tool-repair/ledger-check.json
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ClankerWorld.Tests.ToolProgressionRuntimeTests.' --settings skills/test-audit/references/coverage.runsettings --collect 'XPlat Code Coverage;Format=cobertura,json' --results-directory .evidence/2566-tool-repair/after --logger 'trx;LogFileName=tests.trx' -- xUnit.MaxParallelThreads=4 > .evidence/2566-tool-repair/after.log 2>&1
python skills/test-audit/scripts/trx_summary.py .evidence/2566-tool-repair/after > .evidence/2566-tool-repair/after-summary.json
python - <<'PY'
from pathlib import Path
import subprocess,sys,hashlib
out=Path('.evidence/2566-tool-repair')
def report(folder):
 ps=sorted(folder.rglob('coverage.cobertura.xml'),key=lambda p:len(str(p)));assert ps and len({hashlib.sha256(p.read_bytes()).hexdigest() for p in ps})==1;return ps[0]
r=subprocess.run([sys.executable,'skills/test-audit/scripts/compare_coverage.py',str(report(out/'before')),str(report(out/'after')),'--repo',str(Path.cwd()),'--threshold-scope','each'],capture_output=True,text=True)
(out/'coverage-comparison.json').write_text(r.stdout);(out/'coverage-comparison.stderr').write_text(r.stderr);(out/'coverage-comparison.exit-code').write_text(str(r.returncode)+'\n');assert r.returncode==0,(r.stdout,r.stderr)
PY
python .evidence/2566-tool-repair/compare-branch-identities.py > .evidence/2566-tool-repair/stable-branch-comparison.log
python skills/test-audit/scripts/per_test_coverage.py --assembly tests/ClankerWorld.Tests/bin/Release/net10.0-tool-repair-head-source/ClankerWorld.Tests.dll --settings skills/test-audit/references/coverage.runsettings --methods .evidence/2566-tool-repair/retained-methods.txt --out .evidence/2566-tool-repair/per-method-retained --workers 1 --timeout 600 --adapter-path /home/agent/.nuget/packages/coverlet.collector/6.0.4/build/netstandard2.0 --cwd "$PWD" > .evidence/2566-tool-repair/per-method-retained.log 2>&1

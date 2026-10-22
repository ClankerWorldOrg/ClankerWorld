#!/usr/bin/env bash
set -euo pipefail
export PATH=/workspace/tools/dotnet:$PATH DOTNET_PROCESSOR_COUNT=4
python - <<'PY'
from pathlib import Path
p=Path('tests/ClankerWorld.Tests/.editorconfig');assert not p.exists();p.write_text('[*.cs]\ndotnet_diagnostic.IDE0051.severity = warning\ndotnet_diagnostic.IDE0052.severity = warning\ndotnet_diagnostic.IDE0060.severity = warning\ndotnet_diagnostic.IDE0005.severity = warning\n')
PY
trap 'rm -f tests/ClankerWorld.Tests/.editorconfig' EXIT
dotnet build tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-restore --no-incremental -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -p:EnforceCodeStyleInBuild=true -p:GenerateDocumentationFile=true -p:TreatWarningsAsErrors=false > .evidence/2566-cognition-handler/build-after.log 2>&1
rm tests/ClankerWorld.Tests/.editorconfig
trap - EXIT
python - <<'PY'
from pathlib import Path
import json,hashlib,shutil,re,gzip
out=Path('.evidence/2566-cognition-handler');binary=Path('tests/ClankerWorld.Tests/bin/Release/net10.0');old=json.loads((out/'base-pins.json').read_text());pins={n:hashlib.sha256((binary/n).read_bytes()).hexdigest() for n in old}
for n,h in old.items():
 if not n.startswith('ClankerWorld.Tests.'): assert pins[n]==h,n
frozen=binary.with_name('net10.0-cognition-handler-head-source');assert not frozen.exists();shutil.copytree(binary,frozen)
(out/'head-pins.json').write_text(json.dumps(pins,indent=2))
def warnings(text):
 found=set()
 for line in text.splitlines():
  m=re.match(r'(.+?\.cs)\(\d+,\d+\): warning (IDE\d+): (.*?) \[',line)
  if m:
   p,c,msg=m.groups();found.add((p.split('/ClankerWorld-2566-cognition-handler/',1)[-1],c,msg))
 return found
before={tuple(x) for x in json.loads((out/'unused-before.json').read_text())};after=warnings((out/'build-after.log').read_text());assert before and after
comparison={'before':len(before),'after':len(after),'new':sorted(after-before),'removed':sorted(before-after)};(out/'unused-comparison.json').write_text(json.dumps(comparison,indent=2));assert not comparison['new'],comparison
raw=out/'raw-build';raw.mkdir()
for label,folder in [('base',binary.with_name('net10.0-cognition-handler-base-source')),('head',frozen)]:
 for n in old:
  (raw/(label+'-'+n+'.gz')).write_bytes(gzip.compress((folder/n).read_bytes(),mtime=0))
PY
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --list-tests > .evidence/2566-cognition-handler/after-tests.txt 2>&1
python skills/test-audit/scripts/check_ledger.py .evidence/2566-cognition-handler/ledger.json --tests .evidence/2566-cognition-handler/after-tests.txt > .evidence/2566-cognition-handler/ledger-check.json
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ClankerWorld.Tests.CognitionTests.|FullyQualifiedName~ClankerWorld.Tests.ModelNeedWordsTests.|FullyQualifiedName~ClankerWorld.Tests.MemorySummaryProviderTests.' --settings skills/test-audit/references/coverage.runsettings --collect 'XPlat Code Coverage;Format=cobertura,json' --results-directory .evidence/2566-cognition-handler/after --logger 'trx;LogFileName=tests.trx' -- xUnit.MaxParallelThreads=4 > .evidence/2566-cognition-handler/after.log 2>&1
python skills/test-audit/scripts/trx_summary.py .evidence/2566-cognition-handler/after > .evidence/2566-cognition-handler/after-summary.json
python - <<'PY'
from pathlib import Path
import subprocess,sys,hashlib
out=Path('.evidence/2566-cognition-handler')
def report(folder):
 ps=sorted(folder.rglob('coverage.cobertura.xml'),key=lambda p:len(str(p)));assert ps and len({hashlib.sha256(p.read_bytes()).hexdigest() for p in ps})==1;return ps[0]
r=subprocess.run([sys.executable,'skills/test-audit/scripts/compare_coverage.py',str(report(out/'before')),str(report(out/'after')),'--repo',str(Path.cwd()),'--threshold-scope','each'],capture_output=True,text=True)
(out/'coverage-comparison.json').write_text(r.stdout);(out/'coverage-comparison.stderr').write_text(r.stderr);(out/'coverage-comparison.exit-code').write_text(str(r.returncode)+'\n');assert r.returncode==0,(r.stdout,r.stderr)
PY
python .evidence/2566-cognition-handler/compare-branch-identities.py > .evidence/2566-cognition-handler/stable-branch-comparison.log
python - <<'CHECK'
from pathlib import Path
import sys,json
sys.path.insert(0,'skills/test-audit/scripts')
from check_ledger import read_tests,short
out=Path('.evidence/2566-cognition-handler');tests=read_tests(out/'after-tests.txt');proofs=(out/'retained-proofs.txt').read_text().splitlines();missing=[p for p in proofs if short(p) not in tests];(out/'proof-check.json').write_text(json.dumps({'retained_proofs':len(proofs),'missing':missing},indent=2));assert not missing
for folder in ['base','head']:
 import hashlib
 binary=Path('tests/ClankerWorld.Tests/bin/Release')/('net10.0-cognition-handler-base-source' if folder=='base' else 'net10.0')
 for name,h in json.loads((out/(folder+'-pins.json')).read_text()).items():assert hashlib.sha256((binary/name).read_bytes()).hexdigest()==h,(folder,name)
(out/'head-verification.finished').write_text('All local checks complete; CI pending.\n')
CHECK

from pathlib import Path
import os,subprocess,json,hashlib,shutil
root=Path(__file__).resolve().parents[2];out=root/'.evidence/2561-continuation/content';out.mkdir(parents=True,exist_ok=True)
source=root/'tests/ClankerWorld.Tests/bin/Release/net10.0-runtime-continuation-base-source';worker=out/'base-output';assert not worker.exists();shutil.copytree(source,worker)
pins=json.loads((root/'.evidence/2561-continuation/base-pins.json').read_text())
def verify():
 for name,digest in pins.items():assert hashlib.sha256((worker/name).read_bytes()).hexdigest()==digest,name
verify();env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
scope='FullyQualifiedName~ClankerWorld.Tests.WorldRuntimeTests.|FullyQualifiedName~ClankerWorld.Tests.RecipeOutputAdmissionTests.'
(out/'scope.filter').write_text(scope+'\n')
cmd=['dotnet','test',str(worker/'ClankerWorld.Tests.dll'),'--filter',scope,'--settings',str(root/'skills/test-audit/references/coverage.runsettings'),'--collect','XPlat Code Coverage;Format=cobertura,json','--results-directory',str(out/'before'),'--logger','trx;LogFileName=tests.trx','--logger','console;verbosity=normal','--','xUnit.MaxParallelThreads=4']
(out/'base-command.json').write_text(json.dumps(cmd,indent=2)+'\n')
with (out/'base-coverage.log').open('w') as log:r=subprocess.run(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
verify();(out/'base-coverage.exit-code').write_text(str(r.returncode)+'\n')
print(json.dumps({'exit':r.returncode,'original_six_hashes_restored':True}));raise SystemExit(r.returncode)

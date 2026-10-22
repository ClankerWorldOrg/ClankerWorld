from pathlib import Path
import os,subprocess,json,hashlib,shutil,re,gzip,collections
root=Path(__file__).resolve().parents[2];out=root/'.evidence/2561-content';env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
base=Path('/workspace/ClankerWorld-2561-continuation');assert (base/'.evidence/2561-continuation/content/base-coverage.exit-code').read_text().strip()=='0'
def run(name,cmd):
 with (out/(name+'.log')).open('w') as log:r=subprocess.run(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 (out/(name+'.exit-code')).write_text(str(r.returncode)+'\n')
 if r.returncode:raise SystemExit(name+' failed; read log')
folder=root/'tests/ClankerWorld.Tests/bin/Release/net10.0';basepins=json.loads((out/'base-pins.json').read_text())
def verify(pins):
 for name,digest in pins.items():assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==digest,name
verify({k:v for k,v in basepins.items() if not k.startswith('ClankerWorld.Tests.')})
cfg=root/'tests/ClankerWorld.Tests/.editorconfig';assert not cfg.exists();cfg.write_text('[*.cs]\ndotnet_diagnostic.IDE0051.severity = warning\ndotnet_diagnostic.IDE0052.severity = warning\ndotnet_diagnostic.IDE0060.severity = warning\ndotnet_diagnostic.IDE0005.severity = warning\n')
try:run('build-head',['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','--configuration','Release','--no-restore','--no-incremental','-p:BuildProjectReferences=false','-p:EnforceCodeStyleInBuild=true','-p:GenerateDocumentationFile=true','-p:TreatWarningsAsErrors=false'])
finally:cfg.unlink()
records=set()
for line in (out/'build-head.log').read_text().splitlines():
 m=re.match(r'(.+?)\(\d+,\d+\): warning (IDE(?:0051|0052|0060|0005)): (.*?) \[',line)
 if m:records.add(m.group(1).replace(str(root)+'/', '')+'|'+m.group(2)+'|'+m.group(3))
before=set(json.loads((out/'unused-base-records.json').read_text()));new=sorted(records-before)
(out/'unused-head-records.json').write_text(json.dumps(sorted(records),indent=2)+'\n');(out/'unused-comparison.json').write_text(json.dumps({'before':len(before),'after':len(records),'new':new,'removed':sorted(before-records)},indent=2)+'\n');assert not new,new
verify({k:v for k,v in basepins.items() if not k.startswith('ClankerWorld.Tests.')})
pins={}
for name in basepins:
 data=(folder/name).read_bytes();pins[name]=hashlib.sha256(data).hexdigest();p=out/'raw-build/head'/name;p.parent.mkdir(parents=True,exist_ok=True);p.with_suffix(p.suffix+'.gz').write_bytes(gzip.compress(data,mtime=0))
(out/'head-pins.json').write_text(json.dumps(pins,indent=2)+'\n');frozen=folder.with_name('net10.0-content-lifecycle-head-source');assert not frozen.exists();shutil.copytree(folder,frozen)
run('discovery-head',['dotnet','test',str(frozen/'ClankerWorld.Tests.dll'),'--list-tests'])
tests=[s.strip() for s in (out/'discovery-head.log').read_text().splitlines() if s.strip().startswith('ClankerWorld.Tests.')];(out/'after-tests.txt').write_text('\n'.join(tests)+'\n')
old=(out/'before-tests.txt').read_text().splitlines()
def normalize(s):
 for method,suffix in [('RollbackPreservesRecordedProjectReferences','(stage: "completed")'),('RollbackWithCommittedProductionReferencesRejectsWithoutDeletingWorldState','(completed: False)')]:
  if s.startswith('ClankerWorld.Tests.WorldRuntimeTests.'+method):s=s.removesuffix(suffix)
 return s
assert collections.Counter(map(normalize,old))==collections.Counter(tests),'Discovery differs beyond two intentional theory-to-Fact display names'
affected=[s for s in tests if s.startswith(('ClankerWorld.Tests.WorldRuntimeTests.','ClankerWorld.Tests.RecipeOutputAdmissionTests.'))]
(out/'after-affected-tests.txt').write_text('\n'.join(affected)+'\n');proofs=(out/'retained-proofs.txt').read_text().splitlines();methods={s.split('(')[0] for s in tests};assert all(p in methods for p in proofs)
(out/'discovery-comparison.json').write_text(json.dumps({'cases_before':len(old),'cases_after':len(tests),'methods_after':len(methods),'affected_cases':len(affected),'affected_methods':len({s.split('(')[0] for s in affected}),'intentional_display_changes':2,'all_other_case_names_and_arguments_identical':True,'all_ledger_proofs_discovered':True},indent=2)+'\n')
run('ledger-check',['python3','skills/test-audit/scripts/check_ledger.py',str(out/'ledger.json'),'--tests',str(out/'after-tests.txt')])
run('format',['dotnet','format','--verify-no-changes','--no-restore'])
run('stale-docs',['node','scripts/find-stale-docs.js','ContentRollbackSafetyTests','RecipeOutputAdmissionTests'])
run('diff-check',['git','diff','--check'])
verify(pins)
scope=(out/'scope.filter').read_text().strip();cmd=['dotnet','test',str(folder/'ClankerWorld.Tests.dll'),'--filter',scope,'--settings',str(root/'skills/test-audit/references/coverage.runsettings'),'--collect','XPlat Code Coverage;Format=cobertura,json','--results-directory',str(out/'after'),'--logger','trx;LogFileName=tests.trx','--logger','console;verbosity=normal','--','xUnit.MaxParallelThreads=4'];(out/'head-command.json').write_text(json.dumps(cmd,indent=2)+'\n')
run('head-coverage',cmd);verify(pins)
(out/'head-verification.finished').write_text('Head7fa228e5 build, unused, discovery, ledger, formatter, stale, diff and175-case affected coverage complete. Comparator/stable identity/executed proof verification still separate.\n')
print(json.dumps({'head':'7fa228e5891bdbbdaa05975013c57e46157ffee3','affected_cases':len(affected),'unused_before':len(before),'unused_after':len(records),'new_unused':0,'six_head_hashes_restored':True}))

# Resume after accepted normal archive/discovery; no head coverage previously started.
from pathlib import Path
import datetime,hashlib,json,os,re,shutil,subprocess,sys,time
p=Path('/workspace/2566-audit/weather-boundaries');scope=json.loads((p/'scope.json').read_text());side=sys.argv[1];root=Path(scope['base_checkout'] if side=='before' else scope['head_checkout']);e=root/'.evidence/keep/2566-weather-boundaries';e.mkdir(parents=True,exist_ok=True);env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4';os.nice(10)
def wait(path,condition=lambda s:True):
 for _ in range(900):
  if path.exists() and condition(path.read_text()):return path.read_text()
  time.sleep(2)
 raise RuntimeError('timed out waiting '+str(path))
def run(args,log):
 print('RUN',args,flush=True)
 with (p/log).open('w') as f:subprocess.run(args,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT,check=True)
run(['python',str(p/'run_scoped.py'),str(root),side,str(p/'scope.json'),str(scope['expected_cases'][side])],side+'-driver.log')
if side=='before':
 run(['python','skills/test-audit/scripts/per_test_coverage.py','--assembly',str(root/'tests/ClankerWorld.Tests/bin/Release/net10.0.weather-original/ClankerWorld.Tests.dll'),'--settings',str(root/'skills/test-audit/references/coverage.runsettings'),'--methods',str(p/'methods.txt'),'--out',str(p/'isolated'),'--workers','2','--cwd',str(root)],'isolated-driver.log')
 run(['python','skills/test-audit/scripts/unique_coverage.py',str(p/'isolated'),'--timings',str(e/'before'),'--table',str(p/'unique.tsv'),'--plan','--keep',str(p/'keep.txt')],'unique-plan.json')
 run(['python',str(p/'run_unused.py'),str(root),'before','--no-refs'],'before-unused-driver.log')
else:
 run(['dotnet','format','--verify-no-changes','--no-restore'],'format.log')
 run(['python',str(p/'run_unused.py'),str(root),'after','--no-refs'],'after-unused-driver.log')
 before=Path(scope['base_checkout'])/'.evidence/keep/2566-weather-boundaries';wait(before/'before-unused.json',lambda s:'exit_code' in json.loads(s));assert json.loads((before/'before-unused.json').read_text())['exit_code']==0
 def diagnostics(path):
  records=[]
  for line in path.read_text().splitlines():
   m=re.search(r'^(.+?\.cs)\(\d+,\d+\): warning (IDE(?:0051|0052|0060|0005)): (.+?) \[',line)
   if m:
    name=m[1];name=name[name.index('/tests/')+1:] if '/tests/' in name else name[name.index('/src/')+1:];records.append((name,m[2],m[3]))
  return records
 a=diagnostics(before/'before-unused.log');b=diagnostics(e/'after-unused.log');assert a and b
 d=dict(before_records=len(a),after_records=len(b),before_normalized=len(set(a)),after_normalized=len(set(b)),new=sorted(set(b)-set(a)),removed=sorted(set(a)-set(b)));(p/'unused-comparison.json').write_text(json.dumps(d,indent=2)+'\n');assert not d['new'],d
 run(['python',str(p/'finish_scoped.py'),str(root),'2566-weather-boundaries'],'comparison-driver.log')
 run(['python',str(p/'point_bound.py'),str(before/'before'),str(e/'final'),str(p/'point-bound.json')],'point-bound.log')
 (p/'local-complete.json').write_text(json.dumps(dict(head=scope['head'],success=True,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()),indent=2)+'\n')
print(side,'COMPLETE',flush=True)

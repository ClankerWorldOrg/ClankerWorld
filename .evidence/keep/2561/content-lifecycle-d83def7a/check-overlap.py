from pathlib import Path
import json,subprocess
root=Path(__file__).resolve().parents[2];out=root/'.evidence/2561-content';prs=json.loads((out/'open-pr-files.json').read_text());prs=[p for p in prs if any(f['path'].startswith('tests/') for f in p['files'])]
base='63e5970fd131a341d9ae86cc26fa6c43fac327bf';head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
subprocess.run(['git','fetch','origin',*[f'pull/{p["number"]}/head:refs/test-audit/2561-content/pr-{p["number"]}' for p in prs]],cwd=root,check=True,stdout=subprocess.DEVNULL)
results=[]
for p in prs:
 ref=f'refs/test-audit/2561-content/pr-{p["number"]}';actual=subprocess.check_output(['git','rev-parse',ref],cwd=root,text=True).strip()
 actual_files=subprocess.check_output(['git','diff','--name-only',base+'...'+actual],cwd=root,text=True).splitlines()
 pair={}
 for label,commit in [('base',base),('head',head)]:
  r=subprocess.run(['git','merge-tree','--write-tree','--name-only',commit,actual],cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
  assert r.returncode in [0,1],r.stderr
  parts=r.stdout.split('\n\n',1)[0].splitlines();files=parts[1:] if r.returncode else []
  pair[label]={'returncode':r.returncode,'conflicts':files,'output':r.stdout,'stderr':r.stderr}
 new=sorted(set(pair['head']['conflicts'])-set(pair['base']['conflicts']));results.append({'pr':p['number'],'sha':actual,'api_snapshot_sha':p['head'],'moved_since_api_snapshot':actual!=p['head'],'actual_changed_files':actual_files,'new_conflicts':new,**pair})
(out/'merge-comparison.json').write_text(json.dumps({'base':base,'head':head,'test_prs':len(prs),'comparisons':len(results)*2,'results':results},indent=2)+'\n')
bad=[{'pr':r['pr'],'files':r['new_conflicts']} for r in results if r['new_conflicts']];print(json.dumps({'test_prs':len(prs),'comparisons':len(results)*2,'new_conflicts':bad}));raise SystemExit(bool(bad))

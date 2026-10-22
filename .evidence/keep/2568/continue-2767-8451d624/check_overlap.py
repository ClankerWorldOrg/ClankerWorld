from pathlib import Path
import subprocess,json
p=Path('/workspace/2568-audit/continue-2767-8451d624');root=Path('/workspace/ClankerWorld-2568-continue-2767');heads=json.loads((p/'pr-heads.json').read_text());base='e61a1d8dc3f4b2ca3105e1e50477be077eb902f4';results=[]
args=['git','fetch','origin']+['+refs/heads/'+h['branch']+':refs/remotes/origin/cleanup-check-'+str(h['number']) for h in heads]
subprocess.run(args,cwd=root,check=True,stdout=subprocess.DEVNULL,stderr=(p/'fetch-pr-heads.log').open('w'))
for h in heads:
 actual=subprocess.check_output(['git','rev-parse','refs/remotes/origin/cleanup-check-'+str(h['number'])],cwd=root,text=True).strip()
 if actual!=h['head']:results.append(dict(number=h['number'],moved=True,expected=h['head'],actual=actual));continue
 sides=[]
 for tip in [base,'HEAD']:
  r=subprocess.run(['git','merge-tree','--write-tree','--name-only',tip,actual],cwd=root,capture_output=True,text=True)
  assert r.returncode in [0,1],r.stderr
  conflicts=r.stdout.split('\n\n',1)[0].splitlines()[1:];sides.append(conflicts)
 results.append(dict(number=h['number'],before=sides[0],after=sides[1],new=sorted(set(sides[1])-set(sides[0]))))
(p/'overlap.json').write_text(json.dumps(results,indent=2)+'\n');print('Checked',len(heads),'heads; moved:',[x for x in results if x.get('moved')],'new conflicts:',[x for x in results if x.get('new')])
assert not any(x.get('moved') or x.get('new') for x in results)

from pathlib import Path
import subprocess,json,hashlib
root=Path('/workspace/ClankerWorld-2566-17cc-base');out=Path('/workspace/2566-audit/society-fixtures');original='de24e737';audited='8b575331';current='17ccaf0cc7d2c65add79cd9601602858e5755aad'
def git(*args):return subprocess.check_output(['git',*args],cwd=root,text=True)
def area(path):
 prefix='tests/ClankerWorld.Tests/Simulation/'
 if not path.startswith(prefix) or not path.endswith('.cs'):return False
 parts=path[len(prefix):].split('/')
 return len(parts)==1 or parts[0] in ['Cognition','Society','Kernel','Content'] or parts[0]=='World' and (len(parts)==2 or parts[1] in ['Content','Map'])
def files(ref):return [p for p in git('ls-tree','-r','--name-only',ref,'tests/ClankerWorld.Tests/Simulation').splitlines() if area(p)]
def text(ref,f):return git('show',ref+':'+f)
orig=files(original);now=files(current);assert len(orig)==len(now)==163 and set(orig)==set(now)
notes='\n'.join((Path('/workspace/2566-audit/handoff')/n).read_text() for n in ['parent-world-audit.md','reader-area-audit.md'])
rows=[]
for f in now:
 a=text(original,f);b=text(audited,f);c=text(current,f)
 rows.append(dict(file=f,original_lines=len(a.splitlines()),audited_lines=len(b.splitlines()),current_lines=len(c.splitlines()),changed_since_full_audit=b!=c,current_sha256=hashlib.sha256(c.encode()).hexdigest(),named_in_prior_full_read_ledger=Path(f).stem in notes))
result=dict(original=original,audited=audited,current=current,files=rows,original_lines=sum(r['original_lines'] for r in rows),current_lines=sum(r['current_lines'] for r in rows),missing_prior_names=[r['file'] for r in rows if not r['named_in_prior_full_read_ledger']],changed_since_audit=[r['file'] for r in rows if r['changed_since_full_audit']],limits='Read-only reconciliation, not author claim of new full filesread. Priorreadersd83def7a root+reader fullread ledgers retained; changed files require currentdiffreview. Ready proposals cannot countasmerged.')
result['merged_net_removed']=result['original_lines']-result['current_lines'];(out/'area-reconciliation.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='files'},indent=2))

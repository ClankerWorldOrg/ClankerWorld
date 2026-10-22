from pathlib import Path
import gzip,shutil,json,hashlib
root=Path('/workspace/ClankerWorld-2551-attachment');src=root/'.evidence/2551-attachment';dst=Path('/workspace/ClankerWorld-2551-evidence/.evidence/keep/2551/candidate-attachment-d83def7a');dst.mkdir(parents=True,exist_ok=True)
for p in src.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(src)
 if {'bin','obj'}.intersection(rel.parts):continue
 if 'native' in rel.parts and not (src/'native.exit-code').exists():continue
 if p.name in ['build.log','focused.log','format.log'] and not (src/(p.stem+'.exit-code')).exists():continue
 if 'focused' in rel.parts and not (src/'focused.exit-code').exists():continue
 q=dst/rel;q.parent.mkdir(parents=True,exist_ok=True)
 if p.suffix in ['.log','.trx','.xml']:q.with_name(q.name+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))
 else:shutil.copyfile(p,q)
pins={};old=json.loads((dst/'binary-pins.json').read_text()) if (dst/'binary-pins.json').exists() else {}
repos=[('head-tests',root/'tests/ClankerWorld.Tests/bin/Release/net10.0')] if (src/'build.exit-code').exists() and (src/'build.exit-code').read_text().strip()=='0' else []
if (src/'native.exit-code').exists() and (src/'native.exit-code').read_text().strip()=='0':
 for label in ['base','head']:repos.append(('native-'+label,Path('/workspace/ClankerWorld-2551-attachment-native-'+label)/'src/ClankerWorld.Simulation/bin/Release/net10.0'))
for label,folder in repos:
 pins[label]={}
 for stem in ['ClankerWorld.Simulation','ClankerWorld.Server','ClankerWorld.Tests']:
  for ext in ['dll','pdb']:
   p=folder/(stem+'.'+ext)
   if not p.exists():continue
   data=p.read_bytes();digest=hashlib.sha256(data).hexdigest();pins[label][p.name]=digest;q=dst/'raw-build'/label/(p.name+'.gz');q.parent.mkdir(parents=True,exist_ok=True)
   if old.get(label,{}).get(p.name)!=digest or not q.exists():q.write_bytes(gzip.compress(data,mtime=0))
(dst/'binary-pins.json').write_text(json.dumps(pins,indent=2)+'\n')
(dst/'README.txt').write_text('Evidence only: NEVER MERGE or open a source PR from this evidence branch. PR2756, Codex session d83def7a. Frozen base e7ccf6a9da1fef954ad525025804fc2b1ee14666; head66ee541e2bc0cd788d351820d53849321959c48b. Source+100/-242, tests/docs unchanged. Exact31 decoder/cast/planner/check mappings preserved, one shared candidate update/remove; no helper added. A check is complete only when its exit-code/completion receipt is present. Independent clean native worktrees avoid mutating the test output checkout. No local full-suite or coverage. Read final description and ready receipt for final status.\n')
print(json.dumps({'files':sum(p.is_file() for p in dst.rglob('*')),'bytes':sum(p.stat().st_size for p in dst.rglob('*') if p.is_file())}))

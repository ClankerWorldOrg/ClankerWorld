from pathlib import Path
import gzip,shutil,json,hashlib
root=Path('/workspace/ClankerWorld-2551');src=root/'.evidence/2551';dst=Path('/workspace/ClankerWorld-2551-evidence/.evidence/keep/2551/dead-entry-points-d83def7a');dst.mkdir(parents=True,exist_ok=True)
for p in src.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(src)
 if {'bin','obj'}.intersection(rel.parts):continue
 if p.name in ['focused.log','format.log'] and not (src/(p.stem+'.exit-code')).exists():continue
 if 'focused' in rel.parts and not (src/'focused.exit-code').exists():continue
 q=dst/rel;q.parent.mkdir(parents=True,exist_ok=True)
 if p.suffix in ['.log','.trx','.xml']:q.with_name(q.name+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))
 else:shutil.copyfile(p,q)
pins={}
oldpins=json.loads((dst/'binary-pins.json').read_text()) if (dst/'binary-pins.json').exists() else {}
for label,repo in [('base',Path('/workspace/ClankerWorld-2551-base')),('head',root)]:
 pins[label]={}
 for stem in ['ClankerWorld.Simulation','ClankerWorld.Server','ClankerWorld.Tests']:
  for ext in ['dll','pdb']:
   p=repo/'tests/ClankerWorld.Tests/bin/Release/net10.0'/(stem+'.'+ext);data=p.read_bytes();pins[label][p.name]=hashlib.sha256(data).hexdigest();q=dst/'raw-build'/label/(p.name+'.gz');q.parent.mkdir(parents=True,exist_ok=True);
   if oldpins.get(label,{}).get(p.name)!=pins[label][p.name] or not q.exists():q.write_bytes(gzip.compress(data,mtime=0))
 for name in ['build-base.py','base-build.finished','restore-base.log','restore-base.exit-code','build-base.log','build-base.exit-code']:
  p=repo/'.evidence/2551'/name
  if p.exists():
   q=dst/name
   if p.suffix=='.log':q.with_name(q.name+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))
   else:shutil.copyfile(p,q)
(dst/'binary-pins.json').write_text(json.dumps(pins,indent=2)+'\n')
(dst/'README.txt').write_text('Evidence only: NEVER MERGE or open a source PR. Source PR2747, Codex session d83def7a, fixed basec2df5428 and head31ad20fa. Five uncalled internal Civic methods and freed import removed, source0/-42, tests/docs0. Fresh builds/discovery/IL/native68frames pass; focused/format/CI outcomes valid only when their completed reports exist. Six scanned test-output DLL/PDB bytes per revision archived with hashes. All raw commands, scanner source, TRX/timings/native checkpoints are preserved. Paths in scripts are original worktree roots; adapt roots to reproduce independently. No full-suite local/coverage run.\n')
print(json.dumps({'files':sum(p.is_file() for p in dst.rglob('*')),'bytes':sum(p.stat().st_size for p in dst.rglob('*') if p.is_file())}))

from pathlib import Path
import gzip,shutil,json,hashlib
src=Path('/workspace/ClankerWorld-2551-imports/.evidence/2551-imports');dst=Path('/workspace/ClankerWorld-2551-evidence/.evidence/keep/2551/unused-imports-d83def7a');dst.mkdir(parents=True,exist_ok=True)
for p in src.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(src)
 if {'bin','obj'}.intersection(rel.parts):continue
 q=dst/rel;q.parent.mkdir(parents=True,exist_ok=True)
 if p.suffix in ['.log','.trx','.xml']:q.with_name(q.name+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))
 else:shutil.copyfile(p,q)
(dst/'README.txt').write_text('Evidence only. NEVER MERGE or open a source PR from this branch. PR2751; Codex session d83def7a. Fixed base f82570db918e057ff49a1155f6ab9e7bdb2172e8; head 80c25e1065f43c142477244ccc9d1ea2668dcffc. Source +0/-38, tests/docs unchanged. Pure unused imports, ordered executable source identical. Fresh restore/build/54 focused cases/discovery/full formatting/stale/diff passed. Exact-head CI remains pending until its final receipt exists. No local full-suite coverage or native probe required for imports. CI base/head metadata and timings preserved when available.\n')
print(json.dumps({'files':sum(p.is_file() for p in dst.rglob('*')),'bytes':sum(p.stat().st_size for p in dst.rglob('*') if p.is_file())}))

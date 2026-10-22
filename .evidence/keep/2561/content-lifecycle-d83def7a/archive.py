from pathlib import Path
import gzip,shutil,json
root=Path('/workspace/ClankerWorld-2561-content');src=root/'.evidence/2561-content';dst=Path('/workspace/ClankerWorld-2561-evidence/.evidence/keep/2561/content-lifecycle-d83def7a');dst.mkdir(parents=True,exist_ok=True)
for p in src.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(src)
 if 'head-output-collector' in rel.parts:continue
 if 'before' in rel.parts and 'collector-unavailable' not in rel.parts and not (src/'base-coverage-completion.json').exists():continue
 if 'after' in rel.parts and not (src/'head-verification.finished').exists():continue
 if p.name=='head-coverage.log' and not (src/'head-verification.finished').exists():continue
 if p.name=='format.log' and not (src/'format.exit-code').exists():continue
 if p.suffix not in ['.json','.txt','.filter','.py','.sh','.log','.md','.stderr','.exit-code','.gz','.zip','.trx','.xml','.finished']:continue
 target=dst/rel;target.parent.mkdir(parents=True,exist_ok=True)
 if p.suffix in ['.xml','.trx','.log'] or p.name=='coverage.json':target.with_name(target.name+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))
 else:shutil.copyfile(p,target)
for p in (root/'skills/test-audit/scripts').glob('*.py'):
 target=dst/'audit-tools'/p.name;target.parent.mkdir(exist_ok=True);shutil.copyfile(p,target)
shutil.copyfile(root/'skills/test-audit/references/coverage.runsettings',dst/'audit-tools/coverage.runsettings')
(dst/'README.txt').write_text('Evidence only: NEVER MERGE or open a source PR for this branch. Source PR#2735, Codex session d83def7a. Frozen main63e5970fd131a341d9ae86cc26fa6c43fac327bf, head7fa228e5891bdbbdaa05975013c57e46157ffee3. Base175/175passed; head build/discovery/unused complete; final comparator and CI readiness are recorded only when their finished reports exist. Original script paths/layout and pinned command JSON are preserved; adapt archived script roots to reproduce in independent checkouts. No production/configuration change.\n')
print(json.dumps({'files':sum(p.is_file() for p in dst.rglob('*')),'bytes':sum(p.stat().st_size for p in dst.rglob('*') if p.is_file())}))

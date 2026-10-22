from pathlib import Path
import gzip,shutil,json,subprocess
root=Path('/workspace/ClankerWorld-2566-tool-repair');src=root/'.evidence/2566-tool-repair';dst=Path('/workspace/ClankerWorld-2566-evidence/.evidence/keep/2566/tool-repair-d83def7a');dst.mkdir(parents=True,exist_ok=True)
# Finished evidence only; never copy live instrumented build outputs.
for p in src.rglob('*'):
 if not p.is_file():continue
 if any(part in ['before','after','per-method-before','per-method-retained'] for part in p.relative_to(src).parts):
  if any(part in ['after','per-method-retained'] for part in p.relative_to(src).parts) and not (src/'head-verification.finished').exists():continue
  if p.name not in ['coverage.cobertura.xml','coverage.json','tests.trx','test.trx','test.txt','run.log','completion.json','methods.json']:continue
 elif p.suffix not in ['.json','.txt','.filter','.py','.sh','.log','.md','.stderr','.exit-code','.gz']:continue
 if p.name in ['build-after.log','after.log','per-method-retained.log'] and not (src/'head-verification.finished').exists():continue
 rel=p.relative_to(src); target=dst/rel;target.parent.mkdir(parents=True,exist_ok=True)
 if p.suffix in ['.xml','.trx','.log'] or p.name=='coverage.json':
  target=target.with_name(target.name+'.gz');target.write_bytes(gzip.compress(p.read_bytes(),mtime=0))
 else:shutil.copyfile(p,target)
# Audit scripts must be the actual frozen-base versions.
for p in (root/'skills/test-audit/scripts').glob('*.py'):
 target=dst/'audit-tools'/p.name;target.parent.mkdir(exist_ok=True);shutil.copyfile(p,target)
(dst/'README.txt').write_text('Evidence only: NEVER MERGE or open a source PR for this branch. Source PR #2719, author Codex session d83def7a. Frozen base8b575331, headcdea064417f1be3fbadc02862320a9d00ba1c1d9. '+('Head verification complete.' if (src/'head-verification.finished').exists() else 'Head verification still pending; baseline, ledger and reader evidence saved.')+'\n')
print(json.dumps({'files':sum(p.is_file() for p in dst.rglob('*')),'bytes':sum(p.stat().st_size for p in dst.rglob('*') if p.is_file())}))

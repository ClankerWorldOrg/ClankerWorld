from pathlib import Path
import json,hashlib
root=Path.cwd();out=root/'.evidence/2561-grown';files=sorted((root/'tests/ClankerWorld.Tests/Simulation/World/Runtime').glob('*.cs'));ledgers=list(out.glob('*audit.md'))
rows=[]
for p in files:
 if p.name.startswith('World'):continue
 refs=[x.name for x in ledgers if p.stem in x.read_text()]
 rows.append({'file':str(p.relative_to(root)),'lines':len(p.read_text().splitlines()),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'read_ledger_references':refs,'decision':'retain independent contracts or defer to existing ready PR; selected content setup is in ready2735'})
result={'base':'ca3a5b8dcde715b0fbfabad64e7cc708aae4fa84','files':len(rows),'lines':sum(r['lines'] for r in rows),'missing_named_read_ledgers':[r['file'] for r in rows if not r['read_ledger_references']],'catalog':rows}
(out/'area-file-catalog.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='catalog'},indent=2))

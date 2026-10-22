from pathlib import Path
from collections import Counter
import re,json
area=Path('src/ClankerWorld.Simulation/World/Civic');counts=Counter()
for folder in ['src','tests','scripts']:
 for p in Path(folder).rglob('*'):
  if p.suffix in ['.cs','.py','.sh','.js','.gd','.md'] and not {'bin','obj','node_modules'}.intersection(p.parts):counts.update(re.findall(r'\b[A-Za-z_]\w*\b',p.read_text()))
items=[]
for p in area.glob('*.cs'):
 for m in re.finditer(r'\b(private|internal|public)\s+(?:static\s+)?(?:async\s+)?[\w<>,?\[\].]+\s+(\w+)\s*(?:<[^\n]+?>)?\s*\(',p.read_text()):
  if counts[m[2]]<=2:items.append({'file':str(p),'access':m[1],'method':m[2],'occurrences':counts[m[2]]})
Path('.evidence/2551/static-low-reference-candidates.json').write_text(json.dumps(items,indent=2));print(json.dumps(items,indent=2))

from pathlib import Path
import json,xml.etree.ElementTree as E,hashlib
import sys
sys.path.insert(0,'skills/test-audit/scripts')
from coverage_evidence import source_key
out=Path('.evidence/2561-content')
def read(folder):
 files=list(folder.rglob('coverage.json')); assert files and len({hashlib.sha256(p.read_bytes()).hexdigest() for p in files})==1
 report=json.loads(files[0].read_text());branches={};lines={}
 for module,docs in report.items():
  for raw_doc,classes in docs.items():
   doc=source_key(raw_doc)
   for cls,methods in classes.items():
    for method,coverage in methods.items():
     for line,hits in coverage['Lines'].items():
      key=(module,doc,int(line));lines[key]=max(lines.get(key,0),hits)
     for b in coverage['Branches']:
      key=(module,doc,cls,method,b['Line'],b['Offset'],b['EndOffset'],b['Path'],b['Ordinal'])
      assert key not in branches,key
      branches[key]=b['Hits']
 xmls=list(folder.rglob('coverage.cobertura.xml'));assert len({hashlib.sha256(p.read_bytes()).hexdigest() for p in xmls})==1
 summary=E.parse(xmls[0]).getroot().attrib
 assert len(lines)==int(summary['lines-valid']);assert sum(v>0 for v in lines.values())==int(summary['lines-covered'])
 assert len(branches)==int(summary['branches-valid']);assert sum(v>0 for v in branches.values())==int(summary['branches-covered'])
 return branches,lines
before,blines=read(out/'before');after,alines=read(out/'after');assert before.keys()==after.keys();assert blines.keys()==alines.keys()
results={}
for module in sorted({k[0] for k in before}):
 keys=[k for k in before if k[0]==module];lkeys=[k for k in blines if k[0]==module]
 lost=[k for k in keys if before[k]>0 and not after[k]>0]; lostlines=[k for k in lkeys if blines[k]>0 and not alines[k]>0]
 results[module]={'branches_valid':len(keys),'before_branches':sum(before[k]>0 for k in keys),'after_branches':sum(after[k]>0 for k in keys),'lost_actual_branch_identities':lost,'full_suite_branch_loss_upper_bound_pp':100*len(lost)/len(keys),'lines_valid':len(lkeys),'before_lines':sum(blines[k]>0 for k in lkeys),'after_lines':sum(alines[k]>0 for k in lkeys),'lost_lines':lostlines,'full_suite_line_loss_upper_bound_pp':100*len(lostlines)/len(lkeys)}
results['total']={'branches_valid':len(before),'lost_branches':sum(len(x['lost_actual_branch_identities']) for x in results.values()),'lines_valid':len(blines),'lost_lines':sum(len(x['lost_lines']) for x in results.values())}
(out/'stable-branch-comparison.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))

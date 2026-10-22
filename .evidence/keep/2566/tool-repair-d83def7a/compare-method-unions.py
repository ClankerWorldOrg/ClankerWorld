from pathlib import Path
import json,hashlib,sys
sys.path.insert(0,'skills/test-audit/scripts')
from coverage_evidence import measurement_report,source_key
from per_test_coverage import slug
out=Path('.evidence/2566-tool-repair')
def union(folder):
 found={}
 for method in json.loads((folder/'methods.json').read_text()):
  lane=folder/slug(method);assert json.loads((lane/'completion.json').read_text())=={'method':method,'success':True}
  xml=measurement_report(lane);sources=[x.text for x in xml.findall('./sources/source') if x.text]
  for package in xml.findall('./packages/package'):
   for cls in package.findall('./classes/class'):
    path=source_key(cls.attrib['filename'],sources)
    for line in cls.findall('./lines/line'):
     key=(package.attrib['name'],path,int(line.attrib['number']));covered=int(line.attrib['hits'])>0;branches=int(line.attrib.get('condition-coverage','(0/0)').split('(')[-1].split('/')[0]);old=found.get(key,(False,0));found[key]=(old[0] or covered,max(old[1],branches))
 return found
before,after=union(out/'per-method-before'),union(out/'per-method-retained');assert before.keys()==after.keys();report={}
for package in sorted({k[0] for k in before}):
 keys=[k for k in before if k[0]==package];report[package]={'before_lines':sum(before[k][0] for k in keys),'after_lines':sum(after[k][0] for k in keys),'lost_lines':[list(k[1:]) for k in keys if before[k][0] and not after[k][0]],'reduced_mapped_branch_lower_bounds':[list(k[1:])+[before[k][1],after[k][1]] for k in keys if before[k][1]>after[k][1]]}
(out/'per-method-union-comparison.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
for label,folder in [('base',Path('tests/ClankerWorld.Tests/bin/Release/net10.0-tool-repair-base-source')),('head',Path('tests/ClankerWorld.Tests/bin/Release/net10.0'))]:
 for name,h in json.loads((out/(label+'-pins.json')).read_text()).items():assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==h,(label,name)
print('All six DLL/PDB hashes restored for each build')

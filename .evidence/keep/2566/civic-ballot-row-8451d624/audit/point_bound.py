from pathlib import Path
import json,sys,hashlib,xml.etree.ElementTree as ET

def normalized(path):
 s=path.replace('\\','/')
 assert '/src/' in s,s
 return 'src/'+s.split('/src/',1)[1]

def read(folder):
 reports=list(Path(folder).rglob('coverage.json'));assert reports
 assert len({hashlib.sha256(p.read_bytes()).hexdigest() for p in reports})==1,('conflicting JSON attachments',reports)
 report=min(reports,key=lambda p:len(p.parts))
 data=json.loads(report.read_text());out={}
 for module,docs in data.items():
  name=Path(module).name.removesuffix('.dll');assert name not in out,('duplicate normalized assembly',name)
  lines={};branches={}
  for doc,classes in docs.items():
   filename=normalized(doc)
   for cls,methods in classes.items():
    for method,points in methods.items():
     for line,hits in points['Lines'].items():
      key=(filename,int(line));lines[key]=lines.get(key,False) or hits>0
     for branch in points['Branches']:
      fields=('Line','Offset','EndOffset','Path','Ordinal')
      assert set(branch)==set(fields)|{'Hits'},set(branch)
      key=(filename,cls,method)+tuple(branch[f] for f in fields)
      assert key not in branches,key
      branches[key]=branch['Hits']>0
  out[name]={'lines':lines,'branches':branches}
 xmls=list(Path(folder).rglob('coverage.cobertura.xml'));assert xmls
 assert len({hashlib.sha256(p.read_bytes()).hexdigest() for p in xmls})==1,('conflicting XML attachments',xmls)
 xml=min(xmls,key=lambda p:len(p.parts))
 x=ET.parse(xml).getroot()
 sys.path.insert(0,'/workspace/ClankerWorld-2566-civic-ballot-row/skills/test-audit/scripts')
 from compare_coverage import load_report
 parsed=load_report(xml);assert set(parsed)==set(out),('JSON/XML assembly scope mismatch',set(parsed),set(out))
 for assembly,locations in parsed.items():
  actual=out[assembly]
  assert actual['lines']=={k:v['hit'] for k,v in locations.items()},(assembly,'JSON/XML source line/hit mismatch')
  for package in x.findall('./packages/package'):
   if package.get('name')!=assembly:continue
   branches=actual['branches']
   rate=sum(branches.values())/len(branches) if branches else 0
   assert abs(rate-float(package.get('branch-rate')))<=0.0001,(assembly,'JSON/XML branch rate mismatch')
 # JSON's complete point identities must account for the collector's counters,
 # including branches omitted from source-line mappings by Cobertura.
 for dimension,xmlprefix in [('lines','lines'),('branches','branches')]:
  assert sum(len(v[dimension]) for v in out.values())==int(x.get(xmlprefix+'-valid')),(dimension,'valid')
  assert sum(sum(v[dimension].values()) for v in out.values())==int(x.get(xmlprefix+'-covered')),(dimension,'covered')
 return out

def compare(before,after):
 assert set(before)==set(after),'assembly scope changed'
 result={};totals={k:dict(valid=0,before=0,after=0,lost=0,gained=0) for k in ['lines','branches']}
 for assembly in sorted(before):
  r={}
  for dimension in ['lines','branches']:
   a=before[assembly][dimension];b=after[assembly][dimension]
   assert a.keys()==b.keys(),(assembly,dimension,'identity scope changed')
   lost=[k for k in a if a[k] and not b[k]];gained=[k for k in a if b[k] and not a[k]]
   d=dict(valid=len(a),before=sum(a.values()),after=sum(b.values()),lost=len(lost),gained=len(gained),lost_points=lost,gained_points=gained)
   d['loss_bound_pp']=100*len(lost)/len(a) if a else 0
   d['relative_loss_bound_percent']=100*len(lost)/sum(a.values()) if any(a.values()) else None
   assert d['loss_bound_pp']<=2,(assembly,dimension,d['loss_bound_pp'])
   r[dimension]=d
   for key in totals[dimension]:totals[dimension][key]+=d[key]
  result[assembly]=r
 for d in totals.values():
  d['loss_bound_pp']=100*d['lost']/d['valid'] if d['valid'] else 0
  d['relative_loss_bound_percent']=100*d['lost']/d['before'] if d['before'] else None
  assert d['loss_bound_pp']<=2
 result['total']=totals
 return result

if __name__=='__main__':
 result=compare(read(sys.argv[1]),read(sys.argv[2]));Path(sys.argv[3]).write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({a:{d:{k:v for k,v in r.items() if not k.endswith('_points')} for d,r in scopes.items()} for a,scopes in result.items()},indent=2))

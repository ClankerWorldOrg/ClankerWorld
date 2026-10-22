from pathlib import Path
import json,zipfile,xml.etree.ElementTree as E,hashlib,shutil,sys
out=Path(__file__).resolve().parent
label=sys.argv[1] if len(sys.argv)>1 else 'attempt1'
records=json.loads((out/('head-ci-artifacts-'+label+'.json')).read_text())
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
prefixes=tuple('ClankerWorld.Tests.'+name+'.' for name in ['CognitionTests','ModelNeedWordsTests','MemorySummaryProviderTests'])
cases=[]
def secs(s):
 h,m,x=s.split(':');return int(h)*3600+int(m)*60+float(x)
assert len(records)==8 and len({a['name'] for a in records})==8
for a in records:
 p=Path(a['download']['path']);assert hashlib.sha256(p.read_bytes()).hexdigest()==a['digest'].split(':')[-1]
 target=out/'ci-artifacts'/label/p.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
 with zipfile.ZipFile(p) as z:
  for name in z.namelist():
   if not name.endswith('.trx'):continue
   root=E.fromstring(z.read(name));defs={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in root.findall('.//t:UnitTest',ns)}
   for x in root.findall('.//t:UnitTestResult',ns):
    d=defs[x.attrib['testId']];method=d['className']+'.'+d['name']
    cases.append({'method':method,'outcome':x.attrib['outcome'],'seconds':secs(x.attrib['duration'])})
affected=[c for c in cases if c['method'].startswith(prefixes)]
summary={'run':39725655221,'artifact_set':label,'cases':len(cases),'methods':len({c['method'] for c in cases}),'passed':sum(c['outcome']=='Passed' for c in cases),'skipped':sum(c['outcome']=='NotExecuted' for c in cases),'other_outcomes':sorted({c['outcome'] for c in cases}-{'Passed','NotExecuted'}),'seconds':sum(c['seconds'] for c in cases),'affected_cases':len(affected),'affected_methods':len({c['method'] for c in affected}),'affected_seconds':sum(c['seconds'] for c in affected),'affected':affected,'failed':[c for c in cases if c['outcome'] not in ['Passed','NotExecuted']]}
(out/('head-ci-summary-'+label+'.json')).write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:v for k,v in summary.items() if k!='affected'}))

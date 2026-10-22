from pathlib import Path
import json,zipfile,xml.etree.ElementTree as E,hashlib,shutil
out=Path('.evidence/2566-tool-repair');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def secs(s):
 h,m,x=s.split(':');return int(h)*3600+int(m)*60+float(x)
for label,run in [('base',39712166176),('head',39722452399)]:
 cases=[];methods=set();affected=[]
 metadata={a['name']:a for a in json.loads((out/'ci-artifact-metadata.json').read_text())[label]}
 for a in json.loads((out/(label+'-ci-artifact-paths.json')).read_text()):
  p=Path(a['path']);assert hashlib.sha256(p.read_bytes()).hexdigest()==metadata[a['name']]['digest'].split(':')[-1]
  archive=out/'ci-artifacts'/label/p.name;archive.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,archive)
  with zipfile.ZipFile(p) as z:
   for name in z.namelist():
    if not name.endswith('.trx'):continue
    root=E.fromstring(z.read(name));defs={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in root.findall('.//t:UnitTest',ns)}
    for x in root.findall('.//t:UnitTestResult',ns):
     d=defs[x.attrib['testId']];method=d['className']+'.'+d['name'];methods.add(method);c={'method':method,'outcome':x.attrib['outcome'],'seconds':secs(x.attrib['duration'])};cases.append(c)
     if method.startswith('ClankerWorld.Tests.ToolProgressionRuntimeTests.'):affected.append(c)
 summary={'run':run,'cases':len(cases),'methods':len(methods),'passed':sum(c['outcome']=='Passed' for c in cases),'skipped':sum(c['outcome']=='NotExecuted' for c in cases),'other_outcomes':sorted({c['outcome'] for c in cases}-{'Passed','NotExecuted'}),'seconds':sum(c['seconds'] for c in cases),'affected_cases':len(affected),'affected_methods':len({c['method'] for c in affected}),'affected_seconds':sum(c['seconds'] for c in affected),'affected':affected}
 (out/(label+'-ci-summary.json')).write_text(json.dumps(summary,indent=2));print(label,json.dumps({k:v for k,v in summary.items() if k!='affected'}))

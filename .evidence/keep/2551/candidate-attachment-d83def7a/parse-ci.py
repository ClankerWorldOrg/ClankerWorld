from pathlib import Path
import json,hashlib,zipfile,shutil,collections,xml.etree.ElementTree as E,sys
out=Path('.evidence/2551-attachment');label=sys.argv[1];items=json.loads((out/(label+'-ci-downloads.json')).read_text());ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};cases=[];scope=(out/'focused.filter').read_text().strip();classes={v.split('~')[1].rstrip('.') for v in scope.split('|')};target=out/'ci-artifacts'/label;target.mkdir(parents=True,exist_ok=True)
for item in items:
 a=item['artifact'];p=Path(item['download']['path']);assert hashlib.sha256(p.read_bytes()).hexdigest()==a['digest'].split(':')[1];shutil.copyfile(p,target/(a['name']+'.zip'))
 with zipfile.ZipFile(p) as z:
  names=[n for n in z.namelist() if n.endswith('.trx')];assert len(names)==1,names;t=E.fromstring(z.read(names[0]));methods={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)}
  for x in t.findall('.//t:UnitTestResult',ns):
   m=methods[x.attrib['testId']];d=x.attrib.get('duration','0:0:0').split(':');seconds=int(d[0])*3600+int(d[1])*60+float(d[2]);cases.append({'method':m['className']+'.'+m['name'],'class':m['className'],'name':x.attrib['testName'],'outcome':x.attrib['outcome'],'seconds':seconds})
assert len(items)==8
focused=[x for x in cases if x['class'] in classes]
def summary(xs):return {'cases':len(xs),'methods':len({x['method'] for x in xs}),'outcomes':dict(collections.Counter(x['outcome'] for x in xs)),'seconds':sum(x['seconds'] for x in xs)}
result={'label':label,'artifacts':len(items),'all_digests_verified':True,'all':summary(cases),'focused':summary(focused)};assert not any(x['outcome']=='Failed' for x in cases);(out/(label+'-ci-summary.json')).write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))

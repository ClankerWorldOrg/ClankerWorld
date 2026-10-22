from pathlib import Path
import json, hashlib, re, collections, xml.etree.ElementTree as E, subprocess
A=Path('/workspace/2566-audit/content-fixtures'); S=json.loads((A/'scope.json').read_text()); W=[Path(S['base_checkout']),Path(S['head_checkout'])]; N={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def norm(p):
 p=str(p).replace('\\','/')
 for marker in ['/src/','/tests/']:
  if marker in p:return marker[1:]+p.split(marker,1)[1]
 return p

def single(root,name):
 paths=list(root.rglob(name));assert paths
 assert len({sha(x) for x in paths})==1,(root,name,'different copies')
 return min(paths,key=lambda p:len(p.parts))
def xmlmap(p):
 r=E.parse(p).getroot();sources=[x.text for x in r.findall('./sources/source')];out={};mapped=[0,0]
 for pkg in r.findall('./packages/package'):
  asm=pkg.get('name')
  for c in pkg.findall('./classes/class'):
   doc=c.get('filename');doc=norm(doc if doc.startswith('/') else str(Path(sources[0])/doc))
   for l in c.findall('./lines/line'):
    k=(asm,doc,int(l.get('number')));out[k]=out.get(k,False) or int(l.get('hits'))>0
    cc=l.get('condition-coverage')
    if cc:
     a,b=map(int,re.search(r'\((\d+)/(\d+)\)',cc).groups());mapped[0]+=a;mapped[1]+=b
 return r,out,mapped

def raw(root):
 jp=single(root,'coverage.json');xp=single(root,'coverage.cobertura.xml');j=json.loads(jp.read_text());lines={};branches={};asmstats={}
 for module,docs in j.items():
  asm=Path(module).name.removesuffix('.dll');assert asm not in asmstats;asmstats[asm]=None
  for doc,classes in docs.items():
   doc=norm(doc)
   for cls,methods in classes.items():
    for method,v in methods.items():
     for no,h in v['Lines'].items():
      k=(asm,doc,int(no));lines[k]=lines.get(k,False) or h>0
     for b in v['Branches']:
      k=(asm,doc,cls,method,*[b[t] for t in ['Line','Offset','EndOffset','Path','Ordinal']]);assert k not in branches;branches[k]=b['Hits']>0
 r,xlines,mapped=xmlmap(xp);assert lines==xlines
 assert (len(lines),sum(lines.values()),len(branches),sum(branches.values()))==tuple(int(r.get(k)) for k in ['lines-valid','lines-covered','branches-valid','branches-covered'])
 for asm in asmstats:
  ls=[v for k,v in lines.items() if k[0]==asm];bs=[v for k,v in branches.items() if k[0]==asm];asmstats[asm]={'lines':[len(ls),sum(ls)],'branches':[len(bs),sum(bs)]}
 return lines,branches,asmstats,mapped,{'json':sha(jp),'xml':sha(xp)}
def trx(root):
 p=single(root,'tests.trx') if list(root.rglob('tests.trx')) else single(root,'test.trx');r=E.parse(p).getroot();tests={}
 for t in r.findall('.//t:UnitTest',N):
  m=t.find('t:TestMethod',N);tests[t.get('id')]=m.get('className').split(',')[0]+'.'+m.get('name')
 rows=r.findall('.//t:UnitTestResult',N);assert rows and all(t.get('outcome')=='Passed' for t in rows)
 return collections.Counter((t.get('testName'),t.get('outcome')) for t in rows),collections.Counter(tests[t.get('testId')] for t in rows),r.find('.//t:Counters',N).attrib

import hashlib,json,pathlib,xml.etree.ElementTree as ET
p=pathlib.Path(__file__).parent

def read(folder):
 f=next((p/folder).rglob('coverage.json')); data=json.loads(f.read_text()); result={}
 for assembly,documents in data.items():
  lines={}; branches={}
  for document,classes in documents.items():
   doc='src/'+document.split('/src/',1)[1]
   for cls,methods in classes.items():
    for method,body in methods.items():
     for line,hits in body['Lines'].items():
      key=(doc,cls,method,int(line)); assert key not in lines;lines[key]=hits>0
     for branch in body['Branches']:
      key=(doc,cls,method,*(branch[k] for k in ['Line','Offset','EndOffset','Path','Ordinal']))
      assert key not in branches;branches[key]=branch['Hits']>0
  result[assembly]={'lines':lines,'branches':branches}
 xml=ET.parse(next((p/folder).rglob('coverage.cobertura.xml'))).getroot()
 for dim in ['lines','branches']:
  assert sum(len(v[dim]) for v in result.values())==int(xml.attrib[dim+'-valid'])
  assert sum(sum(v[dim].values()) for v in result.values())==int(xml.attrib[dim+'-covered'])
 return result
before,after=read('before'),read('final'); assert before.keys()==after.keys(); report={}
for assembly in before:
 report[assembly]={}
 for dim in ['lines','branches']:
  a,b=before[assembly][dim],after[assembly][dim]; assert a.keys()==b.keys(),(assembly,dim,'scope differs')
  lost=[list(k) for k in a if a[k] and not b[k]]; gained=[list(k) for k in a if not a[k] and b[k]]
  report[assembly][dim]={'valid':len(a),'covered_before':sum(a.values()),'covered_after':sum(b.values()),'lost':lost,'gained':gained,'gross_loss_pp':100*len(lost)/len(a)}
report['validation']='All JSON identity counts and covered counts equal authoritative Cobertura root counters, including branches without mapped source lines; scope identities equal in both runs. No new hits offset losses.'
(p/'identity-comparison.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

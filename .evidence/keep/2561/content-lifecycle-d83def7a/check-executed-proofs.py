from pathlib import Path
import json,collections,xml.etree.ElementTree as E
p=Path('.evidence/2561-content');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
proofs=set((p/'retained-proofs.txt').read_text().splitlines());summary={}
for label,folder in [('base','before'),('head','after')]:
 t=E.parse(p/folder/'tests.trx').getroot();methods={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)};counts=collections.Counter()
 for case in t.findall('.//t:UnitTestResult',ns):
  assert case.attrib['outcome']=='Passed',case.attrib
  m=methods[case.attrib['testId']];counts[m['className']+'.'+m['name']]+=1
 assert sum(counts.values())==175 and len(counts)==82,(sum(counts.values()),len(counts))
 assert proofs<=counts.keys(),proofs-counts.keys()
 proofcounts={m:counts[m] for m in sorted(proofs)};assert sum(proofcounts.values())==7,proofcounts
 summary[label]={'cases':sum(counts.values()),'methods':len(counts),'retained_proof_cases':proofcounts,'all_passed':True}
 if label=='base':before=counts
 else:assert before==counts
(p/'executed-proof-check.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))

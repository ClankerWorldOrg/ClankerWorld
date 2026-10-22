from pathlib import Path
import json, hashlib, re, collections, xml.etree.ElementTree as E, subprocess
A=Path('/workspace/2566-audit/animal-restores'); S=json.loads((A/'scope.json').read_text()); W=[Path(S['base_checkout']),Path(S['head_checkout'])]; N={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
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
import datetime, os
os.nice(10)
F='2566-animal-restores';R=[];M=[];T=[]
for w,side in zip(W,['before','final']):
 folder=w/'.evidence/keep'/F;m=json.loads((folder/(side+'-run.json')).read_text());assert m['exit_code']==0 and m['head']==S['base' if side=='before' else 'head']
 assert m['args'][m['args'].index('--filter')+1]==S['filter'];assert m['settings_sha256']==sha(w/'skills/test-audit/references/coverage.runsettings');assert m['filter_sha256']==hashlib.sha256(S['filter'].encode()).hexdigest()
 for h in m['binary_hashes']:assert sha(h['source'])==h['sha256']
 rows,methods,c=trx(folder/side);expected=S['expected_cases'][side];assert sum(rows.values())==expected and len(methods)==63 and set(methods)==set(S['selected_methods_by_side'][side]);assert c==m['counters'] and int(c['total'])==int(c['passed'])==expected and int(c['failed'])==int(c['notExecuted'])==0;M.append(m);T.append((rows,methods));R.append(raw(folder/side))
for k in ['DOTNET_PROCESSOR_COUNT','nice','settings_sha256','filter_sha256']:assert M[0][k]==M[1][k]
assert M[0]['DOTNET_PROCESSOR_COUNT']==4 and M[0]['nice']==10
args=lambda m:[('RESULTS' if i==m['args'].index('--results-directory')+1 else a) for i,a in enumerate(m['args'])]
assert args(M[0])==args(M[1]) and 'xUnit.MaxParallelThreads=2' in args(M[0])
prod=lambda m:{Path(x['source']).name:x['sha256'] for x in m['binary_hashes'] if 'Tests.' not in Path(x['source']).name}
assert prod(M[0])==prod(M[1])==json.loads((A/'canonical-ready.json').read_text()) and len(prod(M[0]))==4
for h in M[0]['binary_hashes']:assert sha(W[0]/'tests/ClankerWorld.Tests/bin/Release/net10.0.animal-restores-original'/Path(h['source']).name)==h['sha256']
head_prod={x.name:sha(x) for x in (A/'binaries/head-production').iterdir() if x.suffix in ['.dll','.pdb']};assert len(head_prod)==4
# Independently compiled production may differ in path-sensitive PDBs; both measured runs use the pinned canonical four above.
def discover(p):return re.findall(r'^\s+(ClankerWorld\.Tests\..+?)\s*$',p.read_text(),re.M)
d0=discover(A/'base-discovery.log');d1=discover(A/'head-discovery.log');removed='ClankerWorld.Tests.AnimalPipelineTests.MilkCollectionNeedsRoomForTheWholeBatchButNoVessel(stone: 5, expected: True)'
assert d0.count(removed)==1 and [n for n in d0 if n!=removed]==d1 and [len(d0),len(d1)]==[7396,7395] and len({n.split('(')[0] for n in d0})==len({n.split('(')[0] for n in d1})==3295
# TRX uses display names with or without the namespace; normalize only that prefix.
row0=collections.Counter((n.removeprefix('ClankerWorld.Tests.'),o) for (n,o),v in T[0][0].items() for _ in range(v));row1=collections.Counter((n.removeprefix('ClankerWorld.Tests.'),o) for (n,o),v in T[1][0].items() for _ in range(v))
r=(removed.removeprefix('ClankerWorld.Tests.'),'Passed');assert row0[r]==1;row0.subtract({r:1});row0=+row0;assert row0==row1
for name in ['base-build.log','head-build.log']:
 text=(A/name).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
source=json.loads((A/'reader-source.json').read_text())
assert hashlib.sha256(subprocess.check_output(['git','diff',S['base'],S['head']],cwd=W[1])).hexdigest()==source['patch_sha256']
for e in json.loads((A/'reader-scope.json').read_text())['files']:
 assert sha(W[0]/e['file'])==e['base_sha256'] and sha(W[1]/e['file'])==e['head_sha256']
ledger=json.loads((A/'ledger.json').read_text());assert len(ledger)==63 and sorted(e['test'] for e in ledger)==sorted(S['ledger_proofs_by_side']['before']) and all(p in S['ledger_proofs_by_side']['final'] for e in ledger for p in e['retained_proof'])
rawdiff={}
assert R[0][0].keys()==R[1][0].keys() and R[0][1].keys()==R[1][1].keys()
for asm in list(R[0][2])+['total']:
 metrics={}
 for i,dimension in enumerate(['lines','branches']):
  a={k:v for k,v in R[0][i].items() if asm=='total' or k[0]==asm};b={k:v for k,v in R[1][i].items() if asm=='total' or k[0]==asm};lost=sorted(k for k,v in a.items() if v and not b[k]);gained=sorted(k for k,v in b.items() if v and not a[k]);metrics[dimension]={'valid':len(a),'before':sum(a.values()),'after':sum(b.values()),'lost':len(lost),'gained':len(gained),'lost_points':lost,'gained_points':gained,'loss_bound_pp':100*len(lost)/len(a) if a else 0,'relative_loss_bound_percent':100*len(lost)/sum(a.values()) if any(a.values()) else None}
 rawdiff[asm]=metrics
progress={'signature':'Codex session8451d624/animal-restores-raw-reader','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'role':'Read-only author-side ownership second reader; not independent PR approval','base':S['base'],'head':S['head'],'matched_pair_validated':True,'scope':{'filter':S['filter'],'before_passed':161,'head_passed':160,'methods_each':63,'partial_files':31,'skips':0,'only_removed_case':removed,'all_named_proofs_present':True},'raw_identity_recomputation':rawdiff,'mapped_xml_branch_integer_counts':{'before':R[0][3],'head':R[1][3]},'unmapped_branch_integer_counts':{side:{'valid':len(R[i][1])-R[i][3][1],'covered':sum(R[i][1].values())-R[i][3][0]} for i,side in enumerate(['before','head'])},'equivalence':{'same_filter_settings_scheduling':True,'canonical_four':prod(M[0]),'all_six_normal_binary_archive_hashes_each_match_receipt':True,'original_base_normal_deployment_matches_receipt':True,'independently_built_head_production_preserved':head_prod,'normal_archive_pipeline_read':True,'one_actual_matched_pair_each':True},'discovery':{'before':7396,'head':7395,'methods_each':3295,'only_expected_row_removed_all_other_order_equal':True},'normal_builds':{'both_succeeded':True,'warnings_each':0,'errors_each':0},'source_still_matches_acceptance':True,'remaining_local_receipts':[],'findings':[]}
missing=[n for n in ['local-complete.json','unused-comparison.json','point-bound.json','unique.tsv','unique-plan.json'] if not (A/n).exists()]
for w,side in zip(W,['before','after']):
 if not (w/'.evidence/keep'/F/(side+'-unused.json')).exists():missing.append(side+'-unused.json')
progress['remaining_local_receipts']=missing;(A/'reader-progress.json').write_text(json.dumps(progress,indent=2)+'\n')
if missing and (len(list((A/'isolated').glob('*/completion.json')))!=63 or not (A/'unique.tsv').exists() or not (A/'unique-plan.json').exists()):
 print(json.dumps({'pair_valid':True,'totals':rawdiff['total'],'remaining':missing}));raise SystemExit(0)
iso={};sets={};isolated_case_rows=collections.Counter()
for d in (A/'isolated').iterdir():
 if not d.is_dir():continue
 receipt=json.loads((d/'completion.json').read_text());assert receipt['success'] and receipt['method']==d.name
 rows,methods,c=trx(d);isolated_case_rows.update(rows);assert list(methods)==[d.name] and c['failed']=='0' and int(c['notExecuted'])==0;_,ls,_=xmlmap(single(d,'coverage.cobertura.xml'));sets[d.name]={k for k,v in ls.items() if v};iso[d.name]={'passed':sum(rows.values()),'covered_lines':len(sets[d.name])}
assert set(iso)==set(S['selected_methods_by_side']['before']) and sum(x['passed'] for x in iso.values())==161
assert all(v['passed']==T[0][1][n] for n,v in iso.items()), 'isolated row count differs from actual original scoped TRX'
assert isolated_case_rows==T[0][0], 'original isolated case identities differ from actual original scoped TRX'
for n,pts in sets.items():iso[n]['unique_lines']=len(pts-set.union(*(v for k,v in sets.items() if k!=n)))
for line in (A/'unique.tsv').read_text().splitlines()[1:]:
 v=line.split('\t');assert v[0] in iso and int(v[1])==iso[v[0]]['covered_lines'] and int(v[2])==iso[v[0]]['unique_lines']
planner=json.loads((A/'unique-plan.json').read_text());assert planner['measured_tests']==planner['baseline']['tests']==planner['after_plan']['tests']==63 and not planner['zero_loss_removable'];assert set((A/'keep.txt').read_text().splitlines())==set(iso)
union=set.union(*sets.values());assert planner['baseline']['lines_covered']==len(union) and planner['baseline']['lines_valid']==len(R[0][0]);scoped={k for k,v in R[0][0].items() if v};unioncomparison={'original_union_lines':len(union),'matched_before_scope_lines':len(scoped),'only_in_original_individual_measurements':sorted(union-scoped),'only_in_matched_before_scope':sorted(scoped-union)}
print('Original63 reports and planner independently reconciled; waiting only for final local receipts.',flush=True)
import time
while not all((A/name).exists() for name in ['local-complete.json','unused-comparison.json','point-bound.json']):
 time.sleep(2)
unused=[];warningstats=[]
for w,side in zip(W,['before','after']):
 p=w/'.evidence/keep'/F;m=json.loads((p/(side+'-unused.json')).read_text());assert m['exit_code']==0
 text=(p/(side+'-unused.log')).read_text();unused.append(collections.Counter((norm(path),code,msg) for path,code,msg in re.findall(r'^(.+?\.cs)\(\d+,\d+\): warning (IDE(?:0051|0052|0060|0005)): (.+?) \[',text,re.M)));warningstats.append({'total_reported':int(re.findall(r'(\d+) Warning\(s\)',text)[-1]),'raw_CS1591_occurrences_including_repeated_summary':len(re.findall('warning CS1591:',text))});assert not (w/'tests/ClankerWorld.Tests/.editorconfig').exists()
assert unused[0]==unused[1] and warningstats[0]==warningstats[1]
uc=json.loads((A/'unused-comparison.json').read_text());assert uc['before_records']==uc['after_records']==sum(unused[0].values()) and uc['before_normalized']==uc['after_normalized']==len(unused[0]) and not uc['new'] and not uc['removed']
comp=json.loads((W[1]/'.evidence/keep'/F/'comparison.json').read_text());bound=json.loads((A/'point-bound.json').read_text())
for asm,value in bound.items():
 for metric in ['lines','branches']:
  for key in ['valid','before','after','lost','gained','loss_bound_pp','relative_loss_bound_percent']:assert value[metric][key]==rawdiff[asm][metric][key],(asm,metric,key)
  if asm!='total':
   for key in ['lost_points','gained_points']:assert value[metric][key]==[list(p[1:]) for p in rawdiff[asm][metric][key]]
for v in comp.values():assert not v.get('lost_lines',[]) and not v.get('reduced_branch_lines',[]) and v['line_delta_pp']==v['branch_delta_pp']==0
assert all(v[d]['lost']==0 for v in rawdiff.values() for d in ['lines','branches']), 'loss needs separately reviewed attribution/retained proof'
complete=json.loads((A/'local-complete.json').read_text());assert complete['head']==S['head'] and complete['success']
pause=json.loads((A/'comparison-waiter-pause.json').read_text())
assert pause['head_unused_exit']==pause['real_base_unused_exit']==0 and pause.get('resumed_utc')
assert datetime.datetime.fromisoformat(M[1]['finished_utc']) < datetime.datetime.fromisoformat(pause['paused_utc']) < datetime.datetime.fromisoformat(pause['resumed_utc'])
base_unused_meta=json.loads((W[0]/'.evidence/keep'/F/'before-unused.json').read_text());assert datetime.datetime.fromisoformat(base_unused_meta['finished_utc']) <= datetime.datetime.fromisoformat(pause['resumed_utc'])
progress['bookkeeping_waiter_pause']={'metadata':pause,'guard_reviewed':'Targets one final Python pipeline by exact script/side args, verifies head diagnostic success and no child processes, pauses only idle comparison wait, then resumes only after real baseline unused exit0. No measurements paused/restarted; measured pair metadata unchanged.'}
progress.update({'accepted_local':True,'remaining_local_receipts':[],'original_individual_measurements':iso,'original_union_vs_matched_before_scope':unioncomparison,'unused':{'records_each':sum(unused[0].values()),'normalized_groups_each':len(unused[0]),'new':[],'removed':[],'warnings_each':warningstats[0],'temporary_configs_absent':True},'official_comparator_and_exact_identity_bound_agree':True,'conservative_loss_upper_bound_pp_without_gain_offset':{a:{d:v[d]['loss_bound_pp'] for d in ['lines','branches']} for a,v in rawdiff.items()},'remaining_conditional_gates':['Exact-head CI/readiness parent-owned. Independent PR review remains another top-level session.'],'limits':'Reader ran no builds/tests/coverage/GitHub writes. All63 original methods actually measured separately on pristine original normal deployment. No full-suite coverage or independent whole-suite execution claimed; raw scoped loss bounds do not offset losses with gains and are independent of per-method union.'})
(A/'reader-raw.json').write_text(json.dumps(progress,indent=2)+'\n');print(json.dumps({'accepted_local':True,'report':str(A/'reader-raw.json'),'isolated_methods':len(iso),'total':rawdiff['total'],'unused':progress['unused']},indent=2))

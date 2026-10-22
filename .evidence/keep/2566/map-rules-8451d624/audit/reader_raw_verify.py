from pathlib import Path
import json, hashlib, re, collections, xml.etree.ElementTree as E, subprocess
A=Path('/workspace/2566-audit/map-rules'); S=json.loads((A/'scope.json').read_text()); W=[Path(S['base_checkout']),Path(S['head_checkout'])]; N={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
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
F='2566-map-rules';R=[];M=[];T=[]
assert json.loads((A/'local-complete.json').read_text())['success']
for w,side in zip(W,['before','final']):
 folder=w/'.evidence/keep'/F;m=json.loads((folder/(side+'-run.json')).read_text());assert m['exit_code']==0 and m['head']==S['base' if side=='before' else 'head']
 assert m['args'][m['args'].index('--filter')+1]==S['filter'];assert m['settings_sha256']==sha(w/'skills/test-audit/references/coverage.runsettings');assert m['filter_sha256']==hashlib.sha256(S['filter'].encode()).hexdigest()
 for h in m['binary_hashes']:assert sha(h['source'])==h['sha256']
 rows,methods,c=trx(folder/side);assert sum(rows.values())==20 and len(methods)==16 and sorted(methods.values())==[1]*13+[2,2,3] and set(methods)==set(S['selected_methods_by_side'][side]);assert c==m['counters'] and int(c['total'])==int(c['passed'])==20 and int(c['failed'])==int(c['notExecuted'])==0;M.append(m);T.append((rows,methods));R.append(raw(folder/side))
for k in ['DOTNET_PROCESSOR_COUNT','nice','settings_sha256','filter_sha256']:assert M[0][k]==M[1][k]
assert M[0]['DOTNET_PROCESSOR_COUNT']==4 and M[0]['nice']==10
args=lambda m:[('RESULTS' if i==m['args'].index('--results-directory')+1 else a) for i,a in enumerate(m['args'])]
assert args(M[0])==args(M[1]) and 'xUnit.MaxParallelThreads=2' in args(M[0])
prod=lambda m:{Path(x['source']).name:x['sha256'] for x in m['binary_hashes'] if 'Tests.' not in Path(x['source']).name}
assert prod(M[0])==prod(M[1])==json.loads((A/'canonical-ready.json').read_text()) and len(prod(M[0]))==4
for h in M[0]['binary_hashes']:assert sha(W[0]/'tests/ClankerWorld.Tests/bin/Release/net10.0.map-rules-original'/Path(h['source']).name)==h['sha256']
headprod={p.name:sha(p) for p in (A/'binaries/head-production').iterdir() if p.suffix in ['.dll','.pdb']};assert len(headprod)==4
discover=lambda p:re.findall(r'^\s+(ClankerWorld\.Tests\..+?)\s*$',p.read_text(),re.M)
d0=discover(A/'base-discovery.log');d1=discover(A/'head-discovery.log');assert len(d0)==len(d1) and [S['discovery_renames'].get(n,n) for n in d0]==d1
assert all(d0.count(n)==1 and d1.count(new)==1 for n,new in S['discovery_renames'].items()) and len(S['discovery_renames'])==1
assert {n.split('(')[0] for n in d0}=={n.split('(')[0] for n in d1}
for side,ds in [('before',d0),('final',d1)]:
 selected=[n for n in ds if n.split('(')[0] in S['selected_methods_by_side'][side]];assert len(selected)==20 and {n.split('(')[0] for n in selected}==set(S['ledger_proofs_by_side'][side])
normalized0=collections.Counter()
rename_short={k.removeprefix('ClankerWorld.Tests.'):v.removeprefix('ClankerWorld.Tests.') for k,v in S['discovery_renames'].items()}
for (name,outcome),n in T[0][0].items():
 short=name.removeprefix('ClankerWorld.Tests.');normalized0[(rename_short.get(short,short),outcome)]+=n
assert normalized0==collections.Counter({(name.removeprefix('ClankerWorld.Tests.'),outcome):n for (name,outcome),n in T[1][0].items()})
for name in ['base-build.log','head-build.log']:
 text=(A/name).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
source=json.loads((A/'reader-source.json').read_text());assert source['head']==S['head'] and source['accepted_source']
for f in source['files']:assert sha(W[0]/f['file'])==f['base_sha256'] and sha(W[1]/f['file'])==f['head_sha256']
assert hashlib.sha256(subprocess.check_output(['git','diff',S['base'],S['head']],cwd=W[1])).hexdigest()==source['patch_sha256']
assert set(subprocess.check_output(['git','diff','--name-only',S['base'],S['head']],cwd=W[1],text=True).splitlines())=={f['file'] for f in source['files']}
ledger=json.loads((A/'ledger.json').read_text());assert len(ledger)==16 and sorted(e['test'] for e in ledger)==sorted(S['ledger_proofs_by_side']['before']) and all(p in S['ledger_proofs_by_side']['final'] for e in ledger for p in e['retained_proof'])
rawdiff={};assert R[0][0].keys()==R[1][0].keys() and R[0][1].keys()==R[1][1].keys()
for asm in list(R[0][2])+['total']:
 metrics={}
 for i,dimension in enumerate(['lines','branches']):
  a={k:v for k,v in R[0][i].items() if asm=='total' or k[0]==asm};b={k:v for k,v in R[1][i].items() if asm=='total' or k[0]==asm};lost=sorted(k for k,v in a.items() if v and not b[k]);gained=sorted(k for k,v in b.items() if v and not a[k]);metrics[dimension]={'valid':len(a),'before':sum(a.values()),'after':sum(b.values()),'lost':len(lost),'gained':len(gained),'lost_points':lost,'gained_points':gained,'loss_bound_pp':100*len(lost)/len(a) if a else 0,'relative_loss_bound_percent':100*len(lost)/sum(a.values()) if any(a.values()) else None}
 rawdiff[asm]=metrics
iso={};sets={};isolated_rows=collections.Counter()
for d in (A/'isolated').iterdir():
 if not d.is_dir():continue
 receipt=json.loads((d/'completion.json').read_text());assert receipt=={'method':d.name,'success':True} and (d/'test.txt').read_text().strip()==d.name
 rows,methods,c=trx(d);assert list(methods)==[d.name] and methods[d.name]==T[0][1][d.name];isolated_rows.update(rows);_,ls,_=xmlmap(single(d,'coverage.cobertura.xml'));sets[d.name]={k for k,v in ls.items() if v};iso[d.name]={'passed':sum(rows.values()),'covered_lines':len(sets[d.name]),'xml_sha256':sha(single(d,'coverage.cobertura.xml')),'trx_sha256':sha(single(d,'test.trx'))}
assert set(iso)==set(S['selected_methods_by_side']['before']) and isolated_rows==T[0][0] and sum(v['passed'] for v in iso.values())==20
for n,pts in sets.items():iso[n]['unique_lines']=len(pts-set.union(*(v for k,v in sets.items() if k!=n)))
for line in (A/'unique.tsv').read_text().splitlines()[1:]:
 v=line.split('\t');assert v[0] in iso and int(v[1])==iso[v[0]]['covered_lines'] and int(v[2])==iso[v[0]]['unique_lines']
planner=json.loads((A/'unique-plan.json').read_text());assert planner['measured_tests']==planner['baseline']['tests']==planner['after_plan']['tests']==16 and not planner['zero_loss_removable'];assert set((A/'keep.txt').read_text().splitlines())==set(iso)
union=set.union(*sets.values());assert planner['baseline']['lines_covered']==len(union) and planner['baseline']['lines_valid']==len(R[0][0]);scoped={k for k,v in R[0][0].items() if v};unioncomparison={'original_union_lines':len(union),'matched_before_scope_lines':len(scoped),'only_in_original_individual_measurements':sorted(union-scoped),'only_in_matched_before_scope':sorted(scoped-union)}
unused=[];warningstats=[];unusedmeta=[]
for w,side in zip(W,['before','after']):
 p=w/'.evidence/keep'/F;m=json.loads((p/(side+'-unused.json')).read_text());assert m['exit_code']==0 and m['head']==S['base' if side=='before' else 'head'];unusedmeta.append(m)
 text=(p/(side+'-unused.log')).read_text();unused.append(collections.Counter((norm(path),code,msg) for path,code,msg in re.findall(r'^(.+?\.cs)\(\d+,\d+\): warning (IDE(?:0051|0052|0060|0005)): (.+?) \[',text,re.M)));warningstats.append({'total_reported':int(re.findall(r'(\d+) Warning\(s\)',text)[-1]),'raw_CS1591_occurrences_including_repeated_summary':len(re.findall('warning CS1591:',text))});assert not (w/'tests/ClankerWorld.Tests/.editorconfig').exists()
assert unused[0]==unused[1] and warningstats[0]==warningstats[1] and unusedmeta[0]['args']==unusedmeta[1]['args']
uc=json.loads((A/'unused-comparison.json').read_text());assert uc['before_records']==uc['after_records']==sum(unused[0].values()) and uc['before_normalized']==uc['after_normalized']==len(unused[0]) and not uc['new'] and not uc['removed']
comp=json.loads((W[1]/'.evidence/keep'/F/'comparison.json').read_text());bound=json.loads((A/'point-bound.json').read_text())
for asm,value in bound.items():
 for metric in ['lines','branches']:
  for key in ['valid','before','after','lost','gained','loss_bound_pp','relative_loss_bound_percent']:assert value[metric][key]==rawdiff[asm][metric][key],(asm,metric,key)
  if asm!='total':
   for key in ['lost_points','gained_points']:assert value[metric][key]==[list(p[1:]) for p in rawdiff[asm][metric][key]]
for v in comp.values():assert not v.get('lost_lines',[]) and not v.get('reduced_branch_lines',[]) and v['line_delta_pp']==v['branch_delta_pp']==0
assert all(v[d]['lost']==v[d]['gained']==0 for v in rawdiff.values() for d in ['lines','branches']),'any loss/gain needs specific review rather than presumed timing variation'
complete=json.loads((A/'local-complete.json').read_text());assert complete['head']==S['head'] and complete['success']
report={'signature':'Codex session8451d624/map-rules-raw-reader','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'role':'Read-only author-side ownership second reader; not independent top-level PR approval','base':S['base'],'head':S['head'],'accepted_local':True,'findings':[],'remaining_local_receipts':[],'scope':{'filter':S['filter'],'cases_each':20,'methods_each':16,'skips':0,'only_display_mapping':S['discovery_renames'],'removed_case_credit':0,'all_named_proofs_included':True},'raw_identity_recomputation':rawdiff,'mapped_xml_branch_integer_counts':{'before':R[0][3],'head':R[1][3]},'unmapped_branch_integer_counts':{side:{'valid':len(R[i][1])-R[i][3][1],'covered':sum(R[i][1].values())-R[i][3][0]} for i,side in enumerate(['before','head'])},'equivalence':{'same_settings_filter_scheduling':True,'canonical_production_four':prod(M[0]),'six_normal_archive_hashes_each_verify_actual_bytes':True,'original_pristine_normal_deployment_matches_before':True,'fresh_head_production_archived':headprod,'single_actual_pair':True,'archive_pipeline_read':'Both fresh normal test builds succeeded0warnings/errors; normal test DLL/PDB archived, base canonical4 production DLL/PDB archived and deployed identically to both scoped runs before instrumentation. Original before deployment copied before coverage for per-method workers. No prior campaign hashes assumed.'},'discovery':{'cases_each':len(d0),'methods_each':len({n.split('(')[0] for n in d0}),'only_approved_mapping_order_preserved':True},'original_individual_measurements':iso,'original_case_counter_matches_scoped_before':True,'original_union_vs_matched_before_scope':unioncomparison,'unused':{'records_each':sum(unused[0].values()),'normalized_groups_each':len(unused[0]),'new':[],'removed':[],'warnings_each':warningstats[0],'temporary_configs_absent':True},'official_comparator_and_exact_identity_bound_agree':True,'conservative_loss_upper_bound_pp_without_gain_offset':{a:{d:v[d]['loss_bound_pp'] for d in ['lines','branches']} for a,v in rawdiff.items()},'source_still_matches_acceptance':True,'CI':'Parent owns exact-head CI/timing/native logs; reader does not watch ready older PRs.','limits':'No builds/tests/coverage/GitHub writes by reader. Sixteen original methods actually measured separately by parent; all receipts/raw maps independently inspected. No full-suite coverage execution claimed. Fact conversion preserves exact Medium/High/80 generation input and all native/save assertions. Tie-break test retains one exact expected route with no changed input/state between removed repetitions. Both changes preserve executed case counts.'}
(A/'reader-raw.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'accepted_local':True,'report':str(A/'reader-raw.json'),'total':rawdiff['total'],'original_union':unioncomparison,'unused':report['unused']},indent=2))

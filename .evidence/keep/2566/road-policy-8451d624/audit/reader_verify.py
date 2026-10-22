from pathlib import Path
import json, hashlib, re, collections, xml.etree.ElementTree as E, subprocess
A=Path('/workspace/2566-audit/road-policy'); S=json.loads((A/'scope.json').read_text()); W=[Path(S['base_checkout']),Path(S['head_checkout'])]; N={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
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
results=[];metas=[]
for w,side in zip(W,['before','final']):
 folder=w/'.evidence/keep/2566-road-policy';m=json.loads((folder/(side+'-run.json')).read_text());metas.append(m);assert m['exit_code']==0
 assert m['head']==S['base' if side=='before' else 'head'];assert m['args'][m['args'].index('--filter')+1]==S['filter']
 assert m['settings_sha256']==sha(w/'skills/test-audit/references/coverage.runsettings');assert m['filter_sha256']==hashlib.sha256(S['filter'].encode()).hexdigest()
 for h in m['binary_hashes']:assert sha(h['source'])==h['sha256']
 rows,methods,c=trx(folder/side);assert len(rows)==9 and sum(rows.values())==9 and set(methods)==set(S['ledger_proofs']) and sorted(methods.values())==[1,4,4]
 assert c==m['counters'];results.append(raw(folder/side))
assert results[0][0]==results[1][0] and results[0][1]==results[1][1]
assert results[0][3]==results[1][3]==[239,88022]
assert trx(W[0]/'.evidence/keep/2566-road-policy/before')[:2]==trx(W[1]/'.evidence/keep/2566-road-policy/final')[:2]
for key in ['DOTNET_PROCESSOR_COUNT','nice','settings_sha256','filter_sha256']:assert metas[0][key]==metas[1][key]
def args(m):
 a=m['args'][:];a[a.index('--results-directory')+1]='RESULTS';return a
assert args(metas[0])==args(metas[1])
prod=lambda m:{Path(x['source']).name:x['sha256'] for x in m['binary_hashes'] if 'Tests.' not in Path(x['source']).name}
assert prod(metas[0])==prod(metas[1]) and len(prod(metas[0]))==4
for h in json.loads((A/'head-normal-binaries.json').read_text()):assert sha(h['saved'])==h['sha256']
iso={};sets={}
for d in (A/'isolated').iterdir():
 if not d.is_dir():continue
 receipt=json.loads((d/'completion.json').read_text());assert receipt['success'] and receipt['method']==d.name
 rows,methods,c=trx(d);assert list(methods)==[d.name] and c['failed']=='0';r,ls,mp=xmlmap(single(d,'coverage.cobertura.xml'));sets[d.name]={k for k,v in ls.items() if v};iso[d.name]={'passed':sum(rows.values()),'covered_lines':len(sets[d.name])}
assert set(iso)==set(S['ledger_proofs']) and sorted(x['passed'] for x in iso.values())==[1,4,4]
assert set.union(*sets.values())=={k for k,v in results[0][0].items() if v}
for name,s in sets.items():iso[name]['unique_lines']=len(s-set.union(*(v for k,v in sets.items() if k!=name)))
assert [iso[x]['unique_lines'] for x in S['ledger_proofs']]==[0,9,0]
# Full discovery order and multiplicity remain, including xUnit-truncated duplicate display names.
def discover(p):return re.findall(r'^\s+(ClankerWorld\.Tests\..+?)\s*$',p.read_text(),re.M)
dis=discover(A/'base-discovery.log');assert dis==discover(A/'head-discovery.log');assert len(dis)==7398 and len({x.split('(')[0] for x in dis})==3297
unused=[];docs=[]
for w,side in zip(W,['before','after']):
 p=w/'.evidence/keep/2566-road-policy';m=json.loads((p/(side+'-unused.json')).read_text());assert m['exit_code']==0
 log=(p/(side+'-unused.log')).read_text();items=collections.Counter()
 for path,code,msg in re.findall(r'^(.+?\.cs)\(\d+,\d+\): warning (IDE(?:0051|0052|0060|0005)): (.+?) \[',log,re.M):items[(norm(path),code,msg)]+=1
 unused.append(items);docs.append(len(re.findall(r'warning CS1591:',log)));assert not (w/'tests/ClankerWorld.Tests/.editorconfig').exists()
assert unused[0]==unused[1] and sum(unused[0].values())==384 and len(unused[0])==168 and docs==[8798,8798]
for name in ['base-build.log','head-build.log']:
 text=(A/name).read_text();assert 'Build succeeded.' in text and re.search(r'\b0 Warning\(s\)',text) and re.search(r'\b0 Error\(s\)',text)
source='tests/ClankerWorld.Tests/Simulation/World/RoadCostPolicyTests.cs';sourceapproval=json.loads((A/'reader-source.json').read_text());assert sha(W[1]/source)==sourceapproval['source_hash_candidate']
files=subprocess.check_output(['git','diff','--name-only',S['base'],S['head']],cwd=W[1],text=True).splitlines();assert files==[source]
assert hashlib.sha256(subprocess.check_output(['git','show',S['head']+':'+source],cwd=W[1])).hexdigest()==sourceapproval['source_hash_candidate']
comp=json.loads((W[1]/'.evidence/keep/2566-road-policy/comparison.json').read_text());bound=json.loads((A/'point-bound.json').read_text())
for value in comp.values():
 assert not value.get('lost_lines',[]) and not value.get('reduced_branch_lines',[]) and value['line_delta_pp']==value['branch_delta_pp']==0
for group,value in bound.items():
 for metric in ['lines','branches']:assert value[metric]['lost']==value[metric]['gained']==0 and value[metric]['loss_bound_pp']==0
out={'signature':'Codex session8451d624/raw-reader','role':'Read-only author-side second reader; not independent PR approval','base':S['base'],'head':S['head'],'accepted_local':True,'findings':[],'reporting_clarifications':['The official XML comparator reports239 mapped hits; the original unique-planner233 is a rounded-rate lower bound, not exact collector hits. All352 unmapped collector branches are unhit.','4591 is total warnings in each unused build, not documentation-only. CS1591 appears8798 times in full log including repeated summary, corresponding to4399 initial warnings;192 IDE warnings complete4591.'], 'scope':{'filter':S['filter'],'cases_before':9,'cases_head':9,'methods':3,'all_passed':True,'skipped':0,'whole_changed_nonpartial_class_and_all_helper_consumers':True,'proofs':S['ledger_proofs']},'raw_identity_recomputation':{'line_points':77871,'line_points_hit':263,'branch_points':88374,'branch_points_hit':239,'mapped_branch_points':88022,'mapped_branch_points_hit':239,'unmapped_branch_remainder':352,'unmapped_branch_hits':0,'before_head_line_and_exact_branch_identity_hit_maps_equal':True,'json_xml_line_hit_maps_and_collector_integer_totals_agree':True,'duplicate_attachment_copies_identical':True,'assemblies':results[0][2],'lost_lines':0,'gained_lines':0,'lost_branches':0,'gained_branches':0,'conservative_full_suite_loss_upper_bound_pp_without_gain_offset':0},'run_equivalence':{'filter_settings_hashes_and_scheduling_equal':True,'same_canonical_production_four_archive_hashes':prod(metas[0]),'normal_before_and_head_test_archives_hash_verified':True,'head_normal_binary_provenance_archive_verified':True,'note':'Canonical base production DLL/PDB bytes intentionally used for both runs; original independently built head production bytes archived separately. Current deployment after analyzer builds is not substituted for frozen run receipts.'},'original_method_measurements':iso,'original_method_union_equals_matched_scope':True,'all_three_retained_for_distinct_assertion_contracts':True,'discovery':{'ordered_names_identical':True,'cases':7398,'methods':3297},'normal_builds':{'both_succeeded':True,'warnings_each':0,'errors_each':0},'unused':{'records_before':384,'records_head':384,'normalized_groups_before':168,'normalized_groups_head':168,'new':[],'removed':[],'unused_build_total_warning_count_each':4591,'CS1591_raw_log_occurrences_each_including_summary_repetition':8798,'CS1591_single_emission_warning_count_each':4399,'temporary_configs_absent':True},'source_acceptance_unchanged':{'sha256':sourceapproval['source_hash_candidate'],'only_changed_file':source,'tests_added':16,'tests_removed':33,'source_docs_changed':False,'assertions_and_input_contract':'As accepted source reader: all 9 rows, 3 methods, 15 success proposals, 4 blocked queries and source-derived 98 assertion executions preserved.'},'remaining_conditional_gates':['Exact-head CI and PR readiness managed by parent; this local acceptance does not assert CI completion.'],'limits':'No tests/builds/coverage or GitHub writes run by reader; no full-suite coverage run claimed. Original-method reports are actual separate baseline measurements. Zero loss is an exact identity result for the authorized scope, with zero conservative whole-suite loss upper bound.'}
(A/'reader-raw.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'accepted_local':True,'raw':[77871,263,88374,239],'isolated':iso,'unused_records':384,'report':str(A/'reader-raw.json')},indent=2))

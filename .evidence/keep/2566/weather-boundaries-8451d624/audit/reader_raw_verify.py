from pathlib import Path
import json, hashlib, re, collections, xml.etree.ElementTree as E, subprocess
A=Path('/workspace/2566-audit/weather-boundaries'); S=json.loads((A/'scope.json').read_text()); W=[Path(S['base_checkout']),Path(S['head_checkout'])]; N={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
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
R=[];M=[];T=[]
approved_removed_discovery=['ClankerWorld.Tests.MorningWeatherEpisodeTests.ImportedStormKeepsOnlyTheRemainingCivilDayAllowance(offset: 90, rawTick: 50, expectedStart: 0, expectedEnd: 180)','ClankerWorld.Tests.WeatherWeightBoundaryTests.AcceptedRainWeightsMatchTheDeclaredClimateSplit(rain: 1000000000, climate: Cold, expectedSnow: 400000000)','ClankerWorld.Tests.WeatherWeightBoundaryTests.AcceptedRainWeightsMatchTheDeclaredClimateSplit(rain: 1000000000, climate: Polar, expectedSnow: 800000000)']
for w,side,n in zip(W,['before','final'],[36,33]):
 folder=w/'.evidence/keep/2566-weather-boundaries';m=json.loads((folder/(side+'-run.json')).read_text());assert m['exit_code']==0 and m['head']==S['base' if side=='before' else 'head']
 assert m['args'][m['args'].index('--filter')+1]==S['filter'];assert m['settings_sha256']==sha(w/'skills/test-audit/references/coverage.runsettings');assert m['filter_sha256']==hashlib.sha256(S['filter'].encode()).hexdigest()
 for h in m['binary_hashes']:assert sha(h['source'])==h['sha256']
 rows,methods,c=trx(folder/side);assert sum(rows.values())==n and len(methods)==15 and set(methods)==set(S['ledger_proofs'])
 assert c==m['counters'];M.append(m);T.append((rows,methods));R.append(raw(folder/side))
assert R[0][0].keys()==R[1][0].keys() and R[0][1].keys()==R[1][1].keys()
loss={}
for ix,name in [(0,'lines'),(1,'branches')]:
 loss[name]={'lost':[k for k,v in R[0][ix].items() if v and not R[1][ix][k]],'gained':[k for k,v in R[1][ix].items() if v and not R[0][ix][k]]}
assert len(R[0][0])==77871 and len(R[0][1])==88374
assert R[0][3]==R[1][3]
for key in ['DOTNET_PROCESSOR_COUNT','nice','settings_sha256','filter_sha256']:assert M[0][key]==M[1][key]
def args(m):
 a=m['args'][:];a[a.index('--results-directory')+1]='RESULTS';return a
assert args(M[0])==args(M[1])
prod=lambda m:{Path(x['source']).name:x['sha256'] for x in m['binary_hashes'] if 'Tests.' not in Path(x['source']).name}
assert prod(M[0])==prod(M[1])==json.loads((A/'canonical-ready.json').read_text()) and len(prod(M[0]))==4
for h in M[0]['binary_hashes']:assert sha(W[0]/'tests/ClankerWorld.Tests/bin/Release/net10.0.weather-original'/Path(h['source']).name)==h['sha256']
for side in ['base','head']:
 d=json.loads((A/(side+'-build-provenance.json')).read_text());assert '-p:BuildProjectReferences=false' in d['args'] and '--no-incremental' in d['args'];assert d['production_cached_from']=='/workspace/ClankerWorld-2566-ca3-base'
assert subprocess.check_output(['git','diff','--name-only','ca3a5b8d',S['base'],'--','src','global.json','Directory.Build.props','Directory.Build.targets','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','skills/test-audit/references/coverage.runsettings'],cwd=W[0],text=True)==''
# Current campaign reuses identical source/config production outputs, while fresh-compiling each test assembly.
prior=json.loads(Path('/workspace/2566-audit/content-support/canonical-ready.json').read_text());assert prior==prod(M[0])
def discover(p):return re.findall(r'^\s+(ClankerWorld\.Tests\..+?)\s*$',p.read_text(),re.M)
dis0=discover(A/'base-discovery.log');dis1=discover(A/'head-discovery.log');assert len(dis0)==7394 and len(dis1)==7391 and {n.split('(')[0] for n in dis0}=={n.split('(')[0] for n in dis1} and len({n.split('(')[0] for n in dis0})==3295
assert collections.Counter(dis0)-collections.Counter(dis1)==collections.Counter(approved_removed_discovery) and not collections.Counter(dis1)-collections.Counter(dis0)
assert [n for n in dis0 if n not in approved_removed_discovery]==dis1
trxremoved=T[0][0]-T[1][0];assert sum(trxremoved.values())==3 and not T[1][0]-T[0][0]
expected_short=collections.Counter((name.split('ClankerWorld.Tests.',1)[1],'Passed') for name in approved_removed_discovery)
actual_short=collections.Counter((name.removeprefix('ClankerWorld.Tests.'),outcome) for (name,outcome),count in trxremoved.items() for _ in range(count));assert actual_short==expected_short,(actual_short,expected_short)
for name in ['base-build.log','head-build.log']:
 text=(A/name).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
source=json.loads((A/'reader-source.json').read_text());files=[]
for entry in source['exact_approved_diff']['changed_files'].values():
 p=entry['file'];files.append(p);assert sha(W[1]/p)==entry['head_sha256'];assert hashlib.sha256(subprocess.check_output(['git','show',S['head']+':'+p],cwd=W[1])).hexdigest()==entry['head_sha256']
assert sorted(subprocess.check_output(['git','diff','--name-only',S['base'],S['head']],cwd=W[1],text=True).splitlines())==sorted(files)
ledger=json.loads((A/'ledger.json').read_text());assert sorted(e['test'] for e in ledger)==sorted(S['ledger_proofs']);assert all(p in S['ledger_proofs'] for e in ledger for p in e['retained_proof'])
# Both documented driver corrections precede the head measurement command in run_scoped; failure log stops at copy2 of missing archive, before metadata or dotnet invocation.
failed=(A/'premeasurement-archive-path.log').read_text();assert 'FileNotFoundError' in failed and 'shutil.copy2' in failed and 'weather-boundaries/ClankerWorld.Tests.dll' in failed
runner=(A/'run_scoped.py').read_text();assert runner.index('shutil.copy2')<runner.index("'started_utc'")<runner.index('subprocess.run')
progress={'signature':'Codex session8451d624/weather-boundaries-raw-reader','role':'Read-only author-side second reader, not independent PR approval','base':S['base'],'head':S['head'],'matched_pair_validated':True,'scope':{'filter':S['filter'],'before_passed':36,'head_passed':33,'methods_each':15,'skips':0,'case_names_removed':approved_removed_discovery,'all_named_ledger_proofs_present':True},'raw_identity_recomputation':{'line_points_valid':len(R[0][0]),'line_points_hit_before':sum(R[0][0].values()),'line_points_hit_head':sum(R[1][0].values()),'branch_points_valid':len(R[0][1]),'branch_points_hit_before':sum(R[0][1].values()),'branch_points_hit_head':sum(R[1][1].values()),'mapped_xml_branch_points_valid':R[0][3][1],'mapped_xml_branch_points_hit':R[0][3][0],'unmapped_branch_points':len(R[0][1])-R[0][3][1],'all_assemblies':R[0][2],'lost_and_gained_exact_points':loss},'equivalence':{'same_filter_settings_scheduling':True,'canonical_four':prod(M[0]),'all_six_normal_binary_archive_hashes_each_match_receipt':True,'pristine_original_base_deployment_matches':True,'source_and_production_configuration_ca3_to_c2df_identical':True,'fresh_test_compilation_production_dependencies_reused_explicitly':True,'documented_driver_corrections_before_any_head_measurement':True,'one_completed_matched_pair_receipt_each':True},'discovery':{'cases_before':7394,'cases_head':7391,'methods_each':3295,'exactly_approved_rows_removed_order_of_retained_names_identical':True},'normal_builds':{'both_succeeded':True,'warnings_each':0,'errors_each':0},'committed_source_matches_source_acceptance':True,'remaining_local_receipts':[],'findings':[]}
missing=[]
for filename in ['local-complete.json','unused-comparison.json','point-bound.json','unique.tsv','unique-plan.json']:
 if not (A/filename).exists():missing.append(filename)
for w,side in zip(W,['before','after']):
 if not (w/'.evidence/keep/2566-weather-boundaries'/(side+'-unused.json')).exists():missing.append(side+'-unused.json')
progress['remaining_local_receipts']=missing
(A/'reader-progress.json').write_text(json.dumps(progress,indent=2)+'\n')
if missing:print(json.dumps(progress,indent=2));raise SystemExit(0)
assert all(not xs for v in loss.values() for xs in v.values()),loss
iso={};sets={}
for d in (A/'isolated').iterdir():
 if not d.is_dir():continue
 receipt=json.loads((d/'completion.json').read_text());assert receipt['success'] and receipt['method']==d.name
 rows,methods,c=trx(d);assert list(methods)==[d.name] and c['failed']=='0';_,ls,_=xmlmap(single(d,'coverage.cobertura.xml'));sets[d.name]={k for k,v in ls.items() if v};iso[d.name]={'passed':sum(rows.values()),'covered_lines':len(sets[d.name])}
assert set(iso)==set(S['ledger_proofs']) and sum(x['passed'] for x in iso.values())==36
assert set.union(*sets.values())=={k for k,v in R[0][0].items() if v}
for name,pts in sets.items():iso[name]['unique_lines']=len(pts-set.union(*(v for k,v in sets.items() if k!=name)))
for line in (A/'unique.tsv').read_text().splitlines()[1:]:
 vals=line.split('\t');name=vals[0]
 if name in iso:assert int(vals[1])==iso[name]['covered_lines'] and int(vals[2])==iso[name]['unique_lines']
unused=[];warningstats=[]
for w,side in zip(W,['before','after']):
 p=w/'.evidence/keep/2566-weather-boundaries';m=json.loads((p/(side+'-unused.json')).read_text());assert m['exit_code']==0
 text=(p/(side+'-unused.log')).read_text();unused.append(collections.Counter((norm(path),code,msg) for path,code,msg in re.findall(r'^(.+?\.cs)\(\d+,\d+\): warning (IDE(?:0051|0052|0060|0005)): (.+?) \[',text,re.M)))
 warningstats.append({'total_reported':int(re.findall(r'(\d+) Warning\(s\)',text)[-1]),'raw_CS1591_occurrences_including_repeated_summary':len(re.findall('warning CS1591:',text))});assert not (w/'tests/ClankerWorld.Tests/.editorconfig').exists()
assert unused[0]==unused[1] and warningstats[0]==warningstats[1]
uc=json.loads((A/'unused-comparison.json').read_text());assert uc['before_records']==uc['after_records']==sum(unused[0].values()) and uc['before_normalized']==uc['after_normalized']==len(unused[0]) and not uc['new'] and not uc['removed']
comp=json.loads((W[1]/'.evidence/keep/2566-weather-boundaries/comparison.json').read_text());bound=json.loads((A/'point-bound.json').read_text())
for value in comp.values():assert not value.get('lost_lines',[]) and not value.get('reduced_branch_lines',[]) and value['line_delta_pp']==value['branch_delta_pp']==0
for group,value in bound.items():
 for metric in ['lines','branches']:assert value[metric]['lost']==value[metric]['gained']==0 and value[metric]['loss_bound_pp']==0
 assert value['lines']['valid']==(77871 if group=='total' else R[0][2][group]['lines'][0]);assert value['branches']['valid']==(88374 if group=='total' else R[0][2][group]['branches'][0])
complete=json.loads((A/'local-complete.json').read_text());assert complete['head']==S['head'] and complete['success']
progress.update({'accepted_local':True,'remaining_local_receipts':[],'original_individual_measurements':iso,'original_method_union_equals_matched_scope':True,'unused':{'records_before_and_head':sum(unused[0].values()),'normalized_groups_before_and_head':len(unused[0]),'new':[],'removed':[],'warnings_each':warningstats[0],'temporary_configs_absent':True},'official_comparator_and_exact_identity_bound_agree':True,'conservative_whole_suite_loss_upper_bound_pp_without_gain_offset':0,'remaining_conditional_gates':['Exact-head CI/readiness parent-owned. Independent PR review remains another top-level session.'],'limits':'No builds/tests/coverage executions or GitHub writes by reader. All15 original methods have real separate-process measurements,36original passes; final33 pass. No full-suite coverage claimed.'})
(A/'reader-raw.json').write_text(json.dumps(progress,indent=2)+'\n');print(json.dumps({'accepted_local':True,'report':str(A/'reader-raw.json'),'isolated_methods':len(iso),'unused':progress['unused']},indent=2))

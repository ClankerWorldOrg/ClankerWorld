from pathlib import Path
import datetime,hashlib,json,os,shutil,subprocess,sys,xml.etree.ElementTree as E
root=Path(sys.argv[1]);label=sys.argv[2];scope_path=Path(sys.argv[3]);expected=int(sys.argv[4]);os.chdir(root)
archive=Path('/workspace/ClankerWorld-2564/.evidence/keep/continuation-binaries')
scope=json.loads(scope_path.read_text());archive=Path(scope['archive_path']);head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip();assert head==scope['base' if label=='before' else 'head'],head
for p in Path('/proc').iterdir():
 if not p.name.isdigit():continue
 try:args=(p/'cmdline').read_bytes().replace(b'\0',b' ').decode(errors='replace')
 except OSError:continue
 assert not ('testhost.dll' in args and str(root)+'/tests/ClankerWorld.Tests/bin/' in args),(p,args)
output=root/'tests/ClankerWorld.Tests/bin/Release/net10.0';assert output.is_dir()
records=[]
for name,folder in [('ClankerWorld.Tests',archive/('baseline' if label=='before' else scope.get('archive_label','2592'))),('ClankerWorld.Simulation',archive),('ClankerWorld.Server',archive)]:
 for ext in ['dll','pdb']:
  source=folder/(name+'.'+ext);target=output/source.name;shutil.copy2(source,target)
  sha=hashlib.sha256(target.read_bytes()).hexdigest();assert sha==hashlib.sha256(source.read_bytes()).hexdigest()
  records.append({'source':str(source),'destination':str(target),'sha256':sha})
settings=root/'skills/test-audit/references/coverage.runsettings'
assert settings.read_bytes()==Path('/workspace/ClankerWorld-2566-442f-base/skills/test-audit/references/coverage.runsettings').read_bytes()
folder=root/'.evidence/keep'/scope.get('evidence_folder','scoped-2656');folder.mkdir(parents=True,exist_ok=True)
(folder/'scope.json').write_text(scope_path.read_text())
results=folder/label;assert not results.exists(),results
filter_text=scope['filter']
args=['dotnet','test','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','-c','Release','--no-build','--settings','skills/test-audit/references/coverage.runsettings','--collect','XPlat Code Coverage','--filter',filter_text,'--results-directory',str(results),'--logger','trx;LogFileName=tests.trx','--logger','console;verbosity=normal','--','xUnit.MaxParallelThreads=2','DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,json']
env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
meta={'head':head,'expected_cases':expected,'args':args,'DOTNET_PROCESSOR_COUNT':4,'nice':10,'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'binary_hashes':records,'settings_sha256':hashlib.sha256(settings.read_bytes()).hexdigest(),'filter_sha256':hashlib.sha256(filter_text.encode()).hexdigest()}
metadata=folder/(label+'-run.json');metadata.write_text(json.dumps(meta,indent=2)+'\n');os.nice(10)
print(f'{label}: starting {expected} scoped cases on {head[:8]}',flush=True)
with (folder/(label+'.log')).open('w') as log:r=subprocess.run(args,env=env,stdout=log,stderr=subprocess.STDOUT)
meta.update({'exit_code':r.returncode,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
trx=results/'tests.trx';ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
if trx.exists():
 c=E.parse(trx).find('.//t:Counters',ns);meta['counters']=c.attrib if c is not None else None
metadata.write_text(json.dumps(meta,indent=2)+'\n');print((folder/(label+'.log')).read_text()[-2800:],flush=True);print(json.dumps(meta.get('counters')),flush=True)
assert r.returncode==0,r.returncode
c=meta['counters'];assert int(c['total'])==expected and int(c['passed'])==expected and int(c['failed'])==0 and int(c['notExecuted'])==0,c

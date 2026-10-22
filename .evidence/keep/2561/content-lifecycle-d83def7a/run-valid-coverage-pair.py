from pathlib import Path
import os,subprocess,json,hashlib,shutil,sys,time,signal,xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2];out=root/'.evidence/2561-content';base=Path('/workspace/ClankerWorld-2561-continuation');env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
sys.path.insert(0,str(root/'skills/test-audit/scripts'));from per_test_coverage import collector_path
scope=(out/'scope.filter').read_text().strip();settings=root/'skills/test-audit/references/coverage.runsettings';assert settings.read_bytes()==(base/'skills/test-audit/references/coverage.runsettings').read_bytes()
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for label,source,cwd in [('base',base/'tests/ClankerWorld.Tests/bin/Release/net10.0-runtime-continuation-base-source',base),('head',root/'tests/ClankerWorld.Tests/bin/Release/net10.0-content-lifecycle-head-source',root)]:
 pins=json.loads((out/(label+'-pins.json')).read_text());worker=(base/'.evidence/2561-continuation/content/base-output-collector') if label=='base' else out/'head-output-collector'
 if not worker.exists():shutil.copytree(source,worker)
 def verify():
  for name,digest in pins.items():assert hashlib.sha256((worker/name).read_bytes()).hexdigest()==digest,name
 verify();adapter=collector_path(source/'ClankerWorld.Tests.dll');assert adapter and (adapter/'coverlet.collector.dll').exists(),adapter
 result=out/('before' if label=='base' else 'after');resume=result.exists()
 if resume:assert label=='base' and (out/'base-coverage.exit-code').read_text().strip()=='0'
 cmd=['dotnet','test',str(worker/'ClankerWorld.Tests.dll'),'--filter',scope,'--settings',str(settings),'--collect','XPlat Code Coverage;Format=cobertura,json','--test-adapter-path',str(adapter),'--results-directory',str(result),'--logger','trx;LogFileName=tests.trx','--logger','console;verbosity=normal','--','xUnit.MaxParallelThreads=4'];(out/(label+'-command.json')).write_text(json.dumps(cmd,indent=2)+'\n')
 logpath=out/(label+'-coverage.log')
 if resume:code=0
 else:
  with logpath.open('w') as log:
   proc=subprocess.Popen(cmd,cwd=cwd,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
   while proc.poll() is None:
    time.sleep(2)
    text=logpath.read_text()
    if 'Unable to find a datacollector' in text or 'Could not find data collector' in text:
     os.killpg(proc.pid,signal.SIGINT);proc.wait(timeout=30);raise SystemExit('Collector unavailable: aborted immediately, no completed measurement')
   code=proc.returncode
 verify();(out/(label+'-coverage.exit-code')).write_text(str(code)+'\n');assert code==0,logpath
 xmls=list(result.rglob('coverage.cobertura.xml'));jsons=list(result.rglob('coverage.json'));assert xmls and jsons
 for paths in (xmls,jsons):
  assert len({hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})==1,paths
 xmls=sorted(xmls,key=lambda p:len(p.parts));jsons=sorted(jsons,key=lambda p:len(p.parts))
 attrs=E.parse(xmls[0]).getroot().attrib;assert int(attrs['lines-valid'])>0 and int(attrs['lines-covered'])>0
 data=json.loads(jsons[0].read_text());assert data
 trx=E.parse(result/'tests.trx').getroot();cases=trx.findall('.//t:UnitTestResult',ns);assert len(cases)==175 and all(c.attrib['outcome']=='Passed' for c in cases)
 receipt={'label':label,'success':True,'cases':len(cases),'collector':str(adapter),'settings_sha256':hashlib.sha256(settings.read_bytes()).hexdigest(),'filter':scope,'all_six_hashes_restored':True,'xml':str(xmls[0]),'json':str(jsons[0]),'summary':attrs}
 (out/(label+'-coverage-completion.json')).write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt),flush=True)
(out/'head-verification.finished').write_text('Head7fa228e5 build, unused168/168, discovery7398/3297, ledger, formatter, stale, diff reused. Corrected valid affected coverage pair175/175passed each, XML+JSON present and six hashes restored. Comparator/stable identity/executed proof verification still separate.\n')

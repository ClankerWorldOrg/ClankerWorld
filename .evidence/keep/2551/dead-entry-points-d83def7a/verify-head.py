from pathlib import Path
import subprocess,os,time,json,re
root=Path.cwd();out=root/'.evidence/2551';base=Path('/workspace/ClankerWorld-2551-base');env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
while not (out/'head-build.finished').exists():
 if (out/'build-head.exit-code').exists():assert (out/'build-head.exit-code').read_text().strip()=='0'
 time.sleep(2)
def run(label,cmd,cwd=root):
 (out/(label+'-command.json')).write_text(json.dumps(cmd,indent=2)+'\n')
 with (out/(label+'.log')).open('w') as f:r=subprocess.run(cmd,cwd=cwd,env=env,stdout=f,stderr=subprocess.STDOUT)
 (out/(label+'.exit-code')).write_text(str(r.returncode)+'\n');print(label,r.returncode,flush=True);assert r.returncode==0,label
 return (out/(label+'.log')).read_text()
args=[str(root/'tests/ClankerWorld.Tests/bin/Release/net10.0'/name) for name in ['ClankerWorld.Simulation.dll','ClankerWorld.Server.dll','ClankerWorld.Tests.dll']]
s=run('head-il-scan',['dotnet','run','--no-build','--project','.evidence/2551/il-scan','--',*args]);data=json.loads(s);assert all(v==0 for v in data['declarations'].values());(out/'head-il-scan.json').write_text(s)
for label,cwd in [('base',base),('head',root)]:
 run(label+'-discovery',['dotnet','test',str(cwd/'tests/ClankerWorld.Tests/bin/Release/net10.0/ClankerWorld.Tests.dll'),'--list-tests'],cwd)
# Exact discovery output order is not a contract; all selected display names/multiplicity are.
def names(label):
 lines=(out/(label+'-discovery.log')).read_text().splitlines();start=next(i for i,s in enumerate(lines) if 'The following Tests are available:' in s);return sorted(s.strip() for s in lines[start+1:] if s.strip())
before=names('base');after=names('head');assert before==after;methods={s.split('(',1)[0] for s in before};(out/'discovery-comparison.json').write_text(json.dumps({'cases':len(before),'methods':len(methods),'identical':True},indent=2));print('discovery',len(before),len(methods),flush=True)
classes=['TownSystemTests','GovernmentInvariantTests','CivicInvariantTests','CaseAuthorityInvariantTests','TownMembershipTests','HouseholdLandGrantTests','NonLandAgreementHeldOfferTests','TownLandHearingRuntimeTests','TownNonLandRuntimeTests'];scope='|'.join('FullyQualifiedName~ClankerWorld.Tests.'+c+'.' for c in classes);(out/'focused.filter').write_text(scope+'\n')
run('focused',['dotnet','test',str(root/'tests/ClankerWorld.Tests/bin/Release/net10.0/ClankerWorld.Tests.dll'),'--filter',scope,'--logger','trx;LogFileName=focused.trx','--results-directory',str(out/'focused'),'--','xUnit.MaxParallelThreads=4'])
run('format',['dotnet','format','--verify-no-changes','--no-restore'])
run('diff',['git','diff','--check'])
(out/'local-verification.finished').write_text('Head build/discovery/compiled dead-reference/focused tests/format/diff passed. Native comparison and CI checked separately.\n')

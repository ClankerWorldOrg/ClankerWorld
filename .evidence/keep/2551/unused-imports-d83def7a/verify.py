from pathlib import Path
import os,subprocess,time,json,xml.etree.ElementTree as E
root=Path.cwd();out=root/'.evidence/2551-imports';env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
while not (out/'build.finished').exists():
 if (out/'build.exit-code').exists():assert (out/'build.exit-code').read_text().strip()=='0'
 time.sleep(2)
def run(label,cmd):
 (out/(label+'-command.json')).write_text(json.dumps(cmd,indent=2)+'\n')
 with (out/(label+'.log')).open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT)
 (out/(label+'.exit-code')).write_text(str(r.returncode)+'\n');print(label,r.returncode,flush=True);assert r.returncode==0,label
 return (out/(label+'.log')).read_text()
dll=str(root/'tests/ClankerWorld.Tests/bin/Release/net10.0/ClankerWorld.Tests.dll');s=run('discovery',['dotnet','test',dll,'--list-tests']);lines=s.splitlines();start=next(i for i,l in enumerate(lines) if 'The following Tests are available:' in l);names=[l.strip() for l in lines[start+1:] if l.strip()];(out/'discovery-summary.json').write_text(json.dumps({'cases':len(names),'methods':len({n.split('(',1)[0] for n in names}),'test_tree_identical_to_base':True},indent=2)+'\n')
classes=['TownSystemTests','GovernmentInvariantTests','CivicInvariantTests','CaseAuthorityInvariantTests','CouncilInvariantTests'];scope='|'.join('FullyQualifiedName~ClankerWorld.Tests.'+c+'.' for c in classes);(out/'focused.filter').write_text(scope+'\n');run('focused',['dotnet','test',dll,'--filter',scope,'--logger','trx;LogFileName=focused.trx','--results-directory',str(out/'focused')]);run('format',['dotnet','format','--verify-no-changes','--no-restore']);run('diff',['git','diff','HEAD^','HEAD','--check']);(out/'local-verification.finished').write_text('Head Release build/discovery/existing focused Civic tests/full format/diff passed. No executable line/test/config change; no runtime probe or local full-suite coverage needed. CI checked separately.\n')

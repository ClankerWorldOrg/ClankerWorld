from pathlib import Path
import os,subprocess,json,hashlib,shutil,re,gzip
root=Path(__file__).resolve().parents[2];out=root/'.evidence/2561-continuation';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
def run(name,args):
 with (out/(name+'.log')).open('w') as log:
  r=subprocess.run(args,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 (out/(name+'.exit-code')).write_text(str(r.returncode)+'\n')
 if r.returncode:raise SystemExit(name+' failed; read log')
run('restore-base',['dotnet','restore','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','--locked-mode'])
cfg=root/'tests/ClankerWorld.Tests/.editorconfig';assert not cfg.exists()
cfg.write_text('[*.cs]\ndotnet_diagnostic.IDE0051.severity = warning\ndotnet_diagnostic.IDE0052.severity = warning\ndotnet_diagnostic.IDE0060.severity = warning\ndotnet_diagnostic.IDE0005.severity = warning\n')
try:
 run('build-base',['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','--configuration','Release','--no-restore','--no-incremental','-p:EnforceCodeStyleInBuild=true','-p:GenerateDocumentationFile=true','-p:TreatWarningsAsErrors=false'])
finally:cfg.unlink()
folder=root/'tests/ClankerWorld.Tests/bin/Release/net10.0';frozen=folder.with_name('net10.0-runtime-continuation-base-source');assert not frozen.exists();shutil.copytree(folder,frozen)
pins={}
for name in ['ClankerWorld.Tests','ClankerWorld.Simulation','ClankerWorld.Server']:
 for ext in ['dll','pdb']:
  p=folder/(name+'.'+ext);data=p.read_bytes();pins[p.name]=hashlib.sha256(data).hexdigest();target=out/'raw-build/base'/p.name;target.parent.mkdir(parents=True,exist_ok=True);target.with_suffix(target.suffix+'.gz').write_bytes(gzip.compress(data,mtime=0))
(out/'base-pins.json').write_text(json.dumps(pins,indent=2)+'\n')
diagnostics=set()
for line in (out/'build-base.log').read_text().splitlines():
 m=re.search(r'warning (IDE(?:0051|0052|0060|0005)): (.*?) \[',line)
 if m:diagnostics.add(m.group(1)+': '+m.group(2))
(out/'unused-base.json').write_text(json.dumps(sorted(diagnostics),indent=2)+'\n')
run('discovery-base',['dotnet','test',str(frozen/'ClankerWorld.Tests.dll'),'--list-tests'])
(out/'base-build.finished').write_text('Base63e5970f restore/build/unused/discovery complete; no coverage collected yet.\n')
print(json.dumps({'unused_message_groups':len(diagnostics),'pins':pins,'frozen':str(frozen)}))

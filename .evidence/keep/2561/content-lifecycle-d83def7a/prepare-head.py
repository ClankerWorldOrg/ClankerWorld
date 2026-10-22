from pathlib import Path
import os,subprocess,shutil,json
root=Path(__file__).resolve().parents[2];base=Path('/workspace/ClankerWorld-2561-continuation');out=root/'.evidence/2561-content'
assert (base/'.evidence/2561-continuation/base-build.finished').exists()
for project in ['ClankerWorld.Simulation','ClankerWorld.Server']:
 for directory in ['bin','obj']:
  shutil.copytree(base/'src'/project/directory,root/'src'/project/directory,dirs_exist_ok=True)
shutil.copytree(base/'src/ClankerWorld.GodotClient/.godot/mono/temp',root/'src/ClankerWorld.GodotClient/.godot/mono/temp',dirs_exist_ok=True)
shutil.copytree(base/'tests/ClankerWorld.Tests/bin/Release/net10.0-runtime-continuation-base-source',root/'tests/ClankerWorld.Tests/bin/Release/net10.0',dirs_exist_ok=True)
for name in ['base-pins.json','unused-base-records.json','before-tests.txt','before-affected-tests.txt']:
 shutil.copyfile(base/'.evidence/2561-continuation'/name,out/name)
shutil.copyfile(base/'.evidence/2561-continuation/reader-content-audit.md',out/'reader-content-audit.md')
env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
with (out/'restore-head.log').open('w') as log:r=subprocess.run(['dotnet','restore','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','--locked-mode'],cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
(out/'restore-head.exit-code').write_text(str(r.returncode)+'\n');assert r.returncode==0
(out/'base-build-provenance.json').write_text(json.dumps({'commit':'63e5970fd131a341d9ae86cc26fa6c43fac327bf','source':str(base),'fresh_build':True,'production_bytes':'frozen base Simulation/Server DLL/PDB; head builds tests only; six base hashes saved','base_coverage':'new whole2class175case82method run, no reused earlierarea coverage'},indent=2)+'\n')
print('Frozen production outputs copied; head locked restore passed. No head build/test/coverage yet.')

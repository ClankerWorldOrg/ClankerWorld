from pathlib import Path
import os,json,shutil,subprocess,sys
p=Path('/workspace/2566-audit/weather-boundaries');scope=json.loads((p/'scope.json').read_text());side=sys.argv[1];root=Path(scope['base_checkout'] if side=='base' else scope['head_checkout']);old=Path('/workspace/ClankerWorld-2566-ca3-base')
assert subprocess.run(['git','diff','--quiet','ca3a5b8d',scope['base'],'--','src','global.json','Directory.Build.props','Directory.Build.targets','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','skills/test-audit/references/coverage.runsettings'],cwd=root).returncode==0
for folder in (old/'src').iterdir():
 if folder.is_dir():
  for name in ['bin','obj','.godot']:
   if (folder/name).is_dir():shutil.copytree(folder/name,root/'src'/folder.name/name,dirs_exist_ok=True)
env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='2'
with (p/(side+'-restore.log')).open('w') as f:subprocess.run(['dotnet','restore','--locked-mode'],cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT,check=True)
args=['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','-c','Release','--no-restore','--no-incremental','-m:1','-p:UseSharedCompilation=false','-p:BuildProjectReferences=false']
with (p/(side+'-build.log')).open('w') as f:subprocess.run(args,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT,check=True)
(p/(side+'-build-provenance.json')).write_text(json.dumps(dict(args=args,production_cached_from=str(old),production_and_config_trees_identical_between='ca3a5b8d and '+scope['base'],normal_test_build='Fresh nonincremental Release, unchanged production dependencies reused explicitly; canonical raw bytes verified by coverage receipts.'),indent=2)+'\n');print(side,'normal build complete')

from pathlib import Path
import subprocess,os
out=Path('.evidence/2551');env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
for label,cmd in [('restore',['dotnet','restore','--locked-mode']),('build',['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','-c','Release','--no-restore','--no-incremental'])]:
 with (out/(label+'-head.log')).open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT)
 (out/(label+'-head.exit-code')).write_text(str(r.returncode)+'\n');print(label,r.returncode,flush=True);assert r.returncode==0
(out/'head-build.finished').write_text('Fresh locked Release head build complete.\n')

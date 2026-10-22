from pathlib import Path
import subprocess,os
out=Path('.evidence/2551-imports');env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4';cmd=['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','-c','Release','--no-restore','--no-incremental']
with (out/'build.log').open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT)
(out/'build.exit-code').write_text(str(r.returncode)+'\n');print('build',r.returncode,flush=True);assert r.returncode==0
(out/'build.finished').write_text('Fresh locked-restored Release build passed.\n')

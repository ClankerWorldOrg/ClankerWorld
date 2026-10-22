from pathlib import Path
import subprocess,os,json
root=Path.cwd();out=root/'.evidence/2551';env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4'
config=root/'src/ClankerWorld.Simulation/World/Civic/.editorconfig';assert not config.exists();config.write_text('[*.cs]\ndotnet_diagnostic.IDE0005.severity = warning\n')
try:
 for label,cmd in [('restore',['dotnet','restore','--locked-mode']),('build',['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','-c','Release','--no-restore','--no-incremental','-p:EnforceCodeStyleInBuild=true','-p:GenerateDocumentationFile=true','-p:TreatWarningsAsErrors=false'])]:
  with (out/(label+'-base.log')).open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT)
  (out/(label+'-base.exit-code')).write_text(str(r.returncode)+'\n');print(label,r.returncode,flush=True);assert r.returncode==0
finally:config.unlink()
(out/'base-build.finished').write_text('Fresh locked Release baseline built at c2df5428 with area-only IDE0005 config removed.\n')

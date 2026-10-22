from pathlib import Path
import subprocess,os,json
out=Path('.evidence/2551-attachment');env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='2'
cmd=['bash','scripts/compare-tick-equivalence.sh','/workspace/ClankerWorld-2551-attachment-native-base','/workspace/ClankerWorld-2551-attachment-native-head',str((out/'native').resolve())];(out/'native-command.json').write_text(json.dumps(cmd,indent=2)+'\n')
with (out/'native.log').open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT)
(out/'native.exit-code').write_text(str(r.returncode)+'\n');print('native',r.returncode,flush=True);assert r.returncode==0
(out/'native.finished').write_text('Standard unchanged native probe passed on independent clean fixed-base/head worktrees; see run.json provenance and frame comparison.\n')

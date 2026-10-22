from pathlib import Path
import subprocess,os,sys,json,datetime
root=Path(sys.argv[1]);side=sys.argv[2];e=root/'.evidence/keep/2566-map-rules';e.mkdir(parents=True,exist_ok=True)
config=root/'tests/ClankerWorld.Tests/.editorconfig';assert not config.exists(),config
for p in Path('/proc').iterdir():
 if not p.name.isdigit():continue
 try:args=(p/'cmdline').read_bytes().replace(b'\0',b' ').decode(errors='replace')
 except OSError:continue
 assert not ('testhost.dll' in args and str(root)+'/tests/' in args),(p,args)
args=['dotnet','build','tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj','-c','Release','--no-restore','--no-incremental','-m:1','-p:UseSharedCompilation=false','-p:EnforceCodeStyleInBuild=true','-p:GenerateDocumentationFile=true','-p:TreatWarningsAsErrors=false']
if len(sys.argv)>3 and sys.argv[3]=='--no-refs':args.append('-p:BuildProjectReferences=false')
meta={'args':args,'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='2'
try:
 config.write_text('[*.cs]\n'+''.join('dotnet_diagnostic.'+c+'.severity = warning\n' for c in ['IDE0051','IDE0052','IDE0060','IDE0005']))
 with (e/(side+'-unused.log')).open('w') as log:r=subprocess.run(args,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 meta.update({'exit_code':r.returncode,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()});(e/(side+'-unused.json')).write_text(json.dumps(meta,indent=2)+'\n')
finally:config.unlink(missing_ok=True)
print((e/(side+'-unused.log')).read_text()[-1300:]);raise SystemExit(r.returncode)

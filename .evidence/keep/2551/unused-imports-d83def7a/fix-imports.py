from pathlib import Path
import subprocess,os,json,hashlib,re
root=Path.cwd();out=root/'.evidence/2551-imports';old=Path('/workspace/ClankerWorld-2551/.evidence/2551/base-unused-import-files.json');files=[str(Path(p).relative_to('/workspace/ClankerWorld-2551-base')) for p in json.loads(old.read_text())]
base='f82570db918e057ff49a1155f6ab9e7bdb2172e8';before={p:(root/p).read_text() for p in files};assert all(not re.search(r'^\s*#',s,re.M) for s in before.values());(out/'before-source.json').write_text(json.dumps(before,indent=2)+'\n');(out/'before-ledger.json').write_text(json.dumps([{'file':p,'decision':'remove compiler-proven unused imports only; retain every declaration/statement','base':base,'sha256':hashlib.sha256(s.encode()).hexdigest(),'lines':len(s.splitlines()),'competing_ready_pr':2747 if p in ['src/ClankerWorld.Simulation/World/Civic/TownSystem.NonLandHearings.cs','src/ClankerWorld.Simulation/World/Civic/TownSystem.HouseholdLandGrants.cs','src/ClankerWorld.Simulation/World/Civic/TownSystem.TownMembership.cs'] else None} for p,s in before.items()],indent=2)+'\n')
config=root/'src/ClankerWorld.Simulation/World/Civic/.editorconfig';assert not config.exists();config.write_text('[*.cs]\ndotnet_diagnostic.IDE0005.severity = warning\n');env=os.environ.copy();env['PATH']='/workspace/tools/dotnet:'+env['PATH'];env['DOTNET_PROCESSOR_COUNT']='4';cmd=['dotnet','format','style','--diagnostics','IDE0005','--no-restore','--include',*files];(out/'semantic-fix-command.json').write_text(json.dumps(cmd,indent=2)+'\n')
try:
 with (out/'semantic-fix.log').open('w') as log:r=subprocess.run(cmd,env=env,stdout=log,stderr=subprocess.STDOUT)
 (out/'semantic-fix.exit-code').write_text(str(r.returncode)+'\n');assert r.returncode==0
finally:config.unlink()
def meaningful(s):return [l for l in s.splitlines() if l.strip() and not re.match(r'^using [^;]+;$',l)]
summary=[]
for p,original in before.items():
 after=(root/p).read_text();assert meaningful(original)==meaningful(after),p
 summary.append({'file':p,'before_lines':len(original.splitlines()),'after_lines':len(after.splitlines()),'removed_imports':[l for l in original.splitlines() if l not in after.splitlines() and l.startswith('using ')],'non_import_lines_identical':True})
(out/'semantic-diff-check.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps({'files':sum(x['before_lines']!=x['after_lines'] for x in summary),'imports_removed':sum(len(x['removed_imports']) for x in summary),'net_lines_removed':sum(x['before_lines']-x['after_lines'] for x in summary)},indent=2))

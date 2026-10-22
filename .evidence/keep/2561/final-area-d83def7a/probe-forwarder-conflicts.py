from pathlib import Path
import subprocess,os,json,tempfile
root=Path.cwd();out=root/'.evidence/2561-grown';base=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
entries=[('child','ChildInitialIdentityTests.cs','BeforeChildhoodAsync','InfancyEnds.Value',2718),('later','GrownAgentConversationTests.cs','CreateConversationLaterAdultsAsync','LaterAdult.Value',2688)]
report=[]
for name,file,method,cache,pr in entries:
 path='tests/ClankerWorld.Tests/Simulation/World/Runtime/'+file;before=(root/path).read_text()
 if name=='child':
  after=before.replace('await BeforeChildhoodAsync()', 'WorldRuntimeCodec.Decode(await InfancyEnds.Value)')
  dead='    private static async Task<WorldRuntimeState> BeforeChildhoodAsync() => WorldRuntimeCodec.Decode(await InfancyEnds.Value);\n\n';assert dead in after;after=after.replace(dead,'')
 else:
  after=before.replace('new(CreateConversationLaterAdultsAsync);','new(async () =>\n        await GrowLaterFamilyAsync(WorldRuntimeCodec.Decode(await LaterAdult.Value), 4, 5));')
  dead='    private static async Task<byte[]> CreateConversationLaterAdultsAsync() =>\n        await GrowLaterFamilyAsync(WorldRuntimeCodec.Decode(await LaterAdult.Value), 4, 5);\n\n';assert dead in after;after=after.replace(dead,'')
 ledger={'file':path,'decision':'candidate-conflict-probe','removal':method,'reason':'Pure forwarding/decode with all original awaits and separate cache preserved; no native scenario/row/assertion removed. Reject if conflicts with ready work.','base':base,'net_lines_removed':len(before.splitlines())-len(after.splitlines())};(out/(name+'-candidate-ledger.json')).write_text(json.dumps(ledger,indent=2))
 index=out/(name+'.index');env=os.environ.copy();env['GIT_INDEX_FILE']=str(index);subprocess.run(['git','read-tree',base],env=env,check=True)
 blob=subprocess.check_output(['git','hash-object','-w','--stdin'],input=after,text=True).strip();subprocess.run(['git','update-index','--cacheinfo','100644',blob,path],env=env,check=True);tree=subprocess.check_output(['git','write-tree'],env=env,text=True).strip()
 commit=subprocess.check_output(['git','-c','user.name=Codex','-c','user.email=153792487+compoodment@users.noreply.github.com','commit-tree',tree,'-p',base],input='Unpushed candidate conflict probe d83def7a\n',text=True).strip()
 tests=[]
 for n in sorted({2688,2718,pr}):
  ref='refs/audit/2561-final/pr'+str(n)
  a=subprocess.run(['git','merge-tree','--write-tree','--name-only',base,ref],text=True,capture_output=True)
  b=subprocess.run(['git','merge-tree','--write-tree','--name-only',commit,ref],text=True,capture_output=True)
  tests.append({'pr':n,'before_exit':a.returncode,'after_exit':b.returncode,'before':a.stdout,'after':b.stdout})
 report.append({**ledger,'synthetic_unpushed_commit':commit,'comparisons':tests});index.unlink()
(out/'forwarder-conflict-probes.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

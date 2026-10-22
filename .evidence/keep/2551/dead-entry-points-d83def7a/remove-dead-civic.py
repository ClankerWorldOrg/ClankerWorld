from pathlib import Path
import re,json,subprocess
root=Path.cwd();area=root/'src/ClankerWorld.Simulation/World/Civic';selected={'TownSystem.NonLandHearings.cs':'NonLandText','TownSystem.HouseholdLandGrants.cs':'SubmitExpansionLandRequest','TownSystem.TownMembership.cs':'AcceptTownAdmission','TownSystem.NonLandHearings.Actions.cs':'NonLandTerms','TownSystem.LandHearings.Actions.cs':'LandHearingOutcome'};ledger=[];edits=[]
for file,name in selected.items():
 p=area/file;s=p.read_text();m=re.search(r'^    internal [^\n]+\b'+name+r'\(',s,re.M);assert m,name;start=m.start()
 if name=='NonLandText':end=s.index('\n\n',start)+2
 else:end=s.index('\n    }\n',start)+len('\n    }\n');end+=int(s[end:end+1]=='\n')
 removed=s[start:end];assert name in removed
 ledger.append({'file':str(p.relative_to(root)),'method':name,'decision':'delete-dead-method','source_occurrences':1,'removed_lines':len(removed.splitlines()),'reason':'No source/test/script/doc/CI/reflection-name caller; internal owning class. Current retained-plan binding/check/application paths preserve live rules. No tests or executing rule removed.','base':'c2df5428aa774f6f688aa3df62fc3f78de8d366d','removed_source':removed});edits.append((p,s[:start]+s[end:]))
(root/'.evidence/2551/dead-method-ledger.json').write_text(json.dumps(ledger,indent=2)+'\n')
for p,after in edits:p.write_text(after)
print(json.dumps([{k:v for k,v in x.items() if k!='removed_source'} for x in ledger],indent=2))

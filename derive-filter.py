import pathlib,re,sys,json
sys.path.insert(0,'/workspace/ClankerWorld/skills/test-audit/scripts')
from remove_tests import code_mask,declarations,method_span
root=pathlib.Path('/workspace/review-2727/tests/ClankerWorld.Tests')
chosen={
'GodotClient/UI/WorldEventTextTests.cs':lambda n:n not in {'EventLogShowsLifeEventsAndLeavesOutRoutineSteps','NewcomerOfferRequiresTheCurrentRuleAndAStartedWorld'},
'GodotClient/UI/EventContentNamesTests.cs':lambda n:True,
'GodotClient/UI/WorldEventFormatterTransferredTests.cs':lambda n:True,
'Simulation/World/Family/ContinuityTests.cs':lambda n:n in {'NewFounderWorldStartsWithTheContinuityRuleOnAndSaysSoOnce','OrdinaryRefusalReturnsWithFourEligibleCouples','CloseRelativesAndEldersDoNotCountAsEligibleCouples','EligibleCoupleTransitionsReplayAndRejectedTicksKeepTheFlagAndDeadlines','LegacyContinuityEventsKeepTheirHistoricalReason'},
'Simulation/World/Family/ContinuityPartnerTests.cs':lambda n:n=='ContinuityPartneringFourFoundersChooseMutuallyReloadBeforeDeadlineAndReachTwoCouplesThenChildPlans',
'Simulation/World/Towns/TownBridgeRuntimeTests.cs':lambda n:n=='TownGrowthAcrossATwoTileRiverSavesOneRoadBridgeThatMovementDrawingAndInspectionShare',
'Simulation/World/Runtime/ActionRefusalTests.cs':lambda n:n=='ActualNativeActionsAreRefusedOnlyInLiveSingleActorRecovery'}
rows=[]
for path,predicate in chosen.items():
 source=(root/path).read_text();mask=code_mask(source);cls=re.search(r'public sealed (?:partial )?class (\w+)',source)[1]
 names=re.findall(r'^    public (?:async )?(?:void|Task) (\w+)\(',source,re.M)
 for name in names:
  if not predicate(name):continue
  d=declarations(source,mask,name);assert len(d)==1
  a,b=method_span(source,mask,d[0]);body=source[a:b]
  count=1 if '[Fact]' in body else body.count('[InlineData(')
  assert count>0,(path,name)
  rows.append({'test':'ClankerWorld.Tests.'+cls+'.'+name,'file':path,'cases':count,'decision':'retain','retained_proof':['ClankerWorld.Tests.'+cls+'.'+name]})
p=pathlib.Path(__file__).parent
(p/'consumer-methods.json').write_text(json.dumps(rows,indent=2)+'\n')
(p/'filter.txt').write_text('|'.join('FullyQualifiedName='+r['test'] for r in rows)+'\n')
print('methods',len(rows),'cases',sum(r['cases'] for r in rows));print(json.dumps({f:sum(r['cases'] for r in rows if r['file']==f) for f in chosen},indent=2))

from pathlib import Path
import json,re
p=Path('/workspace/2566-audit/animal-restores');base=Path('/workspace/ClankerWorld-2566-e7cc-base');head=Path('/workspace/ClankerWorld-2566-animal-restores');entries=[];methods=[];files=[]
helpers=['RestoreMountedSaddling','RestoreMountedCollection','RestoreMountedCare','RestoreYardCareSupply']
for f in sorted((base/'tests/ClankerWorld.Tests').rglob('*.cs')):
 s=f.read_text()
 if 'partial class AnimalPipelineTests' not in s:continue
 file=str(f.relative_to(base));files.append(file)
 for method in re.findall(r'public (?:async )?(?:Task|void) (\w+)\(',s):
  fq='ClankerWorld.Tests.AnimalPipelineTests.'+method;methods.append(fq)
  e=dict(file=file,test=fq,decision='retain',rows=[],retained_proof=[fq],regression='Distinct native animal/custody/stock/order/rollback/reload/replay input and assertions retained unchanged.',production='AnimalSystem and native WorldRuntime adapters, inventory authority and PersonalEquipmentRules',history='Native animal regressions and action-plan history; helper bodies identical to retained RestoreAnimalOrder. Full-file secondreader and parent ownership/history records.',support='Shared partial class scope, all31files incl external Economy proof included; CreateYard/choosers/options unchanged.',risk='No assertion, production/config/provider or public support change.',command='FullyQualifiedName~ClankerWorld.Tests.AnimalPipelineTests.')
  if method=='MilkCollectionNeedsRoomForTheWholeBatchButNoVessel':
   e.update(decision='delete_rows',rows=['[InlineData(5, true)]'],regression='Whole2unit batch needs room, no vessel. Keep0strictinitialready-productcodec+emptyload,6exactfitwithballast,7oneshort refusal, custody/conservation/stock/events/no supply trip and finalstrictreload.',history='18544ecd(#1348) vesselcapacity regression; a87c9d44(#1734) ordinaryloosemilk replacesvesselrows with0/5/6/7. 9c920264 renamed paths.5trueinterior follows samepositivebranch as6true exactfit;0keepsdifferent initial inventory branch.',risk='Only redundant interior positive5row removed; allmethodassertions and0/6/7inputs remain.')
  elif any(h in s for h in helpers):
   e.update(decision='consolidate',support='Replace only four identical private helper names with EXISTING RestoreAnimalOrder; same argument expressions, state and capturedactor, same actor-selected AnimalChooser animal_order and fresh defaults for others. Delete4defs; no newhelper.',risk='Alltestbodies identical after helper-name normalization; include every consumer/partialclass and existing helper proof.')
  entries.append(e)
assert len(files)==31,(len(files),files)
assert len(methods)==63,(len(methods),methods)
scope=dict(base='e7ccf6a9da1fef954ad525025804fc2b1ee14666',head=None,filter='FullyQualifiedName~ClankerWorld.Tests.AnimalPipelineTests.',archive_path=str(p/'binaries'),archive_label='animal-restores',evidence_folder='2566-animal-restores',expected_cases=dict(before=161,final=160),selected_methods=len(methods),base_checkout=str(base),head_checkout=str(head),ledger_proofs=methods,ledger_proofs_by_side=dict(before=methods,final=methods),selected_methods_by_side=dict(before=methods,final=methods),files=files)
(p/'scope.json').write_text(json.dumps(scope,indent=2)+'\n');(p/'ledger.json').write_text(json.dumps(entries,indent=2)+'\n');(p/'methods.txt').write_text('\n'.join(methods)+'\n');(p/'keep.txt').write_text('\n'.join(methods)+'\n')
for n in ['run_scoped.py','run_unused.py','finish_scoped.py','point_bound.py','check_overlap.py','pipeline.py']:
 s=(Path('/workspace/2566-audit/content-fixtures')/n).read_text().replace('2566-f825-base','2566-e7cc-base').replace('2566-content-fixtures','2566-animal-restores').replace('/2566-audit/content-fixtures','/2566-audit/animal-restores').replace('net10.0.content-fixtures-original','net10.0.animal-restores-original')
 if n=='pipeline.py':
  start=s.index(" b=names((p/'base-discovery.log').read_text());mapped=");end=s.index(" run(['python','skills/test-audit/scripts/check_ledger.py'",start)
  s=s[:start]+" b=names((p/'base-discovery.log').read_text());removed=['ClankerWorld.Tests.AnimalPipelineTests.MilkCollectionNeedsRoomForTheWholeBatchButNoVessel(stone: 5, expected: True)'];expected=[n for n in b if n not in removed]\n assert len(b)-len(expected)==1 and expected==allnames,'discovery changed beyond approved milkrow'\n (p/'discovery-comparison.json').write_text(json.dumps(dict(before_cases=len(b),after_cases=len(allnames),declarations=len({n.split('(',1)[0] for n in allnames}),removed=removed,all_other_names_order_equal=True),indent=2)+'\\n')\n"+s[end:]
 (p/n).write_text(s)
(p/'state.json').write_text(json.dumps(dict(session='8451d624',issue=2566,claim_comment=6335446672,branch='codex/2566-animal-restores-8451d624',rules_checkpoint=scope['base'],base=scope['base'],head=None,draft=None,checkout=str(head),base_checkout=str(base),base_build_session=85098,status='Freshclaimminute rereadclear. Fullfreshnormalbasebuild running. Preeditledger63methods31partialfiles,161/160cases expectedpending actualdiscovery. No sourceedit yet.',previous_ready_pr=2753,previous_evidence='086227d339d4c3aeb7dad9560f224cc501517149',reported_area_gross=528,reported_area_distinct=516,actual_merged_at_base=235,remaining_after_this='Reconcile entire163fileaudit currentmain/mergedreadyoverlap; finalCloses2566 ifno more safe removal'),indent=2)+'\n')
print('Preeditledger',len(files),'files',len(methods),'methods')

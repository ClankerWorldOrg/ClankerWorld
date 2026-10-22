from pathlib import Path
import json,re,hashlib,subprocess
A=Path('/workspace/2566-audit/content-support');B=Path('/workspace/ClankerWorld-2566-ca3-base');H=Path('/workspace/ClankerWorld-2566-content-support');P='tests/ClankerWorld.Tests/Simulation/Content/'
def tok(s):
 # Preserve literal bytes; ignore only C# layout whitespace.
 return re.findall(r'"""[\s\S]*?"""|(?:\$|@)?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|[A-Za-z_]\w*|\d+|\S',s)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
original={c:(B/(P+c+'.cs')).read_text() for c in ['ContentDefinitionTests','ContentGovernanceTests','WorldAssetReservationTests']};candidate={c:(H/(P+c+'.cs')).read_text() for c in original};ex=original.copy()
s=ex['ContentDefinitionTests'];s=s.replace('        IEnumerable<string>? tags = null,\n','',1).replace('        IEnumerable<ContentQuantity>? costs = null,\n','',1).replace('costs ?? [new ContentQuantity("wood", 1)]','[new ContentQuantity("wood", 1)]').replace('tags ?? ["kitchen"]','["kitchen"]').replace('        BuildingDefinition? workstation = null,\n','').replace('workstationBuildingId ?? workstation?.CanonicalId','workstationBuildingId');ex['ContentDefinitionTests']=s
s=ex['ContentGovernanceTests'];s,n=re.subn(r'Package\(\s*("[^"]+")\s*,\s*"1\.0\.0"\s*,',r'Package(\1,',s);assert n==9
s=s.replace('        string version,\n','').replace('ContentVersion.Parse(version)','ContentVersion.Parse("1.0.0")')
s=s.replace('''        foreach (var package in new[] { core, world })
        {
            registry.Propose(package);
        }''','''        registry.Propose(core);
        registry.Propose(world);''')
s=s.replace('''[Definition("item", $"{id}-content", "sha256:" + new string(digestCharacter, 64))]''','''[new ContentDefinition("item", $"{id}-content", ContentVersion.Parse("1.0.0"), $"{id}-content", "sha256:" + new string(digestCharacter, 64))]''')
s=s.replace('''    private static ContentDefinition Definition(string kind, string localId, string digest) => new(
        kind,
        localId,
        ContentVersion.Parse("1.0.0"),
        localId,
        digest);
''','');ex['ContentGovernanceTests']=s
s=ex['WorldAssetReservationTests']
for v in ['2','long.MaxValue']:
 pattern=r'charge switch\s*\{\s*"durable" => request with \{ DurableStorageBytes = '+re.escape(v)+r' \},\s*"cache" => request with \{ DecodedCacheBytes = '+re.escape(v)+r' \},\s*_ => request with \{ GpuBytes = '+re.escape(v)+r' \},\s*\}'
 replacement='charge == "durable" ? request with { DurableStorageBytes = '+v+' } : request with { GpuBytes = '+v+' }'
 s,n=re.subn(pattern,replacement,s);assert n==1
ex['WorldAssetReservationTests']=s
for c in ex:assert tok(ex[c])==tok(candidate[c]),c
# Every assertion lexical slice, including generic Throws/IsType and nested calls, remains exactly the same.
def assertions(s):
 t=tok(s);out=[]
 for i in range(len(t)-1):
  if t[i:i+2]!=['Assert','.']:continue
  j=i
  while t[j]!='(':j+=1
  depth=1;k=j+1
  while depth:
   depth+=(t[k]=='(')-(t[k]==')');k+=1
  out.append(t[i:k])
 return out
for c in ex:
 assert assertions(original[c])==assertions(candidate[c]),c
 assert re.findall(r'\[(?:Fact|Theory|InlineData)[^\n]*\]',original[c])==re.findall(r'\[(?:Fact|Theory|InlineData)[^\n]*\]',candidate[c])
methods=['ClankerWorld.Tests.'+c+'.'+m for c,s in original.items() for m in re.findall(r'public void (\w+)\(',s)]
ledger=json.loads((A/'ledger.json').read_text());scope=json.loads((A/'scope.json').read_text());assert sorted(e['test'] for e in ledger)==sorted(methods) and scope['ledger_proofs']==methods
assert scope['selected_methods_by_side']['before']==scope['selected_methods_by_side']['final']==methods
assert scope['expected_cases']=={'before':16,'final':16};assert scope['filter']=='|'.join('FullyQualifiedName~ClankerWorld.Tests.'+c+'.' for c in original)
files=subprocess.check_output(['git','diff','--name-only',scope['base']],cwd=H,text=True).splitlines();assert sorted(files)==sorted(P+c+'.cs' for c in original)
num=subprocess.check_output(['git','diff','--numstat',scope['base']],cwd=H,text=True);rows=[x.split('\t') for x in num.splitlines()];assert sum(int(r[0]) for r in rows)==23 and sum(int(r[1]) for r in rows)==49
report={'signature':'Codex session8451d624/content-support-source-reader','role':'Read-only author-side ownership/source second read; not independent PR approval','base':scope['base'],'head_when_read':subprocess.check_output(['git','rev-parse','HEAD'],cwd=H,text=True).strip(),'state':'Uncommitted exact candidate against frozen base; hashes pin approved bytes. Recheck committed content against these hashes.','accepted_source':True,'findings':[],'files':{c:{'file':P+c+'.cs','base_sha256':sha(B/(P+c+'.cs')),'candidate_sha256':sha(H/(P+c+'.cs')),'before_lines':len(original[c].splitlines()),'after_lines':len(candidate[c].splitlines()),'assertion_call_sites':len(assertions(original[c])),'assertion_lexical_slices_equal_in_order':True} for c in original},'lines':{'tests_added':23,'tests_removed':49,'net_removed':26,'before':426,'after':400,'production_added_or_removed':0,'docs_added_or_removed':0},'exact_change_check':'Candidate lexical tokens match original after exactly the approved helper-param/fallback specialization,9constant-version call argument removals, one sole-forwarder inline, firstPropose loop expansion core→world, and2cache-arm→ternary replacements. No unlisted lexical change. First two Package call layouts collapse only whitespace.','proof_preservation':{'all_attributes_and_theory_rows_equal':True,'assertion_call_sites_before_and_after':63,'cases_source_derived_before_and_after':16,'declarations_before_and_after':13,'Building':'Exact wood1/kitchen, width/display/digest/defaults retained; tags/costs never supplied.','Recipe':'All four calls omitted workstation object. Direct workstationBuildingId preserves null, malformed and dangling references; inputs/outputs/duration/tags unchanged.','Governance':'All9versions1.0.0 retained. Inline definition preserves item/id-content/version/digest/defaultnull payload and manifest definitions order; two interpolations equal by value, no identity check. core→world Propose calls preserve event order; later Validate/Approve/Stage loop, bool branches, tick numbers, rollback/restore, failure and ordering assertions untouched.','Assets':'Only durable/gpu rows exist. Each ternary retains original field and value for every row; all request factories, policies, IDs, digest sharing, bytes, render costs, serialization, restore, diagnostics and atomicity assertions untouched. Both cache arms unreachable before edit. No production cache rule/case removed.'},'scope':{'filter':scope['filter'],'whole_classes':list(original),'all_changed_support_consumers_and_ledger_proofs_included':True,'no_partial_or_external_helper_users':True,'selected_methods':methods},'risk_and_pending':['No unique input/assertion or unresolved behavior concern. String LocalId/DisplayName reference aliasing changes harmlessly to value-equal immutable strings; value-based manifests/codec/digests unchanged. Temporary two-element proposal array allocation removed.','Source acceptance only; parent owns normalbuild/discovery/unused/original13method measurements/matched16case comparison/overlap/CI. No checks claimed complete by this source review.','Reader made no repository edits, builds, tests, coverage runs or GitHub writes.'], 'proposal_report':str(A/'reader-proposal.json')}
(A/'reader-source.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'accepted_source':True,'report':str(A/'reader-source.json'),'assertions':{c:len(assertions(original[c])) for c in original},'source_hashes':{c:sha(H/(P+c+'.cs')) for c in original}},indent=2))

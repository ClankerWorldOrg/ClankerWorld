from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/content-fixtures');target=Path('/workspace/ClankerWorld-2566-fixtures-evidence/.evidence/keep/2566/content-fixtures-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-f825-base/.evidence/keep/2566-content-fixtures'),'head':Path('/workspace/ClankerWorld-2566-content-fixtures/.evidence/keep/2566-content-fixtures')}
for label,root in roots.items():
 for f in sorted(root.rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(root)
  if any(x in rel.parts for x in ['binaries','obj','bin','__pycache__']):continue
  data=f.read_bytes();dest=target/label/rel;dest.parent.mkdir(parents=True,exist_ok=True);packed=len(data)>100000 or f.suffix in ['xml','trx']
  if packed:dest=dest.with_name(dest.name+'.gz');dest.write_bytes(gzip.compress(data,compresslevel=9,mtime=0))
  else:dest.write_bytes(data)
  manifest.append(dict(path=str(dest.relative_to(target)),original_path=str(f),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data),gzip=packed))
hashes=[]
for f in sorted((source/'binaries').rglob('*')):
 if f.is_file():hashes.append(dict(path=str(f.relative_to(source)),sha256=hashlib.sha256(f.read_bytes()).hexdigest(),bytes=f.stat().st_size))
(target/'binary-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n');(target/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
complete=(source/'local-complete.json').exists()
(target/'README.md').write_text('# Crafting fixture evidence for PR2753\n\nCodex session8451d624. Frozen basef82570db918e057ff49a1155f6ab9e7bdb2172e8; sourcehead21a4a57dca1e1186da3c11678d706c5c2a1261c8. Evidence-only branch outside source PR.\n\nLocal completion marker present: '+str(complete)+'. Consult audit/state.json and signed receipts for pending gates. Intermediate snapshots include unfinished logs explicitly and do not claim unfinished gates passed.\n\nTests+6/-14 net8,457→449lines, source/docs0. All5cases/4methods remain; single theorycase maps to renamedreserved Fact with zero case-removal credit. All assertions/nativecalls and activefixturevalues unchanged; unusedhousequery onlyincidental fail-fast assumption removed. SharedTailorTestWorld unchanged. Fullfresh normalbuilds0warnings/errors, exact7399cases3295methods discovery with only approvedrename. Exactly one matchedtwo-class coveragepair5/5each, rawcollector19419/77873lines14754/88374branches before=head; actualpointbound and independentacceptance determine no losses. Four originalmethods are separate baselineisolations.\n\nLarge text/XML/JSON/TRX gzip restores original manifestSHA256 bytes via gunzip -c. Binarybytes omitted/provenancehashes retained. Absolute paths describe originalexecution, scripts may be nonportable. No full-suitecoverage/mainmerge. CIintegrates newer339ce322warehousecleanup; frozenpair staysf825.\n')
print('archived',len(manifest),'files',sum(x['bytes'] for x in manifest),'uncompressed bytes, complete',complete)

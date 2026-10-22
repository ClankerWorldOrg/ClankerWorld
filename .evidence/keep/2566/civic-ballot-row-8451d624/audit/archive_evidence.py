from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/civic-ballot-row');target=Path('/workspace/ClankerWorld-2566-civic-evidence/.evidence/keep/2566/civic-ballot-row-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-442f-base/.evidence/keep/2566-civic-ballot-row'),'head':Path('/workspace/ClankerWorld-2566-civic-ballot-row/.evidence/keep/2566-civic-ballot-row')}
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
(target/'README.md').write_text('# Civic ballot evidence for PR2769\n\nCodex session 8451d624. Frozen base442fdcffefb3f97ec8bc49fa966261e7dbf7244f; headf82abff0d1ced0bd5d4a47522bc0a0e05fd0c78d. Evidence-only branch, never merge.\n\nLocal completion marker: '+str(complete)+'. Intermediate outputs explicitly do not claim unfinished gates passed; see state and signed reader receipts.\n\nTests+3/-4,110to109lines, same6cases/4methods/17assertions. Only sole theory scaffold becomes a Fact with identical fixed JSON string. Source/docs/config unchanged. Full fresh locked normal builds0warnings/errors; one wholeclass coverage pair and4originalmethodisolations, no fullsuite coverage/mainmerge. Raw accepted pair630/77873lines343/88374branches, exact0loss/gain allscopes;352unmapped0hit. Plannerkeepsall4. Final diagnostics/comparator/CI must be checked in state/receipts.\n\nGzip files retain original-byte hashes in manifest; binaries omitted but hashes preserved. Absolute paths record provenance; scripts are not portable. Independent top-level PR review remains.\n')
print('Archived',len(manifest),'files; localcomplete',complete)

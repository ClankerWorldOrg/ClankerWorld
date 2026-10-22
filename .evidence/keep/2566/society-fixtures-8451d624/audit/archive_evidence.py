from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/society-fixtures');target=Path('/workspace/ClankerWorld-2566-society-evidence/.evidence/keep/2566/society-fixtures-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-17cc-base/.evidence/keep/2566-society-fixtures'),'head':Path('/workspace/ClankerWorld-2566-society-fixtures/.evidence/keep/2566-society-fixtures')}
for label,root in roots.items():
 for f in sorted(root.rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(root)
  if any(x in rel.parts for x in ['binaries','obj','bin','__pycache__']):continue
  if label=='audit' and rel.parts[0]=='isolated' and len(rel.parts)>1 and not (root/'isolated'/rel.parts[1]/'completion.json').exists():continue
  data=f.read_bytes();dest=target/label/rel;dest.parent.mkdir(parents=True,exist_ok=True);packed=len(data)>100000 or f.suffix in ['xml','trx']
  if packed:dest=dest.with_name(dest.name+'.gz');dest.write_bytes(gzip.compress(data,compresslevel=9,mtime=0))
  else:dest.write_bytes(data)
  manifest.append(dict(path=str(dest.relative_to(target)),original_path=str(f),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data),gzip=packed))
hashes=[]
for f in sorted((source/'binaries').rglob('*')):
 if f.is_file():hashes.append(dict(path=str(f.relative_to(source)),sha256=hashlib.sha256(f.read_bytes()).hexdigest(),bytes=f.stat().st_size))
(target/'binary-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n');(target/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');complete=(source/'local-complete.json').exists()
(target/'README.md').write_text('# Society fixture evidence for PR2770\n\nCodex session 8451d624. Frozen base17ccaf0cc7d2c65add79cd9601602858e5755aad; heade19ffa5e99f520761d920725e6f824634f750dfd. Evidence-only branch, never merge.\n\nLocal completion marker: '+str(complete)+'. Intermediate outputs do not claim unfinished gates passed. State and signed reader receipts distinguish completed measurements. Only original-method directories with actual completion receipts are archived.\n\nTests+4/-6,572to570lines, same29cases/21methods/141assertions. Fixed rate0 Theory becomes Fact with same checkpoint validation; dead ground option omitted with identical defaultnull for all private helper consumers. Source/docs/config unchanged. Both fresh normal builds0warnings/errors; one whole-two-class pair29/29each passes. Raw accepted pair17336/77873lines11772/88374branches, exact0lost/gained allscopes,352unmapped74hit. All21 original methods must finish before final acceptance; see receipts, not this intermediate summary. No fullsuite coverage or mainmerge.\n\nGzip files retain original-byte hashes in manifest; binaries omitted but hashes preserved. Absolute paths record provenance; scripts are not portable. Independent top-level PR review remains.\n')
print('Archived',len(manifest),'files; localcomplete',complete)

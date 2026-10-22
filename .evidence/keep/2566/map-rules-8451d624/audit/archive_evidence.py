from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/map-rules');target=Path('/workspace/ClankerWorld-2566-map-evidence/.evidence/keep/2566/map-rules-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-452e-base/.evidence/keep/2566-map-rules'),'head':Path('/workspace/ClankerWorld-2566-map-rules/.evidence/keep/2566-map-rules')}
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
(target/'README.md').write_text('# Map proof evidence for PR2773\n\nCodex session8451d624. Frozen base452e18a6f8c1380232706e12c8756ee998d0b1d6; headfaa50b5036e8d72eef85af5bfad35d738b76ba33. Evidence-only branch, never merge.\n\nLocal completion marker: '+str(complete)+'. Intermediate reports do not claim unfinished checks passed. State and signed receipts distinguish gates. Original-method folders included only after actual completion. Full inherited163-file audit and fresh ledger distinguish main235merged from ready proposals. Final supported-limit closes2566 below target; retain distinct input/native/refusal/replay contracts.\n\nSource/docs+0/-0,tests+5/-9,471to467lines,same20cases16methods137assertiontokens. Repeated pure route proof10calls to1; soleMedium/High/80 row toFact preserving sameFQ,native generation/options/bytes. No fullsuite coverage ormainmerge.\n\nGzip retains original-byte hashes; binaries omitted with hashes retained. Absolute provenance paths are not portable. Independent PR review remains.\n')
print('Archived',len(manifest),'files; localcomplete',complete)

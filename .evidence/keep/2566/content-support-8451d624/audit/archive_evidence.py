from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/content-support');target=Path('/workspace/ClankerWorld-2566-content-evidence/.evidence/keep/2566/content-support-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-ca3-base/.evidence/keep/2566-content-support'),'head':Path('/workspace/ClankerWorld-2566-content-support/.evidence/keep/2566-content-support')}
for label,root in roots.items():
 for f in sorted(root.rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(root)
  if any(x in rel.parts for x in ['binaries','obj','bin']):continue
  data=f.read_bytes();dest=target/label/rel;dest.parent.mkdir(parents=True,exist_ok=True);packed=len(data)>100000 or f.suffix in ['xml','trx']
  if packed:dest=dest.with_name(dest.name+'.gz');dest.write_bytes(gzip.compress(data,compresslevel=9,mtime=0))
  else:dest.write_bytes(data)
  manifest.append(dict(path=str(dest.relative_to(target)),original_path=str(f),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data),gzip=packed))
hashes=[]
for f in sorted((source/'binaries').rglob('*')):
 if f.is_file():hashes.append(dict(path=str(f.relative_to(source)),sha256=hashlib.sha256(f.read_bytes()).hexdigest(),bytes=f.stat().st_size))
(target/'binary-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n');(target/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
complete=(source/'local-complete.json').exists()
(target/'README.md').write_text('# Content support evidence for PR2742\n\nCodex session8451d624. Frozen base ca3a5b8dcde715b0fbfabad64e7cc708aae4fa84; source head ab4b213f2ef03ae9d431587cc6a95702b62fc014. Evidence-only branch, never merge into source PR.\n\nLocal completion marker present: '+str(complete)+'. Consult audit/state.json, current source PR description and signed reader receipts for pending gates. Initial snapshots while checks run preserve completed paired reports and explicitly include incomplete logs; they do not assert unfinished checks pass.\n\nAll16cases/13methods/63 assertion call sites retained; tests+23/-49(net26), source/docs0. Exactly one whole-three-class matched base/head coverage pair, both16/16pass and collector1131lines412branches hit outof77871/88374; original13method isolation is separate evidence. Normal builds0warnings/errors, full7394case3295method discovery exact; ledger passes.\n\nRaw text/XML/JSON/TRX under audit, before and head. Large files are gzip; gunzip -c restores manifest SHA256 original bytes. Binary bytes omitted, hashes retained. Absolute local paths describe original execution. No full-suite coverage or main merge. Native Windows results/CI are automated checks, no manual game playtest.\n')
print('archived',len(manifest),'files',sum(x['bytes'] for x in manifest),'uncompressed bytes, complete',complete)

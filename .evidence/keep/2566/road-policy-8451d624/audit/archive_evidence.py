from pathlib import Path
import gzip,hashlib,json,shutil
source=Path('/workspace/2566-audit/road-policy');target=Path('/workspace/ClankerWorld-2566-road-evidence/.evidence/keep/2566/road-policy-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-e61-base/.evidence/keep/2566-road-policy'),'head':Path('/workspace/ClankerWorld-2566-road-policy/.evidence/keep/2566-road-policy')}
for label,root in roots.items():
 for f in sorted(root.rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(root)
  if any(x in rel.parts for x in ['binaries','obj','bin']):continue
  data=f.read_bytes();dest=target/label/rel;dest.parent.mkdir(parents=True,exist_ok=True)
  packed=len(data)>100000 or f.suffix in ['xml','trx']
  if packed:dest=dest.with_name(dest.name+'.gz');dest.write_bytes(gzip.compress(data,compresslevel=9,mtime=0))
  else:dest.write_bytes(data)
  manifest.append(dict(path=str(dest.relative_to(target)),original_path=str(f),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data),gzip=packed))
hashes=[]
for f in sorted((source/'binaries').rglob('*')):
 if f.is_file():hashes.append(dict(path=str(f.relative_to(source)),sha256=hashlib.sha256(f.read_bytes()).hexdigest(),bytes=f.stat().st_size))
(target/'binary-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n');(target/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(target/'README.md').write_text('# Road proposal evidence for PR2737\n\nCodex session 8451d624. Frozen base e61a1d8dc3f4b2ca3105e1e50477be077eb902f4; accepted head c7b8fb4723d93b92be07076251075ff5410f05f8. PR source is independent of this evidence-only commit.\n\nAll9cases/3methods retained. Exactly one whole-class matched base/head coverage pair, separate original-method isolated reports, zero losses/gains in77871line/88374branch points;263/239hit before=head. Normal locked builds, exact7398case/3297method discovery, unused384records/168groups, formatter, ledger and second-reader source/raw acceptance pass. CI39730732399 allrequiredgates green, nativeinstaller literalpass; PR ready. Main was never merged/retested.\n\nRaw files are under audit, before and head. Large text/XML/TRX files are gzip-compressed; gunzip -c restores original bytes, verified by manifest SHA256. Binary bytes are omitted, provenance hashes retained. Absolute local paths in run receipts describe the original execution, not portable commands. Run scripts are frozen evidence and may refer to original paths. No full-suite coverage is claimed. All352unmappedbranches are unhit; unique-planner233 is mapped rounded lower bound, actualcollector239. Unused4591 warnings are total,4399CS1591+192IDE.\n')
print('archived',len(manifest),'files',sum(x['bytes'] for x in manifest),'uncompressed bytes')

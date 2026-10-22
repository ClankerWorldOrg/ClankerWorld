from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/weather-boundaries');target=Path('/workspace/ClankerWorld-2566-weather-evidence/.evidence/keep/2566/weather-boundaries-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-c2df-base/.evidence/keep/2566-weather-boundaries'),'head':Path('/workspace/ClankerWorld-2566-weather-boundaries/.evidence/keep/2566-weather-boundaries')}
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
(target/'README.md').write_text('# Weather/daylight evidence for PR2748\n\nCodex session8451d624. Frozen base c2df5428aa774f6f688aa3df62fc3f78de8d366d; source head45f66c6b88526f169d21846a3b40638b376406c1. Evidence-only branch, outside source PR.\n\nLocal completion marker present: '+str(complete)+'. Consult audit/state.json and signed reader receipts for pending gates. Intermediate snapshots explicitly include incomplete logs and make no claim unfinished gates pass.\n\nThree exact theory rows and one duplicate assertion block removed, tests+0/-8, source/docs0. Whole affected filter36→33cases/15methods passes once on each revision. Collector423/77871lines188/88374branches hit before=head; actual identities/comparator receipts determine loss bound. Original15method reports are separate baseline isolation. Normal Release test builds zero warnings/errors reuse unchanged production dependency outputs after ca3..c2df source/config equivalence check; production4 canonical bytes for both measurements. Source reader validates all retained bytes and nine exact clock points.\n\nLarge raw text/XML/JSON/TRX files use gzip; gunzip -c restores original bytes verified by manifest SHA256. Binary bytes omitted, provenance hashes retained. Paths describe original execution and scripts may refer to absolute local paths. No full-suite coverage/main merge. Two driver errors were corrected before any head measurement: discovery must remove exactly3rows, and normal-head archive folder was renamed to match scope; failed premeasurement logs are preserved, no coverage repeated.\n')
print('archived',len(manifest),'files',sum(x['bytes'] for x in manifest),'uncompressed bytes, complete',complete)

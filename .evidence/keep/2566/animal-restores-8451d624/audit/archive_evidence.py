from pathlib import Path
import gzip,hashlib,json
source=Path('/workspace/2566-audit/animal-restores');target=Path('/workspace/ClankerWorld-2566-animal-evidence/.evidence/keep/2566/animal-restores-8451d624');target.mkdir(parents=True,exist_ok=True);manifest=[]
roots={'audit':source,'before':Path('/workspace/ClankerWorld-2566-e7cc-base/.evidence/keep/2566-animal-restores'),'head':Path('/workspace/ClankerWorld-2566-animal-restores/.evidence/keep/2566-animal-restores')}
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
(target/'README.md').write_text('# Animal restore evidence for PR2759\n\nCodex session8451d624. Frozen mainbasee7ccf6a9da1fef954ad525025804fc2b1ee14666; sourcehead774e13439a065afebf4426580a99edc6b398da8a. Evidence-only branch outside sourcePR.\n\nLocal completion marker: '+str(complete)+'. Intermediate unfinished outputs never claim gates passed; consult audit/state.json and signed receipts. Tests+26/-39net13,8files954→941; source/docs0. Actualdiscovery7396→7395cases3295methods, scoped161→160cases63methods31partialfiles including unchanged Economyconsumer. Only milk5true removed; all835Assertcalls remain. Fouridenticalprivatehelpers→existingRestoreAnimalOrder,26calls exact.\n\nFullfreshnormalbuilds0warnings/errors; one matchedwholeclasspair and63originalmethodisolations planned. Frozenpairneverrefreshed/fullsuitecoverage notrun. BranchREADMEupdatedasprogresscompletes. Inherited all163file fullreadledgers fromd83def7aarehistorical; reconciliation identifies9changedsince8baudit. Actualmergedatfrozenbase235/27877lines; reportedreadyproposals separate.\n\nLargefilesgzip withoriginal-byte manifestSHA256, binariesomittedbuthashesretained. Absolutepathsexecutionprovenance, scriptsnotportable. IndependentPRreviewremains.\n')
print('archived',len(manifest),'files;localcomplete',complete)

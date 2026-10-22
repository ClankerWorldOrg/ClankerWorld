from pathlib import Path
import json,subprocess,hashlib,sys
out=Path('.evidence/2551-attachment');ledger=json.loads((out/'area-read-ledger.json').read_text());batch=json.loads((out/'read-batch.json').read_text());done={r['file'] for r in ledger}
for file in batch:
 assert file not in done,file
 data=subprocess.check_output(['git','show','HEAD^:'+file]);ledger.append({'file':file,'read':'full','revision':'e7ccf6a9da1fef954ad525025804fc2b1ee14666','sha256':hashlib.sha256(data).hexdigest(),'lines':len(data.splitlines()),'decision':sys.argv[1],'evidence':'Parent session explicitly read full bounded batch; no output truncation.'})
(out/'area-read-ledger.json').write_text(json.dumps(ledger,indent=2)+'\n');print('Full read:',len(ledger),'files',sum(r['lines'] for r in ledger),'lines')

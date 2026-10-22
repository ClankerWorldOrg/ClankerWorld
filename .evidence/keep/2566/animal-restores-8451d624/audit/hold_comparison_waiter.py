from pathlib import Path
import os,signal,json,time,datetime
p=Path('/workspace/2566-audit/animal-restores');head=Path('/workspace/ClankerWorld-2566-animal-restores');base=Path('/workspace/ClankerWorld-2566-e7cc-base');expected=str(p/'pipeline.py');candidates=[]
for d in Path('/proc').iterdir():
 if not d.name.isdigit():continue
 try:a=(d/'cmdline').read_bytes().decode().split('\0')
 except (OSError,UnicodeDecodeError):continue
 if len(a)>2 and a[1]==expected and a[2]=='final':candidates.append(int(d.name))
assert len(candidates)==1,candidates
pid=candidates[0];receipt=head/'.evidence/keep/2566-animal-restores/after-unused.json';assert json.loads(receipt.read_text())['exit_code']==0;assert not(head/'tests/ClankerWorld.Tests/.editorconfig').exists()
# Both measurements, formatting and the head diagnostic build have already ended.
# Pause only the idle Python comparison waiter, whose inherited30min loop is too short.
children=[]
for d in Path('/proc').iterdir():
 if not d.name.isdigit():continue
 try:status=(d/'status').read_text();ppid=int(next(x.split()[1] for x in status.splitlines() if x.startswith('PPid:')))
 except (OSError,StopIteration,ValueError):continue
 if ppid==pid:children.append(int(d.name))
assert not children,children
meta={'pid':pid,'command':[expected,'final'],'reason':'Preserve single measurements while63originalmethodisolations finish; pause only idle comparison waiter to prevent30min bookkeeping deadline. No test/build/source/coverage changed.','paused_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'head_unused_exit':0};os.kill(pid,signal.SIGSTOP);(p/'comparison-waiter-pause.json').write_text(json.dumps(meta,indent=2)+'\n');print('Paused idle comparison waiter',pid,flush=True)
for _ in range(18000):
 f=base/'.evidence/keep/2566-animal-restores/before-unused.json'
 if f.exists():
  m=json.loads(f.read_text())
  if 'exit_code' in m:
   assert m['exit_code']==0,m
   actual=(Path('/proc')/str(pid)/'cmdline').read_bytes().decode().split('\0');assert actual[1:3]==[expected,'final'],actual
   os.kill(pid,signal.SIGCONT);meta['resumed_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();meta['real_base_unused_exit']=0;(p/'comparison-waiter-pause.json').write_text(json.dumps(meta,indent=2)+'\n');print('Resumed waiter after real base-unused success',flush=True);break
 time.sleep(2)
else:raise RuntimeError('Base audit still unfinished after10hours; resume/continue only after real successful receipt')

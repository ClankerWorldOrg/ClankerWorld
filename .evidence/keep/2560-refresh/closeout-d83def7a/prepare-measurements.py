from pathlib import Path
import gzip, hashlib, json, shutil, subprocess, re

root = Path('/workspace/ClankerWorld-2560-closeout')
base = Path('/workspace/ClankerWorld-2560-base')
out = root / '.evidence/2560-refresh'
pin_root = Path('/workspace/ClankerWorld-2560-evidence/.evidence/keep/2560-refresh/pinned-build')
pins = json.loads((pin_root / 'pins.json').read_text())
for tree in (base, root):
    binary = tree / 'tests/ClankerWorld.Tests/bin/Release/net10.0'
    for item in pins:
        name = item['file']
        if tree == root and name.startswith('ClankerWorld.Tests.'):
            continue
        assert hashlib.sha256((binary / name).read_bytes()).hexdigest() == item['sha256'], name
    source = binary.with_name('net10.0-closeout-per-test-source')
    assert not source.exists()
    shutil.copytree(binary, source)
raw = out / 'raw-build'
raw.mkdir()
hashes = {}
for name in ('ClankerWorld.Tests.dll', 'ClankerWorld.Tests.pdb', 'ClankerWorld.Simulation.dll', 'ClankerWorld.Simulation.pdb', 'ClankerWorld.Server.dll', 'ClankerWorld.Server.pdb'):
    content = (root / 'tests/ClankerWorld.Tests/bin/Release/net10.0' / name).read_bytes()
    hashes[name] = hashlib.sha256(content).hexdigest()
    if name.startswith('ClankerWorld.Tests.'):
        (raw / (name + '.gz')).write_bytes(gzip.compress(content))
(out / 'build-pins.json').write_text(json.dumps(hashes, indent=2))
ledger = json.loads((out / 'ledger.json').read_text())
changed = [x for x in ledger if x['decision'] != 'retain']
retained = sorted({p for x in changed for p in x['retained_proof']})
retained = sorted(set(retained) | {'ClankerWorld.Tests.WorldRuntimeTests.UnsupportedInstructionsDoNotSubstituteARealFoodAction'})
original = sorted({x['test'] for x in changed} | set(retained))
(out / 'baseline-methods.txt').write_text('\n'.join(original) + '\n')
(out / 'retained-methods.txt').write_text('\n'.join(retained) + '\n')
(out / 'scope.filter').write_text('FullyQualifiedName~ClankerWorld.Tests.WorldDeliveryOrderTests.|FullyQualifiedName~ClankerWorld.Tests.WorldRuntimeTests.|FullyQualifiedName~ClankerWorld.Tests.WorldProductionOrderParserTests.|FullyQualifiedName~ClankerWorld.Tests.WorldFireOrderTests.\n')
pattern = re.compile(r'^(.*?\.cs)\(\d+,\d+\): warning (IDE\d+): (.*?) \[')
def warnings(content):
    found = set()
    for line in content.splitlines():
        match = pattern.search(line)
        if match:
            path, code, message = match.groups()
            path = path[path.index('/tests/')+1:] if '/tests/' in path else path[path.index('/src/')+1:]
            found.add((path, code, message))
    return found
old = warnings(gzip.decompress(Path('/workspace/recovery-2560/.evidence/keep/2560-refresh/runtime/unused-final.log.gz').read_bytes()).decode())
new = warnings((out / 'build-unused.log').read_text())
comparison = {'before': len(old), 'after': len(new), 'new': sorted(new-old), 'removed': sorted(old-new)}
assert len(old) == 168 and len(new) > 0
assert not comparison['new'], comparison['new']
(out / 'unused-comparison.json').write_text(json.dumps(comparison, indent=2))
files = subprocess.check_output(['git', 'diff', '--name-only', '7fbe6800', 'HEAD'], cwd=root, text=True).splitlines()
lines = {'before': sum(len(subprocess.check_output(['git', 'show', '7fbe6800:' + f], cwd=root).splitlines()) for f in files), 'after': sum(len((root / f).read_bytes().splitlines()) for f in files if (root / f).exists())}
(out / 'test-lines.json').write_text(json.dumps(lines, indent=2))
print(json.dumps({'pins': hashes, 'unused': comparison, 'lines': lines, 'original_methods': original, 'retained_methods': retained}, indent=2))

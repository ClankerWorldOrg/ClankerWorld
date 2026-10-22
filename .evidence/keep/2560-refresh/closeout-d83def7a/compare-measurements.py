from pathlib import Path
import hashlib, json, subprocess, sys

root = Path('/workspace/ClankerWorld-2560-closeout')
out = root / '.evidence/2560-refresh'
tools = out / 'current-audit-tools'
sys.path.insert(0, str(tools))
from coverage_evidence import measurement_report, source_key

if not (out / 'coverage-comparison.exit-code').exists():
    for label in ('before', 'after'):
        command = [sys.executable, str(tools / 'trx_summary.py'), str(out / ('scoped-' + label))]
        result = subprocess.run(command, capture_output=True, text=True, check=True)
        (out / (label + '-summary.json')).write_text(result.stdout)
        print(label, result.stdout[:230])

    def unique_report(folder):
        files = sorted(folder.rglob('coverage.cobertura.xml'), key=lambda p: len(str(p)))
        assert files and len({hashlib.sha256(p.read_bytes()).hexdigest() for p in files}) == 1
        return files[0]

    command = [sys.executable, str(tools / 'compare_coverage.py'),
               str(unique_report(out / 'scoped-before')), str(unique_report(out / 'scoped-after')),
               '--repo', str(root), '--threshold-scope', 'each', '--max-drop-pp', '0']
    result = subprocess.run(command, capture_output=True, text=True)
    (out / 'coverage-comparison.json').write_text(result.stdout)
    (out / 'coverage-comparison.stderr').write_text(result.stderr)
    (out / 'coverage-comparison.exit-code').write_text(str(result.returncode) + '\n')
    print('comparator exit', result.returncode, result.stdout[:700], result.stderr)

def union(folder):
    found = {}
    methods = json.loads((folder / 'methods.json').read_text())
    for method in methods:
        lane = folder / method
        assert json.loads((lane / 'completion.json').read_text()) == {'method': method, 'success': True}
        xml = measurement_report(lane)
        sources = [x.text for x in xml.findall('./sources/source') if x.text]
        for package in xml.findall('./packages/package'):
            for cls in package.findall('./classes/class'):
                path = source_key(cls.attrib['filename'], sources)
                for line in cls.findall('./lines/line'):
                    key = (package.attrib['name'], path, int(line.attrib['number']))
                    branch = line.attrib.get('condition-coverage', '(0/0)').split('(')[-1].split('/')[0]
                    value = (int(line.attrib['hits']) > 0, int(branch))
                    previous = found.get(key, (False, 0))
                    found[key] = (previous[0] or value[0], max(previous[1], value[1]))
    return found

before, after = union(out / 'per-method-before'), union(out / 'per-method-retained')
assert before.keys() == after.keys()
comparison = {}
for package in sorted({key[0] for key in before}):
    keys = [key for key in before if key[0] == package]
    comparison[package] = {
        'before_lines': sum(before[k][0] for k in keys),
        'retained_lines': sum(after[k][0] for k in keys),
        'lost_lines': [list(k[1:]) for k in sorted(keys) if before[k][0] and not after[k][0]],
        'reduced_mapped_branch_lower_bounds': [list(k[1:]) + [before[k][1], after[k][1]] for k in sorted(keys) if before[k][1] > after[k][1]],
    }
(out / 'per-method-union-comparison.json').write_text(json.dumps(comparison, indent=2))
print(json.dumps(comparison, indent=2))

for tree in (Path('/workspace/ClankerWorld-2560-base'), root):
    pins = json.loads((out / 'build-pins.json').read_text()) if tree == root else {p['file']: p['sha256'] for p in json.loads(Path('/workspace/ClankerWorld-2560-evidence/.evidence/keep/2560-refresh/pinned-build/pins.json').read_text())}
    for name, expected in pins.items():
        assert hashlib.sha256((tree / 'tests/ClankerWorld.Tests/bin/Release/net10.0' / name).read_bytes()).hexdigest() == expected, (tree, name)
print('All production/test bytes restored after measurement')

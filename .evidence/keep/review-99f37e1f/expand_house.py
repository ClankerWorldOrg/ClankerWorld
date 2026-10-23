from pathlib import Path
import re
import subprocess

repo = Path('/workspace/review-2668-99f37e1f')
path = 'tests/ClankerWorld.Tests/Simulation/World/Content/HouseContentTests.cs'
base = subprocess.check_output(['git', 'show', f'014f2ef9:{path}'], cwd=repo, text=True)
head = (repo / path).read_text()


def extract(source, declaration):
    start = source.index(declaration)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[opening + 1:end - 1], source[:start] + source[end:]


prepare, head = extract(head, 'private static async Task PrepareWorldAsync(')
site, head = extract(head, 'private static GridPoint FindHouseSite(')
head, count = re.subn(r'await PrepareWorldAsync\(seed, (\d+)\);',
                     lambda m: re.sub(r'\bticks\b', m[1], prepare), head)
assert count == 4
head, count = re.subn(r'var site = FindHouseSite\((state|initial), actor, startingSite\);',
                     lambda m: re.sub(r'\bstate\b', m[1], site).replace('return reachable', 'var site = reachable'), head)
assert count == 2
base, count = re.subn(r'private static int HouseholdWood\([^;]+;', '', base)
assert count == 1
base, count = re.subn(r'HouseholdWood\(world, ("[^"]+")\)', r'HouseholdQuantity(world, \1, "wood")', base)
assert count == 4
normalize = lambda text: re.sub(r'\s+', '', text)
assert normalize(base) == normalize(head), 'Expanded source differs'
print('PASS: complete current-main class equals candidate after expanding only four preparation calls, two site calls and four wood-query substitutions. All five cases, assertions, inputs, transitions, providers and ordering match.')

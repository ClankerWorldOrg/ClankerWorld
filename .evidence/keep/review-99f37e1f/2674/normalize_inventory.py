import re
import subprocess
from pathlib import Path

repo = Path('/workspace/review-2674-99f37e1f')
paths = ['tests/ClankerWorld.Tests/Simulation/Kernel/InventoryAuthorityTests.cs',
         'tests/ClankerWorld.Tests/Simulation/Society/SocietyDeathReservationIntegrityTests.cs']
normal = lambda text: re.sub(r'\s+', '', text)
base = subprocess.check_output(['git', 'show', f'9b42643d^:{paths[0]}'], cwd=repo, text=True)
head = (repo / paths[0]).read_text()
base = base.replace('[Theory]\n    [InlineData("alpha")]', '[Fact]').replace('Reservations(string party)', 'Reservations()')
base = base.replace('2, party)', '2, "alpha")').replace('1, party)', '1, "alpha")')
base = base.replace('[Theory]\n    [InlineData(false)]', '[Fact]').replace('Acceptance(bool firstAlreadyAccepted)', 'Acceptance()')
base = re.sub(r'if \(firstAlreadyAccepted\)\s*\{[^{}]*\}', '', base)
base = base.replace('firstAlreadyAccepted ? "bravo" : "alpha"', '"alpha"')
base = base.replace('[Theory]\n    [InlineData(InventoryReservationState.Committed)]', '[Fact]')
base = base.replace('Checkpoint(InventoryReservationState state)', 'Checkpoint()')
base = base.replace('State = state', 'State = InventoryReservationState.Committed')
base = re.sub(r'if \(state != InventoryReservationState.Committed\)\s*\{[^{}]*\}', '', base)
assert normal(base) == normal(head), 'Inventory class differs beyond exact sole-row constant folding'

base = subprocess.check_output(['git', 'show', f'9b42643d^:{paths[1]}'], cwd=repo, text=True)
head = (repo / paths[1]).read_text()
count = 0
def replace(match):
    global count
    count += 1
    call = match[1]
    if count == 1:
        return 'var inventory = ' + call + ';'
    result = 'inventory = ' + call.replace('state.Inventory', 'inventory') + ';'
    if count == 4:
        result += '\nstate = state with { Inventory = inventory };'
    return result
base = re.sub(r'state = state with\s*\{\s*Inventory = (.*?),\s*\};', replace, base, flags=re.S)
assert count == 4
assert normal(base) == normal(head), 'Death fixture differs beyond four pure-record copy eliminations'
print('PASS: complete inventory class equals exact sole-row constant folding; complete death class equals the same four inventory calls and one final plain-record assignment. Every executed assertion and operation remains.')

import collections
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def read(path):
    result = {}
    for module, documents in json.loads(Path(path).read_text()).items():
        branches = {}
        for document, classes in documents.items():
            document = document.replace('\\', '/')
            if '/src/' not in document:
                raise ValueError(f'Non-production document: {document}')
            document = 'src/' + document.split('/src/', 1)[1]
            for cls, methods in classes.items():
                for method, data in methods.items():
                    for branch in data['Branches']:
                        key = (document, cls, method, *(branch.get(k) for k in ('Line', 'Offset', 'EndOffset', 'Path', 'Ordinal')))
                        if key in branches:
                            raise ValueError(f'Duplicate branch: {key}')
                        branches[key] = branch['Hits'] > 0
        result[Path(module).name] = branches
    return result


before, after = map(read, sys.argv[1:3])
assert before.keys() == after.keys()
result = {}
lost_total = valid_total = hit_before_total = hit_after_total = 0
for assembly, old in before.items():
    new = after[assembly]
    assert old.keys() == new.keys(), f'Branch identity scope changed: {assembly}'
    lost = [key for key in old if old[key] and not new[key]]
    hit_before, hit_after, valid = sum(old.values()), sum(new.values()), len(old)
    result[assembly] = dict(before=hit_before, after=hit_after, valid=valid,
                            lost=len(lost), loss_bound_pp=100 * len(lost) / valid if valid else 0,
                            lost_branches=lost)
    lost_total += len(lost)
    valid_total += valid
    hit_before_total += hit_before
    hit_after_total += hit_after
for xml, hits in ((sys.argv[3], hit_before_total), (sys.argv[4], hit_after_total)):
    root = ET.parse(xml).getroot()
    assert int(root.attrib['branches-valid']) == valid_total
    assert int(root.attrib['branches-covered']) == hits
result['total'] = dict(before=hit_before_total, after=hit_after_total, valid=valid_total,
                       lost=lost_total, loss_bound_pp=100 * lost_total / valid_total)
print(json.dumps(result, indent=2))
assert all(x['loss_bound_pp'] <= 2 for x in result.values())

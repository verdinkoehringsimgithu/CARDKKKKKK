"""按层级与组件身份保留Unity序列化ID，禁止复用已删除对象的编号。"""
from pathlib import Path
import json
import re

REGISTRY = Path(__file__).with_name('voyage_prefab_ids.json')


def identities(text):
    blocks = list(re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S))
    objects, parents, transforms = {}, {}, {}
    for match in blocks:
        kind, number, body = match.groups()
        if kind == '1':
            name = re.search(r'^  m_Name: (.*)$', body, re.M).group(1)
            objects[number] = json.loads(name) if name.startswith('"') else name
        if kind in ('4', '224'):
            owner = re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1)
            transforms[number] = owner
            parents[owner] = re.search(r'm_Father: \{fileID: (\d+)\}', body).group(1)
    paths = {}
    siblings = {}
    for owner, name in objects.items():
        parent = transforms.get(parents[owner])
        base = (parent, name)
        count = siblings.get(base, 0)
        siblings[base] = count + 1
        paths[owner] = (parent, name + (f'#{count}' if count else ''))
    def path(owner):
        parent, name = paths[owner]
        return (path(parent) + '/' if parent else '') + name
    result = {}
    counts = {}
    for match in blocks:
        kind, number, body = match.groups()
        owner = number if kind == '1' else re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1)
        script = re.search(r'm_Script: .*guid: (\w+)', body)
        key = f'{path(owner)}|{kind}|{script.group(1) if script else ""}'
        count = counts.get(key, 0)
        counts[key] = count + 1
        if count:
            key += f'#{count}'
        result[number] = key
    return result


def stabilize(text):
    registry = json.loads(REGISTRY.read_text(encoding='utf8'))
    mapping = {}
    for temporary, key in identities(text).items():
        if key not in registry['ids']:
            registry['next'] += 1
            registry['ids'][key] = registry['next']
        mapping[temporary] = str(registry['ids'][key])
    assert len(mapping.values()) == len(set(mapping.values()))
    text = re.sub(r'(^--- !u!\d+ &)(\d+)', lambda m: m[1] + mapping[m[2]], text, flags=re.M)
    # 带GUID的fileID属于外部资源，不允许改写。
    text = re.sub(r'\{fileID: (\d+)\}', lambda m: '{fileID: ' + mapping.get(m[1], m[1]) + '}', text)
    REGISTRY.write_text(json.dumps(registry, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return text

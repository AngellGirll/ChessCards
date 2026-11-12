#!/usr/bin/env python3
"""Check committed Unity project structure using Python's standard library."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets'
required = [
    'Packages/manifest.json',
    'ProjectSettings/ProjectVersion.txt',
    'ProjectSettings/EditorBuildSettings.asset',
    'ProjectSettings/EditorSettings.asset',
    'ProjectSettings/ProjectSettings.asset',
    'Assets/ChessCards/Scenes/Main.unity',
    '.gitignore', '.gitattributes', 'README.md',
]
for name in required:
    assert (root / name).is_file(), 'Missing: ' + name

version = (root / 'ProjectSettings/ProjectVersion.txt').read_text()
assert re.search(r'^m_EditorVersion: 6000\.\d+\.\d+f\d+$', version, re.M)
manifest = json.loads((root / 'Packages/manifest.json').read_text())
for module in ['audio', 'imgui', 'jsonserialize', 'ui']:
    assert manifest['dependencies']['com.unity.modules.' + module] == '1.0.0'
settings = (root / 'ProjectSettings/ProjectSettings.asset').read_text()
assert re.search(r'^  activeInputHandler: 0$', settings, re.M)
assert re.search(r'^  defaultInterfaceOrientation: 4$', settings, re.M)

guids = {}
for p in assets.rglob('*'):
    if p.name.endswith('.meta'):
        assert p.with_name(p.name[:-5]).exists(), 'Orphan metadata: ' + str(p)
        continue
    meta = p.with_name(p.name + '.meta')
    assert meta.is_file(), 'Missing metadata: ' + str(p)
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
    assert match, 'Malformed metadata: ' + str(meta)
    guid = match[1]
    assert guid not in guids, 'Duplicate GUID: ' + guid
    guids[guid] = p

scene = root / 'Assets/ChessCards/Scenes/Main.unity'
scene_text = scene.read_text()
script_guids = re.findall(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}', scene_text)
assert len(script_guids) == 1
assert guids[script_guids[0]] == root / 'Assets/ChessCards/Runtime/ChessCardsApp.cs'
object_ids = set(re.findall(r'^--- !u!\d+ &(\d+)', scene_text, re.M))
for reference in re.findall(r'\{fileID: (\d+)\}', scene_text):
    assert reference == '0' or reference in object_ids, 'Broken scene object reference: ' + reference
assert 'm_Name: Main Camera' in scene_text and 'm_TagString: MainCamera' in scene_text
assert 'm_Name: Chess Cards Prototype' in scene_text
build = (root / 'ProjectSettings/EditorBuildSettings.asset').read_text()
scene_guid = re.search(r'^guid: (\w+)', scene.with_name(scene.name + '.meta').read_text(), re.M)[1]
assert 'path: Assets/ChessCards/Scenes/Main.unity' in build and 'guid: ' + scene_guid in build
assert re.search(r'^  webGLTemplate: PROJECT:RoyalCourtyard$', settings, re.M)
template = assets / 'WebGLTemplates/RoyalCourtyard/index.html'
assert template.is_file() and 'createUnityInstance' in template.read_text()
resources = assets / 'ChessCards/Resources/ChessCards'
for side in ['White', 'Black']:
    for piece in ['King', 'Queen', 'Rook', 'Bishop', 'Knight', 'Pawn']:
        assert (resources / (side + piece + '.png')).is_file()
for asset in ['Courtyard.jpg', 'RestoredCourtyard.jpg', 'GameRoom.jpg', 'Chess.png', 'ChessPieces.png', 'Checkers.png', 'Go.png',
              'Domino.png', 'Bench.png', 'Lantern.png', 'Cottage.png', 'Coin.png',
              'YardCrown.png', 'YardCrest.png', 'YardCorner.png',
              'CourtyardMusic.ogg', 'Click.ogg', 'Move.ogg', 'Win.ogg']:
    assert (resources / 'Royal' / asset).is_file(), 'Missing presentation asset: ' + asset
assert (resources / 'UI.ttf').is_file()
assert (resources / 'FONT_LICENSE.txt').is_file()
print('PASS Unity project structure, entry scene, object/GUID references, input, modules and metadata (' + str(len(guids)) + ' assets/folders)')
print('Unity Editor import and device builds still require validation in the real Editor.')

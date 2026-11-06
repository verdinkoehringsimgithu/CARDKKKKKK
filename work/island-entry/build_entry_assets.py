from pathlib import Path
import importlib.util
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/IslandSurvival'
GUID = {name: uuid.uuid5(uuid.NAMESPACE_URL, 'cardkkk/island-entry/' + name).hex
        for name in ['folder', 'Unity', 'Prefabs', 'Scenes', 'assembly', 'view', 'prefab', 'scene']}

spec = importlib.util.spec_from_file_location('existing_ui_helpers', ROOT / 'tools/build_spellcraft_ui.py')
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)

def meta(path, guid, folder=False):
    if folder:
        text = f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif path.suffix == '.cs':
        text = f'fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif path.suffix == '.asmdef':
        text = f'fileFormatVersion: 2\nguid: {guid}\nAssemblyDefinitionImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    else:
        importer = 'PrefabImporter' if path.suffix == '.prefab' else 'DefaultImporter'
        text = f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    Path(str(path)+'.meta').write_text(text,encoding='utf8')

OUT.mkdir(exist_ok=True)
meta(OUT, GUID['folder'], True)
for name in ['Unity','Prefabs','Scenes']:
    (OUT/name).mkdir(exist_ok=True)
    meta(OUT/name, GUID[name], True)
meta(OUT/'CardKKK.IslandSurvival.Unity.asmdef', GUID['assembly'])
meta(OUT/'Unity/IslandPreparationView.cs', GUID['view'])

# 准备界面写入真实Prefab；不在运行时拼界面，不放置模拟生存状态。
ui=helper.UI()
root=ui.node('孤岛生存准备界面',size=(1600,900))
ui.component(root,223,'  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n  m_SortingLayerID: 0\n  m_SortingOrder: 10\n  m_TargetDisplay: 0\n')
ui.mono(root,'0cd44c1031e13a943bb63640046fad76','  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1600, y: 900}\n  m_ScreenMatchMode: 1\n  m_MatchWidthOrHeight: 0.5\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n  m_PresetInfoIsWorld: 0\n')
ui.mono(root,'dc42784cf147c0c48a680349fa168899','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
bg=ui.node('背景',root)
ui.image(bg,(.035,.09,.10,1))
panel=ui.node('准备面板',root,(.5,.5),(.5,.5),(0,0),(720,360),(.5,.5))
ui.image(panel,(.08,.17,.18,1))
ui.txt(panel,'标题','孤岛生存',40,44,640,72,44,helper.CREAM,4)
_,status=ui.txt(panel,'状态','开发准备中',40,135,640,42,24,helper.MUTED,4)
_,back=ui.pxbutton(panel,'返回牌桌','返回牌桌',240,238,240,62,(.22,.38,.37,1),24)
view=ui.mono(root,GUID['view'],f'  returnButton: {{fileID: {back}}}\n  statusText: {{fileID: {status}}}\n  tableScenePath: Assets/CardTable/Scenes/CardTableDemo.unity\n')
prefab=OUT/'Prefabs/IslandPreparation.prefab'
prefab.write_text(ui.dump(),encoding='utf8')
meta(prefab,GUID['prefab'])

# 沿用工程已存在的相机、音频监听器、事件系统序列化格式，替换为独立场景引用。
scene=(ROOT/'Assets/TestGameplay/Spellcraft/Scenes/Spellcraft.unity').read_text(encoding='utf8')
scene=scene.replace('850000000','860000000').replace('法术拆装','孤岛生存').replace('法术背景相机','孤岛背景相机').replace('法术事件系统','孤岛事件系统')
scene=scene.replace('  - component: {fileID: 8600000001002}\n','')
scene=re.sub(r'--- !u!114 &8600000001002\n.*?(?=--- !u!)','',scene,flags=re.S)
scene=re.sub(r'--- !u!114 &8600000002002 stripped\n.*','',scene,flags=re.S)
scene=scene.replace('97f0da7da9554d61991d8d43faf34a44',GUID['prefab'])
scene=scene.replace('m_SubmitButton: SpellcraftSubmit','m_SubmitButton: Submit')
level=OUT/'Scenes/IslandSurvival.unity'
level.write_text(scene,encoding='utf8')
meta(level,GUID['scene'])

# 真实入口Prefab增加相邻按钮；旧入口对象和ID不变。
entry=ROOT/'Assets/TestGameplay/Prefabs/TestGameplayEntry.prefab'
text=entry.read_text(encoding='utf8')
if 'm_Name: 孤岛生存\n' not in text:
    buttons=text[text.index('--- !u!1 &1010'):]
    mapping={i:i+20 for i in [1010,1011,1012,1013,1014,1020,1021,1022,1023]}
    buttons=re.sub(r'&([0-9]+)',lambda m:'&'+str(mapping.get(int(m[1]),int(m[1]))),buttons)
    buttons=re.sub(r'fileID: ([0-9]+)',lambda m:'fileID: '+str(mapping.get(int(m[1]),int(m[1]))),buttons)
    buttons=buttons.replace('m_Name: 测试玩法','m_Name: 孤岛生存').replace('m_Text: 测试玩法','m_Text: 孤岛生存')
    buttons=buttons.replace('m_AnchoredPosition: {x: 32, y: -8}','m_AnchoredPosition: {x: 198, y: -8}')
    # 只将顶层按钮设为第二子物体，按钮自己的文字仍为第0个孩子。
    start=buttons.index('--- !u!224 &1031')
    end=buttons.index('--- !u!222 &1032')
    buttons=buttons[:start]+buttons[start:end].replace('m_RootOrder: 0','m_RootOrder: 1')+buttons[end:]
    text=text.replace('  - {fileID: 1011}\n','  - {fileID: 1011}\n  - {fileID: 1031}\n',1)+buttons
    entry.write_text(text,encoding='utf8')

main=ROOT/'Assets/CardTable/Scenes/CardTableDemo.unity'
text=main.read_text(encoding='utf8')
if '  islandButton:' not in text:
    anchor='  gameplayScenePath: Assets/TestGameplay/Spellcraft/Scenes/Spellcraft.unity\n'
    text=text.replace(anchor,anchor+'  islandButton: {fileID: 9106}\n  islandLabel: {fileID: 9107}\n  islandScenePath: Assets/IslandSurvival/Scenes/IslandSurvival.unity\n')
    block=text[text.index('--- !u!114 &9104 stripped'):]
    # 复制旧按钮和标签的stripped引用格式，只更换新增源对象ID。
    block=block.replace('&9104','&9106').replace('&9105','&9107')
    block=block.replace('fileID: 1014, guid: c8dad487ad7c4e89a969d41ef55959d3','fileID: 1034, guid: c8dad487ad7c4e89a969d41ef55959d3')
    block=block.replace('fileID: 1023, guid: c8dad487ad7c4e89a969d41ef55959d3','fileID: 1043, guid: c8dad487ad7c4e89a969d41ef55959d3')
    text+=block
    main.write_text(text,encoding='utf8')

build=ROOT/'ProjectSettings/EditorBuildSettings.asset'
text=build.read_text(encoding='utf8')
if 'path: Assets/IslandSurvival/Scenes/IslandSurvival.unity' not in text:
    text=text.replace('  m_configObjects: {}',f'  - enabled: 1\n    path: Assets/IslandSurvival/Scenes/IslandSurvival.unity\n    guid: {GUID["scene"]}\n  m_configObjects: {{}}')
    build.write_text(text,encoding='utf8')

print('GUID',GUID)
print('Prefab controller',view,'return',back,'status',status)

"""生成《孤岛》修订稿的uGUI预置，不改场景、不启动Unity。"""
from pathlib import Path
import ast
import json
import re

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / 'Assets/IslandSurvival'
GUID = {
    'prefab': '2f31bb27b133458db02699f60ac01fae',
    'view': '6eb55c3f436a4052ad84447c39fdacb5',
    'card': '48f5208832354aa98f75a2adb5b41dbc',
    'task': 'd4dc806215514befb40c1a3f1d2bbda0',
    'bootstrap': 'a6913fb482da426db27b58125b1dddb4',
    'configuration': 'f94aa48e2c8643519334a2bd41a7d064',
    'layout': 'be52be55958055669406d034e94fb665',
    'theme': 'a8c6d2df97af5333a3be0b24d7b42fc1',
    'desaturate': '16fdc875441041c69dc60f463dfba838',
    'ambience': 'a8b89b6fbf324807bb57eb92528250d2',
    'thought': '80b8720ca07442efa215d37ac5053880',
    'drag': 'f98bb37546d746c0936f87efb73bf92d',
}

# 与现有法术、孤岛预置共用序列化器，不执行其他玩法的生成入口。
source = ast.parse((ROOT / 'tools/build_spellcraft_ui.py').read_text(encoding='utf-8'))
names = {'GUID', 'INK', 'CREAM', 'ORANGE', 'PURPLE', 'GOLD', 'WHITE', 'MUTED', 'DARK'}
helpers = {'validate_guid', 'ref', 'ext', 'vec2', 'color', 'string'}
nodes = [node for node in source.body if isinstance(node, ast.ClassDef) and node.name == 'UI'
         or isinstance(node, ast.FunctionDef) and node.name in helpers
         or isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id in names for t in node.targets)]
namespace = {'json': json, 're': re}
exec(compile(ast.Module(body=nodes, type_ignores=[]), 'voyage_ui_serialization', 'exec'), namespace)
UI, ref, ext = (namespace[k] for k in ('UI', 'ref', 'ext'))
UC = namespace['GUID']

DARK = (.036, .085, .087, 1)
PANEL = (.075, .155, .155, 1)
BUTTON = (.155, .285, .27, 1)
RUST = (.61, .28, .18, 1)
WHITE = (.98, .94, .83, 1)
MUTED = (.66, .76, .69, 1)
GOLD = (.93, .75, .45, 1)
BOARD = (.23, .34, .32, 1)
CARD = (126, 104)


def fields(values):
    return ''.join(f'  {key}: {ref(value)}\n' for key, value in values.items())


def scroll(ui, parent, name, x, y, w, h, content_height):
    panel = ui.box(parent, name, x, y, w, h)
    viewport = ui.node('可视区', panel)
    ui.image(viewport, (1, 1, 1, .004), ray=True)
    ui.mono(viewport, UC['rectmask'], '  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
    content = ui.box(viewport, '内容区', 0, 0, w, content_height)
    component = ui.mono(panel, UC['scroll'], f'''  m_Content: {ref(content['rect'])}
  m_Horizontal: 0
  m_Vertical: 1
  m_MovementType: 2
  m_Elasticity: 0.1
  m_Inertia: 0
  m_DecelerationRate: 0.135
  m_ScrollSensitivity: 38
  m_Viewport: {ref(viewport['rect'])}
  m_HorizontalScrollbar: {{fileID: 0}}
  m_VerticalScrollbar: {{fileID: 0}}
  m_HorizontalScrollbarVisibility: 0
  m_VerticalScrollbarVisibility: 0
  m_HorizontalScrollbarSpacing: 0
  m_VerticalScrollbarSpacing: 0
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
''')
    return component, viewport, content


def liquid(ui, node):
    vial = ui.box(node, '水位槽', 91, 34, 23, 48)
    ui.image(vial, (.52, .62, .59, 1))
    inner = ui.box(vial, '槽内', 2, 2, 19, 44)
    ui.image(inner, (.06, .13, .16, 1))
    fill = ui.node('液面', inner, (0, 0), (1, 1), (0, 0), (0, 0), (0, 0))
    color = ui.image(fill, (.24, .68, .66, 1))
    return vial['go'], color


def card(ui, parent, name, x, y, *, active=True, preview_kind=''):
    node = ui.box(parent, name, x, y, *CARD, active)
    face = ui.image(node, BUTTON, ray=not preview_kind)
    frame = ui.box(node, '卡边', 3, 3, 120, 98)
    inner = ui.image(frame, (.11, .23, .23, 1))
    art = ui.image(ui.box(node, '图案', 31, 31, 64, 50))
    _, glyph = ui.txt(node, '类别印记', '', 12, 30, 96, 50, 28, MUTED, 4)
    _, title = ui.txt(node, '牌名', name if preview_kind else '', 8, 5, 110, 24, 17, WHITE, 4)
    _, status = ui.txt(node, '状态', '', 6, 83, 114, 17, 12, WHITE, 4)
    liquid_go, liquid_fill = liquid(ui, node)
    wear_left = ui.image(ui.box(node, '左边缘磨损', 1, 23, 4, 49), RUST)
    wear_right = ui.image(ui.box(node, '右边缘磨损', 121, 28, 4, 49), RUST)
    if preview_kind:
        ui.txt(node, '预览印记', preview_kind, 8, 31, 88 if name in ('头', '躯干') else 110, 48, 28, MUTED, 4)
        for child in node['children']:
            if child['name'] in ('图案', '左边缘磨损', '右边缘磨损') or child['name'] == '水位槽' and name not in ('头', '躯干'):
                child['active'] = 0
        return node
    return ui.mono(node, GUID['card'], fields(dict(face=face, inner=inner, artwork=art, glyph=glyph,
        title=title, status=status, liquid=liquid_go, liquidFill=liquid_fill, wearLeft=wear_left, wearRight=wear_right)))


def task(ui, parent):
    node = ui.box(parent, '任务行模板', 0, 0, 424, 70, False)
    ui.image(node, BUTTON)
    _, title = ui.txt(node, '名称', '', 9, 3, 222, 23, 17, WHITE, 3)
    _, timer = ui.txt(node, '倒计时', '', 278, 3, 136, 23, 16, GOLD, 5)
    _, inputs = ui.txt(node, '参与卡牌', '', 9, 28, 330, 27, 14, MUTED, 3)
    _, cancel = ui.pxbutton(node, '中断', '中断', 343, 30, 70, 25, RUST, 14)
    track = ui.box(node, '进度槽', 9, 60, 405, 4)
    ui.image(track, DARK)
    fill = ui.node('进度', track, (0, 0), (1, 1), (0, 0), (0, 0), (0, 0))
    ui.image(fill, GOLD)
    vials = {}
    for key, label, x, tone in [('san', 'SAN', 235, (.25,.68,.66,1)), ('hunger', '饥饿', 254, (.86,.55,.25,1))]:
        vial = ui.box(node, label+'水位', x, 5, 14, 20, False)
        ui.image(vial, DARK)
        line = ui.node('液面', vial, (0, 0), (1, 1), (0, 0), (0, 0), (0, 0))
        ui.image(line, tone)
        vials[key+'Vial'] = vial['go']
        vials[key+'Fill'] = line['rect']
    return ui.mono(node, GUID['task'], fields(dict(title=title, timer=timer, inputs=inputs, cancel=cancel, fill=fill['rect'], **vials)))


def audio(ui, root, name, clip=None, loop=False):
    node = ui.node(name, root)
    return ui.component(node, 82, f'''  m_Enabled: 1
  serializedVersion: 4
  OutputAudioMixerGroup: {{fileID: 0}}
  m_audioClip: {ext(clip, 8300000) if clip else ref(0)}
  m_PlayOnAwake: 0
  m_Volume: {0.35 if clip else 1}
  m_Pitch: 1
  Loop: {int(loop)}
  Mute: 0
  Spatialize: 0
  SpatializePostEffects: 0
  Priority: 128
  DopplerLevel: 1
  MinDistance: 1
  MaxDistance: 500
  Pan2D: 0
  rolloffMode: 0
  BypassEffects: 0
  BypassListenerEffects: 0
  BypassReverbZones: 0
''')


def build():
    ui = UI()
    root = ui.node('孤岛生存航行桌面', size=(1600, 900))
    # 固定公开引用，场景和生成器无需根据模板数量重算引用。
    ui.next = 1004
    ui.component(root, 223, '''  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {fileID: 0}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 10
  m_TargetDisplay: 0
''')
    ui.mono(root, '0cd44c1031e13a943bb63640046fad76', '''  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {x: 1600, y: 900}
  m_ScreenMatchMode: 1
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
''')
    ui.mono(root, 'dc42784cf147c0c48a680349fa168899', '  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
    ui.image(ui.node('背景', root), DARK)
    fit = ui.node('适配视口', root, pivot=(.5, .5))
    ui.mono(fit, '86710e43de46f6f4bac7c8e50813a599', '  m_AspectMode: 3\n  m_AspectRatio: 1.7777778\n')
    desk = ui.node('逻辑桌面1600×900', fit, (.5, .5), (.5, .5), (0, 0), (1600, 900), (.5, .5))
    ui.mono(desk, GUID['layout'], f"  viewport: {ref(fit['rect'])}\n  content: {ref(desk['rect'])}\n  logicalSize: {{x: 1600, y: 900}}\n")
    play = ui.node('游戏桌面', desk)
    interaction = ui.component(play, 225, '  m_Enabled: 1\n  m_Alpha: 1\n  m_Interactable: 1\n  m_BlocksRaycasts: 1\n  m_IgnoreParentGroups: 0\n')
    refs = dict(playInteraction=interaction)
    top = ui.box(play, '顶栏', 18, 12, 1564, 57)
    ui.image(top, PANEL)
    ui.txt(top, '游戏名', '孤岛生存', 14, 5, 230, 47, 30, WHITE, 3)
    _, refs['clockText'] = ui.txt(top, '时间', '第 1 天  00:00', 236, 7, 320, 43, 22, MUTED, 3)
    _, refs['pauseButton'] = ui.pxbutton(top, '时间控制', '继续', 1270, 8, 134, 41, RUST, 20)
    _, refs['menuButton'] = ui.pxbutton(top, '菜单入口', '菜单', 1416, 8, 134, 41, BUTTON, 20)

    main = ui.box(play, '桌面区域', 18, 80, 1080, 726)
    refs['tableTone'] = ui.image(main, BOARD)
    ui.txt(main, '区域标题', '海滩桌面', 14, 4, 252, 34, 21, WHITE, 3)
    ui.txt(main, '滚动操作', '滚轮查看', 885, 6, 171, 28, 14, MUTED, 5)
    refs['tableScroll'], viewport, world = scroll(ui, main, '桌面滚动区', 10, 43, 1056, 674, 1700)
    refs['tabletopViewport'] = viewport['rect']
    refs['tabletop'] = world['rect']
    ui.image(world, (.028, .081, .083, .26))
    preview = ui.node('开局预览', world)
    refs['editorPreview'] = preview['go']
    for i, (name, mark) in enumerate([('头', '头'), ('躯干', '身'), ('手', '手'), ('手', '手'), ('腿', '腿'), ('腿', '腿'),
                                      ('全家福', '照'), ('海滩', '滩'), ('黑藻', '藻')]):
        x, y = ((.08 + .13*i), .10) if i < 6 else ((.08 + .13*(i - 6)), .22)
        card(ui, preview, name, max(4, int(x * 1056) - 63), int(y * 1700) - 52, preview_kind=mark)

    tray = ui.box(play, '组合面板', 1110, 80, 472, 493)
    ui.image(tray, PANEL)
    ui.txt(tray, '面板标题', '组合', 17, 7, 140, 36, 24, WHITE, 3)
    _, refs['stageCount'] = ui.txt(tray, '已放入数量', '0 张', 296, 8, 153, 36, 17, MUTED, 5)
    refs['stageScroll'], stage_viewport, stage = scroll(ui, tray, '组合卡槽', 16, 47, 438, 334, 334)
    refs['stageDrop'] = stage_viewport['rect']
    refs['stageCards'] = stage['rect']
    for i in range(9):
        slot = ui.box(stage, '空槽 ' + str(i+1), 8 + i % 3 * 144, 5 + i // 3 * 110, *CARD)
        ui.image(slot, (.12, .26, .255, .7))
        ui.txt(slot, '加号', '＋', 1, 1, 124, 102, 19, MUTED, 4)
    _, refs['stageStatus'] = ui.txt(tray, '匹配状态', '将卡牌拖入组合。', 16, 386, 440, 48, 16, MUTED, 3)
    _, refs['clearButton'] = ui.pxbutton(tray, '退回卡牌', '全部退回', 16, 442, 132, 39, BUTTON, 18)
    _, refs['startButton'] = ui.pxbutton(tray, '开始组合', '开始', 310, 442, 144, 39, RUST, 20)

    tasks = ui.box(play, '进行中的任务', 1110, 585, 472, 296)
    ui.image(tasks, PANEL)
    ui.txt(tasks, '面板标题', '进行中', 17, 5, 240, 37, 22, WHITE, 3)
    _, refs['taskCount'] = ui.txt(tasks, '任务数量', '0', 296, 7, 151, 34, 17, MUTED, 5)
    refs['taskScroll'], _, rows = scroll(ui, tasks, '任务滚动区', 18, 45, 436, 235, 235)
    refs['taskRows'] = rows['rect']
    ui.txt(rows, '空任务提示', '没有正在执行的组合。', 8, 8, 407, 25, 16, MUTED, 3)
    refs['emptyTasks'] = rows['children'][-1]['go']

    bottom = ui.box(play, '牌面信息与操作通知', 18, 818, 1080, 63)
    ui.image(bottom, PANEL)
    _, refs['cardTitle'] = ui.txt(bottom, '当前卡牌', '', 13, 4, 226, 28, 20, WHITE, 3)
    _, refs['cardDetail'] = ui.txt(bottom, '卡牌状态', '', 240, 5, 822, 26, 16, MUTED, 3)
    _, refs['noticeText'] = ui.txt(bottom, '通知', '', 13, 31, 1052, 25, 16, GOLD, 3)

    refs['cardTemplate'] = card(ui, play, '牌面模板', 0, 0, active=False)
    refs['taskTemplate'] = task(ui, play)
    refs['dragLayer'] = ui.node('拖动显示层', play)['rect']
    read = ui.node('阅读卡牌', desk, active=False)
    refs['readPanel'] = read['go']
    ui.image(read, (.025, .055, .054, .84), ray=True)
    paper = ui.box(read, '阅读区', 443, 164, 714, 572)
    ui.image(paper, PANEL)
    _, refs['readTitle'] = ui.txt(paper, '标题', '', 28, 21, 646, 54, 30, WHITE, 3)
    refs['readText'], refs['readScroll'] = ui.scroll_text(paper, '卡牌文字', 31, 86, 647, 391, 21, WHITE)
    _, refs['readClose'] = ui.pxbutton(paper, '关闭阅读', '收起', 527, 501, 150, 49, BUTTON, 22)

    menu = ui.node('游戏菜单', desk, active=False)
    refs['menuPanel'] = menu['go']
    ui.image(menu, (.025, .055, .054, .89), ray=True)
    menu_box = ui.box(menu, '菜单选项', 525, 197, 550, 506)
    ui.image(menu_box, PANEL)
    _, refs['menuTitle'] = ui.txt(menu_box, '标题', '暂停', 22, 25, 506, 65, 32, WHITE, 4)
    for i, (field, name) in enumerate([('menuClose', '返回桌面'), ('saveButton', '保存进度'), ('loadButton', '读取进度'),
                                         ('restartButton', '重新开始'), ('returnButton', '返回牌桌')]):
        _, refs[field] = ui.pxbutton(menu_box, name, name, 70, 105 + i*66, 410, 52, RUST if i == 0 else BUTTON, 22)
    _, refs['menuNotice'] = ui.txt(menu_box, '菜单通知', '', 26, 441, 498, 48, 17, GOLD, 4)

    # 给配置错误保留一个可见的通知，真实卡牌在绑定时替换编辑预览。
    refs['feedback'] = audio(ui, root, '卡牌操作音')
    ambience = audio(ui, root, '海滩环境音', GUID['ambience'], loop=True)
    thought = audio(ui, root, '思绪杂音', GUID['thought'])
    next_id = ui.next
    ui.next = 1002
    ui.mono(root, GUID['view'], fields(refs) + '  theme: ' + ext(GUID['theme'], 11400000, 2) + '\n'
            + '  desaturateShader: ' + ext(GUID['desaturate'], 4800000) + '\n'
            + '  dragPaper: ' + ext(GUID['drag'], 8300000) + '\n')
    ui.mono(root, GUID['bootstrap'], '  configuration: ' + ext(GUID['configuration'], 11400000, 2) + '\n'
            + fields(dict(view=1002, ambience=ambience, thoughtAudio=thought)))
    ui.next = next_id
    return ui


def write_meta(path, guid, importer):
    target = Path(str(path) + '.meta')
    body = f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n'
    if importer == 'MonoImporter':
        body += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    body += '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    if target.is_file() and target.read_text(encoding='utf-8') != body:
        raise ValueError(f'已有资源的元数据发生变化：{target}')
    target.write_text(body, encoding='utf-8')


def dump(ui):
    components = ui.components
    native = {82: 'AudioSource', 225: 'CanvasGroup'}
    ui.components = [entry for entry in components if entry[0] not in native]
    try:
        output = ui.dump()
    finally:
        ui.components = components
    for kind, file_id, owner, body in components:
        if kind not in native:
            continue
        output += (f'--- !u!{kind} &{file_id}\n{native[kind]}:\n  m_ObjectHideFlags: 0\n'
                   '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
                   f"  m_GameObject: {ref(owner)}\n{body}")
    return output


def main():
    ui = build()
    target = MODULE / 'Prefabs/VoyageDesk.prefab'
    target.write_text(dump(ui), encoding='utf-8')
    write_meta(target, GUID['prefab'], 'PrefabImporter')
    for name, key in [('VoyageView', 'view'), ('VoyageCardView', 'card'), ('VoyageTaskView', 'task')]:
        write_meta(MODULE / 'Unity' / (name + '.cs'), GUID[key], 'MonoImporter')
    print(f'{len(ui.nodes)} UI objects; VoyageView 1002; VoyageBootstrap 1003')


if __name__ == '__main__':
    main()

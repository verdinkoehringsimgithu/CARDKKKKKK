"""生成孤岛生存真实uGUI Prefab/场景引用；不启动Unity、编译器或游戏测试。

仅提取既有UI序列化辅助定义，不执行法术生成器的build/main；输出限于孤岛模块。
编辑态有初始物资预览，运行时SurvivalView隐藏PreviewRoot，显示Core快照。
"""
from pathlib import Path
import ast
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/IslandSurvival'
GUID = {
    'view': '39576c9a2c71569a94b70ac62ab3cf97',
    'card': 'c0190082c5f75e3ba04d9cba97df3d34',
    'choice': '3484847f230e5e1286ea6bab8ae3d1a4',
    'bootstrap': '62423035ad965656b76012c39dcd25ab',
    'desktop': 'be52be55958055669406d034e94fb665',
    'config': '38f6d818337c54b7b4206ebe8fa4b81c',
    'prefab': '9bf6f4f12acc553185a7d31db7a7ee7c',
    'scene': 'deeaf43f1b6350ed80d529afb3fd49c3',
}

# AST只保留纯序列化类、函数和常量；不执行任何旧玩法构建或磁盘写入入口。
source = ast.parse((ROOT/'tools/build_spellcraft_ui.py').read_text(encoding='utf-8'))
functions = {'validate_guid','ref','ext','vec2','color','string'}
constants = {'GUID','INK','CREAM','ORANGE','PURPLE','GOLD','WHITE','MUTED','DARK'}
nodes = [n for n in source.body if isinstance(n,ast.ClassDef) and n.name=='UI'
    or isinstance(n,ast.FunctionDef) and n.name in functions
    or isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id in constants for t in n.targets)]
helpers = {'json':json,'re':re}
exec(compile(ast.Module(body=nodes,type_ignores=[]),'island_ui_serialization_helpers','exec'),helpers)
UI = helpers['UI']; ref = helpers['ref']; ext = helpers['ext']
helpers['GUID'].update(GUID)
INK = (.10,.18,.18,1)
PAPER = (.91,.88,.77,1)
WHITE = (.98,.96,.88,1)
MUTED = (.67,.77,.71,1)
TEAL = (.085,.20,.20,1)
TEAL_LIGHT = (.16,.31,.29,1)
RUST = (.73,.30,.18,1)
SAGE = (.36,.47,.34,1)
SCALE = 1104/1536

def rect_scale(node, scale):
    node['scale'] = (scale,scale,1)

def scroll(ui,parent,name,x,y,w,h,content_size=None,list_layout=True):
    panel=ui.box(parent,name,x,y,w,h)
    viewport=ui.node('可视区域',panel)
    ui.image(viewport,(1,1,1,.005),ray=True)
    ui.mono(viewport,helpers['GUID']['rectmask'],'  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
    if content_size:
        content=ui.node('内容',viewport,(0,1),(0,1),(0,0),content_size,(0,1))
    else:
        content=ui.node('内容',viewport,(0,1),(1,1),(0,0),(0,h),(0,1))
    if list_layout:
        ui.layout(content,False,8,True,0,0)
        ui.mono(content,helpers['GUID']['fitter'],'  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
    ui.mono(panel,helpers['GUID']['scroll'],
        '  m_Content: '+ref(content['rect'])+'\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 42\n  m_Viewport: '+ref(viewport['rect'])+'\n  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 0\n  m_HorizontalScrollbarSpacing: 0\n  m_VerticalScrollbarSpacing: 0\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n')
    return panel,content

def template_card(ui,parent):
    node=ui.box(parent,'卡牌模板',0,0,172,94,False)
    face=ui.image(node,TEAL_LIGHT,ray=True)
    _,title=ui.label(node,'名称','木头',(0,1),(1,1),(10,-5),(-20,27),22,WHITE,3,(0,1))
    artwork=ui.box(node,'插画',10,35,40,40)
    artwork_id=ui.image(artwork,(.44,.53,.40,.25))
    _,detail=ui.label(node,'详情','材料',(0,1),(1,1),(58,-36),(-68,39),16,MUTED,0,(0,1))
    _,count=ui.label(node,'数量与状态','×6',(0,1),(1,1),(10,-76),(-20,18),16,WHITE,3,(0,1))
    selected=ui.border(node)
    fields=dict(face=face,artwork=artwork_id,titleText=title,detailText=detail,countText=count,selectedMark=selected['go'])
    return ui.mono(node,GUID['card'],''.join('  '+key+': '+ref(value)+'\n' for key,value in fields.items()))

def template_choice(ui,parent):
    node=ui.box(parent,'动作选项模板',0,0,400,62,False)
    ui.layout_element(node,-1,62,0)
    image=ui.image(node,TEAL_LIGHT,ray=True)
    button=ui.button(node,image)
    _,title=ui.label(node,'名称','采集帽贝',(0,1),(1,1),(12,-5),(-24,27),20,WHITE,3,(0,1))
    _,detail=ui.label(node,'详情','人力 60分钟',(0,1),(1,1),(12,-34),(-24,21),15,MUTED,3,(0,1))
    return ui.mono(node,GUID['choice'],'  button: '+ref(button)+'\n  titleText: '+ref(title)+'\n  detailText: '+ref(detail)+'\n')

def preview_card(ui,parent,name,x,y,w,h,detail,count='',natural=False):
    node=ui.box(parent,name,x,y,w,h)
    ui.image(node,(.26,.36,.28,1) if natural else PAPER)
    ui.txt(node,'名称',name,10,7,w-20,26,20,WHITE if natural else INK,3)
    ui.txt(node,'状态',detail,10,38,w-20,h-42,15,MUTED if natural else INK,0)
    if count: ui.txt(node,'数量',count,w-54,8,44,24,18,INK,5)
    return node

def build():
    ui=UI();root=ui.node('孤岛生存界面',size=(1600,900))
    ui.component(root,223,'  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n  m_SortingLayerID: 0\n  m_SortingOrder: 10\n  m_TargetDisplay: 0\n')
    ui.mono(root,'0cd44c1031e13a943bb63640046fad76','  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1600, y: 900}\n  m_ScreenMatchMode: 1\n  m_MatchWidthOrHeight: 0.5\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n  m_PresetInfoIsWorld: 0\n')
    ui.mono(root,'dc42784cf147c0c48a680349fa168899','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
    background=ui.node('背景',root);ui.image(background,(.035,.09,.10,1))
    safe=ui.node('安全区域',root,pivot=(.5,.5))
    fit=ui.node('适配视口',safe,pivot=(.5,.5))
    ui.mono(fit,'86710e43de46f6f4bac7c8e50813a599','  m_AspectMode: 3\n  m_AspectRatio: 1.7777778\n')
    board=ui.node('逻辑桌面1600x900',fit,(.5,.5),(.5,.5),(0,0),(1600,900),(.5,.5))
    ui.mono(board,GUID['desktop'],'  viewport: '+ref(fit['rect'])+'\n  content: '+ref(board['rect'])+'\n  logicalSize: {x: 1600, y: 900}\n')
    fields={'logicalDesktop':board['rect']}
    sound=ui.component(root,82,'  m_Enabled: 1\n  serializedVersion: 4\n  OutputAudioMixerGroup: {fileID: 0}\n  m_audioClip: {fileID: 0}\n  m_PlayOnAwake: 0\n  m_Volume: 1\n  m_Pitch: 1\n  Loop: 0\n  Mute: 0\n  Spatialize: 0\n  SpatializePostEffects: 0\n  Priority: 128\n  DopplerLevel: 1\n  MinDistance: 1\n  MaxDistance: 500\n  Pan2D: 0\n  rolloffMode: 0\n  BypassEffects: 0\n  BypassListenerEffects: 0\n  BypassReverbZones: 0\n')
    fields['soundSource']=sound
    top=ui.box(board,'状态与时间',16,16,1568,84);ui.image(top,TEAL)
    _,fields['clockText']=ui.txt(top,'日期与时间','第1天  06:00',16,8,250,34,25,WHITE,3)
    ui.txt(top,'游戏名','孤岛生存',16,50,250,24,17,MUTED,3)
    _,fields['bodyText']=ui.txt(top,'身体状态','饱食 100    精神 100    健康 100    生命 100',280,10,850,34,23,WHITE,3)
    _,fields['humanText']=ui.txt(top,'人力','人力  空闲',280,50,550,24,17,MUTED,3)
    _,fields['capacityText']=ui.txt(top,'占地','占地 5 / 24',850,50,260,24,17,MUTED,3)
    for key,name,x,w,c in [('pauseButton','继续',1180,100,RUST),('speedButton','1倍',1292,96,TEAL_LIGHT),('menuButton','菜单',1400,152,TEAL_LIGHT)]:
        _,fields[key]=ui.pxbutton(top,name,name,x,12,w,52,c,22)

    nature=ui.box(board,'自然节点',16,112,1120,112);ui.image(nature,(.06,.14,.13,1))
    natural_surface=ui.box(nature,'自然卡牌面',8,8,1536,132);rect_scale(natural_surface,SCALE)
    ui.image(natural_surface,(1,1,1,.005),ray=True);fields['naturalSurface']=natural_surface['rect']
    table=ui.box(board,'生产桌面',16,236,1120,446);ui.image(table,(.20,.28,.24,1))
    table_surface=ui.box(table,'生产卡牌面',8,8,1536,600);rect_scale(table_surface,SCALE)
    ui.image(table_surface,(.90,.86,.71,.06),ray=True);fields['tableSurface']=table_surface['rect']
    usable=ui.box(table_surface,'当前可用生产范围',0,0,1080,400);ui.image(usable,(.38,.48,.36,.24),ray=False);fields['usableTableRect']=usable['rect']
    status=ui.box(board,'独立状态牌',16,694,1120,108);ui.image(status,(.10,.15,.16,1))
    ui.txt(status,'标题','伤口与异常',12,2,1096,23,16,MUTED,3)
    _,condition_surface=scroll(ui,status,'状态牌滚动区',8,26,1104,76,(1536,600),False)
    rect_scale(condition_surface,SCALE)
    ui.image(condition_surface,(1,1,1,.005),ray=True);fields['conditionSurface']=condition_surface['rect']

    selected=ui.box(board,'选中信息',1152,112,432,176);ui.image(selected,TEAL)
    fields['selectedText'],_=ui.scroll_text(selected,'选中内容',16,12,400,152,18,WHITE,'选择一张卡牌。')
    inventory=ui.box(board,'内容物',1152,300,432,160);ui.image(inventory,TEAL)
    ui.txt(inventory,'标题','内容物',16,6,400,26,18,MUTED,3)
    _,inventory_content=scroll(ui,inventory,'内容物列表',12,40,408,108)
    fields['inventoryContent']=inventory_content['rect']
    actions=ui.box(board,'可用操作',1152,472,432,240);ui.image(actions,TEAL)
    ui.txt(actions,'标题','操作',16,6,400,26,18,MUTED,3)
    _,action_content=scroll(ui,actions,'操作列表',12,40,408,188)
    fields['actionContent']=action_content['rect']
    for key,name,x,y,w in [
        ('recipesButton','配方手册',1152,730,136),('bodyButton','身体详情',1300,730,136),('goalsButton','营地目标',1448,730,136),
        ('tidyButton','整理',1152,770,210),('restButton','休息',1374,770,210)]:
        _,fields[key]=ui.pxbutton(board,name,name,x,y,w,32,TEAL_LIGHT,18)
    footer=ui.box(board,'任务与通知',16,814,1568,72);ui.image(footer,(.06,.13,.14,1))
    _,fields['taskText']=ui.txt(footer,'当前任务','当前没有工作。',16,5,1536,28,18,WHITE,3)
    _,fields['noticeText']=ui.txt(footer,'通知','时间暂停。',16,38,1536,26,17,MUTED,3)

    previews=ui.node('PreviewRoot',board)
    for i,(name,detail) in enumerate([('帽贝岩','帽贝 4 / 4'),('帽贝岩','帽贝 4 / 4'),('野生芦荟','等待长叶'),('海岸采集点','今日潮汐可取'),('灌木地','木头8 · 纤维4')]):
        preview_card(ui,previews,name,28+i*220,122,204,88,detail,natural=True)
    for i,(name,count) in enumerate([('石头','×4'),('木头','×6'),('纤维','×4'),('燧石','×2'),('口粮','×8')]):
        preview_card(ui,previews,name,24+i*129.375,244,123.625,74.75,'材料',count)
    ui.txt(previews,'初始无异常','当前没有伤口或异常。',40,731,1064,30,18,MUTED,3)

    drag=ui.node('拖动卡牌层',board);fields['cardDragLayer']=drag['rect']
    fields['cardTemplate']=template_card(ui,board)
    fields['choiceTemplate']=template_choice(ui,board)

    modal=ui.node('配方与详情',board,active=False);fields['modalPanel']=modal['go']
    ui.image(modal,(.035,.09,.10,1),ray=True)
    _,fields['modalTitle']=ui.txt(modal,'标题','配方手册',32,26,1340,56,30,WHITE,3)
    _,fields['modalCloseButton']=ui.pxbutton(modal,'关闭','关闭',1432,28,136,48,TEAL_LIGHT,22)
    search_box=ui.box(modal,'配方搜索',32,96,600,50);search_image=ui.image(search_box,TEAL_LIGHT,ray=True)
    _,search_text=ui.label(search_box,'输入文字','',pos=(12,0),size=(-24,0),font=21,c=WHITE,align=3)
    _,search_placeholder=ui.label(search_box,'搜索提示','搜索名称、材料、工具或用途',pos=(12,0),size=(-24,0),font=19,c=MUTED,align=3)
    fields['searchInput']=ui.mono(search_box,'d199490a83bb2b844b9695cbf13b01ef','  m_Navigation:\n    m_Mode: 0\n  m_Transition: 0\n  m_Interactable: 1\n  m_TargetGraphic: '+ref(search_image)+'\n  m_TextComponent: '+ref(search_text)+'\n  m_Placeholder: '+ref(search_placeholder)+'\n  m_ContentType: 0\n  m_InputType: 0\n  m_AsteriskChar: 42\n  m_KeyboardType: 0\n  m_LineType: 0\n  m_HideMobileInput: 0\n  m_CharacterValidation: 0\n  m_CharacterLimit: 64\n  m_CaretBlinkRate: 0.85\n  m_CaretWidth: 2\n  m_ReadOnly: 0\n  m_Text: ""\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_OnEndEdit:\n    m_PersistentCalls:\n      m_Calls: []\n')
    option_panel=ui.box(modal,'选项区',32,160,600,708);ui.image(option_panel,TEAL)
    _,option_content=scroll(ui,option_panel,'选项列表',12,12,576,684)
    fields['optionContent']=option_content['rect']
    details=ui.box(modal,'详情区',656,112,912,584);ui.image(details,TEAL)
    fields['modalDetail'],_=ui.scroll_text(details,'完整规则',16,16,880,292,19,WHITE,'选择一个配方，查看投入与产出。')
    fields['previewText'],_=ui.scroll_text(details,'执行预览',16,330,880,238,18,MUTED,'')
    batch=ui.box(modal,'批量与执行',656,716,912,152);ui.image(batch,TEAL)
    _,fields['batchText']=ui.txt(batch,'批量次数','次数 1',20,18,440,36,23,WHITE,3)
    _,fields['batchLessButton']=ui.pxbutton(batch,'减少','−',20,78,72,52,TEAL_LIGHT,27)
    _,fields['batchMoreButton']=ui.pxbutton(batch,'增加','＋',104,78,72,52,TEAL_LIGHT,27)
    _,fields['startButton']=ui.pxbutton(batch,'开始','开始',568,50,324,80,RUST,28)

    menu=ui.node('菜单',board,active=False);fields['menuPanel']=menu['go'];ui.image(menu,(.035,.09,.10,1),ray=True)
    ui.txt(menu,'标题','菜单',520,78,560,64,36,WHITE,4)
    for i,(key,name) in enumerate([('menuCloseButton','继续'),('saveButton','保存手动存档'),('loadButton','读取手动存档'),('backupButton','恢复自动备份'),('restartButton','重新开始'),('settingsButton','设置与制作人员'),('returnButton','返回牌桌'),('quitButton','保存并退出')]):
        _,fields[key]=ui.pxbutton(menu,name,name,560,152+i*88,480,64,RUST if i==0 else TEAL_LIGHT,25)
    confirmation=ui.node('操作确认',board,active=False);fields['confirmationPanel']=confirmation['go']
    ui.image(confirmation,(.01,.03,.035,.96),ray=True)
    confirm_panel=ui.box(confirmation,'确认内容',400,220,800,450);ui.image(confirm_panel,TEAL)
    fields['confirmationText'],_=ui.scroll_text(confirm_panel,'确认说明',40,36,720,250,24,WHITE,'确认执行这项操作？')
    _,fields['confirmYesButton']=ui.pxbutton(confirm_panel,'确认','确认',40,336,344,72,RUST,25)
    _,fields['confirmNoButton']=ui.pxbutton(confirm_panel,'取消','取消',416,336,344,72,TEAL_LIGHT,25)

    relations=ui.node('局部工艺关系',board,active=False);fields['relationsPanel']=relations['go'];ui.image(relations,(.035,.09,.10,1),ray=True)
    ui.txt(relations,'标题','局部工艺关系',32,26,1300,56,30,WHITE,3)
    _,fields['relationCloseButton']=ui.pxbutton(relations,'关闭','关闭',1432,28,136,48,TEAL_LIGHT,22)
    for x,label in [(32,'来源工艺'),(1104,'可用工艺')]: ui.txt(relations,label,label,x,120,464,32,23,MUTED,4)
    _,relation_sources=scroll(ui,relations,'来源节点',32,170,464,686);fields['relationSources']=relation_sources['rect']
    _,relation_uses=scroll(ui,relations,'用途节点',1104,170,464,686);fields['relationUses']=relation_uses['rect']
    center=ui.box(relations,'中心牌',568,280,464,330);ui.image(center,TEAL_LIGHT)
    fields['relationCenterText'],_=ui.scroll_text(center,'卡牌与说明',20,20,424,290,24,WHITE,'选择一张卡牌。')
    ui.txt(relations,'输入连线','→',500,410,64,64,42,WHITE,4)
    ui.txt(relations,'输出连线','→',1036,410,64,64,42,WHITE,4)

    view=ui.mono(root,GUID['view'],''.join('  '+key+': '+ref(value)+'\n' for key,value in fields.items())+'  theme: '+ext('a8c6d2df97af5333a3be0b24d7b42fc1',11400000,2)+'\n')
    ui.mono(root,GUID['bootstrap'],'  configuration: '+ext(GUID['config'],11400000,2)+'\n  view: '+ref(view)+'\n')
    return ui,root,view,fields

def dump(ui):
    original=ui.components
    audio=[c for c in original if c[0]==82]
    ui.components=[c for c in original if c[0]!=82]
    try: text=ui.dump()
    finally: ui.components=original
    for cid,fid,go,body in audio:
        text+=f'--- !u!82 &{fid}\nAudioSource:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n'+body
    for node in ui.nodes:
        if 'scale' not in node: continue
        begin=text.index('--- !u!224 &'+str(node['rect'])+'\n')
        end=text.find('--- !u!',begin+1)
        section=text[begin:end if end>=0 else len(text)]
        x,y,z=node['scale']
        section=section.replace('m_LocalScale: {x: 1, y: 1, z: 1}',f'm_LocalScale: {{x: {x}, y: {y}, z: {z}}}')
        text=text[:begin]+section+text[end if end>=0 else len(text):]
    return text

def write_meta(path,guid,kind):
    meta=Path(str(path)+'.meta')
    if meta.exists():
        actual=re.search(r'^guid: (.+)$',meta.read_text(encoding='utf-8'),re.M).group(1)
        if actual!=guid: raise ValueError('已有资源GUID不符，拒绝改写：'+str(path))
        return
    body='fileFormatVersion: 2\nguid: '+guid+'\n'+kind+':\n  externalObjects: {}\n'
    if kind=='MonoImporter': body+='  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    body+='  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    meta.write_text(body,encoding='utf-8')

def main():
    ui,root,view,fields=build()
    prefab=OUT/'Prefabs/IslandPreparation.prefab'
    write_meta(prefab,GUID['prefab'],'PrefabImporter')
    prefab.write_text(dump(ui),encoding='utf-8')
    scene=OUT/'Scenes/IslandSurvival.unity'
    write_meta(scene,GUID['scene'],'DefaultImporter')
    text=scene.read_text(encoding='utf-8')
    if GUID['prefab'] not in text or 'fileID: 1001' not in text:
        raise ValueError('场景原Prefab接线已改变，请人工核对后重建。')
    text=text.replace('m_SubmitButton: Submit','m_SubmitButton: IslandSubmit')
    scene.write_text(text,encoding='utf-8')
    for name,key in [('SurvivalView','view'),('SurvivalCardView','card'),('SurvivalChoiceView','choice'),('SurvivalBootstrap','bootstrap'),('IslandDesktopLayout','desktop')]:
        write_meta(OUT/'Unity'/(name+'.cs'),GUID[key],'MonoImporter')
    print(json.dumps(dict(prefab=str(prefab.relative_to(ROOT)),scene=str(scene.relative_to(ROOT)),nodes=len(ui.nodes),components=len(ui.components),view=view,wiredFields=len(fields),logicalSize=[1600,900]),ensure_ascii=True))

if __name__=='__main__': main()

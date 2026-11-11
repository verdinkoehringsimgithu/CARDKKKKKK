"""生成法术拆装的真实序列化 uGUI；不调用 Unity、编译器或测试。"""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/TestGameplay/Spellcraft'
GUID = {
    'view': 'ad17e93c4c3d4aafbec6c9f4a334b528',
    'card': '67bf3dc66cd64adab76c9d2efc0e717a',
    'device': 'cb01d7d0d4924505a4153668449bb994',
    'prefab': '97f0da7da9554d61991d8d43faf34a44',
    'scene': 'b460ea0a1bfe4bf495d4741780537ba8',
    'bootstrap': '6ed40fdc02b94b67ab19666af459d2f3',
    'config': '234d8a2a518849f4a2659d4284b22d36',
    'font': 'e629089c69d84961a5cc29dd704f6301',
    'atlas': 'cd2b4d319fad49cfba036409d8b3884e',
    'background': 'f99513b667db493b90054647f7d849a9',
    'frame': 'dc044df145cb4648be54e99e02711ee8',
    'image': 'fe87c0e1cc204ed48ad3b37840f39efc',
    'text': '5f7201a12d95ffc409449d95f23cf332',
    'button': '4e29b1a8efbd4b44bb3f3716e73f07ff',
    'hbox': '30649d3a9faa99c48a7b1166b86bf2a0',
    'vbox': '59f8146938fff824cb5fd77236b75775',
    'layout': '306cc8c2b49d7114eaa3623786fc2126',
    'fitter': '3245ec927659c4140ac4f8d17403cc18',
    'scroll': '1aa08ab6e0800fa44ae55d278d1423e3',
    'rectmask': '3312d7739989d2b4e91e6319e9a96d76',
}
INK = (0.11, .17, .20, 1)
CREAM = (.96, .93, .82, 1)
ORANGE = (.96, .39, .27, 1)
PURPLE = (.48, .35, .69, 1)
GOLD = (1, .80, .29, 1)
WHITE = (.99, .97, .90, 1)
MUTED = (.62, .75, .77, 1)
DARK = (.045, .12, .16, 1)

def validate_guid(guid):
    if not isinstance(guid, str) or re.fullmatch(r'[0-9a-fA-F]{32}', guid) is None:
        raise ValueError('Unity GUID必须为32位十六进制：' + str(guid))
    return guid

for guid in GUID.values():
    validate_guid(guid)

def ref(i): return '{fileID: %d}' % i
def ext(guid, fid=21300000, typ=3):
    return '{fileID: %d, guid: %s, type: %d}' % (fid, validate_guid(guid), typ)
def vec2(v): return '{x: %s, y: %s}' % v
def color(c): return '{r: %s, g: %s, b: %s, a: %s}' % c
def string(s): return json.dumps(s, ensure_ascii=False)

class UI:
    def __init__(self): self.next = 1000; self.nodes = []; self.components = []
    def alloc(self): value = self.next; self.next += 1; return value
    def node(self, name, parent=None, amin=(0,0), amax=(1,1), pos=(0,0), size=(0,0), pivot=(0,0), active=True):
        node = {'go': self.alloc(), 'rect': self.alloc(), 'name': name, 'parent': parent, 'children': [],
                'amin': amin, 'amax': amax, 'pos': pos, 'size': size, 'pivot': pivot, 'active': int(active), 'components': []}
        self.nodes.append(node)
        if parent: parent['children'].append(node)
        return node
    def component(self, node, cid, body):
        fileid = self.alloc(); node['components'].append(fileid); self.components.append((cid, fileid, node['go'], body)); return fileid
    def mono(self, node, guid, fields=''):
        return self.component(node,114, '  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: '+ext(guid,11500000)+'\n  m_Name:\n  m_EditorClassIdentifier:\n'+fields)
    def renderer(self,node): return self.component(node,222,'  m_CullTransparentMesh: 1\n')
    def image(self,node,c=WHITE,sprite=None,ray=False,sliced=False):
        self.renderer(node)
        return self.mono(node,GUID['image'], '  m_Material: {fileID: 0}\n  m_Color: '+color(c)+'\n  m_RaycastTarget: '+str(int(ray))+'\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: '+(sprite or ref(0))+'\n  m_Type: '+str(int(sliced))+'\n  m_PreserveAspect: '+str(int(sprite is not None and GUID['atlas'] in sprite))+'\n  m_FillCenter: 1\n  m_FillMethod: 4\n  m_FillAmount: 1\n  m_FillClockwise: 1\n  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: '+str(7 if sliced else 1)+'\n')
    def label(self,parent,name,text,amin=(0,0),amax=(1,1),pos=(0,0),size=(0,0),font=24,c=INK,align=0,pivot=(0,0)):
        node=self.node(name,parent,amin,amax,pos,size,pivot); self.renderer(node)
        comp=self.mono(node,GUID['text'],'  m_Material: {fileID: 0}\n  m_Color: '+color(c)+'\n  m_RaycastTarget: 0\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_FontData:\n    m_Font: '+ext(GUID['font'],12800000)+'\n    m_FontSize: '+str(font)+'\n    m_FontStyle: 0\n    m_BestFit: 0\n    m_MinSize: 10\n    m_MaxSize: 40\n    m_Alignment: '+str(align)+'\n    m_AlignByGeometry: 0\n    m_RichText: 1\n    m_HorizontalOverflow: 0\n    m_VerticalOverflow: 0\n    m_LineSpacing: 1\n  m_Text: '+string(text)+'\n')
        return node,comp
    def button(self,node,graphic):
        return self.mono(node,GUID['button'],'''  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {fileID: 0}
    m_SelectOnDown: {fileID: 0}
    m_SelectOnLeft: {fileID: 0}
    m_SelectOnRight: {fileID: 0}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}
    m_HighlightedColor: {r: 1, g: 0.91, b: 0.69, a: 1}
    m_PressedColor: {r: 0.78, g: 0.69, b: 0.55, a: 1}
    m_SelectedColor: {r: 1, g: 0.86, b: 0.49, a: 1}
    m_DisabledColor: {r: 0.83, g: 0.85, b: 0.86, a: 1}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.09
  m_SpriteState:
    m_HighlightedSprite: {fileID: 0}
    m_PressedSprite: {fileID: 0}
    m_SelectedSprite: {fileID: 0}
    m_DisabledSprite: {fileID: 0}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: '''+ref(graphic)+'''
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
''')
    def layout(self,node,horizontal=True,gap=16,control=True,padding=0,align=3):
        return self.mono(node,GUID['hbox' if horizontal else 'vbox'],f'''  m_Padding:
    m_Left: {padding}
    m_Right: {padding}
    m_Top: {padding}
    m_Bottom: {padding}
  m_ChildAlignment: {align}
  m_Spacing: {gap}
  m_ChildForceExpandWidth: {int(not horizontal)}
  m_ChildForceExpandHeight: 0
  m_ChildControlWidth: {int(control)}
  m_ChildControlHeight: {int(control)}
  m_ChildScaleWidth: 0
  m_ChildScaleHeight: 0
  m_ReverseArrangement: 0
''')
    def layout_element(self,node,w=-1,h=-1,fw=0):
        return self.mono(node,GUID['layout'],f'''  m_IgnoreLayout: 0
  m_MinWidth: 0
  m_MinHeight: 0
  m_PreferredWidth: {w}
  m_PreferredHeight: {h}
  m_FlexibleWidth: {fw}
  m_FlexibleHeight: 0
  m_LayoutPriority: 1
''')
    def border(self,parent):
        node=self.node('选中轮廓',parent,active=False)
        for name,amin,amax,pos,size in [('上',(0,1),(1,1),(0,-3),(0,3)),('下',(0,0),(1,0),(0,0),(0,3)),('左',(0,0),(0,1),(0,0),(3,0)),('右',(1,0),(1,1),(-3,0),(3,0))]:
            edge=self.node(name,node,amin,amax,pos,size);self.image(edge,GOLD)
        return node
    def simple_button(self,parent,name,text,amin=(0,0),amax=(1,1),pos=(0,0),size=(0,0),c=ORANGE,font=24):
        node=self.node(name,parent,amin,amax,pos,size); img=self.image(node,c,ray=True); button=self.button(node,img)
        self.label(node,'文字',text,pos=(8,0),size=(-16,0),font=font,c=WHITE,align=4)
        return node,button
    def box(self,parent,name,x,y,w,h,active=True):
        return self.node(name,parent,(0,1),(0,1),(x,-y),(w,h),(0,1),active)
    def txt(self,parent,name,text,x,y,w,h,font=20,c=INK,align=0):
        return self.label(parent,name,text,(0,1),(0,1),(x,-y),(w,h),font,c,align,(0,1))
    def pxbutton(self,parent,name,text,x,y,w,h,c=ORANGE,font=22):
        node=self.box(parent,name,x,y,w,h); img=self.image(node,c,ray=True); component=self.button(node,img)
        self.label(node,'文字',text,pos=(12,0),size=(-24,0),font=font,c=WHITE,align=4)
        return node,component
    def card(self,parent,name,kind='hand',title='敲击',rules='造成3点直伤。',status='',price='1能量',art=4,active=True):
        w,h=(476,112) if kind=='option' else (300,410) if kind=='reward' else (192,252) if kind=='enemy' else (168,252)
        node=self.node(name,parent,amin=(0,0),amax=(0,0),size=(w,h),active=active); self.layout_element(node,-1 if kind=='option' else w,h,0)
        visual=self.node('牌面',node)
        image=self.image(visual,CREAM,None if kind=='option' else ext(GUID['frame']),True,kind!='option'); button=self.button(node,image)
        if kind=='option':
            artwork=self.box(visual,'零件插画',16,24,64,64); artworkid=self.image(artwork,WHITE,ext(GUID['atlas'],21300000+art*2))
            _,heading=self.label(visual,'名字',title,(0,1),(1,1),(96,-10),(-190,32),20,INK,3,(0,1))
            _,body=self.label(visual,'规则',rules,(0,1),(1,1),(96,-46),(-112,54),17,INK,0,(0,1))
            _,cost=self.label(visual,'选择状态',price,(1,1),(1,1),(-82,-12),(68,26),14,(.34,.25,.51,1),5,(0,1))
            statnode,stat=self.txt(visual,'附注','',0,0,20,24,12);statnode['active']=0
        elif kind=='reward':
            _,heading=self.txt(visual,'名字',title,20,14,260,44,27,INK,4)
            artwork=self.box(visual,'插画',64,66,172,164);artworkid=self.image(artwork,WHITE,ext(GUID['atlas'],21300000+art*2))
            _,body=self.txt(visual,'规则',rules,22,236,256,76,19,INK)
            footer=self.box(visual,'信息底',12,314,276,84);self.image(footer,(.055,.18,.24,1))
            _,cost=self.txt(visual,'分类',price,24,324,252,30,18,WHITE,4)
            _,stat=self.txt(visual,'附注',status,24,358,252,30,16,WHITE,4)
        elif kind=='enemy':
            _,heading=self.txt(visual,'名字',title,12,8,132,34,20,INK,3)
            artwork=self.box(visual,'插画',38,44,116,96);artworkid=self.image(artwork,WHITE,ext(GUID['atlas'],21300000+art*2))
            _,body=self.txt(visual,'下一动作',rules,14,144,164,44,15,INK,4)
            footer=self.box(visual,'信息底',10,194,172,50);self.image(footer,(.055,.18,.24,1))
            _,stat=self.txt(visual,'生命状态',status,14,196,164,48,15,WHITE,4)
            _,cost=self.txt(visual,'目标',price,142,13,40,24,12,(.74,.24,.15,1),5)
        else:
            _,heading=self.txt(visual,'名字',title,12,8,144,34,20,INK,3)
            artwork=self.box(visual,'插画',40,44,88,64);artworkid=self.image(artwork,WHITE,ext(GUID['atlas'],21300000+art*2))
            _,body=self.txt(visual,'规则',rules,12,112,144,70,16,INK)
            footer=self.box(visual,'信息底',8,188,152,56);self.image(footer,(.055,.18,.24,1))
            _,cost=self.txt(visual,'费用',price,12,192,144,25,14,WHITE,3)
            _,stat=self.txt(visual,'状态',status,12,220,144,25,13,WHITE,3)
        selection=self.border(visual)
        _,feedback=self.txt(visual,'数字反馈','',0,60,w,62,36,ORANGE,4)
        fields={'visual':visual['rect'],'frame':image,'artwork':artworkid,'selection':selection['go'],'heading':heading,'body':body,'cost':cost,'status':stat,'feedback':feedback,'button':button}
        component=self.mono(node,GUID['card'],''.join('  '+k+': '+ref(v)+'\n' for k,v in fields.items())+'  useCardFrame: '+str(int(kind!='option'))+'\n')
        return node,component
    def device(self,parent,index,title,trigger,effect,modifier,art):
        node=self.node('装置'+str(index+1),parent,amin=(0,0),amax=(0,0),size=(494,210));self.layout_element(node,494,210,0)
        visual=self.node('装置面板',node);frame=self.image(visual,CREAM,ray=True);button=self.button(node,frame)
        _,heading=self.txt(visual,'槽位','装置 '+str(index+1),18,8,458,34,22,INK,3)
        artwork=self.box(visual,'效果插画',18,46,84,82);art=self.image(artwork,WHITE,ext(GUID['atlas'],21300000+art*2))
        link=self.box(visual,'触发连线',112,47,4,82);connection=self.image(link,PURPLE)
        _,when=self.txt(visual,'触发',trigger,132,44,344,30,18,(.38,.27,.56,1),3)
        _,what=self.txt(visual,'效果',effect,132,76,344,30,20,INK,3)
        _,how=self.txt(visual,'改装',modifier,132,108,344,28,17,(.24,.39,.40,1),3)
        _,limit=self.txt(visual,'次数','每回合最多2次',18,138,458,28,16,(.34,.39,.42,1),3)
        action=self.box(visual,'操作色带',12,170,470,30);self.image(action,(.12,.31,.37,1))
        _,actionlabel=self.label(action,'操作','更换零件',pos=(10,0),size=(-20,0),font=18,c=WHITE,align=4)
        selection=self.border(visual)
        fields={'visual':visual['rect'],'frame':frame,'artwork':art,'connection':connection,'selection':selection['go'],'heading':heading,'trigger':when,'effect':what,'modifier':how,'limit':limit,'actionLabel':actionlabel,'button':button}
        comp=self.mono(node,GUID['device'],''.join('  '+k+': '+ref(v)+'\n' for k,v in fields.items()))
        return node,comp
    def scroll_column(self,parent,name,title,x):
        panel=self.box(parent,name,x,0,500,536);self.image(panel,(.10,.22,.28,1))
        self.txt(panel,'分类',title,16,8,468,42,24,WHITE,3)
        viewport=self.box(panel,'可视区域',12,58,476,466);self.image(viewport,(1,1,1,.01),ray=True)
        self.mono(viewport,GUID['rectmask'],'  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
        content=self.node('零件列表',viewport,(0,1),(1,1),(0,0),(0,760),(0,1));self.layout(content,False,10,True,0,0)
        self.mono(content,GUID['fitter'],'  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
        self.mono(panel,GUID['scroll'],'  m_Content: '+ref(content['rect'])+'\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 40\n  m_Viewport: '+ref(viewport['rect'])+'\n  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 0\n  m_HorizontalScrollbarSpacing: 0\n  m_VerticalScrollbarSpacing: 0\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n')
        return content
    def scroll_text(self,parent,name,x,y,w,h,font=18,c=WHITE,value=''):
        panel=self.box(parent,name,x,y,w,h)
        viewport=self.node('可视区域',panel);self.image(viewport,(1,1,1,.01),ray=True)
        self.mono(viewport,GUID['rectmask'],'  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
        content,text=self.label(viewport,'完整内容',value,(0,1),(1,1),(0,0),(0,0),font,c,0,(0,1))
        self.mono(content,GUID['fitter'],'  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
        scroll=self.mono(panel,GUID['scroll'],'  m_Content: '+ref(content['rect'])+'\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 36\n  m_Viewport: '+ref(viewport['rect'])+'\n  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 0\n  m_HorizontalScrollbarSpacing: 0\n  m_VerticalScrollbarSpacing: 0\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n')
        return text,scroll
    def dump(self):
        parts=['%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n']
        common='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
        for node in self.nodes:
            parts.append('--- !u!1 &'+str(node['go'])+'\nGameObject:\n'+common+'  serializedVersion: 6\n  m_Component:\n'+''.join('  - component: '+ref(i)+'\n' for i in [node['rect']]+node['components'])+'  m_Layer: 5\n  m_Name: '+string(node['name'])+'\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: '+str(node['active'])+'\n')
            children='\n'+''.join('  - '+ref(n['rect'])+'\n' for n in node['children']) if node['children'] else ' []\n'
            order=node['parent']['children'].index(node) if node['parent'] else 0
            parts.append('--- !u!224 &'+str(node['rect'])+'\nRectTransform:\n'+common+'  m_GameObject: '+ref(node['go'])+'\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:'+children+'  m_Father: '+ref(node['parent']['rect'] if node['parent'] else 0)+'\n  m_RootOrder: '+str(order)+'\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: '+vec2(node['amin'])+'\n  m_AnchorMax: '+vec2(node['amax'])+'\n  m_AnchoredPosition: '+vec2(node['pos'])+'\n  m_SizeDelta: '+vec2(node['size'])+'\n  m_Pivot: '+vec2(node['pivot'])+'\n')
        for cid,fid,go,body in self.components:
            label={114:'MonoBehaviour',222:'CanvasRenderer',223:'Canvas'}[cid]
            parts.append(f'--- !u!{cid} &{fid}\n{label}:\n'+common+'  m_GameObject: '+ref(go)+'\n'+body)
        return ''.join(parts)

def build():
    ui=UI();root=ui.node('法术拆装界面',size=(1600,900))
    ui.component(root,223,'  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n  m_SortingLayerID: 0\n  m_SortingOrder: 10\n  m_TargetDisplay: 0\n')
    ui.mono(root,'0cd44c1031e13a943bb63640046fad76','  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1600, y: 900}\n  m_ScreenMatchMode: 1\n  m_MatchWidthOrHeight: 0.5\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n  m_PresetInfoIsWorld: 0\n')
    ui.mono(root,'dc42784cf147c0c48a680349fa168899','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
    bg=ui.node('背景',root);background=ui.image(bg,WHITE,ext(GUID['background']))
    shade=ui.node('背景压暗',root);ui.image(shade,(.015,.04,.055,.82))
    safe=ui.node('安全区域',root,pivot=(.5,.5));fields={'safeArea':safe['rect'],'background':background}
    fit=ui.node('适配视口',safe,pivot=(.5,.5));ui.mono(fit,'86710e43de46f6f4bac7c8e50813a599','  m_AspectMode: 3\n  m_AspectRatio: 1.7777778\n')
    board=ui.node('逻辑桌面1600x900',fit,(.5,.5),(.5,.5),(0,0),(1600,900),(.5,.5))
    ui.mono(board,'d93c854365cf49c48a9da81d2fa944ec','  viewport: '+ref(fit['rect'])+'\n  content: '+ref(board['rect'])+'\n  logicalSize: {x: 1600, y: 900}\n')
    desk=ui.node('主桌面',board);fields['desktopPanel']=desk['go']
    top=ui.box(desk,'顶栏',32,16,1536,72);ui.image(top,(.035,.105,.14,1))
    _,fields['encounterText']=ui.txt(top,'关卡','1 / 4  机械甲虫',18,8,360,42,24,WHITE,3)
    _,fields['turnText']=ui.txt(top,'回合','装配',18,46,360,24,15,MUTED,3)
    for key,text,x,w,c in [('heroHealth','生命 42/42',398,180,WHITE),('heroBlock','护盾 0',596,148,WHITE),('sparkText','火花 3/12',766,180,GOLD),('energyText','能量 3/3',968,176,WHITE)]:
        _,fields[key]=ui.txt(top,key,text,x,18,w,42,22,c,3)
    _,fields['menuButton']=ui.pxbutton(top,'菜单','菜单',1350,14,164,44,(.16,.31,.37,1),20)
    hero=ui.box(desk,'角色',32,112,280,252);ui.image(hero,(.045,.13,.17,1))
    portrait=ui.box(hero,'发明家',56,8,168,182);ui.image(portrait,WHITE,ext(GUID['atlas']))
    ui.txt(hero,'提示','选择目标后出牌',16,202,248,34,18,MUTED,4)
    row=ui.box(desk,'敌牌行',332,112,724,252);ui.layout(row,gap=24,padding=0,align=4)
    enemyrefs=[]
    for i in range(3):
        _,v=ui.card(row,'敌人'+str(i+1),'enemy','机械甲虫','下一步：攻击4','生命 26/26','',1,active=i==0);enemyrefs.append(v)
    aside=ui.box(desk,'反馈区',1080,112,488,252);ui.image(aside,(.035,.105,.14,1))
    ui.txt(aside,'标题','连锁记录',18,10,452,34,20,WHITE,3)
    _,fields['eventText']=ui.txt(aside,'最近事件','选择装置，准备下一场试炼。',18,52,452,184,16,MUTED)
    devices=ui.box(desk,'装置行',32,390,1536,210);ui.layout(devices,gap=18,padding=0,align=4)
    devicerefs=[]
    for i,(a,b,c,art) in enumerate([('手动启动','电击器 · 4','无改装',4),('受伤触发','充能器 · 3','无改装',6),('耗火花触发','护盾器 · 4','无改装',5)]):
        _,v=ui.device(devices,i,b,a,b,c,art);devicerefs.append(v)
    hand=ui.box(desk,'手牌与回合',32,628,1536,252,False);fields['handPanel']=hand['go']
    handrow=ui.box(hand,'手牌行',0,0,1130,252);ui.layout(handrow,gap=18,padding=0,align=4)
    handrefs=[]
    for i,(name,desc,art) in enumerate([('敲击','造成3点直伤。',10),('防御','获得6点护盾。',5),('点燃','施加2层燃烧。',7),('充能','获得3火花。',6),('全部放电','每点火花造成2点直伤。',4)]):
        _,v=ui.card(handrow,'手牌'+str(i+1),title=name,rules=desc,art=art);handrefs.append(v)
    _,fields['endTurnButton']=ui.pxbutton(hand,'结束回合','结束回合',1172,112,340,68,font=27)
    _,fields['pileText']=ui.txt(hand,'牌堆','抽牌 5   弃牌 0',1172,192,340,36,18,MUTED,4)
    workshop=ui.box(desk,'准备试炼',32,628,1536,252);fields['workshopPanel']=workshop['go'];ui.image(workshop,(.04,.125,.16,1))
    _,fields['workshopText']=ui.txt(workshop,'下一场','下一场：机械甲虫\n甲虫会交替攻击和防御。\n战后恢复9生命。',28,26,1088,114,23,WHITE,3)
    ui.txt(workshop,'操作提示','点击上方装置，可以更换零件。',28,182,1088,36,18,MUTED,3)
    _,fields['startBattleButton']=ui.pxbutton(workshop,'进入试炼','进入试炼',1172,156,340,64,font=27)
    tip=ui.box(desk,'候选预演',1080,112,488,252,False);fields['tooltipPanel']=tip['go'];ui.image(tip,(.025,.085,.12,1),ray=True)
    fields['tooltipText'],fields['tooltipScroll']=ui.scroll_text(tip,'预演内容',18,14,452,224,16)
    reward=ui.node('奖励选择',board,active=False);fields['rewardPanel']=reward['go'];ui.image(reward,(.025,.075,.10,1),ray=True)
    _,fields['rewardText']=ui.txt(reward,'奖励标题','选择一个新零件',160,52,1280,120,28,WHITE,4)
    rewardrow=ui.box(reward,'奖励卡',210,240,1180,410);ui.layout(rewardrow,gap=50,padding=0,align=4);rewardrefs=[]
    for i,(name,desc,art) in enumerate([('全体作用','攻击与异常作用于全部敌人。',13),('冷冻器','跳过敌人下一次行动。',8),('回合开始','己方回合开始时触发。',11)]):
        _,v=ui.card(rewardrow,'奖励'+str(i+1),'reward',name,desc,'获得后可以拆装','零件',art);rewardrefs.append(v)
    result=ui.node('结果页',board,active=False);fields['resultPanel']=result['go'];ui.image(result,(.025,.075,.10,1),ray=True)
    inner=ui.box(result,'结果面板',400,190,800,500);ui.image(inner,(.08,.20,.26,1))
    _,fields['resultTitle']=ui.txt(inner,'标题','试炼完成',40,52,720,76,40,WHITE,4)
    _,fields['resultText']=ui.txt(inner,'结果','剩余生命 24 / 42',40,154,720,130,26,WHITE,4)
    _,fields['resultRestartButton']=ui.pxbutton(inner,'再次试炼','再试一次',60,350,320,64,font=24)
    _,fields['resultReturnButton']=ui.pxbutton(inner,'返回','返回牌桌',420,350,320,64,(.20,.38,.42,1),24)
    menu=ui.node('菜单页',board,active=False);fields['menuPanel']=menu['go'];ui.image(menu,(.025,.075,.10,1),ray=True)
    ui.txt(menu,'标题','菜单',560,84,480,66,36,WHITE,4)
    for i,(key,name) in enumerate([('closeMenuButton','继续'),('saveButton','保存进度'),('loadButton','读取进度'),('restartButton','重新开始'),('returnButton','返回牌桌')]):
        _,fields[key]=ui.pxbutton(menu,name,name,560,190+i*116,480,72,ORANGE if i==0 else (.15,.30,.36,1),25)
    assembly=ui.node('拆装页',board,active=False);fields['assemblyPanel']=assembly['go'];ui.image(assembly,(.025,.075,.10,1),ray=True)
    _,fields['assemblyTitle']=ui.txt(assembly,'标题','编辑装置 1',32,24,1260,56,30,WHITE,3)
    _,fields['closeAssemblyButton']=ui.pxbutton(assembly,'关闭','关闭',1428,24,140,52,(.18,.33,.38,1),22)
    columns=ui.box(assembly,'三列零件',32,112,1536,536);contents=[]
    for i,(name,title) in enumerate([('触发','触发条件'),('效果','执行效果'),('改装','改装方式')]):contents.append(ui.scroll_column(columns,name,title,i*518))
    fields['triggerContent'],fields['effectContent'],fields['modifierContent']=[x['rect'] for x in contents]
    previews=[ [('手动启动','由你启动，消耗1能量。',10),('受伤触发','生命受到伤害后启动。',12),('格挡触发','护盾吸收伤害后启动。',5),('耗火花触发','消耗火花后启动。',6),('回合开始','第1场胜利后可选。',11),('燃烧触发','第2场胜利后可选。',7)], [('电击器','每次1火花，造成4点直伤。',4),('护盾器','每次1火花，获得4点护盾。',5),('充能器','每次1火花，获得3火花。',6),('喷火器','每次1火花，施加3层燃烧。',7),('冷冻器','第1场胜利后可选。',8),('吸血器','第2场胜利后可选。',9),('重型电击器','第3场胜利后可选。',4),('备用电池','第3场胜利后可选。',6)], [('无改装','按原效果执行。',13),('全体作用','第1场胜利后可选。',13),('重复两次','第2场胜利后可选。',14),('增强输出','第3场胜利后可选。',15)] ]
    for column,items in zip(contents,previews):
        for i,(name,desc,art) in enumerate(items):ui.card(column,name,'option',name,desc,'','已选' if i==0 else '',art)
    footer=ui.box(assembly,'安装汇总',32,676,1140,192);ui.image(footer,(.08,.20,.26,1))
    fields['assemblyPreview'],fields['assemblyScroll']=ui.scroll_text(footer,'装配结果',20,16,1100,160,value='手动启动 → 电击器 → 无改装\n每次消耗1能量、1火花，每回合最多2次。\n触发：由你启动，消耗1能量。\n效果：造成4点直伤。')
    _,fields['installButton']=ui.pxbutton(assembly,'确认安装','确认安装',1200,716,368,68,font=28)
    _,fields['clearButton']=ui.pxbutton(assembly,'清空装置','清空装置',1200,806,368,52,(.18,.33,.38,1),22)
    templatenode,template=ui.card(board,'零件模板','option',active=False);fields['optionTemplate']=template
    message=ui.box(board,'提示条',32,601,1536,26,False);fields['messagePanel']=message['go'];ui.image(message,(.08,.15,.20,1))
    _,fields['messageText']=ui.label(message,'提示','',pos=(16,0),size=(-32,0),font=15,c=WHITE,align=4)
    text=''.join('  '+k+': '+ref(v)+'\n' for k,v in fields.items())
    for name,refs in [('enemies',enemyrefs),('handCards',handrefs),('rewardCards',rewardrefs),('devices',devicerefs)]:text+='  '+name+':\n'+''.join('  - '+ref(v)+'\n' for v in refs)
    view=ui.mono(root,GUID['view'],text)
    return ui,root,view

def meta(path,guid,folder=False):
    validate_guid(guid)
    if folder: body='fileFormatVersion: 2\nguid: '+guid+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif path.suffix=='.cs': body='fileFormatVersion: 2\nguid: '+guid+'\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif path.suffix=='.prefab':body='fileFormatVersion: 2\nguid: '+guid+'\nPrefabImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    else:body='fileFormatVersion: 2\nguid: '+guid+'\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    Path(str(path)+'.meta').write_text(body,encoding='utf-8')

def scene(root,view):
    # 沿用工程已有相机/输入模块格式；本地ID与源Prefab完全分离。
    text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &4200000001000\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 4200000001001}\n  - component: {fileID: 4200000001002}\n  - component: {fileID: 4200000001003}\n  m_Layer: 0\n  m_Name: 测试工坊\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &4200000001001\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000001000}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 4200000002001}\n  - {fileID: 4200000003001}\n  - {fileID: 4200000004001}\n  m_Father: {fileID: 0}\n  m_RootOrder: 0\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n--- !u!114 &4200000001002\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000001000}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 61c35c5ec6824d8ba630844f55a2e773, type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n  configuration: {fileID: 11400000, guid: 364828e17c4549d9b186cb7c9c7f16e0, type: 2}\n  view: {fileID: 4200000002002}\n  resumeSave: 1\n--- !u!81 &4200000001003\nAudioListener:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000001000}\n  m_Enabled: 1\n--- !u!1 &4200000003000\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 4200000003001}\n  - component: {fileID: 4200000003002}\n  m_Layer: 0\n  m_Name: 测试背景相机\n  m_TagString: MainCamera\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &4200000003001\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000003000}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: -10}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 4200000001001}\n  m_RootOrder: 1\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n--- !u!20 &4200000003002\nCamera:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000003000}\n  m_Enabled: 1\n  serializedVersion: 2\n  m_ClearFlags: 2\n  m_BackGroundColor: {r: 0.025, g: 0.055, b: 0.07, a: 1}\n  m_projectionMatrixMode: 1\n  m_SensorSize: {x: 36, y: 24}\n  m_LensShift: {x: 0, y: 0}\n  m_GateFitMode: 2\n  m_FocalLength: 50\n  m_NormalizedViewPortRect:\n    serializedVersion: 2\n    x: 0\n    y: 0\n    width: 1\n    height: 1\n  near clip plane: 0.1\n  far clip plane: 100\n  field of view: 60\n  orthographic: 1\n  orthographic size: 5\n  m_Depth: -1\n  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 0\n  m_RenderingPath: -1\n  m_TargetTexture: {fileID: 0}\n  m_TargetDisplay: 0\n  m_TargetEye: 3\n  m_HDR: 0\n  m_AllowMSAA: 0\n  m_AllowDynamicResolution: 0\n  m_ForceIntoRT: 0\n  m_OcclusionCulling: 0\n  m_StereoConvergence: 10\n  m_StereoSeparation: 0.022\n--- !u!1 &4200000004000\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 4200000004001}\n  - component: {fileID: 4200000004002}\n  - component: {fileID: 4200000004003}\n  m_Layer: 0\n  m_Name: 测试事件系统\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &4200000004001\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000004000}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 4200000001001}\n  m_RootOrder: 2\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n--- !u!114 &4200000004002\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000004000}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 76c392e42b5098c458856cdf6ecaaaa1, type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n  m_FirstSelected: {fileID: 0}\n  m_sendNavigationEvents: 0\n  m_DragThreshold: 10\n--- !u!114 &4200000004003\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4200000004000}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 4f231c4fb786f3946a6b90b886c48677, type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n  m_HorizontalAxis: Horizontal\n  m_VerticalAxis: Vertical\n  m_SubmitButton: Submit\n  m_CancelButton: Cancel\n  m_InputActionsPerSecond: 10\n  m_RepeatDelay: 0.5\n  m_ForceModuleActive: 0\n--- !u!1001 &4200000002000\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 4200000001001}\n    m_Modifications:\n    - target: {fileID: 1001, guid: 51c92a155d764a5092c172f9bd571220, type: 3}\n      propertyPath: m_RootOrder\n      value: 0\n      objectReference: {fileID: 0}\n    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: 51c92a155d764a5092c172f9bd571220, type: 3}\n--- !u!224 &4200000002001 stripped\nRectTransform:\n  m_CorrespondingSourceObject: {fileID: 1001, guid: 51c92a155d764a5092c172f9bd571220, type: 3}\n  m_PrefabInstance: {fileID: 4200000002000}\n  m_PrefabAsset: {fileID: 0}\n--- !u!114 &4200000002002 stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {fileID: 1002, guid: 51c92a155d764a5092c172f9bd571220, type: 3}\n  m_PrefabInstance: {fileID: 4200000002000}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ad16aa18fc0f456b8766c26e9887840e, type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n'
    text=text.replace('420000000','850000000')
    text=text.replace('测试工坊','法术拆装').replace('测试背景相机','法术背景相机').replace('测试事件系统','法术事件系统')
    text=text.replace('61c35c5ec6824d8ba630844f55a2e773',GUID['bootstrap']).replace('364828e17c4549d9b186cb7c9c7f16e0',GUID['config'])
    text=text.replace('51c92a155d764a5092c172f9bd571220',GUID['prefab']).replace('ad16aa18fc0f456b8766c26e9887840e',GUID['view'])
    text=text.replace('m_sendNavigationEvents: 0','m_sendNavigationEvents: 1').replace('m_SubmitButton: Submit','m_SubmitButton: SpellcraftSubmit')
    # 原Prefab的1001仍为新根RectTransform；视图组件由生成器实际分配。
    text=text.replace('m_CorrespondingSourceObject: {fileID: 1002, guid: '+GUID['prefab'], 'm_CorrespondingSourceObject: {fileID: '+str(view)+', guid: '+GUID['prefab'])
    return text

if __name__=='__main__':
    for name in ['Prefabs','Scenes','Unity']:
        path=OUT/name;path.mkdir(parents=True,exist_ok=True)
        if not Path(str(path)+'.meta').exists():meta(path,uuid.uuid5(uuid.NAMESPACE_URL,'cardkkk/spellcraft/'+name).hex,True)
    ui,root,view=build()
    prefab=OUT/'Prefabs/ForgeView.prefab';prefab.write_text(ui.dump(),encoding='utf-8');meta(prefab,GUID['prefab'])
    level=ROOT/'Assets/Archives/Scenes/Spellcraft.unity';level.write_text(scene(root,view),encoding='utf-8');meta(level,GUID['scene'])
    for name,key in [('ForgeView','view'),('ForgeCardView','card'),('ForgeDeviceView','device')]:meta(OUT/'Unity'/str(name+'.cs'),GUID[key])
    print('Prefab根GameObject:',root['go'],'RectTransform:',root['rect'],'Canvas:',1002,'ForgeView:',view)
    print('序列化对象:',len(ui.nodes),'组件:',len(ui.components),'下一个ID:',ui.next)

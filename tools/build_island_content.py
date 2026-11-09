"""生成孤岛1.2默认配置和CardDefinition文本资产；不启动Unity/编译/测试。

首次实现用构建源，策划后续以真实.asset为准。再次生成须显式传--replace；
已有资源GUID不变，已接入的Artwork会保留。全部未定系数为可修改临时值。
"""
from pathlib import Path
import argparse
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/IslandSurvival'
CONFIG_GUID = '38f6d818337c54b7b4206ebe8fa4b81c'
SCRIPT_GUID = 'a75253adcbd15676a8eb65d5d71f164a'
CARD_SCRIPT = 'e0cb1b8de3b778351ed4f5af8d2ce569'
KINDS = {'材料': 0, '工具': 1, '设施': 2, '区域': 3, '生物': 4, '状态': 5}
ROLES = dict(zip(['None', 'NaturalNode', 'Fire', 'Garden', 'Pen', 'Compost', 'Storage', 'DryingRack', 'AloePlant', 'YamPlant', 'AdultSnail', 'YoungSnail', 'Ornament'], range(13)))
RECIPE_KINDS = dict(zip(['Craft','Gather','Destroy','Repair','Salvage','Demolish','Rest','Plant','Harvest','Divide','Breed','SelectSeed','Care','Clean','Expand'], range(15)))
CARE = {'None': 0, 'Dressing': 1, 'Gel': 2, 'Leaf': 3, 'Clean': 4}

def guid(key):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'cardkkk/island/' + key).hex

def meta(path, value=None, folder=False, script=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    value = value or guid(str(path.relative_to(OUT)).replace('\\', '/'))
    importer = 'folderAsset: yes\nDefaultImporter:' if folder else ('MonoImporter:' if script else 'NativeFormatImporter:')
    details = ('\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}' if script else ('\n  mainObjectFileID: 11400000' if path.suffix == '.asset' else ''))
    target.write_text(f'fileFormatVersion: 2\nguid: {value}\n{importer}\n  externalObjects: {{}}{details}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')

def scalar(value):
    if isinstance(value, bool):
        return '1' if value else '0'
    if value is None:
        return "''"
    if isinstance(value, (int, float)):
        return str(value)
    return json.dumps(value, ensure_ascii=False)

class Ref(str):
    pass

def yaml_lines(value, level=0):
    prefix = ' ' * level
    lines = []
    if isinstance(value, dict):
        for key, item in value.items():
            if isinstance(item, Ref):
                lines.append(f'{prefix}{key}: {item}')
            elif isinstance(item, list):
                lines.append(f'{prefix}{key}: []' if not item else f'{prefix}{key}:')
                if item:
                    lines.extend(yaml_lines(item, level))
            elif isinstance(item, dict):
                lines.append(f'{prefix}{key}:')
                lines.extend(yaml_lines(item, level + 2))
            else:
                lines.append(f'{prefix}{key}: {scalar(item)}')
    else:
        for item in value:
            if isinstance(item, dict):
                children = yaml_lines(item, level + 2)
                lines.append(prefix + '- ' + children[0].lstrip())
                lines.extend(children[1:])
            elif isinstance(item, Ref):
                lines.append(prefix + '- ' + item)
            else:
                lines.append(prefix + '- ' + scalar(item))
    return lines

def asset(path, script, name, fields):
    header = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    base = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=Ref('{fileID: 0}'), m_PrefabInstance=Ref('{fileID: 0}'), m_PrefabAsset=Ref('{fileID: 0}'), m_GameObject=Ref('{fileID: 0}'), m_Enabled=1, m_EditorHideFlags=0, m_Script=Ref('{fileID: 11500000, guid: '+script+', type: 3}'), m_Name=name, m_EditorClassIdentifier='')
    base.update(fields)
    path.write_text(header + '\n'.join(yaml_lines(base, 2)) + '\n', encoding='utf-8')

def amounts(**values):
    return [dict(DefinitionId=key, Count=value, TraitId='') for key, value in values.items()]

CARD_DEFAULTS = dict(Id='', Name='', Description='', Family='', SortRank=0, Kind=0, Role=0,ConditionRuleId='',WoundCard=False,
    StackLimit=20, Footprint=1, Nutrition=0, ShelfMinutes=0, FuelValue=0, OrganicWeight=0,
    FoodSource='', DecayId='', CountsFood=False, Ability='', Hardness=0, InitialDurability=0,
    Repairable=False, RepairLoss=0, RepairMinutes=0, RepairToolId='', DepletedStatus='', CareStrength=0,CareDuration=0,Salvage=[], BuildingCost=[],
    Structure=0, DailyWear=0, WorkWear=0, InputSlots=1, OutputSlots=4, PlantSlots=0,
    AdultSlots=0, YoungSlots=0, StorageSlots=0, FuelCapacity=0, AshEveryFuel=0,
    CarryingCapacity=8, LoadPower=2, AshId='', SpecialtyEfficiency=1, SpecialtyTags=0, OrnamentRestBonus=0)
CARDS = []
PRESENTATION = []

def card(id, name, family='材料', role='None', desc='', **fields):
    item = dict(CARD_DEFAULTS)
    item.update(Id=id, Description=desc, Role=ROLES[role])
    item.update(fields)
    if family != '材料':
        item['StackLimit'] = 1
    if family == '区域':
        item['Footprint'] = 4
    if role == 'NaturalNode':
        item['Footprint'] = 0
    if family == '状态':
        item['Footprint'] = 0
    if fields.get('BuildingCost'):
        wood = next((a['Count'] for a in fields['BuildingCost'] if a['DefinitionId']=='wood'),0)
        item['Salvage'] = amounts(wood=wood//2) if wood//2 else []
    rank = (len(CARDS) + 1) * 10
    PRESENTATION.append(dict(Id=id, Name=name, Family=family, SortRank=rank, Attrs=[
        dict(Key='堆叠上限', Value=item['StackLimit']), dict(Key='占地单位', Value=item['Footprint']),
        dict(Key='初始耐久', Value=item['InitialDurability']), dict(Key='结构耐久', Value=item['Structure']),
        dict(Key='硬度', Value=item['Hardness'])]))
    CARDS.append(item)

card('limpet_rock','帽贝岩','设施','NaturalNode',desc='每日06:00补2帽贝，上限4。可永久破坏换石头。')
card('coast','海岸采集点','设施','NaturalNode',desc='一天一包潮汐物资；未取物资次日替换。')
card('bush','灌木地','设施','NaturalNode',desc='每日补木头4、纤维2，上限各两日供应。')
card('wild_aloe','野生芦荟','生物','NaturalNode',desc='成熟株每24小时储叶1，上限2；可分株或移除。')
card('stone','石头')
card('wood','木头',FuelValue=2)
card('fiber','纤维')
card('flint','燧石')
card('ration','口粮',Nutrition=25,desc='仅为开局物资，不计稳定营地食物统计。')
card('limpet','帽贝',ShelfMinutes=1440,DecayId='organic')
card('shell_meat','贝肉',Nutrition=20,ShelfMinutes=1440,DecayId='organic',FoodSource='limpet',CountsFood=True)
card('cooked_shell','熟贝肉',Nutrition=25,ShelfMinutes=1440,DecayId='organic',FoodSource='limpet',CountsFood=True)
card('small_shell','小贝壳')
card('large_shell','大贝壳')
card('shell_knife','贝壳刀','工具',Ability='cut',Hardness=1,InitialDurability=6,DepletedStatus='破裂',Salvage=amounts(debris=1),SpecialtyTags=4|8,SpecialtyEfficiency=1.1,desc='破裂后不能磨刀，拆解得碎屑1。专用效率为临时值。')
card('flint_knife','燧石刀','工具',Ability='cut',Hardness=3,InitialDurability=24,Repairable=True,RepairLoss=4,RepairMinutes=90,RepairToolId='stone',DepletedStatus='崩口',Salvage=amounts(debris=2),SpecialtyTags=2,SpecialtyEfficiency=1.15,desc='重整刃口降低最大耐久4，耗时90分钟；临时值。')
card('bone_knife','骨刀','工具',Ability='cut',Hardness=2,InitialDurability=12,Repairable=True,RepairLoss=2,RepairMinutes=60,RepairToolId='stone',DepletedStatus='钝化',Salvage=amounts(bone_piece=1),SpecialtyTags=8,SpecialtyEfficiency=1.1,desc='磨刀降低最大耐久2，耗时60分钟；临时值。')
card('campfire','篝火','设施','Fire',Structure=20,WorkWear=1,FuelCapacity=8,AshEveryFuel=2,AshId='ash',InputSlots=2,BuildingCost=amounts(stone=3,wood=2))
card('organic','有机废物',OrganicWeight=1,desc='堆肥与养殖原料；只有实际接触劳动产生暴露。')
card('ash','灰烬')
card('debris','碎屑')
card('charcoal','炭',FuelValue=4)
card('algae','黑藻')
card('fertilizer','肥料')
card('aloe_leaf','芦荟叶')
card('gel','凝胶')
card('bandage','绷带',CareStrength=4,CareDuration=1440,desc='止血保护强度4、有效期24小时为临时值。完成护理后才附着单处伤口。')
card('simple_dressing','简易敷料',CareStrength=1.5,CareDuration=480,desc='止血保护强度1.5、有效期8小时为临时值。')
card('shell_ornament','贝饰','工具','Ornament',OrnamentRestBonus=.25,desc='仅装备后改善休息条件；倍率0.25为临时值，不每日加精神。')
card('garden','花圃','区域','Garden',Structure=20,DailyWear=1,PlantSlots=2,BuildingCost=amounts(wood=3,stone=1,fiber=1))
card('pen','围栏','区域','Pen',Structure=20,DailyWear=1,InputSlots=2,AdultSlots=2,YoungSlots=2,BuildingCost=amounts(wood=4,fiber=2))
card('compost','堆肥区','区域','Compost',Structure=20,DailyWear=1,InputSlots=3,BuildingCost=amounts(wood=2,fiber=2))
card('storage','储物箱','设施','Storage',StorageSlots=6,InputSlots=0,OutputSlots=0,BuildingCost=amounts(wood=3,fiber=2))
card('drying_rack','晾晒架','设施','DryingRack',InputSlots=1,BuildingCost=amounts(wood=3,fiber=2),desc='木头3、纤维2及建造60分钟为临时值。与篝火共同占用一个干燥任务。')
card('yam_seed','芋种')
card('yam_plant','芋株','生物','YamPlant',desc='成熟后24小时必须完成收获。种苗性状保留。')
card('yam','芋',ShelfMinutes=1440,DecayId='organic',FoodSource='yam',CountsFood=True,desc='不可生食；烤熟后营养25。')
card('cooked_yam','熟芋',Nutrition=25,ShelfMinutes=1440,DecayId='organic',FoodSource='yam',CountsFood=True)
card('aloe_bud','芦荟繁殖芽')
card('aloe_plant','种植芦荟','生物','AloePlant')
card('snail','养殖螺','生物','AdultSnail',desc='虚构生物。每日需黑藻1和有机废物1；生产与喂养分开。')
card('young_snail','幼螺','生物','YoungSnail',desc='每日需黑藻1；在围栏中48小时成长。')
card('snail_meat','螺肉',Nutrition=20,ShelfMinutes=1440,DecayId='organic',FoodSource='snail',CountsFood=True)
card('cooked_snail','熟螺肉',Nutrition=25,ShelfMinutes=1440,DecayId='organic',FoodSource='snail',CountsFood=True)
card('bone_piece','骨质片')
card('dried_meat','干肉',Nutrition=25,ShelfMinutes=5760,DecayId='organic',CountsFood=True,desc='保质期96小时为临时值；只干燥一次，不新增营养生产统计。')
card('condition_wound','伤口','状态',WoundCard=True,desc='独立伤口实例；保存来源、结构损伤份额、清洁与敷料。护理直接作用于此牌。')
card('condition_bleeding','流血','状态',ConditionRuleId='bleeding',desc='关联伤口的异常实例；敷料控制流血，原始结构损伤仍需修复。')
card('condition_infection','局部感染','状态',ConditionRuleId='infection',desc='开放伤口与污染接触形成；清洁降低新输入，护理改变恢复。')
card('condition_pain','疼痛','状态',ConditionRuleId='pain',desc='按伤口功能标签影响相应行动的注意负担。')
card('condition_irritation','环境刺激','状态',ConditionRuleId='irritation',desc='由实际接触累积形成，停止接触后按已有模型消退。')
card('condition_hunger','饥饿性衰弱','状态',ConditionRuleId='hunger',desc='由无法支付的劳动营养缺口触发，补给后按已有模型恢复。')

RECIPE_DEFAULTS = dict(Id='',Name='',Description='',Kind=0,Inputs=[],Outputs=[],TargetDefinitions=[],
    RequiredRole=0,SecondaryRole=0,RequiredAbility='',RequiredHardness=0,ToolWear=0,
    Minutes=60,Physical=1,Cognitive=.3,Danger=0,Fuel=0,Exposure=0,Tags=1,
    Human=True,Environmental=False,InitialKnown=True,TargetRequired=False,Permanent=False,
    RequiresCareMilestone=False,DiscoverMaterial='',DiscoverMilestone='',Care=0,CareStrength=0,
    CareDuration=0,ExtraTraitCost=[],AutomaticMinutes=0,ExtraTraitMinutes=0,RepairAmount=0,
    AllowPartialInputs=False,ForcedGather=False)
RECIPES = []

def ingredient(ids, count=1, consumed=True):
    return dict(Alternatives=ids.split('|'),Count=count,Consumed=consumed)

def recipe(id, name, minutes, inputs=(), outputs=None, **fields):
    rule = dict(RECIPE_DEFAULTS)
    rule.update(Id=id,Name=name,Minutes=minutes,Inputs=list(inputs),Outputs=outputs or [])
    for key in ('RequiredRole','SecondaryRole'):
        if key in fields and isinstance(fields[key], str): fields[key] = ROLES[fields[key]]
    if isinstance(fields.get('Kind'), str): fields['Kind'] = RECIPE_KINDS[fields['Kind']]
    if isinstance(fields.get('Care'), str): fields['Care'] = CARE[fields['Care']]
    rule.update(fields)
    RECIPES.append(rule)

I = ingredient
recipe('R01','开帽贝',15,[I('limpet')],amounts(shell_meat=1,small_shell=1),Physical=.4,Cognitive=.2)
recipe('R02','留大壳',30,[I('limpet',2)],amounts(shell_meat=2,large_shell=1),RequiredAbility='cut',RequiredHardness=1,ToolWear=1,Tags=1|8,Physical=.6)
recipe('R03','磨贝壳刀',60,[I('large_shell'),I('stone',consumed=False)],amounts(shell_knife=1),Physical=.8)
recipe('R04','燧石磨刀',30,[I('large_shell'),I('flint',consumed=False)],amounts(shell_knife=1),Physical=.8)
recipe('R05','打制燧石刀',120,[I('flint'),I('fiber')],amounts(flint_knife=1,debris=1),Physical=1.2,Tags=1|2)
recipe('R06','建篝火',60,[I('stone',3),I('wood',2)],amounts(campfire=1),Physical=1.2,Tags=1|2)
recipe('R07','烤贝肉',60,[I('shell_meat',2)],amounts(cooked_shell=2),RequiredRole='Fire',Human=False,Fuel=1,TargetRequired=True,Physical=0,Cognitive=0)
recipe('R08','烤芋',60,[I('yam',2)],amounts(cooked_yam=2),RequiredRole='Fire',Human=False,Fuel=1,TargetRequired=True,Physical=0,Cognitive=0,InitialKnown=False,DiscoverMaterial='yam_seed')
recipe('R09','削纤维',30,[I('aloe_leaf')],amounts(fiber=1,organic=1),RequiredAbility='cut',RequiredHardness=1,ToolWear=1,Tags=1|8,InitialKnown=False,DiscoverMaterial='aloe_leaf',Physical=.6)
recipe('R10','提取凝胶',30,[I('aloe_leaf')],amounts(gel=1,organic=1),RequiredAbility='cut',RequiredHardness=1,ToolWear=1,Tags=1|8,InitialKnown=False,DiscoverMaterial='aloe_leaf',Physical=.6)
recipe('R11','草药护理',30,[I('gel')],Kind='Care',Care='Gel',CareDuration=1440,Tags=1|16,InitialKnown=False,DiscoverMaterial='aloe_leaf',Physical=.2,Cognitive=.5,Description='护理持续1440分钟为临时值；0强度读取身体全局凝胶强度。')
recipe('R12','制绷带',30,[I('fiber',2),I('gel')],amounts(bandage=1),InitialKnown=False,DiscoverMaterial='aloe_leaf',Physical=.3)
recipe('R13','使用或更换敷料',15,[I('bandage|simple_dressing')],Kind='Care',Care='Dressing',Tags=1|16,Physical=.2,Cognitive=.5)
recipe('R14','编贝饰',60,[I('small_shell',3),I('fiber')],amounts(shell_ornament=1),Physical=.4)
recipe('R15','建花圃',120,[I('wood',3),I('stone'),I('fiber')],amounts(garden=1),Physical=1.2,Tags=1|2)
recipe('R16','建围栏',120,[I('wood',4),I('fiber',2)],amounts(pen=1),Physical=1.2,Tags=1|2)
recipe('R17','建堆肥区',120,[I('wood',2),I('fiber',2)],amounts(compost=1),Physical=1.2,Tags=1|2)
recipe('R18','堆肥',720,[I('organic',2),I('algae')],amounts(fertilizer=1),RequiredRole='Compost',TargetRequired=True,Human=False,Environmental=True,Physical=0,Cognitive=0)
recipe('R19','加速堆肥',480,[I('organic',2),I('algae'),I('ash')],amounts(fertilizer=1),RequiredRole='Compost',TargetRequired=True,Human=False,Environmental=True,Physical=0,Cognitive=0)
recipe('R20','制骨刀',60,[I('bone_piece',2),I('fiber')],amounts(bone_knife=1,debris=1),RequiredAbility='cut',RequiredHardness=2,ToolWear=1,Tags=1|2,InitialKnown=False,DiscoverMaterial='bone_piece')
recipe('R21','建储物箱',60,[I('wood',3),I('fiber',2)],amounts(storage=1),Physical=1.2,Tags=1|2)
recipe('R22','清理废物',60,[I('organic',4)],Kind='Destroy',AllowPartialInputs=True,Physical=.5,Exposure=1)
recipe('R23','埋碎屑或灰烬',60,[I('debris|ash',4)],Kind='Destroy',AllowPartialInputs=True,Physical=.8)
recipe('R24','制木炭',120,[I('wood',2)],amounts(charcoal=1,ash=1),RequiredRole='Fire',TargetRequired=True,Human=False,Fuel=1,Physical=0,Cognitive=0)
recipe('R25','休息',120,Kind='Rest',Tags=32,Physical=0,Cognitive=0)
recipe('R26','仔细开壳',60,[I('limpet',2)],amounts(shell_meat=2,large_shell=1),Physical=.6,Cognitive=.4)
recipe('R27','制简易敷料',15,[I('fiber')],amounts(simple_dressing=1),Physical=.3,Description='纤维1、15分钟为临时值。制作不自动附着伤口。')
recipe('R28','芦荟应急护理',15,[I('aloe_leaf')],Kind='Care',Care='Leaf',CareDuration=480,Tags=1|16,InitialKnown=False,DiscoverMaterial='aloe_leaf',Physical=.2,Cognitive=.4,Description='叶1、15分钟、护理480分钟为临时值；0强度读取全局原叶强度。')
recipe('R29','基础清洁',15,Kind='Care',Care='Clean',Tags=1|16,Physical=.2,Cognitive=.4,Description='营地背景供水，无材料费；15分钟为临时值。')
for care_id, targets in {
    'R11': ['condition_wound','condition_infection','condition_pain'],
    'R13': ['condition_wound','condition_bleeding'],
    'R28': ['condition_wound','condition_pain'],
    'R29': ['condition_wound','condition_infection'],
}.items():
    care_rule = next(r for r in RECIPES if r['Id']==care_id)
    care_rule.update(TargetRequired=True,TargetDefinitions=targets)
recipe('R30','建晾晒架',60,[I('wood',3),I('fiber',2)],amounts(drying_rack=1),InitialKnown=False,DiscoverMaterial='cooked_shell|cooked_snail',Physical=1.2,Tags=1|2,Description='木头3、纤维2、60分钟为临时值。')
recipe('R31','干燥熟肉',360,[I('cooked_shell|cooked_snail',2)],amounts(dried_meat=2),RequiredRole='Fire',SecondaryRole='DryingRack',TargetRequired=True,Human=False,Fuel=1,Physical=0,Cognitive=0,InitialKnown=False,DiscoverMaterial='cooked_shell|cooked_snail',Description='同种熟肉2→干肉2，燃料1、360分钟、保质期96小时均为临时值。')

recipe('gather_limpet','采集帽贝',60,outputs=amounts(limpet=2),Kind='Gather',TargetRequired=True,TargetDefinitions=['limpet_rock'],Tags=1|4,Physical=1)
recipe('gather_coast','收取潮汐物资',60,Kind='Gather',TargetRequired=True,TargetDefinitions=['coast'],Tags=1|4,Physical=1)
recipe('gather_bush','采集灌木',120,outputs=amounts(wood=2,fiber=1),Kind='Gather',TargetRequired=True,TargetDefinitions=['bush'],Tags=1|4,Physical=1.2,AllowPartialInputs=True)
recipe('gather_aloe','采野生芦荟叶',30,outputs=amounts(aloe_leaf=2),Kind='Gather',TargetRequired=True,TargetDefinitions=['wild_aloe'],Tags=1|4,Physical=.5,AllowPartialInputs=True)
recipe('destroy_limpet','敲碎帽贝岩',120,outputs=amounts(stone=4),Kind='Destroy',TargetRequired=True,TargetDefinitions=['limpet_rock'],Permanent=True,Tags=1|2,Physical=1.5)
recipe('destroy_aloe','移除野生芦荟',30,outputs=amounts(aloe_leaf=2),Kind='Destroy',TargetRequired=True,TargetDefinitions=['wild_aloe'],Permanent=True,Physical=.6)
recipe('force_limpet','强行采集帽贝',60,outputs=amounts(limpet=2),Kind='Gather',TargetRequired=True,TargetDefinitions=['limpet_rock'],Tags=1|4,Physical=1.5,Danger=1,ForcedGather=True,RequiresCareMilestone=True,Description='须先完成本节点普通采集和应急护理储备；每来源每日一次，消耗库存。负荷与风险为临时值。')
recipe('force_bush','强行采集灌木',120,outputs=amounts(wood=2,fiber=1),Kind='Gather',TargetRequired=True,TargetDefinitions=['bush'],Tags=1|4,Physical=1.8,Danger=1,ForcedGather=True,RequiresCareMilestone=True,AllowPartialInputs=True)
recipe('repair_tool','修理刀具',60,Kind='Repair',TargetRequired=True,TargetDefinitions=['flint_knife','bone_knife'],Physical=.8,Description='实际工时、非消耗磨具和最大耐久损失读取选中刀具规则。')
recipe('repair_building','维修建筑',60,[I('wood')],Kind='Repair',TargetRequired=True,TargetDefinitions=['campfire','garden','pen','compost'],RepairAmount=10,Physical=1,Tags=1|2)
recipe('salvage_tool','拆解刀具',30,Kind='Salvage',TargetRequired=True,TargetDefinitions=['shell_knife','flint_knife','bone_knife'],Permanent=True,Physical=.6,Description='按选中工具的回收表交付，不回收制造全额。')
recipe('demolish_building','拆除建筑',60,Kind='Demolish',TargetRequired=True,TargetDefinitions=['campfire','garden','pen','compost','storage','drying_rack'],Permanent=True,Physical=1,Tags=1|2,Description='先移出内容物；仅返还建造木头的一半，向下取整。')
recipe('remove_material','搬离营地',120,[I('|'.join(x['Id'] for x,p in zip(CARDS,PRESENTATION) if p['Family']=='材料'),4)],Kind='Destroy',AllowPartialInputs=True,Physical=1,Description='一次最多4份同种材料，永久搬离，无产物。')
recipe('expand','扩建生产区',120,Kind='Expand',Physical=1.2,Tags=1|2,Description='读取扩建阶段表；成本木头4、纤维2，人工120分钟。')
recipe('seed_yam','从芋取种',30,[I('yam')],amounts(yam_seed=1),InitialKnown=False,DiscoverMaterial='yam_seed',Physical=.4)
recipe('plant_yam','种芋',30,[I('yam_seed'),I('fertilizer')],amounts(yam_plant=1),Kind='Plant',RequiredRole='Garden',TargetRequired=True,InitialKnown=False,DiscoverMaterial='yam_seed',Physical=.8)
recipe('plant_aloe','种芦荟',30,[I('aloe_bud'),I('fertilizer')],amounts(aloe_plant=1),Kind='Plant',RequiredRole='Garden',TargetRequired=True,InitialKnown=False,DiscoverMaterial='aloe_bud',Physical=.8)
recipe('harvest_yam','收获芋',60,outputs=amounts(yam=4,yam_seed=1,organic=2),Kind='Harvest',TargetRequired=True,TargetDefinitions=['yam_plant'],InitialKnown=False,DiscoverMaterial='yam_seed',Physical=1,Tags=1|4)
recipe('harvest_aloe','采种植芦荟叶',30,outputs=amounts(aloe_leaf=2),Kind='Harvest',TargetRequired=True,TargetDefinitions=['aloe_plant'],InitialKnown=False,DiscoverMaterial='aloe_bud',AllowPartialInputs=True,Tags=1|4,Physical=.5)
recipe('divide_aloe','芦荟分株',60,outputs=amounts(aloe_bud=1),Kind='Divide',TargetRequired=True,TargetDefinitions=['wild_aloe','aloe_plant'],InitialKnown=False,DiscoverMaterial='aloe_leaf',Physical=.8)
recipe('remove_aloe','移除种植芦荟',30,outputs=amounts(aloe_leaf=2),Kind='Destroy',TargetRequired=True,TargetDefinitions=['aloe_plant'],Permanent=True,Physical=.6)
recipe('breed_snail','繁殖养殖螺',30,[I('algae',2)],amounts(young_snail=1),Kind='Breed',RequiredRole='AdultSnail',TargetDefinitions=['snail'],TargetRequired=True,AutomaticMinutes=2880,ExtraTraitMinutes=120,ExtraTraitCost=amounts(algae=2),InitialKnown=False,DiscoverMaterial='snail',Physical=.5)
recipe('select_yam','芋定向选育',120,[I('yam_seed',2),I('fertilizer')],amounts(yam_seed=1),Kind='SelectSeed',InitialKnown=False,DiscoverMilestone='first_harvest',Physical=.4,Cognitive=1)
recipe('roast_snail','烤螺肉',60,[I('snail_meat',2)],amounts(cooked_snail=2),RequiredRole='Fire',TargetRequired=True,Human=False,Fuel=1,Physical=0,Cognitive=0,InitialKnown=False,DiscoverMaterial='snail')
recipe('produce_snail','养殖螺生产',1440,outputs=amounts(snail_meat=1,organic=1,bone_piece=1),RequiredRole='AdultSnail',TargetRequired=True,Human=False,Environmental=True,Physical=0,Cognitive=0,InitialKnown=False,DiscoverMaterial='snail',Description='当前喂养窗口已喂养，每个窗口最多启动一次；不再重复扣饲料。')

BIO_DEFAULT = dict(Role=0,MatureId='',SeedId='',GrowMinutes=0,ProductionMinutes=0,MatureHoldMinutes=0,
    FeedWindowMinutes=1440,DivisionCooldownMinutes=0,Feed=[],Production=[],Death=[],
    HealthGain=10,HealthLoss=25,StoredLimit=2,ProductionItem='',BreedMinimumHealth=60)
def bio(role, **fields):
    value = dict(BIO_DEFAULT,Role=ROLES[role])
    value.update(fields)
    return value

BODY = dict(HealthWeight=.5,FoodRate=.035,MindRate=.015,PhysicalPower=1.2,CognitivePower=1.1,
    DangerPower=1.2,DangerMindWeight=.3,StructureEfficiency=1,FunctionEfficiency=.7,MindEfficiency=.6,
    OtherEfficiency=.4,EfficiencyMin=.2,EfficiencyMax=2,EfficiencyPower=2,RepairRate=.025,
    StructureDamageRate=.035,NutritionHalf=.3,FunctionFloor=.5,FunctionStructurePower=1,WorsenHours=8,
    RecoverHours=12,VitalRecovery=.02,VitalDamage=.09,RestRate=.35,HungerInput=.8,PollutionHalf=8,
    ExposureScale=.02,RiskRate=.03,ForcedInjuryChance=.12,InjuryStructure=.08,InjuryVital=.02,
    GelStrength=2,
    LeafStrength=1.2,CleanAmount=.65,DirtRate=.01,SafeEventMinutes=360,FoodWarning=.25,VitalWarning=.3,
    TreatmentMax=4,TreatmentHalf=2,ExposureWarning=.5)
AFF_DEFAULT = dict(Id='',Name='',Description='',Kind=0,Growth=0,Clearance=.1,FunctionExponent=1,
    StructureWeight=0,FunctionWeight=0,VitalWeight=0,StructureThreshold=.6,FunctionThreshold=0,
    VitalThreshold=.2,CurvePower=2,CognitiveMultiplier=1,WoundRelated=False,WorkTags=0)
def aff(id, name, kind, **fields):
    value = dict(AFF_DEFAULT,Id=id,Name=name,Kind=kind)
    value.update(fields)
    return value

DEFINITION = dict(Version=1,TemporaryBalance=True,RealSecondsPerMinute=.5,StartMinute=360,
    StartCapacity=24,MaxMinutesPerAdvance=120,Columns=8,Rows=6,Width=1536,Height=600,
    CellWidth=180,CellHeight=100,Cards=CARDS,Recipes=RECIPES,
    Nodes=[
        dict(CardId='limpet_rock',Initial=amounts(limpet=4),Daily=amounts(limpet=2),Capacity=amounts(limpet=4),RefreshMinutes=1440,Tide=False),
        dict(CardId='coast',Initial=[],Daily=[],Capacity=[],RefreshMinutes=1440,Tide=True),
        dict(CardId='bush',Initial=amounts(wood=8,fiber=4),Daily=amounts(wood=4,fiber=2),Capacity=amounts(wood=8,fiber=4),RefreshMinutes=1440,Tide=False),
        dict(CardId='wild_aloe',Initial=[],Daily=amounts(aloe_leaf=1),Capacity=amounts(aloe_leaf=2),RefreshMinutes=1440,Tide=False)],
    Biology=[
        bio('YamPlant',MatureId='yam_plant',SeedId='yam_seed',GrowMinutes=1440,MatureHoldMinutes=1440,Death=amounts(organic=2)),
        bio('AloePlant',MatureId='aloe_plant',SeedId='aloe_bud',GrowMinutes=1440,ProductionMinutes=1440,Feed=amounts(fertilizer=1),Production=amounts(aloe_leaf=2),ProductionItem='aloe_leaf',DivisionCooldownMinutes=2880),
        bio('AdultSnail',MatureId='snail',ProductionMinutes=1440,Feed=amounts(algae=1,organic=1),Production=amounts(snail_meat=1,organic=1,bone_piece=1),Death=amounts(organic=2)),
        bio('YoungSnail',MatureId='snail',SeedId='young_snail',GrowMinutes=2880,Feed=amounts(algae=1),Death=amounts(organic=2))],
    Traits=[dict(Id='early',Name='早熟',Species=ROLES['YamPlant'],DurationMultiplier=20/24,AddedOutputs=[]),
        dict(Id='bountiful',Name='丰产',Species=ROLES['YamPlant'],DurationMultiplier=1,AddedOutputs=amounts(yam=1)),
        dict(Id='thick_bone',Name='厚骨',Species=ROLES['AdultSnail'],DurationMultiplier=1,AddedOutputs=amounts(bone_piece=1)),
        dict(Id='meaty',Name='高肉',Species=ROLES['AdultSnail'],DurationMultiplier=1,AddedOutputs=amounts(snail_meat=1))],
    Afflictions=[aff('bleeding','流血',0,Clearance=.2,VitalWeight=.8,VitalThreshold=0,WoundRelated=True),
        aff('infection','局部感染',1,Growth=.12,Clearance=.08,StructureWeight=.6,FunctionWeight=.7,VitalWeight=.6,StructureThreshold=.55,VitalThreshold=.65,WoundRelated=True),
        aff('pain','疼痛',2,Clearance=.35,CognitiveMultiplier=1.5,WoundRelated=True),
        aff('irritation','环境刺激',3,Clearance=.2,FunctionWeight=.2,FunctionThreshold=.25,CognitiveMultiplier=1.2),
        aff('hunger','饥饿性衰弱',4,Clearance=.15,StructureWeight=.15,FunctionWeight=.6,VitalWeight=.7,StructureThreshold=.8,VitalThreshold=.55)],
    Start=[dict(DefinitionId=id,Count=count,X=x,Y=y) for id,count,x,y in [
        ('limpet_rock',1,0,-96),('limpet_rock',1,196,-96),('wild_aloe',1,392,-96),
        ('coast',1,588,-96),('bush',1,784,-96),('stone',4,0,0),('wood',6,180,0),
        ('fiber',4,360,0),('flint',2,540,0),('ration',8,720,0)]],
    FirstTide=amounts(yam_seed=2,stone=2),TideFixed=amounts(wood=2,algae=2),
    TideRandom=amounts(stone=1,fiber=1,algae=1),
    TidePeriodic=[dict(EveryDays=3,Items=amounts(flint=1,yam_seed=1)),
        dict(EveryDays=2,Items=amounts(aloe_bud=1)),dict(EveryDays=5,Items=amounts(snail=2))],
    Expansions=[dict(Milestone='first_harvest',Capacity=32,Cost=amounts(wood=4,fiber=2),Minutes=120),
        dict(Milestone='first_breed',Capacity=40,Cost=amounts(wood=4,fiber=2),Minutes=120)],
    Body=BODY,Goal=dict(MinimumDay=31,WindowDays=3,CompostCount=3,MinimumHealth=.6,
        MinimumVital=.6,MinimumMind=.4,DailyFood=100,StoredFood=200,MinimumShelfMinutes=720,
        SourceCount=2,MaxWaste=8,CompostRecipeId='R18',AcceleratedCompostId='R19',BoneKnifeId='bone_knife',WasteId='organic'))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--replace',action='store_true',help='明确覆盖已有默认资产；Artwork引用保留')
    args = parser.parse_args()
    config = OUT/'Configuration/孤岛生存规则.asset'
    if config.exists() and not args.replace:
        raise SystemExit('默认资产已存在。策划修改保留；确认重建请使用--replace。')
    for directory in [OUT/'Configuration',OUT/'Configuration/Cards',OUT/'Editor']:
        directory.mkdir(parents=True,exist_ok=True)
        meta(directory,folder=True)
    refs = []
    for rule, display in zip(CARDS,PRESENTATION):
        path = OUT/'Configuration/Cards'/(display['Name']+'.asset')
        art = '{fileID: 0}'
        if path.exists():
            match = re.search(r'^  Artwork: (.+)$',path.read_text(encoding='utf-8'),re.M)
            if match: art = match.group(1)
        asset(path,CARD_SCRIPT,display['Name'],dict(Id=display['Id'],DisplayName=display['Name'],
            Family=display['Family'],SortRank=display['SortRank'],Attributes=display['Attrs'],
            Artwork=Ref(art),LegacyDefinitionId='',LegacyRank=0))
        card_guid = guid('card/'+display['Id'])
        meta(path,card_guid)
        refs.append(Ref('{fileID: 11400000, guid: '+card_guid+', type: 2}'))
    asset(config,SCRIPT_GUID,'孤岛生存规则',dict(Rules=DEFINITION,Cards=refs))
    meta(config,CONFIG_GUID)
    for script in ['Unity/SurvivalConfiguration.cs','Editor/SurvivalConfigurationEditor.cs']:
        path = OUT/script
        if path.exists(): meta(path,SCRIPT_GUID if script.startswith('Unity/') else None,script=True)
    print(f'已生成 {len(CARDS)} 张卡牌资产、{len(RECIPES)} 条动作/配方、4类生物、4性状、5异常。')
    print('规则资产GUID：'+CONFIG_GUID+'；全部未定数值已标记临时。未运行Unity或编译。')

if __name__ == '__main__':
    main()

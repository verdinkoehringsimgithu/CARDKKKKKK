"""写入《孤岛》修订稿默认规则和卡牌定义。数值均是策划可改的暂定值。

发行数据以 Unity 资产为准。重复生成须显式传 --replace，已接入的插画保留。
"""
import argparse
import re

from build_island_content import CARD_SCRIPT, OUT, Ref, asset, guid, meta


RULES_GUID = 'f94aa48e2c8643519334a2bd41a7d064'
CONFIG_GUID = '23fe25c00e524d82ac49576a983c59a9'
CONFIGURATION = OUT / 'Configuration'
CARD_DIR = CONFIGURATION / 'VoyageCards'
RULES_FILE = CONFIGURATION / 'VoyageRules.asset'
CARDS = {}
RECIPES = []
INPUT_TAGS = {
    'wood': '木材', 'observed': '航海观察物', 'weaving': '编织材料', 'rope_fibre': '制绳纤维',
    'compost_waste': '有机废料', 'shell_piece': '贝壳料', 'container': '容器', 'medical': '护理材料',
    'knife': '刀', 'edge': '刀或斧', 'axe': '斧', 'grinder': '打磨物',
    'plant_node': '植物资源点', 'navigation_note': '航海线索', 'relic': '海滩遗物',
}


def card(key, name, family, *, art=None, durability=0, description='', tags=(), **rules):
    card_id = f'item_{key}' if isinstance(key, int) else f'voyage.{key}'
    display = dict(name=name, family=family, art=art, durability=durability)
    runtime = dict(Id=card_id, Description=description,
                   Tags=[INPUT_TAGS.get(tag, tag) for tag in tags], GrantsRecipes=[],
                   TransformId='', NextInjuryId='', LimbId='', SeekRecipeId='', Charges=0,
                   RechargeMinutes=0, TimerMinutes=0, InjuryLevel=0, LimbDamage=0, SpawnHunger=0, SpawnSan=0)
    runtime.update(rules)
    if name in CARDS or any(row[1]['Id'] == card_id for row in CARDS.values()):
        raise ValueError(f'卡牌重复：{name} {card_id}')
    CARDS[name] = display, runtime


def card_id(name):
    return CARDS[name][1]['Id'] if name else ''


def output(name, quantity=1, *, chance=1.0, group='', unique=False):
    return dict(CardId=card_id(name), Quantity=quantity, Chance=chance, Group=group, Unique=unique)


def choice(group, *pairs):
    return [output(name, chance=chance, group=group) for name, chance in pairs]


def slot(name='', quantity=1, *, tag='', keep=False, wear=0, repair=0, recharge=0, match_limb=False):
    return dict(CardId=card_id(name), Tag=INPUT_TAGS.get(tag, tag), Quantity=quantity, IsConsumed=not keep,
                DurabilityCost=wear, DurabilityRestore=repair, ChargesRestore=recharge,
                MatchLimb=match_limb)


def hand(quantity=1):
    return slot('手', quantity, keep=True)


def recipe(key, name, minutes, inputs, outputs=(), *, location='', charges=0, wear=0,
           tag='', known=True, automatic=False, discovery='', hunger=0, san=0):
    RECIPES.append(dict(Id=key, Name=name, LocationId=card_id(location), LocationWear=wear,
                        LocationCharges=charges, Tag=tag, Minutes=minutes, InitiallyKnown=known,
                        DiscoveryCardId=card_id(discovery), Automatic=automatic,
                        Inputs=list(inputs), Outputs=list(outputs), HungerDelta=hunger, SanDelta=san))


def build_cards():
    card(1, '头', '身体', durability=18, tags=('head',))
    card(2, '手', '身体', durability=18)
    card(3, '躯干', '身体', durability=18, tags=('torso',))
    card(4, '腿', '身体', durability=18)

    resources = (
        (5, '海滩', 4, 100, '海岸采集点'), (6, '帽贝岩', 5, 60, '帽贝岩'),
        (11, '芦荟簇', 2, 90, '野生芦荟'), (20, '水洼', 4, 70, None),
        (30, '鸟巢岩', 2, 110, None), (50, '灌木丛', 3, 70, '灌木地'),
        (52, '椰树林', 4, 110, None),
    )
    for key, name, charges, recharge, art in resources:
        tags = ('plant_node',) if key in (11, 50, 52) else ()
        timer = dict(TimerMinutes=400) if key == 20 else {}
        card(key, name, '资源点', art=art, tags=tags, Charges=charges, RechargeMinutes=recharge, **timer)

    card(7, '大帽贝', '生物', art='帽贝')
    card(8, '小帽贝', '生物', art='帽贝')
    card(24, '虫子', '生物')
    card(26, '雏鸟', '生物', SeekRecipeId='C06')
    card('caged_bird', '笼中雏鸟', '生物')
    card(39, '腐烂物', '材料', art='有机废物', tags=('compost_waste',))
    card(43, '荧光黑藻', '生物', art='黑藻', description='微光来自藻叶内部。',
         tags=('san_colour', 'oddity'))
    card(10, '黑藻', '生物', art='黑藻', TimerMinutes=180, TransformId=card_id('荧光黑藻'))

    for key, name, art, durability in (
        (31, '篝火', '篝火', 22), (32, '晒架', '晾晒架', 18), (33, '滚木架', None, 18),
        (34, '织架', None, 18), (35, '烟熏架', None, 18), (36, '暖窝', None, 15),
        (37, '鸟笼', None, 15), (38, '堆肥角', '堆肥区', 20), (48, '虫笼', None, 15),
        (72, '坐垫', None, 28),
    ):
        card(key, name, '生产点', art=art, durability=durability)

    for key, name, art, tags in (
        (12, '芦荟肉', None, ()), (13, '芦荟凝胶', '凝胶', ('medical',)),
        (14, '芦荟皮', None, ()), (15, '漂木', '木头', ('wood', 'observed')), (17, '树脂', None, ()),
        (18, '破陶片', None, ('observed',)), (19, '燧石', '燧石', ()), (21, '细枝', None, ()),
        (22, '韧皮纤维', '纤维', ('weaving', 'rope_fibre')), (27, '羽毛', None, ()),
        (28, '巢材', None, ()), (29, '鸟粪', None, ()), (40, '干黑藻', None, ()),
        (41, '黑藻丝', None, ('weaving', 'rope_fibre')),
        (42, '碎渣', '碎屑', ('compost_waste',)), (44, '大贝壳', '大贝壳', ('shell', 'shell_piece')),
        (45, '小贝壳', '小贝壳', ('shell', 'shell_piece')),
        (46, '碎贝壳', None, ('shell_piece',)), (47, '贝壳粉', None, ()), (49, '叶子', None, ()),
        (51, '堆肥', '肥料', ()), (53, '椰子', None, ()), (56, '椰壳', None, ('container',)),
        (57, '木头', '木头', ('wood',)), (58, '绳索', None, ()),
        (62, '椰树叶', None, ('weaving',)), (68, '骨头', '骨质片', ()),
        (75, '头骨', None, ('oddity', 'relic')), (108, '木偶', None, ('oddity', 'relic')),
    ):
        card(key, name, '材料', art=art, tags=tags)

    foods = (
        (9, '白肉', 40, '贝肉'), (23, '浆果', 70, None), (25, '鸟蛋', 105, None),
        (54, '椰肉', 110, None), (55, '椰汁', 140, None), (65, '烟熏贝肉', 310, '干肉'),
        (73, '黑藻汤', 65, None), (74, '烤贝肉', 115, '熟贝肉'), (103, '淡水', 0, None),
    )
    for key, name, minutes, art in foods:
        timer = dict(TimerMinutes=minutes, TransformId=card_id('腐烂物')) if minutes else {}
        card(key, name, '食物', art=art, **timer)

    for key, name, art, durability, tags in (
        (66, '绷带', '绷带', 0, ('medical',)), (67, '针', None, 16, ()),
        (69, '贝壳刀', '贝壳刀', 9, ('knife', 'edge')),
        (70, '燧石刀', '燧石刀', 24, ('knife', 'edge')),
        (71, '燧石斧', None, 26, ('edge', 'axe')),
        (76, '碗', None, 0, ('container',)), (90, '贝壳项链', '贝饰', 12, ()),
        (100, '草鞋', None, 14, ()), ('grinder', '打磨石', '石头', 20, ('grinder',)),
    ):
        card(key, name, '工具', art=art, durability=durability, tags=tags)

    injuries = (
        (77, '水泡', '手', 90, 2, None), (78, '晒伤', '腿', 105, 2, None),
        (79, '伤口', '手', 125, 4, '伤口'), (80, '眩晕', '头', 100, 3, None),
        (81, '骨折', '腿', 185, 5, None), (82, '拉伤', '腿', 125, 3, None),
        (83, '腹泻', '躯干', 95, 3, None), (84, '失眠', '头', 130, 3, None),
        (85, '感染', '手', 160, 5, '局部感染'),
    )
    for key, kind, limb, timer, damage, art in injuries:
        for tier, suffix in enumerate((' I', ' II', ' III'), 1):
            injury_id = key if tier == 1 else f'injury.{key}.{tier}'
            rule = dict(LimbId=card_id(limb), InjuryLevel=tier)
            if tier < 3:
                rule.update(TimerMinutes=timer * tier, NextInjuryId=f'voyage.injury.{key}.{tier + 1}')
            else:
                rule['LimbDamage'] = damage
                if key == 79:
                    rule['TransformId'] = 'item_85'
            card(injury_id, kind + suffix, '伤势', art=art, tags=('injury',), **rule)

    for key, name, san in (
        (91, '灵感', 0), (92, '幻觉', -2), (93, '思念', -1), (94, '冲动', -1), (95, '痛苦', -2),
        (96, '愉悦', 1), (97, '感动', 1), (98, '恐惧', -3), (99, '谨慎', 0), (104, '幻听', -2),
        (106, '悲伤', -2),
    ):
        tags = ('san_colour', 'whisper') if key in (92, 98, 104) else ()
        duration = 110 if key in (91, 92) else 75
        card(key, name, '思绪', tags=tags, TimerMinutes=duration, SpawnSan=san,
             LimbId=card_id('头') if key == 98 else '')

    card(105, '全家福', '信息卡', tags=('san_colour',), description='照片背后写着回家的日期。')
    for key, name, text, grants in (
        ('task.food', '下一顿', '岩缝间有帽贝。用一只手采下来，掰开贝壳后拿白肉来。', ('G01',)),
        ('task.water', '寻找淡水', '沿海滩找水洼。碗或椰壳能装水，出航前再留一份。', ()),
        ('task.ship', '出航准备', '备齐木筏、帆、索具、防水涂料、船桨、航线和淡水。', ('E06',)),
        ('note.grind', '打磨与修理', '两只手配上打磨石，能把大贝壳磨成刀。碎贝壳能修刀或磨粉，燧石能修打磨石。',
         ('B01', 'F14')),
        ('note.current', '海流记录', '漂来的碎片都落在同一段潮线。顺着来向找。', ()),
        ('note.algae', '黑藻笔记', '黑藻可以晒干。放得太久，藻叶会自己发光。', ('F09',)),
        ('story.home', '回家之前', '照片里的人记得这条海路。你开始想起登船前听见的潮声。', ()),
        ('story.odd', '潮声里的话', '那声音和照片里的人很像。它每次都在退潮时出现。', ()),
        ('knowledge.dry', '配方·晒干黑藻', '把黑藻放入晒架，等叶片脱水。', ('C01',)),
    ):
        tags = ('navigation_note',) if key in ('note.current', 'story.home') else ()
        card(key, name, '信息卡', description=text, tags=tags, GrantsRecipes=list(grants))

    for key, name in ((59, '木筏'), (60, '索具'), (61, '帆'), (63, '船桨'), (64, '航线'), (101, '防水涂料')):
        card(key, name, '造船部件')
    card(102, '出航！', '结局')


def build_recipes():
    # PDF §11 的编号留在规则 ID 中。缺失的参数只用于打通试做流程。
    r = recipe
    p = output
    s = slot
    head = s('头', keep=True)
    torso = s('躯干', keep=True)
    wood = s(tag='wood')
    knife = s(tag='knife', keep=True, wear=1)
    axe = s(tag='axe', keep=True, wear=1)
    grinder = s(tag='grinder', keep=True, wear=1)

    r('A01', '采帽贝', 4, [hand()], [*choice('catch', ('大帽贝', .45), ('小帽贝', .55)), p('黑藻')],
      location='帽贝岩', charges=1, tag='gather')
    r('A02', '掰开小帽贝', 2, [hand(), s('小帽贝')], [p('白肉'), p('小贝壳')], tag='craft')
    r('A03', '掰开大帽贝', 3, [hand(), s('大帽贝')], [p('白肉', 2), p('大贝壳')], tag='craft')
    r('A04', '沿海滩探索', 8, [s('腿', 2, keep=True)], [
        p('帽贝岩', unique=True), p('灌木丛', unique=True), p('下一顿', unique=True),
        p('打磨石', unique=True), p('打磨与修理', unique=True),
        *choice('drift', ('漂木', .28), ('燧石', .25), ('树脂', .23), ('破陶片', .10), ('头骨', .07), ('木偶', .07)),
        p('水洼', chance=.26, group='site', unique=True), p('芦荟簇', chance=.20, group='site', unique=True),
        p('椰树林', chance=.30, group='site', unique=True), p('鸟巢岩', chance=.24, group='site', unique=True),
        p('晒伤 I', chance=.10), p('拉伤 I', chance=.05), p('骨折 I', chance=.025),
    ], location='海滩', charges=1, tag='explore')
    r('A05', '采椰树林', 6, [hand()], [
        p('椰子', chance=.28, group='palms'), p('木头', 2, chance=.47, group='palms'),
        p('椰树叶', chance=.25, group='palms'),
    ], location='椰树林', charges=1, tag='gather')
    r('A06', '取芦荟肉', 5, [hand()], [p('芦荟肉')], location='芦荟簇', charges=1, tag='gather')
    r('A07', '采灌木', 5, [hand()], [
        p('细枝'), p('韧皮纤维'), p('叶子'), p('浆果', chance=.50), p('虫子', chance=.30),
        p('伤口 I', chance=.05),
    ], location='灌木丛', charges=1, tag='gather')
    r('A08', '探鸟巢岩', 6, [hand()], [
        p('鸟蛋', chance=.76), p('巢材'), p('羽毛', chance=.43), p('鸟粪'), p('骨头', chance=.32),
    ], location='鸟巢岩', charges=1, tag='gather')
    r('A09', '敲开帽贝岩', 8, [hand(), s('帽贝岩'), s('打磨石', keep=True, wear=3)],
      [p('大帽贝'), p('小帽贝'), p('碎贝壳', 2)], tag='craft')
    r('A10', '给植物补肥', 4, [hand(), s('堆肥'), s(tag='plant_node', keep=True, recharge=2)],
      tag='craft')

    r('B01', '打磨贝壳刀', 8, [hand(2), s('大贝壳'), grinder], [p('贝壳刀'), p('碎贝壳')], tag='craft')
    r('B02', '切取芦荟凝胶', 4, [hand(), s('芦荟肉'), knife],
      [p('芦荟凝胶'), p('芦荟皮'), p('水泡 I', chance=.14)], tag='craft')
    r('B03', '烤贝肉', 6, [s('白肉')], [p('烤贝肉')], location='篝火', wear=1)
    r('B04', '熏贝肉', 12, [s('白肉'), s('篝火', keep=True, wear=1)], [p('烟熏贝肉')],
      location='烟熏架', wear=1)
    r('B05', '开椰子', 5, [hand(), s('椰子'), s(tag='edge', keep=True, wear=1)],
      [p('椰肉'), p('椰汁'), p('椰壳')], tag='craft')
    r('B06', '舀淡水', 3, [hand(), s(tag='container', keep=True)], [p('淡水')], location='水洼', charges=1,
      tag='gather')
    for index, (food, hunger, risk) in enumerate((
        ('烤贝肉', 29, 0), ('淡水', 17, 0), ('白肉', 18, .16), ('烟熏贝肉', 33, 0),
        ('椰肉', 24, 0), ('椰汁', 16, 0), ('浆果', 12, .04), ('黑藻汤', 23, 0),
        ('鸟蛋', 13, .09), ('腐烂物', 5, .68),
    )):
        label = '喝' if food in ('淡水', '椰汁', '黑藻汤') else '吃'
        injuries = [p('腹泻 I', chance=risk)] if risk else []
        r(f'B07.{index + 1:02}', label + food, 2, [torso, s(food)], injuries, hunger=hunger)

    r('C01', '晒干黑藻', 12, [s('黑藻')], [p('干黑藻')], location='晒架', wear=1,
      known=False, discovery='配方·晒干黑藻')
    r('C02', '取黑藻丝', 5, [hand(), s('干黑藻'), knife], [p('黑藻丝'), p('碎渣')], tag='craft')
    r('C03', '孵鸟蛋', 22, [s('鸟蛋'), s('巢材')], [p('雏鸟'), p('感动')], location='暖窝', wear=1)
    r('C04', '养虫', 9, [s('叶子')], [p('虫子', 2)], location='虫笼', wear=1)
    r('C05', '制堆肥', 12, [s('鸟粪'), s(tag='compost_waste'), s('贝壳粉')], [p('堆肥')],
      location='堆肥角', wear=1)
    r('C06', '雏鸟觅食', 1, [s('雏鸟', keep=True), s('虫子')],
      [p('鸟粪'), p('羽毛', chance=.36)], known=False, automatic=True)
    r('C07', '把雏鸟放进笼', 3, [hand(), s('雏鸟')], [p('笼中雏鸟')], location='鸟笼', wear=1)
    r('C08', '给笼中雏鸟喂虫', 5, [s('笼中雏鸟', keep=True), s('虫子')], [p('鸟粪'), p('羽毛', chance=.36)],
      location='鸟笼', wear=1)
    r('C09', '放出雏鸟', 3, [hand(), s('笼中雏鸟')], [p('雏鸟')], location='鸟笼', wear=1)

    r('D01', '研究黑藻', 10, [head, s('黑藻', keep=True)],
      [p('配方·晒干黑藻', unique=True), p('黑藻笔记', unique=True), p('灵感', chance=.80), p('眩晕 I', chance=.08)],
      location='坐垫', wear=1, tag='research')
    r('D02', '看全家福', 8, [head, s('全家福', keep=True)],
      [p('回家之前', unique=True), *choice('memory', ('思念', .52), ('悲伤', .15), ('感动', .33))],
      location='坐垫', wear=1, tag='research')
    # D03 的二合一及 III 级结算由卡牌的 NextInjuryId/LimbDamage 配置驱动。
    # D04、D06 由下方副产线数据匹配关联伤势或额外投入的项链。
    for name, (_, rule) in CARDS.items():
        tier = rule.get('InjuryLevel', 0)
        if tier not in (1, 2):
            continue
        limb = next(key for key, (_, row) in CARDS.items() if row['Id'] == rule['LimbId'])
        lowered = name[:-3] + ' I' if tier == 2 else ''
        target = [s(limb, keep=True), s(name, match_limb=True)]
        r('D05.' + rule['Id'], '休养·' + name, 12 if tier == 1 else 18, target,
          [p(lowered)] if lowered else [], location='坐垫', wear=1, tag='care')
        r('D05.med.' + rule['Id'], '护理·' + name, 7 if tier == 1 else 11,
          [*target, s(tag='medical')], location='坐垫', wear=1, tag='care')

    r('D07', '推算潮流', 11, [head, s(tag='observed', keep=True)],
      [p('海流记录', unique=True), p('灵感'), p('谨慎', chance=.17)],
      location='坐垫', wear=1, tag='research')
    r('D08', '研究荧光黑藻', 8, [head, s('荧光黑藻', keep=True)],
      [*choice('oddity', ('幻觉', .72), ('幻听', .18), ('冲动', .10)), p('潮声里的话', unique=True),
       p('眩晕 I', chance=.12)], location='坐垫', wear=1, tag='research', san=-5)
    r('D09', '研究海滩遗物', 9, [head, s(tag='relic', keep=True)],
      [*choice('oddity', ('幻觉', .57), ('幻听', .18), ('恐惧', .25)), p('潮声里的话', unique=True)],
      location='坐垫', wear=1, tag='research', san=-4)
    r('D10', '睡一觉', 18, [head, torso, hand(2), s('腿', 2, keep=True)],
      [p('失眠 I', chance=.04)], location='坐垫', wear=1, tag='care', san=18)
    r('D11', '静坐回想', 10, [head, s('全家福', keep=True), s('感动')], [p('愉悦')],
      location='坐垫', wear=1, tag='research', san=8)
    r('D12', '尝一口荧光黑藻', 3, [torso, s('荧光黑藻')],
      [*choice('oddity', ('幻觉', .70), ('幻听', .15), ('冲动', .15)), p('腹泻 I', chance=.25)],
      hunger=7, san=-8)
    r('D13', '看着照片缓一缓', 8, [head, s('全家福', keep=True), s('恐惧')], [p('谨慎')],
      location='坐垫', wear=1, tag='care', san=5)

    route = [p('航线', unique=True), p('出航准备', unique=True)]
    r('E01', '推导航线', 15, [head, s(tag='navigation_note', keep=True), s('灵感')], route,
      location='坐垫', wear=1, tag='research')
    r('E02', '循幻觉辨认航线', 11, [head, s('荧光黑藻'), s('幻觉')], route,
      location='坐垫', wear=1, tag='research', san=-12)
    r('E03', '扎木筏', 16, [hand(2), s(tag='wood', quantity=2), s('绳索'), axe], [p('木筏')],
      location='滚木架', wear=1, tag='craft')
    r('E04', '织帆', 15, [hand(2), s(tag='weaving', quantity=2)], [p('帆')],
      location='织架', wear=1, tag='craft')
    r('E05.rigging', '编索具', 7, [hand(), s('绳索'), knife], [p('索具')], tag='craft')
    r('E05.coating', '调防水涂料', 6, [hand(), s('树脂'), s('贝壳粉'), grinder], [p('防水涂料')], tag='craft')
    r('E05.oar', '削船桨', 9, [hand(), wood, knife], [p('船桨')], tag='craft')
    r('E06', '出航', 10, [s(name) for name in ('木筏', '索具', '帆', '防水涂料', '船桨', '航线', '淡水')],
      [p('出航！')], known=False)

    # 教学表以外的材料和设施制法补齐依赖，避免产生无法取得的核心投入。
    r('F01', '架篝火', 8, [hand(), wood, s('细枝')], [p('篝火')], tag='craft')
    r('F02', '做烟熏架', 11, [hand(), wood, s('细枝'), s('绳索')], [p('烟熏架')], tag='craft')
    r('F03', '编坐垫', 7, [hand(), s('韧皮纤维'), s('叶子')], [p('坐垫')], tag='craft')
    r('F04', '扎绳索', 5, [hand(), s(tag='rope_fibre')], [p('绳索')], tag='craft')
    r('F05', '敲制燧石刀', 7, [hand(2), s('燧石'), grinder], [p('燧石刀')], tag='craft')
    r('F06', '绑装燧石斧', 8, [hand(2), s('燧石'), s('细枝'), s('绳索'), grinder], [p('燧石斧')], tag='craft')
    r('F07', '做滚木架', 10, [hand(2), wood, s('细枝'), axe], [p('滚木架')], tag='craft')
    r('F08', '做织架', 9, [hand(2), s('细枝'), s('绳索')], [p('织架')], tag='craft')
    r('F09', '搭晒架', 8, [hand(), s('细枝'), s('绳索')], [p('晒架')], tag='craft', known=False)
    r('F10', '搭暖窝', 8, [hand(), s('巢材'), s('叶子')], [p('暖窝')], tag='craft')
    r('F11', '编虫笼', 7, [hand(), s('细枝'), s('韧皮纤维')], [p('虫笼')], tag='craft')
    r('F12', '扎鸟笼', 9, [hand(2), s('细枝', 2), s('绳索')], [p('鸟笼')], tag='craft')
    r('F13', '围堆肥角', 8, [hand(), s('细枝', 2), s('叶子')], [p('堆肥角')], tag='craft')
    r('F14', '磨贝壳粉', 5, [hand(), s(tag='shell_piece'), grinder], [p('贝壳粉')], tag='craft')
    r('F15', '削木碗', 6, [hand(), wood, s('燧石刀', keep=True, wear=2), grinder], [p('碗')], tag='craft')
    r('F16', '串贝壳项链', 5, [hand(), s('小贝壳'), s('韧皮纤维')], [p('贝壳项链')], tag='craft')
    r('F17', '搓芦荟纤维', 5, [hand(), s('芦荟皮')], [p('韧皮纤维')], tag='craft')
    r('F18', '磨骨针', 6, [hand(), s('骨头'), grinder], [p('针')], tag='craft')
    r('F19', '做绷带', 5, [hand(), s('韧皮纤维'), s('针', keep=True, wear=1)], [p('绷带')], tag='craft')
    r('F20', '煮黑藻汤', 6, [s('黑藻'), s('淡水')], [p('黑藻汤')], location='篝火', wear=1)
    r('F21', '编草鞋', 7, [hand(), s('椰树叶'), s('韧皮纤维')], [p('草鞋')], tag='craft')

    maintenance = (
        ('贝壳刀', '碎贝壳'), ('燧石刀', '燧石'), ('燧石斧', '细枝'), ('打磨石', '燧石'),
        ('贝壳项链', '韧皮纤维'), ('针', '骨头'), ('草鞋', '椰树叶'), ('篝火', '木头'),
        ('晒架', '细枝'), ('滚木架', '木头'), ('织架', '细枝'), ('烟熏架', '木头'),
        ('暖窝', '巢材'), ('鸟笼', '细枝'), ('堆肥角', '细枝'), ('虫笼', '细枝'), ('坐垫', '叶子'),
    )
    for name, material in maintenance:
        key = card_id(name)
        r('repair.' + key, '修理' + name, 6, [hand(), s(name, keep=True, repair=8), s(material)], tag='repair')
        r('salvage.' + key, '拆解' + name, 4, [hand(), s(name)], [p(material)], tag='salvage')
    r('G01', '备好下一顿', 1, [s('下一顿'), s('白肉', keep=True)], [p('寻找淡水', unique=True)], known=False)


def build_auxiliaries():
    def line(key, name, tag, *, body='', wear=0, minutes=0, san=0, outputs=()):
        return dict(Id=key, CardId=card_id(name), RecipeTag=tag, RequiredInputId=card_id(body),
                    DurabilityCost=wear, MinutesDelta=minutes, OutputMultiplier=1.0,
                    SanDelta=san, Outputs=list(outputs))

    lines = [
        line('D06', '贝壳项链', 'craft', body='手', wear=1,
             outputs=choice('necklace', ('愉悦', .33), ('感动', .12))),
        line('explore.photo', '全家福', 'explore', body='腿',
             outputs=[*choice('photo', ('思念', .38), ('悲伤', .14)), output('回家之前', chance=.30, unique=True)]),
        line('explore.shoes', '草鞋', 'explore', body='腿', wear=1, minutes=-2),
        line('research.fear', '恐惧', 'research', body='头', minutes=2),
    ]
    penalty_tags = {
        '手': ('gather', 'craft', 'repair'), '头': ('research',), '腿': ('explore',), '躯干': (),
    }
    for name, (_, rule) in CARDS.items():
        tier = rule.get('InjuryLevel', 0)
        if tier not in (1, 2):
            continue
        limb = next(key for key, (_, row) in CARDS.items() if row['Id'] == rule['LimbId'])
        for tag in penalty_tags[limb]:
            key = 'injury.' + rule['Id'] + '.' + tag
            outputs = [output('痛苦', chance=.12)] if name in ('伤口 II', '骨折 II') else []
            lines.append(line(key, name, tag, body=limb, minutes=tier * 2, san=-1 if tier == 2 else 0,
                              outputs=outputs))
    return lines


def opening():
    first = ('头', '躯干', '手', '手', '腿', '腿')
    second = ('全家福', '海滩', '黑藻')
    return [dict(CardId=card_id(name), X=round(.08 + .13 * i, 2), Y=y)
            for row, y in ((first, .10), (second, .22)) for i, name in enumerate(row)]


def find_artwork(path, reuse):
    for source in (path, CONFIGURATION / 'Cards' / (reuse + '.asset') if reuse else None):
        if source and source.is_file():
            text = source.read_text(encoding='utf-8')
            match = re.search(r'^  Artwork: (\{.*\})$', text, flags=re.MULTILINE)
            if match and match[1] != '{fileID: 0}':
                return Ref(match[1])
    return Ref('{fileID: 0}')


def write_assets(rules):
    CARD_DIR.mkdir(parents=True, exist_ok=True)
    meta(CARD_DIR, folder=True)
    refs = []
    for rank, (name, (display, rule)) in enumerate(CARDS.items(), start=1):
        path = CARD_DIR / (name + '.asset')
        card_guid = guid('voyage/card/' + rule['Id'])
        art = find_artwork(path, display['art'])
        fields = dict(Id=rule['Id'], DisplayName=name, Family=display['family'], SortRank=rank * 10,
                      Attributes=[dict(Key='初始耐久', Value=display['durability'])],
                      Artwork=art, LegacyDefinitionId='', LegacyRank=0)
        asset(path, CARD_SCRIPT, name, fields)
        meta(path, card_guid)
        refs.append(Ref('{fileID: 11400000, guid: ' + card_guid + ', type: 2}'))
    asset(RULES_FILE, CONFIG_GUID, '孤岛出航规则', dict(Rules=rules, Cards=refs))
    meta(RULES_FILE, RULES_GUID)


def build_rules():
    build_cards()
    build_recipes()
    return dict(Cards=[row[1] for row in CARDS.values()], Recipes=RECIPES,
                AuxiliaryLines=build_auxiliaries(), StartCards=opening(), HungerPerMinute=.42,
                RealSecondsPerMinute=.60, Seed=1109, BirdStep=.012, BirdRange=.18, BirdReach=.012)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--replace', action='store_true', help='允许重写已经存在的规则和卡牌资产。')
    args = parser.parse_args()
    if (RULES_FILE.exists() or CARD_DIR.exists()) and not args.replace:
        parser.error('已经存在出航资产。保留策划修改，重新生成须传 --replace。')
    rules = build_rules()
    write_assets(rules)
    print(f'{len(CARDS)} 张卡牌，{len(RECIPES)} 条组合，{len(rules["AuxiliaryLines"])} 条副产线')


if __name__ == '__main__':
    main()

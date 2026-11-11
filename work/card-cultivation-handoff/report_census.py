"""从逐文件账本生成可审查覆盖表；不执行二进制，不公开账号配置值。"""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/handoffs/2026-11-09-卡牌桌面底层'
AUDIT=OUT/'复核'
rows=json.loads((AUDIT/'全包逐文件普查.json').read_text(encoding='utf8'))
for item in rows:
    path=ROOT/'Card Cultivation'/item['path']
    if path.suffix.lower() in ('.json','.info','.config','.txt','.ini'):
        raw=path.read_bytes()
        text=raw.decode('utf-8-sig',errors='replace')
        item['text_review']={'lines':len(text.splitlines()),'replacement_characters':text.count('\ufffd')}
        if path.suffix.lower()=='.json':
            data=json.loads(text)
            item['text_review']['root_type']=type(data).__name__
            if isinstance(data,dict):item['text_review']['root_keys']=list(data)
        item['depth']='全文文本已读取；配置值未复制至报告，加载/消费链待核'
    if path.name=='GameAssembly.dll':
        item['depth']='PE/程序集方法表已解析；15545方法地址索引、58份关注函数反汇编，非全DLL行为还原'
    if path.name=='global-metadata.dat':
        item['depth']='IL2CPP v31元数据及成员索引已读；与原生方法Token关联，非可执行方法体'
(AUDIT/'全包逐文件普查.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
intro='''# 全包文件覆盖

日期：2026-11-10。原包只读。原包共有95个文件、1,529,195,385字节。**95/95逐文件登记、完整SHA256及文件头；没有文件被判为无用。**

覆盖深度必须逐层读：55个UnityFS包全部读取块/节点目录（110节点），其中全部60个序列化节点读取对象表、资产路径、MonoScript目录，共10,653条资产路径、275种脚本身份；其余50节点是原始媒体载荷，只核目录和范围，未逐个解码画面/声音。脚本身份目录不是所有动态逻辑类的集合。

另11个内置SerializedFile无TypeTree，已读取类型/对象表并验证全部对象数据范围；其对象字段、跨文件引用和行为尚未还原。此前解析失败是工具错误地按有TypeTree布局读取，已补独立无树目录读取，不能把它说成原包损坏。

三项非UnityFS的.dat：global-metadata.dat另走元数据解析；mscorlib.dll-resources.dat只识别文件与指纹，嵌入资源未深读；RemoteResource.dat有ECP头，格式与包依赖关系仍待解析。后两项均保留待办，不能归为无关。

GameAssembly已做程序集方法映射和关注函数反汇编，未遍历所有原生函数控制流。其他DLL/EXE只读完整字节指纹与头，没有执行、没有全部反汇编。启动配置/JSON/文本全文读入，配置消费链未全部追踪；账号类配置值不公开。

## 覆盖表

“待读”是明确欠账；“目录已读”不能改写成“文件行为已拆完”。较大的地图、特效、音频、美术包保留在索引中，它们可能承载对象引用和运动/反馈依赖，不能仅凭目录名排除。

| 原包相对路径 | 字节 | 已检查深度 |
|---|---:|---|
'''
lines=[]
for x in rows:
    depth=x['depth']
    if 'bundle'in x and x['bundle'].get('nodes'):
        nodes=x['bundle']['nodes']; assets=sum(len(n.get('assets',{})) for n in nodes)
        depth=f"UnityFS全目录：{len(nodes)}节点，{assets}资产路径；序列化节点目录已读，媒体内容未逐个解码"
    lines.append(f"| `{x['path']}` | {x['size']:,} | {depth} |")
tail='''

## 可复现文件

- `复核/全包逐文件普查.json`：全部路径、字节、SHA256、读取深度和包内节点/类型/脚本目录。
- `复核/全包普查汇总.json`：计数；unparsed为空仅表示该脚本请求的目录解析无异常，不代表全部语义解析完毕。
- `复核/全包资产路径索引.json`：10,653项资产路径及pathID、来源包。
- `复核/全包脚本身份索引.json`：275种MonoScript身份；不是IL2CPP所有类。
- `work/card-cultivation-handoff/census_package.py`、`report_census.py`：生成普查和本表；二进制中间物放catalogs，已加入.gitignore，未移动/修改原包。

## 下一轮内容优先级

1. Configs、RemoteResource清单、内置启动场景：先建立加载/引用关系，找出真实堆叠、槽位、输入、UI与反馈配置来源。
2. Entities/Timer、Entities/Map、UI、Animations、UIAnis：槽位、场所、收取/弹出、容器、StackType/Bag等引用和运动。
3. Effect、Sounds与ArtData全部包：沿对象真实引用找对应反馈；地图大包不豁免目录/依赖检查。
4. 原生事件订阅、虚调用与异步产物链：继续07中的未闭环分支，逐项给可定位证据。
5. 其余内置资源、运行库/插件/加载器：保留深度与待核项；只有追到内容/依赖后才能决定某部分对本项目迁移无须复用，不能从文件名直接下结论。

对“已遍历”的准确说法：全文件普查和全包资产目录覆盖已完成；所有内容、行为、媒体和依赖的穷尽分析尚未完成。当前报告没有据此签发拆解通过。
'''
(OUT/'09-全包文件覆盖.md').write_text(intro+'\n'.join(lines)+tail,encoding='utf8')
print('已写覆盖表：',len(rows),'项')

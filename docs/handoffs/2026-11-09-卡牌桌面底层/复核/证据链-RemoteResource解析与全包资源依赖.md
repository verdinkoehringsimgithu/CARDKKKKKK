# 证据链-RemoteResource解析与全包资源依赖

- 目标文件：`Card Cultivation/CardCultivation_Data/StreamingAssets/RemoteResource.dat`（676,589 字节）
- 证据级别：**E1 事实**（由 `GameAssembly.dll` 原生反汇编指令逆向证实，676,589/676,589 字节全量消费并经 EOF 断言验证）
- 产出清单：`docs/handoffs/2026-11-09-卡牌桌面底层/复核/RemoteResource完整清单.json`（55个资源包、10,653个资产及依赖关系）

---

## 1. 原生指令与格式闭环（E1）

### 1.1 反汇编定位
在 `GameAssembly.dll` 中，`UnityGameFramework.Runtime.BuiltinVersionListSerializer` 负责反序列化 `RemoteResource.dat`：
- 方法：`PackageVersionListDeserializeCallback_V2`
- Method Token：`0x600049b`（RID 1179）
- 虚拟地址（VA）：`0x182688090`
- 辅助方法：
  - `ReadString(BinaryReader, byte[] encryptBytes)`：VA `0x18266BAC0`
  - `ReadInt32_7Bit(BinaryReader)`：VA `0x18266B760`
  - `Utility.Converter.GetString(byte[] bytes, int offset, int length, byte[] encryptBytes)`：VA `0x18084A730`

### 1.2 原生反汇编指令证实（dumpbin 逐行提取）
```x86asm
; 头部与版本信息
00000001826881B9: mov   edx,4
00000001826881BE: call  rax                 ; BinaryReader.ReadBytes(4) -> encryptBytes (c2 a5 73 25)
00000001826881EB: call  000000018266BAC0   ; ReadString -> ApplicableGameVersion ('2.2.0.24_beta')
00000001826881FF: call  000000018266B760   ; ReadInt32(7-bit) -> InternalResourceVersion (5)
000000018268820F: call  000000018266B760   ; ReadInt32(7-bit) -> AssetCount (10653)

; 1. 资产循环（0 .. AssetCount - 1）：
000000018268827C: call  000000018266BAC0   ; ReadString -> AssetName
000000018268828B: call  000000018266B760   ; ReadInt32(7-bit) -> DependencyAssetCount
; 依赖项循环：
00000001826882D4: call  000000018266B760   ; ReadInt32(7-bit) -> DependencyAssetIndex

; 2. 资源包循环（0 .. ResourceCount - 1）：
000000018268837B: call  000000018266B760   ; ReadInt32(7-bit) -> ResourceCount (55)
00000001826883EC: call  000000018266BAC0   ; ReadString -> ResourceName
0000000182688401: call  000000018266BAC0   ; ReadString -> Variant
0000000182688419: call  000000018266BAC0   ; ReadString -> Extension
000000018268844B: call  rax                ; BinaryReader.ReadByte() -> LoadType (1)
0000000182688473: call  000000018266B760   ; ReadInt32(7-bit) -> Length (字节长度)
000000018268849B: call  r8                 ; BinaryReader.ReadInt32() -> HashCode (4字节二进制小端)
00000001826884A9: call  000000018266B760   ; ReadInt32(7-bit) -> AssetCount (包含资产数)
; 包内资产循环：
00000001826884F4: call  000000018266B760   ; ReadInt32(7-bit) -> AssetIndex

; 3. 资源组循环（0 .. ResourceGroupCount - 1）：
0000000182688606: call  000000018266B760   ; ReadInt32(7-bit) -> ResourceGroupCount (0)

; 4. 文件系统循环（0 .. FileSystemCount - 1）：
000000018268877B: call  000000018266B760   ; ReadInt32(7-bit) -> FileSystemCount (0)
; 原脚本初版因漏读此尾部 7-bit 整数导致剩余 1 字节未消费；补齐后 676,589 字节全量消费并验证 EOF
```

### 1.3 字符串解密核心（VA 0x18084A730）
- 字符串长度由单字节 `ReadByte()` 读取。
- 字节内容逐字节与 `encryptBytes[i % 4]` 异或：
  `000000018084A7C7: xor byte ptr [r8], cl`（`cl = encryptBytes[i % 4]`）
- 异或后转换为标准 UTF-8 编码字符串。

---

## 2. 55个资源包与资产分布统计

`RemoteResource.dat` 精确登记了 55 个 AssetBundle，与 `CardCultivation_Data/StreamingAssets/` 目录下的 55 个 `.dat` 文件完全一一对应（包括字节长度完全吻合）：

| 序号 | 资源包名 | 文件类型 | 资产数 | 字节大小 | 核心桌面交互内容说明 |
|:---:|:---|:---:|:---:|:---:|:---|
| 0 | `ArtData/Boss` | .dat | 27 | 7,770,504 | Boss 卡牌插画与立绘 |
| 1 | `ArtData/Buff` | .dat | 9 | 62,904 | 状态效果图标 |
| 2 | `ArtData/Card` | .dat | 197 | 14,420,144 | **基础卡牌正面插画、牌面背景** |
| 3 | `ArtData/CardTitle` | .dat | 22 | 2,303,721 | 卡牌品阶/流派称号与边框 |
| 4 | `ArtData/Cloud` | .dat | 6 | 358,410 | 场景环境云雾 |
| 5 | `ArtData/Common` | .dat | 9 | 21,199 | 通用按钮与状态角标 |
| 6 | `ArtData/Drive` | .dat | 186 | 7,978,343 | 驱动法宝与御物插图 |
| 7 | `ArtData/Enemy` | .dat | 58 | 5,902,495 | 敌人实体立绘 |
| 8 | `ArtData/Items` | .dat | 660 | 46,141,865 | 道具、材料、丹药与符箓插图 |
| 9 | `ArtData/Maps/Map1` | .dat | 850 | 652,984,724 | 大地图场景纹理 |
| 10 | `ArtData/MapsTimer` | .dat | 115 | 36,934,462 | **地图资源点进度计时器外观** |
| 11 | `ArtData/Role` | .dat | 208 | 25,306,151 | 人物角色各境界立绘 |
| 12 | `ArtData/Skill` | .dat | 344 | 16,336,706 | 功法与神通卡牌图素 |
| 13 | `ArtData/Spine` | .dat | 42 | 19,661,379 | 动态骨骼动画资源 |
| 14 | `ArtData/SpiritBeastPuppet` | .dat | 61 | 7,249,734 | 灵兽与傀儡实体卡图素 |
| 15 | `ArtData/Weapon` | .dat | 186 | 14,815,101 | 武器与法宝装备插图 |
| 16 | `Configs` | .dat | 6 | 1,213,632 | **全局基础配置、常量与路径表** |
| 17 | `Effect` | .dat | 721 | 44,621,332 | **卡牌拖拽选中粒子、高亮描边着色器与材质** |
| 18 | `Entities/Animations` | .dat | 216 | 71,932 | **卡牌飞入、跳跃与窗口展合动画剪辑** |
| 19 | `Entities/Area` | .dat | 1 | 4,871,069 | 桌面区域划分 Prefab |
| 20 | `Entities/Boss` | .dat | 9 | 5,050,791 | Boss 实体 Prefab |
| 21 | `Entities/Card` | .dat | 144 | 5,707,443 | **CardBase、CardStack、BaseCardEntity 原型 Prefab** |
| 22 | `Entities/Effect` | .dat | 159 | 19,070,024 | 各种反馈效果 Prefab（含 EffectSelect） |
| 23 | `Entities/Map` | .dat | 2,139 | 23,418,733 | **地图场所实体、CardWidget、工作场所 Prefab** |
| 24 | `Entities/Timer` | .dat | 112 | 14,528,552 | **圆形/条形进度计时器与卡槽装配 Prefab** |
| 25 | `Entities/Tips` | .dat | 31 | 5,506,016 | 桌面悬停提示与引导泡泡 Prefab |
| 26~28 | `Localization/*` | .dat | 156 | ~6,688,843 | 简中、繁中、英文多语言字串与图片 |
| 29 | `LubanTables` | .dat | 164 | 1,963,587 | **Luban 二进制配方、堆叠门禁、场所配置表** |
| 30 | `Scenes` | .dat | 3 | 148,881 | 主场景与启动流程场景 |
| 31 | `Sounds` | .dat | 83 | 121,465,031 | 牌面交互音效、入槽声音、环境背景音乐 |
| 32 | `UI/UIAnis` | .dat | 142 | 59,632 | **UI 动画：窗口弹开、卡槽收缩、存档旋转** |
| 33 | `UI/UIForms` | .dat | 262 | 8,011,739 | **工作台场所窗口、探索面板、炼丹/锻造UI** |
| 34 | `UI/UISounds` | .dat | 2 | 3,715 | UI 点击与松手音效 |
| 35~54 | `UI/UISprites/*` | .dat | 3,363 | ~200 MB | 各类 UI 图集（卡槽槽框、按钮、角标） |

---

## 3. 证据效力与边界说明

1. **格式与字节完整性**：
   - 文件结构确证为 `GameFramework` 内置 PackageVersionList V2 格式。
   - 提取器在读完 `ResourceGroups` 后追加读取 `FileSystems`（`0x18268877B`），成功解释了此前残留的最后 1 字节（`0x00` 表示 0 个 FileSystems），达成 676,589/676,589 字节的全流校验。
2. **关系边界澄清**：
   - 本清单提供的是**包清单层级声明的直接资产归属与前向依赖索引**（共 17,252 条依赖弧），并非运行时的全部动态 PPtr 引用、虚方法派发或动态加载链。
   - 提取数据与全包 95 文件普查目录相吻合，但使用时需注意路径大小写正规化处理。
   - 产出清单可用于定位各功能模块所需的具体包与 Prefab 路径，不应夸大为所有交互逻辑已全量闭环。

# 美术占位与替换

当前已接入现有素材：棕色卡面、棕色花纹卡背和暗色桌面底图。物件插画、字体和音频仍可留空。

`卡牌桌面 > 游戏规则配置 > 功能与资源` 指定主题，或用 `卡牌桌面 > 选择美术主题` 定位默认主题。卡牌插画单独配置在 `CardDefinition`，主题只管理通用外观与声音。停止运行后修改，下次运行应用。

| 目录 | 放置内容 | 接入位置 |
|---|---|---|
| `CardFaces` | 单张插画，导入类型为 Sprite (2D and UI) | 卡牌目录中对应卡牌定义的“卡面插画” |
| `CardFrames` | 通用卡框、卡背 | 主题 CardFrame、CardBack |
| `Table` | 桌面图像 | 主题 TableSurface |
| `Icons` | 操作图标 | ActionIcon 预留；当前按钮用中文文字 |
| `Audio/SFX` | 拾起、落牌、拒绝、翻面 | PickUpSound、DropSound、RejectedSound、FlipSound |
| `Audio/Music` | 循环音乐 | 主题 Music |
| `Assets` | 用户已有资源 | 当前卡面、卡背和桌面从这里引用，源图片不改动 |

卡名、序号与属性由数据提供，插画无需包含文字。字体在主题“界面字体”指定，空值时使用 Windows 中文字体。空图像引用使用文字和色块占位，空音频引用保持静音。

布局在 `Assets/CardTable/Prefabs/CardTableView.prefab`，牌面在 `CardView.prefab`；主题与定义不承担控件位置。过渡时长由主题 `TransitionSeconds` 控制，动画只展示模型，不修改归属、数量和牌序。

## 已配置的素材

| 原文件 | 使用部分 | 接入位置 |
|---|---|---|
| `Assets/sprite_sheet.png` | `card_front_brown`，原图棕色空卡面 | 主题“通用卡框” |
| 同一图集 | `card_back_brown`，原图棕色花纹卡背 | 主题“卡牌背面” |
| `Assets/High Resolution/card_background.png` | 暗色边框底图 | 主题“桌面背景” |

图集保留原图和 GUID，只在 `.meta` 中切片；卡面与卡背各为 68×109。卡框边界 4、桌面边界 100，使用九宫格保持边角。主题的贴图颜色设为白色以保留素材原色，“卡面文字颜色”单独配置。

其他可用素材：`Card` 里的独立空模板可以换另一种卡牌风格；`High Resolution` 的 blank/back 是成套高对比卡面与卡背。普通 frame 版本自带战斗数值槽，Duel Kit 是另一套战斗像素样张，这轮没有混用。`Card/card game templates.jpg` 是含英文的展示海报，不接入游戏。

目前没有找到木料、锅、火堆、水罐等物件插画，也没有字体或音频文件，因此没有给九种示例牌硬绑无关图案。之后把物件图放入定义的“卡面插画”即可。

运行与编辑预览共用 `CardTableArt` 设置；预览在保存场景前恢复桌面图片、颜色和九宫格模式，避免将临时表现写成场景覆盖。

旧主题中的 `CardFaces/DefinitionId` 二次映射已移除，避免同一张牌同时在两个位置维护图像。默认九种定义使用稳定 ID `archive/supply/tool` 各 `.1/.2/.3`；旧版 `.sample` 只用于存档迁移，不再作为新图像接入方式。

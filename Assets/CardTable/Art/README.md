# 美术占位与替换

桌面使用色块、中文文字、位移和翻面动画占位；图像和音频都可留空。

`卡牌桌面 > 游戏规则配置 > 功能与资源` 指定主题，或用 `卡牌桌面 > 选择美术主题` 定位默认主题。卡牌插画单独配置在 `CardDefinition`，主题只管理通用外观与声音。停止运行后修改，下次运行应用。

| 目录 | 放置内容 | 接入位置 |
|---|---|---|
| `CardFaces` | 单张插画，导入类型为 Sprite (2D and UI) | 卡牌目录中对应卡牌定义的“卡面插画” |
| `CardFrames` | 通用卡框、卡背 | 主题 CardFrame、CardBack |
| `Table` | 桌面图像 | 主题 TableSurface |
| `Icons` | 操作图标 | ActionIcon 预留；当前按钮用中文文字 |
| `Audio/SFX` | 拾起、落牌、拒绝、翻面 | PickUpSound、DropSound、RejectedSound、FlipSound |
| `Audio/Music` | 循环音乐 | 主题 Music |
| `Assets` | 用户已有参考/资源 | 保留原样，不会自动接入游戏 |

卡名、序号与属性由数据提供，插画无需包含文字。字体在主题“界面字体”指定，空值时使用 Windows 中文字体。空图像引用使用文字和色块占位，空音频引用保持静音。

布局在 `Assets/CardTable/Prefabs/CardTableView.prefab`，牌面在 `CardView.prefab`；主题与定义不承担控件位置。过渡时长由主题 `TransitionSeconds` 控制，动画只展示模型，不修改归属、数量和牌序。

旧主题中的 `CardFaces/DefinitionId` 二次映射已移除，避免同一张牌同时在两个位置维护图像。默认九种定义使用稳定 ID `archive/supply/tool` 各 `.1/.2/.3`；旧版 `.sample` 只用于存档迁移，不再作为新图像接入方式。

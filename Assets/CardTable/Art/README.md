# 美术占位与替换

首版由 Unity 色块、文字、网格、位移和翻面动画构成，可在没有图片或音频的情况下完整操作。

在菜单选择 `CardKKK > Select Art Theme`，编辑 `CardTableTheme.asset`。演示场景中的 `CardTableDemo.theme` 已引用此资源。更改主题后重新进入 Play，加载新的外观。

| 目录 | 放置内容 | 主题字段 |
|---|---|---|
| `CardFaces` | 单张卡面插画。导入类型设为 Sprite (2D and UI) | CardFaces 列表。DefinitionId 必须与卡牌定义一致，例如 `archive.sample` |
| `CardFrames` | 通用卡框和卡背 | CardFrame、CardBack |
| `Table` | 桌面背景图 | TableSurface |
| `Icons` | 操作图标 | ActionIcon 预留字段，当前按钮使用文字 |
| `Audio/SFX` | 拾起、落牌、拒绝落点、翻面音效 | PickUpSound、DropSound、RejectedSound、FlipSound |
| `Audio/Music` | 循环背景音乐 | Music |

卡名、属性、排序数字都来自运行时数据，卡面插画不需要包含这些文字。空的 Sprite / AudioClip 字段会使用占位显示或静音。

动画速度使用 `TransitionSeconds`。动画只负责展示，不改动卡牌归属、排序和存档状态。拖动期间显示半透明落点，绿色可提交，红色会恢复原位。绿色横线标出插入槽位。

当前占位牌有 Archive、Supply、Tool 三种 Family，同 Family 可以叠放，不同 Family 分开放置。示例 DefinitionId 为 `archive.sample`、`supply.sample`、`tool.sample`。

Unity 的 `.anim` / Animator 预留扩展应继续读取模型状态，避免在动画事件里增删卡牌。

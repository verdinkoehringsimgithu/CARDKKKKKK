# 法术拆装美术

2026-11-05。SpriteCook余额核查为0，按用户授权使用内置image_gen生成，未使用CLI或外部API密钥。三张原创图统一采用深青蓝粗轮廓、圆润夸张造型、干净的奶白/珊瑚橙/琥珀/淡紫色块。完整实际提示词见GenerationPrompts.md，尺寸、透明度、GUID和摘要见manifest.json。

- SpellcraftAtlas.png：1254×1254 RGBA，4×4共16张Sprite，每格313.5×313.5。按左到右、上到下编号，Unity坐标从左下换算；fileID自21300000起每项加2。轮廓完整留在格内，少量极低alpha残留保留原生像素，未用程序改图。
- SpellcraftWorkshop.png：1672×941 RGB，不透明，近16:9。中央青蓝桌面留给卡牌，器材装饰集中四角。单Sprite fileID21300000。
- SpellcraftCardFrame.png：1024×1536 RGBA，奶白卡身、深蓝下方信息区，外沿alpha0，主体alpha254。九宫格边距为左144、下480、右144、上216；单Sprite fileID21300000。

所有图片均已目视检查并配好meta。导入使用Clamp、双线性、无mipmap、无压缩；图集使用Full Rect网格和中心轴。小卡框使用Image.Type.Sliced并以约7倍pixelsPerUnitMultiplier匹配尺寸。文字、规则和数值由Unity独立叠放，原图不带文字。

图集第一稿因物件触格边被淘汰，仅交付修正后的最终图。该目录没有复制其他游戏素材；没有启动Unity导入、编译或运行检查。
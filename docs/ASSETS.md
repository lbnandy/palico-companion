# 资源布局 / Asset layout

| 文件 | 用途 / Purpose |
| --- | --- |
| `Assets/Palico.png` | 31 × 28 像素一帧，43 帧纵向排列 / 43 vertically stacked 31 × 28 frames |
| `Assets/Palico-head.png` | 31 × 26 透明画布，使用画师待机帧头部的整数倍裁切 / 31 × 26 canvas with an integer-scaled crop of the artist idle-frame head |
| `Assets/Palico-cart-cat.png` | 推车呆猫，6 帧纵向排列，每帧 64 × 32；支持服装颜色与染料 / Six 64 × 32 cart-pushing cat frames, supporting outfit colors and dyes |
| `Assets/Palico-cart.png` | 与推车呆猫逐帧对齐的车身与轮子；不应用呆猫染料 / Matching cart and wheel frames, unaffected by companion dyes |
| `Assets/FlightKite.png` | 飞行风筝 / Flight kite |
| `Assets/Training/` | 装备与工作台贴图 / Equipment and workbench sprites |
| `Assets/GripOffsets.csv` | 投射物类型、朝向、握持偏移 X/Y / Projectile type, direction, grip offset X/Y |
| `Assets/ItemFrames.csv` | 物品类型、原生绘制帧宽高 / Item type, native draw-frame width and height |
| `icon.png` | 80 × 80 模组图标 / Mod icon |
| `icon_small.png` | 30 × 30 设置图标 / Settings icon |
| `icon_workshop.png` | 480 × 480 工坊封面 / Workshop cover |

动画帧名与手部锚点由 `Systems/CompanionSpriteLayout.cs` 定义。修改图集帧序时需一起更新。猫头在加载时按整数倍复制像素，再由 UI 绘制流程缩放；不需要另存不同大小的头像。CSV 提供专用服务器无法读取 GPU 贴图时所需的尺寸与握持数据。

Frame names and hand anchors are defined in `Systems/CompanionSpriteLayout.cs`; update them when changing the atlas order. The UI portrait is enlarged on an integer grid at load time before UI scaling, so separate portrait-size files are unnecessary. The CSV files provide dimensions and grip data for dedicated servers, where GPU textures are unavailable.

本体、举桶和起身帧共用脚底锚点；推车的脚底与轮底对齐。画师源文件中的背景、色板、编号、桶定位块和玩家占位层不进入游戏贴图。玩家使用原版角色绘制，在车身前沿后方显示。

Body, barrel-carrying and get-up poses share a foot anchor; the cart wheels align with the pushing cat’s feet. Source backgrounds, palette swatches, frame numbers and placement markers are excluded. The passenger uses the native player renderer behind the near side of the cart.

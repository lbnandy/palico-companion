# 资源布局 / Asset layout

| 文件 | 用途 / Purpose |
| --- | --- |
| `Assets/Palico.png` | 31 × 28 像素一帧，22 帧纵向排列 / 22 vertically stacked 31 × 28 frames |
| `Assets/Palico-head.png` | 31 × 26 的 UI 头像 / 31 × 26 UI portrait |
| `Assets/FlightKite.png` | 飞行风筝 / Flight kite |
| `Assets/Training/` | 装备与工作台贴图 / Equipment and workbench sprites |
| `Assets/GripOffsets.csv` | 投射物类型、朝向、握持偏移 X/Y / Projectile type, direction, grip offset X/Y |
| `Assets/ItemFrames.csv` | 物品类型、原生绘制帧宽高 / Item type, native draw-frame width and height |
| `icon.png` | 80 × 80 模组图标 / Mod icon |
| `icon_small.png` | 30 × 30 设置图标 / Settings icon |
| `icon_workshop.png` | 480 × 480 工坊封面 / Workshop cover |

动画帧名与手部锚点由 `Systems/CompanionSpriteLayout.cs` 定义。修改图集帧序时需一起更新。猫头在加载时按整数倍复制像素，再由 UI 绘制流程缩放；不需要另存不同大小的头像。CSV 提供专用服务器无法读取 GPU 贴图时所需的尺寸与握持数据。

Frame names and hand anchors are defined in `Systems/CompanionSpriteLayout.cs`; update them when changing the atlas order. The UI portrait is enlarged on an integer grid at load time before UI scaling, so separate portrait-size files are unnecessary. The CSV files provide dimensions and grip data for dedicated servers, where GPU textures are unavailable.

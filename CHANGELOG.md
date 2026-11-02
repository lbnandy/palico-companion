# Changelog

## 1.1.1 — 2026-11-02

- 新增可自定义的收回／叫出呆猫快捷键。
- 新增主动进攻／协同进攻／不攻击三种模式，点击剑形按钮循环切换。协同模式在主人命中或指定目标、主人或呆猫受击后参战；不攻击模式仍保留工作、拾取和治疗。
- 接入原版手柄界面导航，支持背包、配装、指南、外观与改名操作。
- 修正呆猫背包中的武器提示框错误使用玩家属性的问题。
- 改进旅人归途专用服务器兼容，去掉固定版本号限制。
- 新增暗影奥秘（SOTS）刀光与虚空资源适配。
- 修复雀杖召唤物在呆猫头上停留时反复消失、重新召唤的问题。
- 调整特大桶爆弹炸飞落地后的短暂停顿与起身衔接。

- Added an optional keybind to stow or call out your Palico.
- Added proactive, assist and passive combat modes, cycled by clicking the sword button. Assist mode engages enemies the owner hits or marks, or retaliates when the owner or Palico is hit. Passive mode retains work, pickup and healing.
- Added native gamepad navigation for inventory, loadouts, the guide, appearance and naming.
- Fixed weapon tooltips in the Palico inventory incorrectly using the owner's stats.
- Improved Homeward Journey dedicated-server compatibility without a fixed-version restriction.
- Added Secrets of the Shadows (SOTS) trail and void-resource integration.
- Fixed Finch Staff minions repeatedly disappearing and being resummoned while perched on the Palico.
- Refined the brief landing pause and get-up sequence after a Giga Barrel Bomb blast.

## 1.1 — 2026-11-01

- 接入画师新版角色、推车与举桶动画；UI 头像和模组图标改用新版素材。
- 新增投喂、特大桶爆弹、猫车演出与呆猫风筝。
- 新增瑟银乐器、治疗与投掷武器联动。
- 新增状态卡拖动、可自定义显隐快捷键、染色范围、可调索敌距离和默认关闭的 Boss 生命补偿。
- 调整收藏物品的使用与批量取回规则，修正钓鱼奖励、命中判定和拾取拉扯。
- 修复已复现的部分弹幕、枪械、界面和联机兼容问题；更新装备名称与部分冷却、伤害。

- Integrated the artist's new character, cart and barrel animations; replaced UI portraits and mod icons with the new artwork.
- Added feeding, the Giga Barrel Bomb, cart death scenes and Palico kite flying.
- Added Thorium instrument, healing and throwing-weapon integration.
- Added draggable HUD placement, an optional HUD keybind, dye scope, configurable targeting range and an opt-in boss health bonus.
- Revised favorite-item use and bulk retrieval; corrected fishing rewards, hit rules and competing item pickup.
- Fixed reproduced projectile, gun, UI and multiplayer compatibility cases; revised equipment names and selected cooldowns and damage.

## 1.0.6 — 2026-10-24

- 更新角色与头像美术及界面头像尺寸，玩法沿用 1.0.5。
- Updated artwork and portrait sizing; gameplay remained based on 1.0.5.

## 1.0.5 — 2026-10-24

- 新增暂停环境改动按钮及可自定义快捷键，建房时可暂停呆猫的自动挖掘、采集和放置行为。
- 调整挖掘协助的触发判断，开始建造时停止旧的挖掘任务，减少修补房屋时的误拆。
- 新增工作速度、钓鱼速度的独立设置，默认均为 100%，不随强度预设改变。
- 将设置分类统一为「呆猫能力」「战斗能力」，更新中英文说明。

- Added an environment-work toggle and an optional keybind to pause automatic digging, planting and block placement while building.
- Refined digging-assistance detection. Starting construction now cancels stale digging tasks, reducing unwanted excavation during repairs.
- Added separate work-speed and fishing-speed settings, both defaulting to 100% and independent of strength presets.
- Renamed the settings categories to Companion abilities and Combat abilities, with updated English and Simplified Chinese descriptions.

## 1.0.4 — 2026-10-24

- 修复与积分商店同时启用时，购买商品可能扣除积分却无法获得物品的问题。

- Fixed purchases deducting points without granting items when used with Point Shop.

## 1.0.3 — 2026-10-24

- 修复部分情况下界面绘制异常导致鼠标指针消失、状态卡无法恢复显示的问题。
- 修正进入世界和切换窗口时的指令快捷键状态处理。

- Fixed interface rendering errors that could hide the cursor and prevent the status card from recovering.
- Fixed command hotkey state handling when entering a world or switching windows.

## 1.0.2 — 2026-10-23

- 新增辅助、标准和强力三档预设，也可分别调整伤害、生命与防御继承、战斗移速、跟随与工作移速、主动闪避。
- 默认命中伤害和战斗移速调整为原来的 75%，主动闪避改为标准；生命与防御仍默认继承玩家的 75%。跟随与工作移速保持不变。
- 联机时共用服务器的强度设置，房主可在游戏内修改。调整生命上限保留当前血量比例，不会复活倒地的呆猫。
- 统一设置下拉框和预设按钮的文字样式，并补充中英文说明。

- Added Support, Standard and Strong presets, with individual controls for damage, inherited health and defense, combat movement, following and work movement, and active evasion.
- Default hit damage and combat movement are now 75% of their previous values, with Standard evasion. Health and defense still default to 75% of the player's values; following and work movement are unchanged.
- Multiplayer uses shared server balance settings, editable in-game by the host. Changing maximum health preserves the current health percentage and does not revive a downed Palico.
- Unified dropdown and preset-button typography, with updated English and Simplified Chinese descriptions.

## 1.0.1 — 2026-10-23

补充装备使用与换装台词，减少连续触发的重复发言。
修正采矿拾取、收竿后的转向，以及拾取和存放物品时持续闭眼的问题。
修正天顶剑剑光的位置，并改善敌人替换后的选敌与攻击距离判断。
修正灾厄诘责、发条弓、麦格纳炮蓄力后不释放的问题。

Added equipment reactions and reduced chatter during repeated actions.
Fixed pickup while mining, turning after reeling in a catch, and prolonged eye closing after collecting or storing items.
Corrected Zenith trail positioning and target tracking when an NPC slot is reused.
Fixed charged attacks with Calamity's Condemnation, Clockwork Bow and Magna Cannon.

## 1.0.0 — 2026-10-22

首次发布：呆猫伙伴。

- 战斗、采集、照明与探索协助。
- 支援装备、工作装备、升级与游戏内指南。
- 独立背包、配装切换、箱子授权与物资补充。
- 名字、衣服配色、染料、台词与表情。
- 单人、多人、简体中文与英文；可选魔法存储、更好的体验、灾厄联动。

First release of Palico Companion.

- Combat, gathering, lighting and exploration assistance.
- Support and work equipment, upgrades and an in-game guide.
- A separate backpack, saved loadouts, chest permissions and restocking.
- Names, outfit colors, dyes, dialogue and emotes.
- Single-player, multiplayer, English and Simplified Chinese; optional Magic Storage, Quality of Terraria and Calamity integrations.

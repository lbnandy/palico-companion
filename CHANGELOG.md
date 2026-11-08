# Changelog

## 1.2.2 — 2026-11-09

### 简体中文

- 两种属性模式均可共享常规药水及利器站、弹药箱、水晶球、施法桌、战争桌的增益；修正继承模式中部分属性重复计算的问题。
- 新增减伤继承比例设置，默认 100%，设为 0 可关闭常规减伤继承。
- 修复共享狱火药水时伤害触发过于频繁的问题，并补上呆猫周围的原版火圈显示。
- 补齐共享药水的部分受击效果，以及瑟银灵感、耗竭和药水冷却的更新处理。
- 呆猫使用瑟银吟游诗人武器时，面板改为显示自身灵感；调整属性面板的信息和间距，使其与呆猫面板等高。
- 修复部分模组组合下，异常弹幕数据导致呆猫召唤失败、反复闪光传送的问题，并加固相关钓鱼和弹幕处理。
- 修正部分法狗装备效果可能触发的异常玩家同步，补齐中英文设置及指南说明。

### English

- Both attribute modes can now share ordinary potion buffs and buffs from Sharpening Stations, Ammo Boxes, Crystal Balls, Bewitching Tables and War Tables. Fixed some stat bonuses being applied twice in inheritance mode.
- Added a damage-reduction inheritance setting, defaulting to 100%; set it to 0 to disable ordinary damage-reduction inheritance.
- Fixed shared Inferno Potions dealing damage too frequently and added the native fire-ring visual around the Palico.
- Added missing on-hit effects from shared potions and corrected Thorium inspiration, exhaustion and potion-cooldown updates.
- When using a Thorium bard weapon, the companion panels now show the Palico's own inspiration. Refined the stats layout to match the main panel's height.
- Fixed malformed projectile data in some mod combinations preventing companion summons and causing repeated teleport flashes, with related fishing and projectile handling fixes.
- Fixed unintended player synchronization from some Fargo equipment effects, and updated bilingual settings and guide text.

## 1.2.1 — 2026-11-08

### 简体中文

- 独立配装模式新增共享玩家药水增益的开关，默认开启；同步补齐部分永久资源与恢复升级。
- 能穿透地形的武器现在可以隔墙索敌并攻击，普通武器仍遵守原有阻挡规则。
- 修复部分模组组合下持火把、荧光棒及钓鱼相关的异常，改善联机持物显示。
- 修复岩浆附近可能反复传送的问题，改为利用飞行离开岩浆并寻找安全落点。
- 补齐阿瑞斯外骨骼的自动模块选择，修复联机显示、跟随位置及特斯拉连线同步。
- 调整无限大地等持握近战武器的攻击距离与动作、音效表现。
- 调整背负武器的大小限制和部分携带姿势，减少细长武器被过度缩小的情况。
- 修复部分灾厄及法狗复合饰品效果导致的绘制崩溃，并补齐相关联机效果与持续伤害处理。

### English

- Added a default-on option to share the owner's potion buffs in independent equipment mode, with additional permanent resource and regeneration upgrades.
- Weapons that can pass through terrain can now acquire and attack targets through walls; ordinary weapons retain their obstruction checks.
- Fixed torch, glowstick and fishing issues with some mod combinations, and improved multiplayer held-item presentation.
- Fixed repeated teleporting near lava by using return flight to escape lava and find a safe landing position.
- Added automatic module selection for Ares' Exoskeleton and fixed multiplayer visibility, follow positioning and Tesla arc synchronization.
- Adjusted attack reach, motion and sound for held melee weapons including Earth.
- Adjusted carried-weapon size limits and selected carrying poses to reduce excessive shrinking of thin weapons.
- Fixed rendering crashes from some Calamity and Fargo composite accessory effects, along with related multiplayer effects and damage-over-time handling.

## 1.2 — 2026-11-08

### 简体中文

- 新增独立配装模式，可为呆猫装备护甲和饰品；也可继续选择继承玩家属性，切换模式时保留装备。
- 新增首次见面的属性模式选择、属性面板和套装奖励提示，调整相关设置的分类与显示。
- 接入护甲、套装及饰品的多种被动效果，补充灾厄、瑟银相关适配。
- 补齐法狗装备的被动效果、计时和资源恢复，修正多人服务器上的效果执行中断；修复装备致死保命、生命／魔力基础值及切换模式清空保命冷却的问题。
- 部分支援装备升级后会缩短冷却时间。
- 新增传送音效独立开关，并调整反复传送相关处理。
- 呆猫拾取物品时也会遵守主人的负重石限制。
- 修复与 MEAC 同开时，永夜刃等武器可能朝向错误，以及挥剑中断后呆猫无法继续攻击或召唤的问题。
- 修复 Koishi 原版武器重做中部分枪械打空弹匣后不换弹的问题，并修正联机换弹状态同步。
- 修复同时装备召唤武器时，反复尝试召唤可能打断弑君等武器预热的问题。
- 修复与灾厄、法狗同开时，清理呆猫弹幕可能报错、导致突变体转阶段中断的问题。
- 修复呆猫接触终灾黑洞后反复触发玩家死亡提示、却不正常倒地的问题；保留黑洞原本的持续伤害和呆猫复活保护。
- 更新呆猫指南及中英文提示，补充属性模式、装备与收藏共用等说明。

### English

- Added independent equipment mode with armor and accessory slots, alongside player-stat inheritance. Equipment is retained when switching modes.
- Added an attribute-mode choice on first meeting, a stats panel and armor-set bonus tooltips; reorganized related settings and conditional visibility.
- Added support for various passive armor, set and accessory effects, including additional Calamity and Thorium integrations.
- Added missing Fargo equipment passives, timers and resource recovery, and fixed interrupted effect updates on dedicated servers. Fixed equipment death prevention, permanent life/mana baselines and revival cooldowns being cleared when switching modes.
- Upgrading selected support gadgets now reduces their cooldowns.
- Added a separate teleport-sound toggle and adjusted repeated-teleport handling.
- Companion item pickups now respect the owner's Encumbering Stone.
- Fixed incorrect aiming with MEAC weapons such as Night's Edge, and companions becoming unable to attack or summon after an interrupted melee attack.
- Fixed some guns from Koishi's vanilla weapon rework failing to reload after emptying their magazines, and corrected multiplayer reload-state synchronization.
- Fixed repeated summon attempts interrupting weapon windup, including Kingsbane, when a summon weapon is also equipped.
- Fixed errors during companion projectile cleanup with Calamity and Fargo's Souls that could interrupt the Mutant's phase transition.
- Fixed repeated player-death notifications without proper companion knockdown when the Palico touches Supreme Calamitas's maelstrom. Native continuous damage and companion respawn protection are preserved.
- Updated the Palico Guide and bilingual tooltips for attribute modes, equipment and shared favorites.

## 1.1.7 — 2026-11-07

### 简体中文

- 新增战斗属性继承比例（0%～100%），按武器职业继承主人的伤害、暴击、攻速、击退、穿甲等数值加成；新配置默认 100%，旧配置维持原有行为，可自行开启。护甲、饰品的特殊触发效果不随之复制。
- 瑟银治疗武器现在可以按各自原本的规则治疗呆猫，包括生命涌动法杖等追踪治疗；修正自疗、多人治疗与目标收回后的处理。
- 呆猫复活后的无敌时间调整为 3 秒，减少刚起身就再次被击倒的情况。
- 修复虚空召唤的狱烬聚能枪无法正常攻击，以及灾熵“切换装备时清空潜行条”可能阻止呆猫使用武器的问题。
- 改进 Koishi 原版武器重做的蓄力弓、Homura 的换弹与后坐力，以及空想归途部分弓枪的瞄准和持握显示。
- 修复嗡鸣绞轮等持续使用武器在联机中的输入同步与显示问题；补充 MEAC 弹幕额外数据的联机兼容处理。
- 调整灾厄螺丝刀等部分近战武器的接敌距离，减少停在实际命中范围外的问题。

### English

- Added a combat-stat inheritance slider (0–100%). Weapons inherit a proportion of the owner's damage-class bonuses, including damage, critical chance, attack speed, knockback and armor penetration. New configurations default to 100%; existing configurations retain their previous behavior and can opt in. Armor and accessory trigger effects are not copied.
- Thorium healing weapons can now heal the Palico according to their native rules, including Life Surge Staff homing heals. Improved self-healing, multiplayer healing and handling of recalled patients.
- Increased post-revival invulnerability to 3 seconds to reduce immediate knockdowns.
- Fixed the Call of Void energy gun's missing attacks and Calamity Entropy's equipment-change stealth setting potentially preventing Palico weapon use.
- Improved charged bows from Koishi's vanilla weapon rework, Homura magazine and recoil handling, and aiming and held-weapon visuals for supported Lostbelt Journey bows and guns.
- Fixed multiplayer input and visual synchronization for sustained-use weapons such as Buzzkill, and added compatibility for MEAC projectile extra-data synchronization.
- Adjusted engagement distances for several Calamity melee weapons, including the Wulfrum Screwdriver, to avoid stopping outside their effective hit range.

## 1.1.6 — 2026-11-04

### 简体中文

- 收藏主武器、召唤武器或支援装备后，切换配装时可共用同一件装备；目标栏位已有装备时仍使用该配装自己的装备。取消收藏后留在当前配装。
- 改进铁制飞斧等部分灾厄投掷武器及原版飞刀、手里剑、普通箭矢的抛物线瞄准，减少直线瞄准导致攻击提前落地的问题。
- 修复手柄操作面板按钮后，焦点可能跳回左上角的问题。
- 修正配装按钮悬停描边底部缺失和多余细线，并保留收藏装备栏位的配装颜色。

### English

- Favorited main weapons, summon weapons and support equipment can now be shared between loadouts when the matching destination slot is empty. Occupied slots retain their own equipment; unfavoriting leaves the item in the current loadout.
- Improved ballistic aiming for supported Calamity throwing weapons, including the Iron Francisca, and vanilla throwing knives, shurikens and basic arrows, reducing shots that fall short of their targets.
- Fixed controller focus jumping back to the top-left button after panel actions.
- Fixed missing bottom outlines and stray lines on hovered loadout buttons, and preserved loadout slot colors for favorited equipment.

## 1.1.5 — 2026-11-04

### 简体中文

- 新增背负武器显示，默认开启，可在外观设置中关闭。未使用的主武器按类型背在身后或挂在腰间，随动作移动，飞行时也保留；消耗类投掷武器有剩余时显示一件。
- 修复虚空召唤的刻尘者、奈萨里奥等武器只朝固定方向平射的问题。
- 修复灾厄 T 粒子炮等部分武器蓄满后不释放的问题，沿用武器自身的蓄力、射击和冷却过程。
- 修正部分武器弹幕出射时可能立即撞地的问题。

### English

- Added carried weapon visuals, enabled by default and configurable under Appearance. Unused main weapons sit on the back or waist by type, follow the Palico's poses and remain visible during flight. Consumable throwing weapons display one item while the stack remains.
- Fixed Call of Void weapons such as Dust Carver and Neltharion firing only in a fixed horizontal direction.
- Fixed some Calamity weapons, including the Tau Cannon, not releasing after a full charge. Their native charging, firing and cooldown behavior is retained.
- Corrected some weapon projectiles colliding with the ground immediately upon launch.

## 1.1.4 — 2026-11-03

### 简体中文

- 新增回血继承比例设置，默认继承主人通用附加回血的 50%，可调为 0～100%；受伤恢复计时仍独立计算。
- 新增瑟银 Boss、探索、治疗和演奏情景台词，中英文各 56 句。
- 改进蓄力、松手释放与持续使用武器的处理，支持带电爆破炮的完整充能和持续光束，并补充部分灾厄武器适配。
- 修复与 Amulet of Many Minions 同时启用时，召唤武器可能导致呆猫消失或命中时报错的问题。
- 修复开启 PvP 时呆猫的武器和弹幕可能伤到玩家的问题。
- 回收的同种消耗性投掷武器优先补回仍有余量的武器槽。
- 装备槽不再提供收藏操作并保持原有颜色；背包收藏仍可正常使用，一键取回时会保留。
- 调整部分近战武器的接敌距离，减少有一定攻击距离却仍然贴脸的情况。
- 修复 Terraria Overhaul 的瞄准与专用服务器输入兼容问题，并去掉呆猫触发的脚步、起跳和落地音效。
- 同时提供 tModLoader 2026.8.3.0 与 2026.9.3.1 构建。

### English

- Added an inherited health regeneration setting: 50% of the owner's standard bonus regeneration by default, adjustable from 0% to 100%. The Palico keeps its own recovery timer after damage.
- Added 56 lines per language for Thorium bosses, exploration, healing and music.
- Improved charging, release-to-fire and sustained-use weapons, including the Charged Blaster Cannon's full charge sequence and beam, plus support for additional Calamity weapons.
- Fixed missing companions and minion-hit errors when using summon weapons alongside Amulet of Many Minions.
- Fixed Palico weapons and projectiles potentially damaging players in PvP.
- Recovered matching consumable throwing weapons now refill a nonempty equipped stack first.
- Equipment slots no longer offer favorite toggling and retain their normal colors. Backpack favorites remain usable and protected from bulk withdrawal.
- Refined engagement distance for several melee weapon behaviors, reducing unnecessary contact with enemies.
- Fixed Terraria Overhaul aiming and dedicated-server input compatibility, and suppressed footsteps, takeoff and landing sounds triggered by the Palico.
- Includes builds for tModLoader 2026.8.3.0 and 2026.9.3.1.

## 1.1.3 — 2026-11-03

### 简体中文

- 修复部分灾厄盗贼武器蓄力后不出手的问题。
- 修正部分近身武器的接敌距离，以及鱼骨回旋镖投出时可能立即撞地的问题。
- 修复灾厄与瑟银同时启用时，武器攻击距离适配互相覆盖的问题。

### English

- Fixed some Calamity rogue weapons failing to release after charging.
- Corrected engagement distance for a close-range Calamity weapon and prevented Fishbone Boomerang throws from starting inside the ground.
- Fixed weapon reach integrations overriding each other when Calamity and Thorium are both enabled.

## 1.1.2 — 2026-11-03

### 简体中文

- 新增独立召唤武器栏，可放入随从或哨兵武器。补召或部署后切回主武器，支持与鞭子及其他武器配合使用。
- 配装同步保存召唤武器；旧存档中的召唤武器会自动移入新槽位。
- 支持灾厄的消耗型盗贼武器，修正部分弹幕出生位置过低、立即撞地消失的问题。
- 修复部分灾厄武器在联机时弹幕尚未初始化就参与绘制造成的异常。
- 修复新版 tModLoader 中打开呆猫设置可能崩溃的问题。

### English

- Added a separate slot for minion or sentry weapons. Your Palico returns to its main weapon after summoning or deploying, allowing summons to accompany whips and other weapons.
- Loadouts now include the summon weapon. Summon weapons in older saves automatically move to the new slot.
- Added support for consumable Calamity rogue weapons and corrected low projectile origins that could cause immediate ground collisions.
- Fixed premature rendering of some Calamity weapon projectiles in multiplayer before their native initialization.
- Fixed a crash when opening Palico settings on newer tModLoader builds.

## 1.1.1 — 2026-11-02

### 简体中文

- 新增可自定义的收回／叫出呆猫快捷键。
- 新增主动进攻／协同进攻／不攻击三种模式，点击剑形按钮循环切换。协同模式在主人命中或指定目标、主人或呆猫受击后参战；不攻击模式仍保留工作、拾取和治疗。
- 接入原版手柄界面导航，支持背包、配装、指南、外观与改名操作。
- 修正呆猫背包中的武器提示框错误使用玩家属性的问题。
- 改进旅人归途专用服务器兼容，去掉固定版本号限制。
- 新增暗影奥秘（SOTS）刀光与虚空资源适配。
- 修复雀杖召唤物在呆猫头上停留时反复消失、重新召唤的问题。
- 调整特大桶爆弹炸飞落地后的短暂停顿与起身衔接。

### English

- Added an optional keybind to stow or call out your Palico.
- Added proactive, assist and passive combat modes, cycled by clicking the sword button. Assist mode engages enemies the owner hits or marks, or retaliates when the owner or Palico is hit. Passive mode retains work, pickup and healing.
- Added native gamepad navigation for inventory, loadouts, the guide, appearance and naming.
- Fixed weapon tooltips in the Palico inventory incorrectly using the owner's stats.
- Improved Homeward Journey dedicated-server compatibility without a fixed-version restriction.
- Added Secrets of the Shadows (SOTS) trail and void-resource integration.
- Fixed Finch Staff minions repeatedly disappearing and being resummoned while perched on the Palico.
- Refined the brief landing pause and get-up sequence after a Giga Barrel Bomb blast.

## 1.1 — 2026-11-01

### 简体中文

- 接入画师新版角色、推车与举桶动画；UI 头像和模组图标改用新版素材。
- 新增投喂、特大桶爆弹、猫车演出与呆猫风筝。
- 新增瑟银乐器、治疗与投掷武器联动。
- 新增状态卡拖动、可自定义显隐快捷键、染色范围、可调索敌距离和默认关闭的 Boss 生命补偿。
- 调整收藏物品的使用与批量取回规则，修正钓鱼奖励、命中判定和拾取拉扯。
- 修复已复现的部分弹幕、枪械、界面和联机兼容问题；更新装备名称与部分冷却、伤害。

### English

- Integrated the artist's new character, cart and barrel animations; replaced UI portraits and mod icons with the new artwork.
- Added feeding, the Giga Barrel Bomb, cart death scenes and Palico kite flying.
- Added Thorium instrument, healing and throwing-weapon integration.
- Added draggable HUD placement, an optional HUD keybind, dye scope, configurable targeting range and an opt-in boss health bonus.
- Revised favorite-item use and bulk retrieval; corrected fishing rewards, hit rules and competing item pickup.
- Fixed reproduced projectile, gun, UI and multiplayer compatibility cases; revised equipment names and selected cooldowns and damage.

## 1.0.6 — 2026-10-24

### 简体中文

- 更新角色与头像美术及界面头像尺寸，玩法沿用 1.0.5。

### English

- Updated artwork and portrait sizing; gameplay remained based on 1.0.5.

## 1.0.5 — 2026-10-24

### 简体中文

- 新增暂停环境改动按钮及可自定义快捷键，建房时可暂停呆猫的自动挖掘、采集和放置行为。
- 调整挖掘协助的触发判断，开始建造时停止旧的挖掘任务，减少修补房屋时的误拆。
- 新增工作速度、钓鱼速度的独立设置，默认均为 100%，不随强度预设改变。
- 将设置分类统一为「呆猫能力」「战斗能力」，更新中英文说明。

### English

- Added an environment-work toggle and an optional keybind to pause automatic digging, planting and block placement while building.
- Refined digging-assistance detection. Starting construction now cancels stale digging tasks, reducing unwanted excavation during repairs.
- Added separate work-speed and fishing-speed settings, both defaulting to 100% and independent of strength presets.
- Renamed the settings categories to Companion abilities and Combat abilities, with updated English and Simplified Chinese descriptions.

## 1.0.4 — 2026-10-24

### 简体中文

- 修复与积分商店同时启用时，购买商品可能扣除积分却无法获得物品的问题。

### English

- Fixed purchases deducting points without granting items when used with Point Shop.

## 1.0.3 — 2026-10-24

### 简体中文

- 修复部分情况下界面绘制异常导致鼠标指针消失、状态卡无法恢复显示的问题。
- 修正进入世界和切换窗口时的指令快捷键状态处理。

### English

- Fixed interface rendering errors that could hide the cursor and prevent the status card from recovering.
- Fixed command hotkey state handling when entering a world or switching windows.

## 1.0.2 — 2026-10-23

### 简体中文

- 新增辅助、标准和强力三档预设，也可分别调整伤害、生命与防御继承、战斗移速、跟随与工作移速、主动闪避。
- 默认命中伤害和战斗移速调整为原来的 75%，主动闪避改为标准；生命与防御仍默认继承玩家的 75%。跟随与工作移速保持不变。
- 联机时共用服务器的强度设置，房主可在游戏内修改。调整生命上限保留当前血量比例，不会复活倒地的呆猫。
- 统一设置下拉框和预设按钮的文字样式，并补充中英文说明。

### English

- Added Support, Standard and Strong presets, with individual controls for damage, inherited health and defense, combat movement, following and work movement, and active evasion.
- Default hit damage and combat movement are now 75% of their previous values, with Standard evasion. Health and defense still default to 75% of the player's values; following and work movement are unchanged.
- Multiplayer uses shared server balance settings, editable in-game by the host. Changing maximum health preserves the current health percentage and does not revive a downed Palico.
- Unified dropdown and preset-button typography, with updated English and Simplified Chinese descriptions.

## 1.0.1 — 2026-10-23

### 简体中文

- 补充装备使用与换装台词，减少连续触发的重复发言。
- 修正采矿拾取、收竿后的转向，以及拾取和存放物品时持续闭眼的问题。
- 修正天顶剑剑光的位置，并改善敌人替换后的选敌与攻击距离判断。
- 修正灾厄诘责、发条弓、麦格纳炮蓄力后不释放的问题。

### English

- Added equipment reactions and reduced chatter during repeated actions.
- Fixed pickup while mining, turning after reeling in a catch, and prolonged eye closing after collecting or storing items.
- Corrected Zenith trail positioning and target tracking when an NPC slot is reused.
- Fixed charged attacks with Calamity's Condemnation, Clockwork Bow and Magna Cannon.

## 1.0.0 — 2026-10-22

### 简体中文

- 首次发布：呆猫伙伴。
- 战斗、采集、照明与探索协助。
- 支援装备、工作装备、升级与游戏内指南。
- 独立背包、配装切换、箱子授权与物资补充。
- 名字、衣服配色、染料、台词与表情。
- 单人、多人、简体中文与英文；可选魔法存储、更好的体验、灾厄联动。

### English

- First release of Palico Companion.
- Combat, gathering, lighting and exploration assistance.
- Support and work equipment, upgrades and an in-game guide.
- A separate backpack, saved loadouts, chest permissions and restocking.
- Names, outfit colors, dyes, dialogue and emotes.
- Single-player, multiplayer, English and Simplified Chinese; optional Magic Storage, Quality of Terraria and Calamity integrations.

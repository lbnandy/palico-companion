# 兼容说明 / Compatibility

[English](#english)

呆猫使用原版物品与弹幕流程，但独立维护自己的背包、魔力、仆从和哨兵。在继承玩家属性模式下，它的魔力、仆从与哨兵上限跟随主人的对应上限；新配置默认完整继承主人的职业数值加成，旧配置保留原来的开关选择。

- 独立配装模式使用呆猫自己的护甲和饰品计算防御、战斗加成、装备回血及召唤上限，不再叠加主人的装备加成。基础生命和魔力跟随主人的永久提升，生命倍率仍然生效。支持多种原版被动装备效果，并补充灾厄、瑟银和法狗的被动效果适配；需要玩家按键发动的主动技能不自动执行，也不保证所有模组装备效果均可用。切回继承模式后，护甲和饰品保留但不生效。

- 继承玩家属性模式下，「呆猫能力 → 战斗属性继承比例」范围 0%～100%，新配置默认 100%，0% 关闭继承，由房主统一设置，切换能力预设不会改变它。按设置比例、武器所属职业继承主人通过原版职业系统提供的伤害、暴击、攻速、击退和穿甲，以及支持的弹药、魔力和职业资源加成。比例只缩放额外数值，例如主人 +60% 增伤在 50% 继承时提供 +30%；原版省弹、箭术、近战大小和魔力恢复等开关效果在比例大于 0 时保留完整效果，不拆分它们的原版机制。混合职业沿用其原本的继承规则，不取主人最高的职业加成。生命、防御和回血保留各自的比例；伤害倍率仍作用于呆猫的最终命中伤害。灾厄的数值型暴击伤害、盗贼投速和瑟银的治疗速度、灵感与投掷耐力加成通过各自的联动接口处理。不会复制护甲、饰品的触发效果或共享当前魔力、灵感、怒气、肾上腺素、弹匣和蓄力。模组若在独立钩子中实现特殊装备效果，需要另外适配。

- 呆猫本体使用原版 `ProjectileID.Sets.ForcePlateDetection` 接口关闭接触式机关检测，不会因走过压力板、青绿压力垫或地雷而触发它们。武器弹幕仍保留各自的原版机关交互，已启动的陷阱也仍能伤害呆猫。这项保护不涉及挖掘造成的地形变化，例如挖掉巨石的支撑块。

- 默认自动索敌距离为 75 格，已交战或用原版仆从指令标记的目标可追踪到 100 格；目标需同时处于主人和呆猫的对应范围内，并满足视线条件。这些是世界距离，不随分辨率或缩放变化。「呆猫能力 → 索敌范围」可在 50%～150% 间同步缩放这些距离和普通远程开火限制，默认 100%，独立于强度预设；由房主通过原版服务器配置同步给所有客户端。默认普通远程武器允许在 100 格内开火，但弹幕仍保留原有的速度、轨迹和寿命；近战、悠悠球、链球等有限射程武器仍会接近到自己的有效距离。
- 魔法武器正常消耗魔力。付不起下一次施法、且没有可用蓝药时，呆猫会等待自然回满后再攻击；期间喝到蓝药可以提前恢复。普通停手不强制等满蓝。
- 暗影奥秘的刀光保留原有的拖尾更新。虚空武器使用呆猫独立的虚空值，上限与永久提升跟随主人，消耗和恢复沿用暗影奥秘的规则；不足时停手恢复，不扣主人的虚空值。
- 手柄可在原版控制设置中绑定「呆猫面板」。面板、装备指南和外观页支持原版方向导航；肩键可在呆猫面板与玩家背包间切换。取放、拆分、收藏和取回使用相同的背包规则，改名使用原版虚拟键盘。
- 带电爆破炮支持完整充能与持续光束。灾厄的 T 粒子炮等松手开火武器会在自身蓄力完成后释放，并等待原有射击与冷却结束再开始下一次蓄力；自动发射和持续引导武器保留各自的使用方式。消耗、弹幕与恢复过程沿用武器原有逻辑；部分模组的特殊蓄力机制仍需单独适配。
- 呆猫自己的武器与弹幕不会对玩家造成 PvP 伤害。回收的同种消耗性投掷武器优先补入尚有余量的武器槽；主武器、召唤武器和支援装备收藏后，可在其他配装的同一栏位为空时共用；取消收藏后留在当前配装。背包收藏仍会保留在一键取回之外。
- Amulet of Many Minions：修复专用服务器上的问答界面查询异常，并补齐呆猫使用召唤物时所需的寻路状态，避免呆猫消失或召唤物命中时报错。
- Terraria Overhaul：呆猫不会触发它的脚步、起跳或落地音效；修正呆猫武器瞄准与专用服务器翻滚输入的兼容问题，保留玩家自己的移动音效和操作。
- 虚空召唤（Call of Void）：将呆猫的瞄准位置传入模组自身的瞄准状态，修正刻尘者、奈萨里奥等武器只朝固定方向平射的问题；保留武器原有弹幕和散布，不改变玩家操作。
- 瑟银的乐器使用呆猫自己的灵感，上限跟随主人的永久提升，耗尽后恢复再演奏；附近玩家可获得乐器原有的增益。鼓槌会先放鼓再敲击。
- 瑟银的治疗工具会瞄准附近受伤的主人或友军；恢复术用于呆猫自身。保留原有的治疗弹幕、范围与接触条件，圣光法杖散出的治疗光球仍需玩家接触。治疗加成按所选属性模式计算，魔力与生命消耗由呆猫承担。
- 瑟银的投掷武器使用呆猫装备栏中的实际数量；非消耗型投掷武器保留疲劳机制，过度疲劳时休息恢复。这些联动不需要另装附属模组，也不改变玩家自己的武器操作。
- 旅人归途：在专用服务器上补齐缺失的旅人按键状态，并对匹配的天空特效查询处理未注册的情况，避免人物更新中断后呆猫无法出现。两项处理独立判断，不依赖固定版本号，也不改变客户端的按键或天空特效；不匹配的内部代码保持原样。
- 有独立右键模式、特殊鼠标操作或自定义玩家状态的武器，不保证每种操作都能由呆猫执行。请按具体武器反馈问题。
- 联机时，每名玩家有独立的呆猫和装备；服务端与所有玩家需要安装本模组。联动模组按它们各自的联机要求安装。

当前验证基于 tModLoader 1.4.4。其他模组更新后可能改变引用或行为，尤其是涉及内部接口的联动。遇到问题请提供完整版本号和最小复现步骤。

## English

The Palico uses native item and projectile behavior while keeping its own inventory, mana, minions and sentries. In player-stat inheritance mode, its mana and summon capacities follow its owner's corresponding limits; new configurations inherit class-stat bonuses by default, while existing configurations retain their previous choice.

- Independent equipment mode uses the Palico’s own armor and accessories for defense, combat bonuses, equipment-based regeneration and summon limits, without stacking the owner’s equipment bonuses. Base life and mana follow the owner’s permanent upgrades, and the health multiplier still applies. Various native equipment passives are supported, with additional passive-effect integrations for Calamity, Thorium and Fargo’s Souls. Player-triggered active skills are not automated, and not every modded equipment effect is guaranteed to work. Armor and accessories are retained but inactive when switching back to inheritance mode.

- In player-stat inheritance mode, Palico Abilities → Combat stats inherited ranges from 0% to 100%, defaults to 100% for new configurations (0% disables it), and is controlled by the host and independent of ability presets. Weapons inherit the selected proportion of the owner's native damage-class modifiers for damage, critical chance, attack speed, knockback and armor penetration, plus supported ammunition, mana and class-resource bonuses. Only bonus values are scaled: +60% damage at 50% inheritance becomes +30%. Native on/off ammo, Archery, melee-size and mana-regeneration effects retain their full behavior above 0% rather than rewriting those mechanics. Hybrid classes keep their own inheritance rules rather than taking the owner's highest class bonus. Health, defense and regeneration retain their separate ratios; damage scaling still applies to the Palico's final hit damage. The existing integration bridges handle Calamity's numerical critical-damage and rogue-velocity bonuses and Thorium's healing speed, inspiration and throwing-exhaustion bonuses. Armor/accessory triggers and current mana, inspiration, rage, adrenaline, magazines and charge state are not copied. Special equipment effects implemented through separate mod hooks may need individual integration.

- The Palico's body opts out of native contact-switch detection through `ProjectileID.Sets.ForcePlateDetection`, so crossing pressure plates, teal pressure pads or land mines does not activate them. Weapon projectiles retain their own native switch interactions, and active traps can still damage the Palico. This protection does not cover terrain changes caused by mining, such as removing a boulder's support blocks.

- By default, automatic target acquisition reaches 75 tiles; an engaged target or one marked with the native minion command can be tracked up to 100 tiles. Targets must be within the respective range of both the owner and Palico and satisfy line-of-sight checks. These are world distances, independent of resolution or zoom. Palico Abilities → Targeting range scales these distances and the ordinary ranged firing limit from 50% to 150% (default 100%), independently of strength presets. The host controls it through native server-config synchronization. By default, ordinary ranged weapons can fire within 100 tiles, while projectiles keep their native speed, trajectory and lifetime. Melee weapons, yoyos, flails and other weapons with limited reach still approach their own effective distance.
- Magic weapons consume mana normally. If the Palico cannot afford the next cast and has no usable mana potion, it waits for a full natural refill. Drinking a potion can end that wait early. Ordinary pauses in combat do not force a full refill.
- Secrets Of The Shadows blade trails retain their native updates. Void weapons use a separate Palico meter, with capacity and permanent upgrades following the owner. Native costs and regeneration remain in effect; the Palico pauses when depleted without spending the owner's void.
- Bind the Palico panel in the native controller settings. The panel, equipment guide and appearance editor support directional navigation; shoulder buttons switch between the Palico panel and player inventory. Pickup, splitting, favorites and withdrawal use the same inventory rules. Naming uses the native virtual keyboard.
- The Charged Blaster Cannon supports its full charge sequence and sustained beam. Calamity release-to-fire weapons such as the Tau Cannon are released when their own charge completes, then finish their native firing and cooldown before charging again. Automatic-fire and sustained-use weapons retain their own input behavior, costs, projectiles and recovery. Some mod-specific charging mechanics still need individual integration.
- The Palico's weapons and projectiles cannot deal PvP damage to players. Recovered matching consumable throwing weapons refill a nonempty equipped stack first. Favorited main weapons, summon weapons and support equipment are shared with loadouts whose matching slot is empty. Unfavoriting leaves the item in the current loadout. Backpack favorites remain protected from bulk withdrawal.
- Amulet of Many Minions: guards a dedicated-server quiz UI query and initializes the pathfinding state needed by the Palico's summons, preventing missing companions or errors on minion hits.
- Terraria Overhaul: the Palico does not trigger its footstep, takeoff or landing sounds. Weapon aiming and dedicated-server dodgeroll input are handled without changing human movement sounds or controls.
- Call of Void: feeds the Palico's aim into the mod's own aiming state, correcting fixed horizontal fire with weapons such as Dust Carver and Neltharion. Native projectiles and spread are preserved, and human controls are unchanged.
- Thorium instruments use the Palico's own inspiration, with capacity following the owner's permanent upgrades. It recovers before playing again after running dry, and nearby players receive the instrument's native empowerments. The Drum Mallet places a drum before striking it.
- Thorium healing tools aim at injured owners or nearby allies; Renew heals the Palico itself. Native projectiles, ranges and contact requirements remain in effect, including collecting the Holy Staff's healing orbs. Healing bonuses follow the selected attribute mode; the Palico pays its own mana and life costs.
- Consumable Thorium throwing weapons use the actual equipped stack. Non-consumable throws retain exhaustion and rest to recover from overexertion. These integrations need no separate addon and do not change the human player's weapon controls.
- Homeward Journey: on dedicated servers, fills missing Homeward key states and guards matching sky queries against an unregistered effect. These fixes prevent interrupted player updates from blocking Palico spawning. Each is checked independently, without a fixed version requirement; client input and sky effects are unchanged, and unrecognized internal code is left intact.
- Weapons with alternate attacks, special cursor controls or custom player state may need individual support. Not every manual input is available to the companion.
- Each player has a separate Palico and equipment in multiplayer. Install this mod on the server and all clients; follow each optional integration's own multiplayer requirements.

Verification currently targets tModLoader 1.4.4. Updates to other mods can change their APIs or behavior. Please include exact versions and a small reproducible example when reporting a compatibility problem.

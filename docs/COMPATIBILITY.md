# 兼容说明 / Compatibility

[English](#english)

呆猫使用原版物品与弹幕流程，但独立维护自己的背包、魔力、仆从和哨兵。它的魔力、仆从与哨兵上限跟随主人的对应上限；这不代表它拥有玩家的整套职业装备加成。

- 呆猫本体使用原版 `ProjectileID.Sets.ForcePlateDetection` 接口关闭接触式机关检测，不会因走过压力板、青绿压力垫或地雷而触发它们。武器弹幕仍保留各自的原版机关交互，已启动的陷阱也仍能伤害呆猫。这项保护不涉及挖掘造成的地形变化，例如挖掉巨石的支撑块。

- 默认自动索敌距离为 75 格，已交战或用原版仆从指令标记的目标可追踪到 100 格；目标需同时处于主人和呆猫的对应范围内，并满足视线条件。这些是世界距离，不随分辨率或缩放变化。「呆猫能力 → 索敌范围」可在 50%～150% 间同步缩放这些距离和普通远程开火限制，默认 100%，独立于强度预设；由房主通过原版服务器配置同步给所有客户端。默认普通远程武器允许在 100 格内开火，但弹幕仍保留原有的速度、轨迹和寿命；近战、悠悠球、链球等有限射程武器仍会接近到自己的有效距离。
- 魔法武器正常消耗魔力。付不起下一次施法、且没有可用蓝药时，呆猫会等待自然回满后再攻击；期间喝到蓝药可以提前恢复。普通停手不强制等满蓝。
- 带电爆破炮保留小、大能量球阶段，暂不使用最终的持续光束。玩家自己的武器行为不受影响。
- 瑟银的乐器使用呆猫自己的灵感，上限跟随主人的永久提升，耗尽后恢复再演奏；附近玩家可获得乐器原有的增益。鼓槌会先放鼓再敲击。
- 瑟银的治疗工具会瞄准附近受伤的主人或友军；恢复术用于呆猫自身。保留原有的治疗弹幕、范围与接触条件，圣光法杖散出的治疗光球仍需玩家接触。治疗加成跟随主人，魔力与生命消耗由呆猫承担。
- 瑟银的投掷武器使用呆猫装备栏中的实际数量；非消耗型投掷武器保留疲劳机制，过度疲劳时休息恢复。这些联动不需要另装附属模组，也不改变玩家自己的武器操作。
- 旅人归途 0.8.71.17：兼容其服务端按键和天空特效初始化问题，避免人物更新中断后呆猫无法出现。处理仅对该版本的专用服务器生效，保留客户端原有功能。
- 有独立右键模式、特殊鼠标操作或自定义玩家状态的武器，不保证每种操作都能由呆猫执行。请按具体武器反馈问题。
- 联机时，每名玩家有独立的呆猫和装备；服务端与所有玩家需要安装本模组。联动模组按它们各自的联机要求安装。

当前验证基于 tModLoader 1.4.4。其他模组更新后可能改变引用或行为，尤其是涉及内部接口的联动。遇到问题请提供完整版本号和最小复现步骤。

## English

The Palico uses native item and projectile behavior while keeping its own inventory, mana, minions and sentries. Its mana and summon capacities follow its owner's corresponding limits; it does not inherit the player's entire class loadout and bonuses.

- The Palico's body opts out of native contact-switch detection through `ProjectileID.Sets.ForcePlateDetection`, so crossing pressure plates, teal pressure pads or land mines does not activate them. Weapon projectiles retain their own native switch interactions, and active traps can still damage the Palico. This protection does not cover terrain changes caused by mining, such as removing a boulder's support blocks.

- By default, automatic target acquisition reaches 75 tiles; an engaged target or one marked with the native minion command can be tracked up to 100 tiles. Targets must be within the respective range of both the owner and Palico and satisfy line-of-sight checks. These are world distances, independent of resolution or zoom. Palico Abilities → Targeting range scales these distances and the ordinary ranged firing limit from 50% to 150% (default 100%), independently of strength presets. The host controls it through native server-config synchronization. By default, ordinary ranged weapons can fire within 100 tiles, while projectiles keep their native speed, trajectory and lifetime. Melee weapons, yoyos, flails and other weapons with limited reach still approach their own effective distance.
- Magic weapons consume mana normally. If the Palico cannot afford the next cast and has no usable mana potion, it waits for a full natural refill. Drinking a potion can end that wait early. Ordinary pauses in combat do not force a full refill.
- The Charged Blaster Cannon uses its small and large energy orbs, but currently stops before the sustained beam. The player's own weapon is unaffected.
- Thorium instruments use the Palico's own inspiration, with capacity following the owner's permanent upgrades. It recovers before playing again after running dry, and nearby players receive the instrument's native empowerments. The Drum Mallet places a drum before striking it.
- Thorium healing tools aim at injured owners or nearby allies; Renew heals the Palico itself. Native projectiles, ranges and contact requirements remain in effect, including collecting the Holy Staff's healing orbs. Healing bonus follows the owner; the Palico pays its own mana and life costs.
- Consumable Thorium throwing weapons use the actual equipped stack. Non-consumable throws retain exhaustion and rest to recover from overexertion. These integrations need no separate addon and do not change the human player's weapon controls.
- Homeward Journey 0.8.71.17: handles its headless input and sky initialization issues, which otherwise interrupt player updates and prevent Palicos from appearing. The compatibility fix applies only to that version on dedicated servers; client behavior is preserved.
- Weapons with alternate attacks, special cursor controls or custom player state may need individual support. Not every manual input is available to the companion.
- Each player has a separate Palico and equipment in multiplayer. Install this mod on the server and all clients; follow each optional integration's own multiplayer requirements.

Verification currently targets tModLoader 1.4.4. Updates to other mods can change their APIs or behavior. Please include exact versions and a small reproducible example when reporting a compatibility problem.

# 参与修改 / Contributing

欢迎提交问题、翻译修正和补丁。较大的玩法调整可以先开 Issue 讨论，避免做完以后才发现方向不一致。

1. 按[开发说明](docs/DEVELOPMENT.md)完成构建。
2. 一次修改围绕一个问题，尽量复用 tModLoader 与原版的物品、碰撞、合成和网络接口。
3. 玩家可见的文本放在 `Localization` 中；中英文本保持相同的格式参数，英文台词可以自然改写，不必直译。
4. 按改动范围验证，PR 写清楚原来的问题、现在的行为和检查结果。涉及联机时，请说明是否用加入服务器的客机验证。

不要提交本机路径、存档、日志或第三方程序集。保留已有存档迁移，不要仅为统一命名而修改内部 ID。贴图修改保持原生像素网格，布局见 [ASSETS.md](docs/ASSETS.md)。

## English

Bug reports, translation improvements and patches are welcome. Please open an issue before starting a large gameplay change so we can agree on the direction.

Follow the [development guide](docs/DEVELOPMENT.md#english), keep each change focused, and reuse native tModLoader/Terraria behavior where practical. Put player-facing text in `Localization` and preserve format arguments across languages. Dialogue can be adapted naturally instead of translated word for word.

In a pull request, describe the problem, the resulting behavior and what you checked. For multiplayer changes, say whether you tested as a joining client. Do not include local paths, saves, logs or third-party assemblies. Preserve save migrations and internal IDs; keep sprite edits on the native pixel grid.

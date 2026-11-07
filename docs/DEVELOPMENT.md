# 开发说明 / Development

[English](#english)

## 环境

- tModLoader **1.4.4**；当前版本使用 **2026.9.3.1 / Terraria 1.4.4.9** 构建与验证。正式发布跟随稳定版，旧分支不保证获得每次更新。
- .NET 8 SDK。`global.json` 允许使用已安装的 .NET 8 功能更新版本。
- 下载灾厄、魔法存储、更好的体验、瑟银、暗影奥秘（SOTS）、法狗之魂及其依赖。原生编译还会解析这些模组的弱引用，因此也需要 **Calamity Mod Music**。当前验证环境包含 **absoluteAquarian Utilities / SerousCommonLib**、**SilkyUI Framework**、**Subworld Library**、**Fargo’s Mutant Mod** 和 **Luminance**。

联动模组是编译时引用，游玩时仍然可选。它们的程序集不随本仓库分发。

## 游戏内构建

1. 在 tModLoader 的「创意工坊 → 开发模组」打开模组源码文件夹。
2. 将本仓库放在其中，文件夹必须叫 **`PalicoCompanion`**。这个内部名称用于资源路径和存档识别，不等于显示名。
3. 确认上面的引用模组已经下载，点击「构建并重新加载」。

如果使用 Git，在模组源码文件夹中运行以下命令，确保文件夹名称正确：

```sh
git clone https://github.com/lbnandy/palico-companion.git PalicoCompanion
```

只修改文案或贴图也需要重新构建。

## 命令行构建

`tools/build.ps1` 调用 tModLoader 自带编译器，自动从已安装的模组读取引用。路径由你提供，不依赖某个固定 Steam 库位置。

```powershell
./tools/build.ps1 -TmlInstallRoot "你的 tModLoader 安装目录"
```

默认写入 tModLoader 当前保存目录下的 `Mods/PalicoCompanion.tmod`。如需隔离构建，准备一个单独的保存目录，把所需 `.tmod` 引用放进它的 `Mods` 文件夹，再运行：

```powershell
./tools/build.ps1 -TmlInstallRoot "你的 tModLoader 安装目录" -SaveDirectory "你的构建保存目录"
```

脚本不启动游玩、不上传或发布。`-Dotnet` 可指定 .NET 可执行文件；Windows PowerShell 或 PowerShell 7 均可使用该脚本。Linux/macOS 也可在 tModLoader 安装目录直接调用原生构建命令：

```sh
dotnet tModLoader.dll -server -build /path/to/PalicoCompanion -nosteam
```

## IDE / dotnet build

游戏内编译器与 IDE 的引用解析方式不同。IDE 需要六个模组的 DLL：`CalamityMod.dll`、`MagicStorage.dll`、`ImproveGame.dll`、`ThoriumMod.dll`、`SOTS.dll`、`FargowiltasSouls.dll`。可从各模组源码构建取得，放在被 Git 忽略的 `references/` 中，或用 `ModReferencePath` 指向已有参考程序集目录。

```sh
dotnet build -c Release -p:TmlInstallRoot="/path/to/tModLoader" -p:ModReferencePath="/path/to/references"
```

如果项目位于原生 `ModSources/PalicoCompanion`，可使用 tModLoader 在上级生成的 `tModLoader.targets`，无需另设 `TmlInstallRoot`。不要提交这个机器相关的文件或第三方 DLL。

## 检查修改

游戏内检查按改动范围选择：

- UI 或文本：切换中英文，并检查不同 UI 缩放下的长文本。
- 武器：单人和客机都检查命中、持武器表现、消耗、缺蓝或缺弹后的行为。
- 背包或存档：检查鼠标正在使用的物品、换装、保存重进，确认物品既不丢失也不复制。
- 联动：至少检查启用与未启用对应模组两种情况。

## 发布与更新说明

- 发布版本号以本次确认的版本为准。整理改动时，对比上一次正式发布的版本，不以本地预览包的版本为基准。
- `CHANGELOG.md` 按版本倒序排列，每次使用 `## 版本号 — YYYY-MM-DD`，下面分 `### 简体中文` 和 `### English` 两段。同一条更新在两种语言中保持含义一致。
- 工坊的改动说明可分语言编辑。简体中文栏以“版本号 更新”开头，英语栏以“版本号 Update”开头，空一行后逐条列出。首次发布可写“版本号 首次发布”／“版本号 Initial Release”。不要把中英文一起填进英语栏。
- 每条写清玩家会看到的变化，优先列新增功能、体验调整，再列修复。一件事一条；短更新不强行分组，长更新才按内容分组。保留有意义的适用条件、默认值和限制，不列内部实现细节或验证过程，也不把部分适配写成全面兼容。
- `changelog.txt` 和上传时传入的改动说明必须包含版本标题。发布后在工坊编辑页补齐两个语言栏，重新打开确认各自保存，再核对实际展示。日期使用平台显示的发布时间，不在工坊正文重复。
- GitHub Release 标题使用版本号，正文使用与 `CHANGELOG.md` 对应的中英文分段。上传前核对版本、正文与构建包一致。
- 修改历史说明只统一格式或修正有依据的错误，保留当次的实际内容与适用范围，不把后续更新写回旧版本。仅整理说明时，不重传模组包、不改版本或历史标签。

## 代码位置

| 内容 | 入口 |
| --- | --- |
| 玩家数据、保存读取 | `CompanionPlayer.cs` |
| 呆猫行为与绘制 | `CompanionCat.cs`、`Systems/CompanionAnimation.cs` |
| 原生武器使用 | `Systems/CompanionNativeUse.cs`、`CompanionProjectileScope.cs` |
| 联机与物品确认 | `Systems/CompanionNetwork*.cs`、`CompanionSession.cs` |
| 面板、指南、设置 | `CompanionPanel.cs`、`Systems/TrainingPanel.cs`、`CompanionConfig.cs` |
| 台词、装备与指南文本 | `Localization/` |

动画布局与握持偏移见 [资源布局](ASSETS.md)。物品名称、内部模组名和已有存档键不要随意重命名；旧格式迁移仍有用途。

## English

Use tModLoader **1.4.4** and the **.NET 8 SDK**. The current release is built and verified with tModLoader **2026.9.3.1 / Terraria 1.4.4.9**. Releases follow the stable branch; older branches are not guaranteed to receive every update.

Download **Calamity**, **Magic Storage**, **Quality of Terraria**, **Thorium**, **Secrets of the Shadows (SOTS)**, **Fargo’s Souls** and their dependencies before building. tModLoader's compiler also resolves their weak references, including **Calamity Mod Music**. The verified setup includes **SerousCommonLib**, **SilkyUI Framework** **Subworld Library**, **Fargo’s Mutant Mod** and **Luminance**. These are build references; they remain optional for players using the compiled mod.

For an in-game build, open the source folder from **Workshop → Develop Mods**, put this repository in a folder named **PalicoCompanion**, then choose **Build + Reload**. Keep the internal folder name: resource paths and saved data use it.

When cloning, run `git clone https://github.com/lbnandy/palico-companion.git PalicoCompanion` from your ModSources folder. If you download a ZIP instead, rename the extracted folder to `PalicoCompanion`.

For a command-line build, use the commands above with your own paths. `tools/build.ps1` calls tModLoader's native compiler and reads installed mod references. By default, it writes `Mods/PalicoCompanion.tmod` in your tModLoader save directory. To keep builds separate, pass `-SaveDirectory` and place the required reference mods in that directory's `Mods` folder first. It does not publish anything. PowerShell 7 can run the script outside Windows; alternatively, run the `dotnet tModLoader.dll -server -build ...` command from your tModLoader installation.

For IDE builds, provide `CalamityMod.dll`, `MagicStorage.dll`, `ImproveGame.dll`, `ThoriumMod.dll`, `SOTS.dll` and `FargowiltasSouls.dll` in the ignored `references/` folder, or set `ModReferencePath`. These can be built from the respective projects' sources. Set `TmlInstallRoot`, or use the parent `tModLoader.targets` generated by tModLoader when working inside `ModSources`.

Check both languages for UI changes, single-player and a joining client for weapon changes, and save/reload plus item transfer for inventory changes. Documentation is excluded from the mod package.

### Release notes

Use the agreed release version, and compare changes against the last public release rather than a local preview. Keep `CHANGELOG.md` in reverse chronological order with `## Version — YYYY-MM-DD`, then separate Simplified Chinese and English sections. Workshop change notes support localized entries: save Chinese and English separately, each beginning with the version followed by “更新” or “Update”, a blank line, and one bullet per change. Reopen both language entries to verify they persisted. The upload note and `changelog.txt` must also include the version heading.

Describe player-visible changes, starting with additions and improvements, then fixes. Preserve meaningful defaults, conditions and limitations; omit implementation and verification details. Use the version as the GitHub Release title and matching language sections in its body. Historical formatting edits must preserve the scope of the original release; they do not require rebuilding packages or moving tags.

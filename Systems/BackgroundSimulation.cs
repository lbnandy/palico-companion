using System;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Only replace the single focus-pause branch, not Main.hasFocus itself.
// Native input focus, inactive audio, and CanPauseGame (menus) stay independent.
public sealed class BackgroundSimulation : ModSystem {
    public static bool Applied { get; private set; }
    public static bool ContinueUnfocused => Applied && !Main.dedServ &&
        Main.netMode==NetmodeID.SinglePlayer && ModContent.GetInstance<CompanionConfig>().RunInBackground;
    public override void Load() {if(!Main.dedServ)IL_Main.DoUpdate+=PatchFocusPause;}
    public override void Unload() {IL_Main.DoUpdate-=PatchFocusPause;Applied=false;}
    void PatchFocusPause(ILContext il) {
        var cursor=new ILCursor(il);
        // Match the assignment followed by its immediate conditional read.
        // Do not change arbitrary uses of hasFocus elsewhere in the engine.
        if(!cursor.TryGotoNext(MoveType.After,
            instruction=>instruction.MatchStsfld(typeof(Main),nameof(Main.hasFocus)),
            instruction=>instruction.MatchLdsfld(typeof(Main),nameof(Main.hasFocus)))) {
            Mod.Logger.Warn("Background simulation unavailable: focus-pause branch not found.");return;
        }
        cursor.EmitDelegate<Func<bool,bool>>(focused=>focused||ContinueUnfocused);
        Applied=true;
    }
}

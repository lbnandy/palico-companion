using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Homeward Journey 0.8.71.17 reads client input and sky state in ResetEffects.
// Both can abort the server's Player.Update before companion simulation runs.
public sealed class HomewardServerCompatibility:ModSystem {
    ILHook skyReset;
    public override void Load(){
        if(!Main.dedServ||!ModLoader.TryGetMod("ContinentOfJourney",out var journey)||journey.Version!=new Version(0,8,71,17))return;
        const string player="ContinentOfJourney.Items.Donar._Permanent_AiFaDianYongHu_Vg3x.DiscordSonataPlayer";
        var reset=journey.Code.GetType(player)?.GetMethod(nameof(ModPlayer.ResetEffects),Type.EmptyTypes);
        if(reset==null)return;
        skyReset=new ILHook(reset,il=>{
            var cursor=new ILCursor(il);
            if(!cursor.TryGotoNext(MoveType.After,i=>i.MatchStfld(player,"discordSonataActive"))){
                Mod.Logger.Warn("Homeward Journey server sky compatibility: unrecognized ResetEffects body.");return;
            }
            // Preserve the native countdown. The remainder only queries and
            // deactivates a sky that Homeward registers on graphical clients.
            cursor.Emit(OpCodes.Ret);
        });
    }
    public override void PostSetupContent(){
        if(skyReset==null)return;
        // Dedicated servers set up input before mod keybind registration.
        // Homeward reads CoJDash even there; reuse tML's setup now that all
        // bindings exist, with every key released. Client input is untouched.
        Terraria.GameInput.PlayerInput.Triggers.Initialize();
    }
    public override void Unload(){skyReset?.Dispose();skyReset=null;}
}

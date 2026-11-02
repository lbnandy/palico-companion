using System;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Some Homeward builds read client input and an unregistered sky on servers.
// Guard the missing state rather than tying compatibility to a version number.
public sealed class HomewardServerCompatibility:ModSystem {
    const string Journey="ContinentOfJourney";
    const string Sky="CojDonarDiscordSonata_Sky";
    ILHook skyReset;
    public override void Load(){
        if(!Main.dedServ||!ModLoader.TryGetMod(Journey,out var journey))return;
        const string player="ContinentOfJourney.Items.Donar._Permanent_AiFaDianYongHu_Vg3x.DiscordSonataPlayer";
        var reset=journey.Code.GetType(player)?.GetMethod(nameof(ModPlayer.ResetEffects),Type.EmptyTypes);
        if(reset==null)return;
        skyReset=new ILHook(reset,il=>{
            if(!GuardMissingSky(il))
                Mod.Logger.Warn($"Homeward Journey {journey.Version}: no matching server sky query; ResetEffects left unchanged.");
        });
    }
    static bool GuardMissingSky(ILContext il){
        var cursor=new ILCursor(il);
        if(!cursor.TryGotoNext(MoveType.Before,
            i=>i.MatchLdsfld<SkyManager>(nameof(SkyManager.Instance)),
            i=>i.MatchLdstr(Sky),
            i=>i.MatchCallvirt<EffectManager<CustomSky>>("get_Item"),
            i=>i.MatchCallvirt<CustomSky>(nameof(CustomSky.IsActive))))return false;
        // Only replace the null-unsafe query. Keep the countdown, existing sky
        // behavior and any later gameplay logic in Homeward's method intact.
        cursor.Index+=3;
        cursor.Remove();
        cursor.EmitDelegate<Func<CustomSky,bool>>(sky=>sky?.IsActive()??false);
        return true;
    }
    public override void PostSetupContent(){
        if(!Main.dedServ||!ModLoader.HasMod(Journey))return;
        // Use tML's registered key names, but fill only missing Homeward keys.
        // Do not reset existing input or depend on whether the sky needed a fix.
        var registered=new TriggersSet();
        registered.SetupKeys();
        var triggers=PlayerInput.Triggers;
        foreach(var key in registered.KeyStatus.Keys){
            if(!key.StartsWith(Journey+"/",StringComparison.Ordinal))continue;
            triggers.Current.KeyStatus.TryAdd(key,false);
            triggers.Old.KeyStatus.TryAdd(key,false);
            triggers.JustPressed.KeyStatus.TryAdd(key,false);
            triggers.JustReleased.KeyStatus.TryAdd(key,false);
        }
    }
    public override void Unload(){skyReset?.Dispose();skyReset=null;}
}

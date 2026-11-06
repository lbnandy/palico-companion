using System.Linq;
using System.Reflection;
using Terraria.ModLoader;
using CalamityMod.CalPlayer;

namespace PalicoCompanion.Systems;

[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class EntropyEquipmentBridge:ModSystem {
    ModPlayer prototype;
    FieldInfo last,reset;
    PropertyInfo config,enabled;
    public override void PostSetupContent(){
        if(!ModLoader.TryGetMod("CalamityEntropy",out var mod))return;
        prototype=mod.GetContent<ModPlayer>().FirstOrDefault(p=>p.GetType().FullName=="CalamityEntropy.Common.EModPlayer");
        last=prototype?.GetType().GetField("StealthMaxLast");reset=prototype?.GetType().GetField("RstStealth");
        var settings=mod.Code.GetType("CalamityEntropy.Common.ServerConfig");
        config=settings?.GetProperty("Instance");enabled=settings?.GetProperty("ClearStealthWhenChangeEquipSet");
        if(last?.FieldType==typeof(float)&&reset?.FieldType==typeof(bool)&&config!=null&&enabled?.PropertyType==typeof(bool))
            CompanionCrossMod.UpdateResources+=Update;
    }
    void Update(CompanionNativeUse use){
        var state=use.Actor.GetModPlayer(prototype);var calamity=use.Actor.GetModPlayer<CalamityPlayer>();
        float previous=(float)last.GetValue(state),current=calamity.rogueStealthMax;
        // Mirror the native PostUpdate bookkeeping, including a real maximum
        // change. Do not disable CanUseItem or change the human's configuration.
        if(previous!=-1&&previous!=current&&(bool)enabled.GetValue(config.GetValue(null))){
            calamity.rogueStealth=0;reset.SetValue(state,true);
        }
        last.SetValue(state,current);
    }
    public override void Unload(){CompanionCrossMod.UpdateResources-=Update;prototype=null;last=reset=null;config=enabled=null;}
}

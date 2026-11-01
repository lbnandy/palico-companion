using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace PalicoCompanion.Systems;

public sealed class CompanionCart : ModSystem {
    readonly CompanionCartVisual visual=new();
    bool failed;
    public override void PostUpdateEverything(){
        if(Main.dedServ||Main.gameMenu)return;
        var cart=Main.LocalPlayer.GetModPlayer<CompanionCartPlayer>();
        try{cart.UpdatePresentation();}
        catch(Exception e){cart.Cancel();Report(e);}
    }
    public override void OnWorldUnload(){if(!Main.dedServ)Main.LocalPlayer?.GetModPlayer<CompanionCartPlayer>().Cancel();}
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers){
        int i=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        if(i>=0)layers.Insert(i,new LegacyGameInterfaceLayer("PalicoCompanion: Cat cart",Draw,InterfaceScaleType.UI));
    }
    bool Draw(){
        if(Main.dedServ||Main.gameMenu)return true;
        var cart=Main.LocalPlayer.GetModPlayer<CompanionCartPlayer>();
        if(!cart.Active||!Main.LocalPlayer.dead||Main.mapFullscreen||Main.inFancyUI||Main.ingameOptionsWindow)return true;
        try{visual.Cart=cart;visual.Draw(Main.spriteBatch);failed=false;}
        catch(Exception e){cart.Cancel();Report(e);}
        return true;
    }
    internal void Report(Exception e){if(!failed)Mod.Logger.Warn("Unable to show the cat cart animation.",e);failed=true;}
}

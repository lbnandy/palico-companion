using System.Collections.Generic;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
namespace PalicoCompanion.Systems;

// Per-chest permission in the free left gutter of the native chest grid.
// The area below Rename belongs to the vanilla bestiary/emote controls.
public sealed class CompanionChestUI:ModSystem {
    static readonly Terraria.GameContent.UI.Elements.UIPanel button=new();
    internal static bool Drawn {get;private set;}
    public static Rectangle Bounds=>new(30,Main.instance.invBottom+4,34,34);
    public static Chest OpenChest {
        get {
            var p=Main.LocalPlayer;
            if(Main.gameMenu||Main.netMode==NetmodeID.Server||!Main.playerInventory||Main.inFancyUI||Main.mapFullscreen||Main.editChest||Main.recBigList||p.chest<0||p.chest>=Main.maxChests)return null;
            var chest=Main.chest[p.chest];
            if(chest==null||!WorldGen.InWorld(chest.x,chest.y,10)||Chest.IsLocked(chest.x,chest.y))return null;
            var t=Main.tile[chest.x,chest.y];
            return t.HasTile&&t.TileType is TileID.Containers or TileID.Containers2?chest:null;
        }
    }
    public static bool Linked(CompanionPlayer s,Chest chest)=>chest!=null&&s.Chests.Allows(chest.x,chest.y);
    static string L(string key)=>Language.GetTextValue("Mods.PalicoCompanion.UI."+key);
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers){
        int i=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        if(i>=0)layers.Insert(i,new LegacyGameInterfaceLayer("PalicoCompanion: Supply chest",Draw,InterfaceScaleType.UI));
    }
    internal bool Draw(){
        Drawn=false;
        var chest=OpenChest;if(chest==null)return DrawExternal();if(CompanionUIFlow.Covers(Bounds))return true;
        var p=Main.LocalPlayer;var s=p.GetModPlayer<CompanionPlayer>();bool linked=Linked(s,chest);
        var r=Bounds;bool hover=r.Contains(Main.mouseX,Main.mouseY)&&!PlayerInput.IgnoreMouseInterface;
        DrawIcon(r,linked,hover);
        Drawn=true;
        if(!hover)return true;
        p.mouseInterface=true;
        string hint=L(linked?"ChestSupplyOn":"ChestSupplyOff")+"\n"+L(linked?"ChestSupplyUnlinkHint":"ChestSupplyLinkHint");
        if(!ModContent.GetInstance<CompanionConfig>().AutoLogistics)hint+="\n"+L("ChestSupplyDisabledHint");
        Main.instance.MouseText(hint);
        if(Main.mouseLeft&&Main.mouseLeftRelease&&Main.mouseItem.IsAir){
            Main.mouseLeftRelease=false;
            if(linked)s.UnlinkChest();else s.AuthorizeChest();
            s.Report(s.Status);SoundEngine.PlaySound(SoundID.MenuTick);
        }
        return true;
    }
    bool DrawExternal(){
        if(CompanionCrossMod.Storage?.Open(out var access,out var permission,out var r)!=true||CompanionUIFlow.Covers(r))return true;
        var p=Main.LocalPlayer;var s=p.GetModPlayer<CompanionPlayer>();bool linked=s.Chests.Allows(permission.X,permission.Y);
        bool hover=r.Contains(Main.mouseX,Main.mouseY)&&!PlayerInput.IgnoreMouseInterface;
        DrawIcon(r,linked,hover);Drawn=true;if(!hover)return true;p.mouseInterface=true;
        Main.instance.MouseText(CompanionText.Get(linked?"Storage.Allowed":"Storage.Allow"));
        if(Main.mouseLeft&&Main.mouseLeftRelease&&Main.mouseItem.IsAir){Main.mouseLeftRelease=false;CompanionNetwork.ExternalPermission(s,access,!linked);SoundEngine.PlaySound(SoundID.MenuTick);}
        return true;
    }
    internal static void DrawIcon(Rectangle r,bool allowed,bool hover){
        var batch=Main.spriteBatch;
        // Native panel drawing, like the separate icon/state backgrounds used
        // by Magic Storage's toggle buttons. No badge covers the portrait.
        button.Left.Set(r.X,0);button.Top.Set(r.Y,0);button.Width.Set(r.Width,0);button.Height.Set(r.Height,0);
        button.BackgroundColor=allowed?new Color(63,82,136):new Color(40,50,86);
        button.BorderColor=hover?Color.White:allowed?Main.OurFavoriteColor:new Color(16,22,39);
        button.Recalculate();button.Draw(batch);
        // Shared doubled portrait and native UI filtering, including cloth and dye.
        batch.End();
        batch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Matrix.Identity);
        var source=CompanionStatusBar.PortraitSource;
        var icon=CompanionStatusBar.FitPortraitPixels(new Rectangle(r.X+5,r.Y+5,r.Width-10,r.Height-10),Main.UIScale);
        try {CompanionDye.Portrait(batch,Main.LocalPlayer.GetModPlayer<CompanionPlayer>(),icon,source,allowed||hover?Color.White:new Color(185,190,206));}
        finally {batch.End();batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,null,null,null,Main.UIScaleMatrix);}
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
namespace PalicoCompanion.Systems;

// Glowsticks use vanilla held-item drawing AND held-light behavior (including
// Spelunker glowstick detection). Merely holding one never consumes it in vanilla.
// This local visual proxy never enters Main.player or executes ItemCheck.
public sealed class CompanionGlowstickDrawing {
    static readonly Action<Player,Item> NativeLight=typeof(Player).GetMethod("ItemCheck_EmitHeldItemLight",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,Item>>();
    readonly Player proxy=new();
    readonly List<DrawData> cache=new();
    void Prepare(Projectile cat,Item item) {
        proxy.position=cat.position;proxy.width=cat.width;proxy.height=cat.height;
        proxy.direction=cat.direction;proxy.gravDir=1;proxy.velocity=Vector2.Zero;proxy.wet=cat.wet;
        proxy.inventory[0]=item;proxy.selectedItem=0;proxy.heldProj=-1;
    }
    public void Emit(Projectile cat,Item item,Vector2 lightPoint) {
        Prepare(cat,item);proxy.itemLocation=lightPoint-new Vector2(cat.direction<0?-16:6,-14);
        NativeLight(proxy,item);
    }
    public void Draw(Projectile cat,Item item,int pose,Color light) {
        Prepare(cat,item);cache.Clear();
        var hand=CompanionSprite.Hand(cat,true)+CompanionLighting.GripOffset(cat);
        proxy.itemLocation=hand;
        var info=new PlayerDrawSet{drawPlayer=proxy,heldItem=item,DrawDataCache=cache,Position=proxy.position,ItemLocation=hand,
            itemColor=light,itemEffect=cat.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None};
        PlayerDrawLayers.DrawPlayer_27_HeldItem(ref info);
        if(cache.Count==0)return;
        Vector2 shift=hand-Main.screenPosition-CompanionGrip.DrawnGrip(cache[0]);
        foreach(var entry in cache){var draw=entry;draw.position+=shift;draw.Draw(Main.spriteBatch);}
        CompanionItemVisual.DrawGrip(Main.spriteBatch,(cat.Bottom+new Vector2(0,cat.gfxOffY)-Main.screenPosition).Floor(),pose,cat.direction,light,true);
    }
}

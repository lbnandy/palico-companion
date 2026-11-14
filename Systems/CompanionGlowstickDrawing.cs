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
// After its scoped initialization this actor only presents held light/items;
// it never executes ItemCheck or shares state with another companion.
public sealed class CompanionGlowstickDrawing {
    static readonly Action<Player,Item> NativeLight=typeof(Player).GetMethod("ItemCheck_EmitHeldItemLight",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.CreateDelegate<Action<Player,Item>>();
    readonly Player proxy=new();
    internal Player Actor=>proxy;
    int lastFlameTick=-100;
    readonly List<DrawData> cache=new();
    void Prepare(Projectile cat,Item item) {
        proxy.position=cat.position;proxy.width=cat.width;proxy.height=cat.height;
        proxy.direction=cat.direction;proxy.gravDir=1;proxy.velocity=Vector2.Zero;proxy.wet=cat.wet;
        proxy.inventory[0]=item;proxy.selectedItem=0;proxy.heldProj=-1;
    }
    internal bool Update(Projectile cat,Item item){
        proxy.Center=cat.Center;
        if(!CompanionActorSlots.Initialize(proxy))return false;
        Prepare(cat,item);
        int tick=(int)Main.GameUpdateCount;
        if(item.flame&&!Main.dedServ&&(tick<lastFlameTick||tick-lastFlameTick>=5)){
            lastFlameTick=tick;
            for(int i=0;i<7;i++)proxy.itemFlamePos[i]=new Vector2(Main.rand.Next(-10,11)*0.15f,Main.rand.Next(-10,1)*0.35f);
        }
        return true;
    }
    public void Emit(Projectile cat,Item item,Vector2 lightPoint) {
        if(!CompanionActorSlots.Initialized(proxy))return;
        Prepare(cat,item);proxy.itemLocation=lightPoint-new Vector2(cat.direction<0?-16:6,-14);
        NativeLight(proxy,item);
    }
    public void Draw(Projectile cat,Item item,int pose,Color light) {
        if(!CompanionActorSlots.Initialized(proxy))return;
        Prepare(cat,item);cache.Clear();
        var offset=CompanionSpriteLayout.OffHand[pose];
        var hand=cat.Bottom+new Vector2(offset.X*(cat.direction<0?-1:1),offset.Y+cat.gfxOffY)+CompanionLighting.GripOffset(cat);
        proxy.itemLocation=hand;
        var info=new PlayerDrawSet{drawPlayer=proxy,heldItem=item,DrawDataCache=cache,Position=proxy.position,ItemLocation=hand,
            itemColor=light,itemEffect=cat.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None};
        CompanionItemVisual.DrawHeldItem(ref info);
        if(cache.Count==0)return;
        Vector2 shift=hand-Main.screenPosition-CompanionGrip.DrawnGrip(cache[0]);
        foreach(var entry in cache){var draw=entry;draw.position+=shift;draw.Draw(Main.spriteBatch);}
        CompanionItemVisual.DrawHeldItemForeground(Main.spriteBatch,(cat.Bottom+new Vector2(0,cat.gfxOffY)-Main.screenPosition).Floor(),pose,cat.direction,light);
    }
}

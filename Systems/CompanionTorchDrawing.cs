using System;
using System.Collections.Generic;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
namespace PalicoCompanion.Systems;

// Use native held-item layers for the companion torch and its flame.
public static class CompanionTorchDrawing {
    static readonly Player proxy=new();
    static readonly List<DrawData> cache=new();
    static int lastFlameTick=-100;
    public const int ForwardReach=2;
    public static Vector2 Offset(int direction)=>new(direction<0?-ForwardReach:ForwardReach,0);
    public static Vector2 Origin(Texture2D torch,int direction) => new(direction<0?torch.Width-1:1,torch.Height-2);
    public static void Draw(SpriteBatch batch,Texture2D atlas,Vector2 feet,int pose,int direction,Color bodyLight,Color torchLight,int tick,Item item) {
        Item held=item;
        if(!CompanionLighting.Torch(held))return;
        Main.instance.LoadItem(held.type);if(held.flame)Main.instance.LoadItemFlames(held.type);
        var torch=TextureAssets.Item[held.type].Value;
        float scale=CompanionSpriteLayout.Scale;
        var off=CompanionSpriteLayout.OffHand[pose];
        int facing=direction<0?-1:1;
        Vector2 oldHand=feet+new Vector2(off.X*facing,off.Y);
        Vector2 hand=oldHand+new Vector2(facing*ForwardReach,0);
        var flip=facing<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        // The native 14x16 torch is diagonal. Grip the solid butt at (1,14),
        // inside the EXISTING paw, instead of the transparent old anchor (7,12).
        // SpriteEffects mirrors texture coordinates, so the origin must mirror too.
        Vector2 origin=Origin(torch,facing);
        // Let the actual player layer choose frame, colour and native flame offsets.
        // Only translate its resulting draw data to the paw grip.
        proxy.inventory[0]=held;proxy.selectedItem=0;
        proxy.direction=facing;proxy.gravDir=1;proxy.heldProj=-1;proxy.itemRotation=0;
        proxy.Center=feet+Main.screenPosition;proxy.itemLocation=hand+Main.screenPosition;
        // LegacyPlayerRenderer refreshes these seven offsets every five frames.
        // Maintain our own sequence: the owner may be holding a sword, not a torch.
        if(tick<lastFlameTick||tick-lastFlameTick>=5) {
            lastFlameTick=tick;
            for(int i=0;i<7;i++)proxy.itemFlamePos[i]=new Vector2(Main.rand.Next(-10,11)*0.15f,Main.rand.Next(-10,1)*0.35f);
        }
        cache.Clear();
        var info=new PlayerDrawSet{drawPlayer=proxy,heldItem=held,DrawDataCache=cache,
            Position=proxy.position,ItemLocation=proxy.itemLocation,itemColor=torchLight,itemEffect=flip};
        PlayerDrawLayers.DrawPlayer_27_HeldItem(ref info);
        if(cache.Count>0) {
            var first=cache[0];
            // Preserve (1,14) shaft contact, not the player's long-arm position.
            Vector2 shaft=(origin-first.origin).RotatedBy(first.rotation);
            Vector2 shift=hand.Floor()-first.position-shaft;
            foreach(var entry in cache){var draw=entry;draw.position+=shift;draw.Draw(batch);}
        }
        Vector2 anchor=CompanionSpriteLayout.Origin+off/scale;
        int px=(int)anchor.X-1,py=(int)anchor.Y;
        var paw=new Rectangle(px,pose*CompanionSprite.Height+py,3,2);
        float left=facing<0?CompanionSpriteLayout.Origin.X-px-paw.Width:px-CompanionSpriteLayout.Origin.X;
        // Redraw the original paw at its ORIGINAL position. No shifted/extended
        // limb, new skin blocks or change to the silhouette.
        Vector2 at=feet+new Vector2(left,py-CompanionSpriteLayout.Origin.Y)*scale;
        CompanionDye.Draw(batch,new DrawData(atlas,at,paw,bodyLight,0,Vector2.Zero,scale,flip));
    }
}

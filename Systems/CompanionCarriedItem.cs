using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
namespace PalicoCompanion.Systems;

// A visual attachment on the existing world drop. Native item packets carry
// the binding; native pickup remains the only way to consume or heal with it.
public sealed class CompanionCarriedItem:GlobalItem {
    public override bool InstancePerEntity=>true;
    public override bool AppliesToEntity(Item item,bool lateInstantiation)=>item.type is
        ItemID.Heart or ItemID.CandyApple or ItemID.CandyCane or ItemID.Star or ItemID.SoulCake or ItemID.SugarPlum;
    public override GlobalItem Clone(Item from,Item to){
        var clone=(CompanionCarriedItem)base.Clone(from,to);
        // Carrying belongs to one world drop, not copies made for inventory or spawning.
        clone.Release();return clone;
    }
    int owner=-1;
    ulong instance;
    CompanionCat carrier;
    internal const int Frame=CompanionSpriteLayout.WorkStrike;
    internal void Attach(CompanionCat cat){owner=cat.OwnerIndex;instance=cat.NetworkInstance;carrier=cat;}
    internal void Release(){owner=-1;instance=0;carrier=null;}
    public override void SetDefaults(Item item)=>Release();
    public override bool OnPickup(Item item,Player player){Release();return true;}
    public override void NetSend(Item item,BinaryWriter writer){writer.Write((short)owner);writer.Write(instance);}
    public override void NetReceive(Item item,BinaryReader reader){
        int next=reader.ReadInt16();ulong token=reader.ReadUInt64();
        // Clients cannot grant themselves control of world drops.
        if(!CompanionNetwork.Client)return;
        if(next!=owner||token!=instance)carrier=null;
        owner=next>=0&&next<Main.maxPlayers?next:-1;instance=token;
    }
    CompanionCat Carrier {
        get {
            if(owner<0||!Main.player[owner].active||Main.player[owner].dead)return null;
            if(carrier?.Projectile.active!=true||carrier.OwnerIndex!=owner||carrier.NetworkInstance!=instance){
                carrier=null;
                foreach(var p in Main.projectile)if(p.active&&p.ModProjectile is CompanionCat cat&&cat.OwnerIndex==owner&&cat.NetworkInstance==instance){carrier=cat;break;}
            }
            var s=Main.player[owner].GetModPlayer<CompanionPlayer>();
            return s.Enabled&&!s.Paused&&!s.Vitals.Downed?carrier:null;
        }
    }
    internal static Item Find(CompanionCat cat){
        foreach(var item in Main.item)
            if(item.active&&!item.IsAir&&item.TryGetGlobalItem<CompanionCarriedItem>(out var tag)&&tag.Carrier==cat)return item;
        return null;
    }
    internal static Vector2 Position(Projectile cat){
        var hand=CompanionSpriteLayout.MainHand[Frame];hand.X*=cat.direction<0?-1:1;
        return cat.Bottom+hand+new Vector2(2*cat.direction,-2+cat.gfxOffY);
    }
    public override bool PreDrawInWorld(Item item,SpriteBatch spriteBatch,Color lightColor,Color alphaColor,ref float rotation,ref float scale,int whoAmI)=>Carrier==null;
    internal static void Draw(Item item,Projectile cat,Vector2 feet,Color light){
        Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;
        var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
        // Match Main.DrawItem, including the native heart/star pulse.
        float scale=1f;var color=item.GetAlpha(light);
        ItemSlot.GetItemLight(ref color,ref scale,item);
        var at=(Position(cat)-Main.screenPosition).Floor();
        Main.spriteBatch.Draw(texture,at,frame,color,0,frame.Size()*.5f,scale,cat.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
        CompanionItemVisual.DrawGrip(Main.spriteBatch,feet,Frame,cat.direction,light);
    }
}

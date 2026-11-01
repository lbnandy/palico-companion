using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

internal enum CompanionGesture:byte { None,Drink,Deliver,Plant,Harvest,RecoveryDeliver,Eat }

// Successful action receipts only. ExtraAI carries the age as well as the
// sequence, so periodic snapshots do not restart a gesture on observers.
// This presentation never changes inventory, work priority or attack timing.
internal sealed class CompanionActionVisual {
    internal CompanionGesture Kind {get;private set;}
    internal int ItemType {get;private set;}
    internal uint Sequence {get;private set;}
    ulong start;
    internal const int Duration=36;
    int duration=Duration;
    Item foodItem;
    internal int Age=>(int)Math.Min((ulong)duration,Main.GameUpdateCount>=start?Main.GameUpdateCount-start:0);
    internal bool Eating=>Kind==CompanionGesture.Eat&&Active;
    internal void Cancel(CompanionCat cat){if(!Active)return;Kind=CompanionGesture.None;ItemType=0;Sequence++;if(Sequence==0)Sequence=1;cat.PendingPresentation=true;}
    internal bool Active=>Sequence!=0&&Kind!=CompanionGesture.None&&Age<duration;
    internal void Begin(CompanionCat cat,CompanionGesture kind,int item){
        if(CompanionNetwork.Client||kind==CompanionGesture.None||item<=0||item>=ItemLoader.ItemCount)return;
        Kind=kind;ItemType=item;start=Main.GameUpdateCount;Sequence++;if(Sequence==0)Sequence=1;
        duration=kind==CompanionGesture.Eat?CompanionFeeding.Duration:kind is CompanionGesture.Plant or CompanionGesture.Harvest?CompanionWorkTiming.Ticks(Duration):Duration;
        cat.PendingPresentation=true;
    }
    internal void Write(BinaryWriter w){w.Write(Sequence);w.Write((byte)Kind);w.Write(ItemType);w.Write((byte)Age);w.Write((byte)duration);}
    internal void Read(BinaryReader r,bool newInstance){
        uint seq=r.ReadUInt32();byte kind=r.ReadByte();int item=r.ReadInt32();int age=r.ReadByte();int length=Math.Clamp((int)r.ReadByte(),18,144);
        if(!newInstance&&seq<=Sequence)return;
        Sequence=seq;Kind=kind<=(byte)CompanionGesture.Eat?(CompanionGesture)kind:CompanionGesture.None;
        duration=length;
        ItemType=item>0&&item<ItemLoader.ItemCount?item:0;
        // Local clock is unrelated to the dedicated server's update count.
        start=Main.GameUpdateCount>=(ulong)age?Main.GameUpdateCount-(ulong)age:0;
        if(ItemType==0||age>=duration)Kind=CompanionGesture.None;
    }
    internal bool Visible(CompanionCat cat,CompanionPlayer s)=>Active&&!s.Vitals.Downed&&!s.Paused&&!cat.Fishing.Active&&cat.Weapons.Actor.itemAnimation<=0&&cat.Animation.Motion is not (CatMotion.Attack or CatMotion.Mine or CatMotion.Thrust);
    internal int Frame=>Eating?CompanionSpriteLayout.WorkRecover:Age*Duration/duration<8?CompanionSpriteLayout.WorkRecover:Age*Duration/duration<27?(Kind==CompanionGesture.Drink?CompanionSpriteLayout.WorkWindup:CompanionSpriteLayout.WorkStrike):CompanionSpriteLayout.WorkRecover;
    internal void Draw(SpriteBatch sb,Vector2 feet,int direction,Color light,Player actor){
        // Delivery already shows the real world drop until native pickup.
        // Keep the hand gesture, but never draw a second prop after handing it off.
        if(!Active||ItemType<=0||Kind==CompanionGesture.RecoveryDeliver)return;
        Main.instance.LoadItem(ItemType);var texture=TextureAssets.Item[ItemType].Value;
        var source=Main.itemAnimations[ItemType]?.GetFrame(texture)??texture.Frame();
        if(Kind==CompanionGesture.Eat){DrawFood(sb,texture,actor.GetItemDrawFrame(ItemType),feet,direction,light,actor);return;}
        float pulse=MathF.Sin(MathHelper.Pi*Age/(duration-1f));
        var hand=CompanionSpriteLayout.MainHand[Frame];hand.X*=direction;
        // Bottle neck tilts inward toward the mouth. Planting reaches down;
        // depth follows the same near/far-paw rule as weapons and tools.
        float tilt=Kind==CompanionGesture.Drink?-1.05f*pulse*direction:Kind==CompanionGesture.Plant?.30f*pulse*direction:0;
        float scale=Math.Min(.8f,18f/Math.Max(source.Width,source.Height));
        Vector2 at=feet+hand+new Vector2(direction*2,Kind==CompanionGesture.Plant?4:Kind==CompanionGesture.Drink?1:-2);
        sb.Draw(texture,at.Floor(),source,light,tilt,source.Size()*.5f,scale,direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
        CompanionItemVisual.DrawHeldItemForeground(sb,feet,Frame,direction,light);
    }
    void DrawFood(SpriteBatch sb,Texture2D texture,Rectangle source,Vector2 feet,int direction,Color light,Player actor){
        // Use the native held frame and scale, just like PlayerDrawLayers.
        // Hold the complete food in one pose for the entire gesture.
        if(foodItem?.type!=ItemType)foodItem=new Item(ItemType);
        var hand=CompanionSpriteLayout.MainHand[Frame];hand.X*=direction;
        var at=feet+hand+new Vector2(direction,-3);
        float scale=actor.GetAdjustedItemScale(foodItem);
        sb.Draw(texture,at.Floor(),source,light,-.12f*direction,source.Size()*.5f,scale,direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
        CompanionItemVisual.DrawHeldItemForeground(sb,feet,Frame,direction,light);
    }

}

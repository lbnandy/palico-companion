using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

// The companion remains the server-owned entity. Adapt AI_160_Kites' wind,
// drag and tether constraint here; its direct Main.mouse reads are local-only.
internal sealed class CompanionKite {
    internal const float MinLength=24,MaxLength=500,ReelSpeed=5;
    internal bool Active {get;private set;}
    internal float Length {get;private set;}
    internal int ItemType {get;private set;}
    internal uint Session {get;private set;}
    sbyte input;
    ulong lastInput;
    Vector2 previousOwner;
    internal static bool Item(Item item)=>item is {IsAir:false,ModItem:null}&&item.type<ItemID.Count&&ItemID.Sets.IsAKite[item.type];
    internal static string Text(string key)=>Language.GetTextValue("Mods.PalicoCompanion.UI.Kite"+key);
    internal static string Message(string key)=>CompanionText.MessageFull("Mods.PalicoCompanion.UI.Kite"+key);
    internal static string Unavailable(CompanionPlayer s){
        var p=s.Player;
        if(!s.Enabled||s.Stowed||p.dead||p.CCed||p.noItems||p.pulley||s.Vitals.Downed||s.Cat?.ModProjectile is not CompanionCat c)return "Unavailable";
        if(c.Kite.Active)return "Help";
        if(!Item(p.HeldItem)||p.selectedItem is <0 or >=10)return "NeedItem";
        if(!CompanionInteraction.TalkRange(p).Intersects(c.Projectile.Hitbox)||!Reachability.Clear(p.Center,c.Projectile.Center))return "Closer";
        if(s.Paused||s.PetCooldown>510||c.ActionVisual.Eating||c.Barrel.Active||c.Weapons.Busy||p.itemAnimation>0||s.Directive.Active)return "Busy";
        foreach(var shot in Main.ActiveProjectiles)if(shot.owner==p.whoAmI&&shot.aiStyle==160)return "ReelFirst";
        return null;
    }
    internal static bool Start(CompanionPlayer s,out string reason){
        reason=Unavailable(s);if(reason!=null||CompanionNetwork.Client)return false;
        var c=(CompanionCat)s.Cat.ModProjectile;var k=c.Kite;
        c.StopWork(s);c.Navigation.HoldReturn(c.Projectile);c.ActionVisual.Cancel(c);
        k.Active=true;k.ItemType=s.Player.HeldItem.type;k.Session++;
        k.Length=Math.Clamp(Vector2.Distance(s.Player.MountedCenter,c.Projectile.Center),80,MaxLength);
        k.previousOwner=s.Player.Center;k.SetInput(0);
        c.Projectile.velocity=new Vector2(-s.Player.direction,-2);
        c.Projectile.netUpdate=true;s.Status=Message("Status");return true;
    }
    internal void SetInput(sbyte direction){input=(sbyte)Math.Clamp((int)direction,-1,1);lastInput=Main.GameUpdateCount;}
    internal void Stop(CompanionCat c){
        if(!Active)return;
        Active=false;input=0;
        c.Navigation.ResumeFlight(c.Projectile);c.Projectile.netUpdate=true;
        Main.player[c.OwnerIndex].GetModPlayer<CompanionPlayer>().Status=CompanionText.MessageFull("Mods.PalicoCompanion.UI.Following");
    }
    internal bool Tick(CompanionPlayer s,CompanionCat c){
        if(!Active)return false;
        var p=s.Player;var cat=c.Projectile;
        if(s.Paused||s.Stowed||s.Vitals.Downed||p.dead||p.CCed||p.noItems||p.pulley||p.HeldItem.type!=ItemType||!Item(p.HeldItem)||
            Vector2.DistanceSquared(p.Center,previousOwner)>640*640||Vector2.DistanceSquared(p.Center,cat.Center)>2000*2000||
            CompanionNetwork.Server&&Main.GameUpdateCount-lastInput>180){Stop(c);return false;}
        previousOwner=p.Center;
        // A missing release packet must never keep reeling indefinitely.
        if(Main.GameUpdateCount-lastInput>60)input=0;
        Length=Math.Clamp(Length+input*ReelSpeed,MinLength,MaxLength);
        if(Length<=MinLength&&input<0){Stop(c);return false;}
        float wind=WorldGen.InAPlaceWithWind(cat.position,cat.width,cat.height)?Main.WindForVisuals:0;
        Step(cat,p.RotatedRelativePoint(p.MountedCenter),Length,wind,Main.cloudAlpha,Main.GlobalTimeWrappedHourly);
        // Unlike a paper kite, the cat can rest on the ground when the wind dies.
        cat.tileCollide=true;
        cat.direction=CompanionCat.Facing(cat.Center,p.Center,cat.direction);
        c.Light.Update(p,s,cat,true);s.Status=Message("Status");return true;
    }
    internal static void Step(Projectile cat,Vector2 anchor,float length,float wind,float clouds,float time){
        float lift=Utils.GetLerpValue(.2f,.5f,Math.Abs(wind),true)*.5f;
        var offset=new Vector2(wind,(float)Math.Sin(time)+clouds*5)*25;
        var desired=offset.SafeNormalize(Vector2.Zero)*(3+clouds*7);
        if(lift==0)desired=cat.velocity;
        float vertical=cat.velocity.Y,distance=offset.Length();
        if(distance>10)cat.velocity=Vector2.Lerp(cat.velocity,desired,.075f*Utils.GetLerpValue(5,10,distance,true));
        cat.velocity.Y=Math.Clamp(vertical+.02f-lift*.75f,-2,2);
        if(cat.velocity.Y<offset.Y)cat.velocity.Y=MathHelper.Lerp(cat.velocity.Y,cat.velocity.Y+lift+.01f,.75f);
        cat.velocity.X*=.98f;
        var toward=anchor-cat.Center;distance=toward.Length();
        if(distance<=length)return;
        toward/=distance;
        // Keep the native length constraint, but do not pull the cat through a wall.
        var pull=toward*(distance-length);
        cat.position+=Collision.TileCollision(cat.position,pull,cat.width,cat.height);
        cat.velocity.Y+=toward.Y*.05f;
        if(toward.Y<.8f||lift>0)cat.velocity.Y-=.15f;
        cat.velocity.X+=toward.X*.2f;
    }
    internal void Write(BinaryWriter w){w.Write(Active);w.Write(Session);if(Active){w.Write(ItemType);w.Write(Length);}}
    internal void Read(BinaryReader r){Active=r.ReadBoolean();Session=r.ReadUInt32();if(Active){ItemType=r.ReadInt32();Length=r.ReadSingle();}}
    internal static float ArmAngle(Player p,Projectile cat)=>(cat.Center-p.MountedCenter).ToRotation()-MathHelper.PiOver2;
    internal void Draw(CompanionCat c){
        if(!Active)return;
        var p=Main.player[c.OwnerIndex];var end=c.Projectile.Center;
        var start=p.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,ArmAngle(p,c.Projectile));
        var texture=TextureAssets.FishingLine.Value;
        // Main.DrawKite uses the same native line texture and lit short segments.
        float distance=Vector2.Distance(start,end),slack=Math.Clamp(Length-distance,0,80)*.25f;
        int count=Math.Clamp((int)Math.Ceiling(distance/12),1,100);
        var last=start;
        for(int i=1;i<=count;i++){
            float t=i/(float)count;var next=Vector2.Lerp(start,end,t)+new Vector2(0,4*t*(1-t)*slack);
            var delta=next-last;float span=delta.Length();
            if(span>.01f)Main.EntitySpriteDraw(texture,last-Main.screenPosition,null,Lighting.GetColor(last.ToTileCoordinates()),
                delta.ToRotation()-MathHelper.PiOver2,new Vector2(texture.Width*.5f,0),new Vector2(1,span/texture.Height),SpriteEffects.None);
            last=next;
        }
    }
}

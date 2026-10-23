using System;
using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;

// Short physical sidesteps. Native collision and damage remain authoritative;
// this grants neither immunity nor a projectile-destruction shortcut.
public sealed class CompanionEvasion {
    int cooldown,remaining;Vector2 chosen;
    CompanionEvasionMode lastStyle=CompanionEvasionMode.Standard;
    static float Exposure(Vector2 origin,Vector2 velocity,int width,int height,Vector2 cat,Vector2 motion,int cw,int ch,int horizon){
        float risk=0;
        for(int t=1;t<=horizon;t++){
            Vector2 delta=origin+velocity*t-cat-motion*t;
            float x=(width+cw)*.5f+2-Math.Abs(delta.X),y=(height+ch)*.5f+2-Math.Abs(delta.Y);
            if(x>0&&y>0)risk+=1+Math.Min(x,y)*.1f;
        }
        return risk;
    }
    static float Risk(Projectile cat,Vector2 motion,int horizon){
        float risk=0;
        foreach(var shot in Main.projectile)if(shot.active&&shot.hostile&&shot.damage>0&&shot.velocity.LengthSquared()>.25f&&
            Vector2.DistanceSquared(shot.Center,cat.Center)<240*240)
            risk+=Exposure(shot.Center,shot.velocity*(shot.extraUpdates+1),shot.width,shot.height,cat.Center,motion,cat.width,cat.height,horizon);
        // Stationary enemies still deal contact damage. Use their nearest edge
        // for broad phase so large bosses are not discarded by center distance.
        foreach(var npc in Main.npc)if(npc.active&&!npc.friendly&&!npc.dontTakeDamage&&npc.damage>0&&
            Vector2.DistanceSquared(npc.Hitbox.ClosestPointInRect(cat.Center),cat.Center)<240*240)
            risk+=Exposure(npc.Center,npc.velocity,npc.width,npc.height,cat.Center,motion,cat.width,cat.height,horizon);
        return risk;
    }
    static bool Safe(Projectile cat,Vector2 velocity,bool flying){
        for(int t=2;t<=12;t+=2){
            Vector2 pos=cat.Center+velocity*t;
            if(!CompanionNavigation.Fits(pos))return false;
            int x=(int)(pos.X/16),y=(int)((pos.Y+cat.height/2f)/16);
            if(!WorldGen.InWorld(x,y,10))return false;
            if(Main.tile[x,y].LiquidAmount>0&&Main.tile[x,y].LiquidType==Terraria.ID.LiquidID.Lava)return false;
            if(!flying&&!Collision.SolidCollision(new Vector2(pos.X-cat.width/2f,pos.Y+cat.height/2f),cat.width,20,true))return false;
        }
        return true;
    }
    public void Update(Player owner,CompanionPlayer state,Projectile cat,CompanionNavigation nav){
        var settings=CompanionBalanceConfig.Current;
        var style=settings.EvasionStyle;
        if(style!=lastStyle){remaining=0;cooldown=0;lastStyle=style;}
        if(style==CompanionEvasionMode.Off){remaining=cooldown=0;return;}
        bool agile=style==CompanionEvasionMode.Agile;
        if(cooldown>0)cooldown--;
        if(state.Paused||state.Vitals.Downed||!state.Enabled||Vector2.DistanceSquared(owner.Center,cat.Center)>480*480){remaining=0;return;}
        bool flying=nav.Riding;
        if(!flying&&!nav.Grounded(cat)){remaining=0;return;}
        bool contact=false;
        foreach(var npc in Main.npc)if(npc.active&&!npc.friendly&&!npc.dontTakeDamage&&npc.damage>0&&npc.Hitbox.Intersects(cat.Hitbox)){contact=true;break;}
        if(remaining>0&&!contact){
            if(Safe(cat,chosen,flying)){cat.velocity.X=chosen.X;if(flying)cat.velocity.Y=chosen.Y;remaining--;return;}
            remaining=0;
        }
        if(cooldown>0&&(!contact||!agile))return;
        // Ground movement includes gravity before Terraria resolves the floor.
        // Compare the displacement that can actually occur, not that downward
        // velocity against sidesteps whose vertical component is already zero.
        Vector2 motion=flying?cat.velocity:Collision.TileCollision(cat.position,cat.velocity,cat.width,cat.height,nav.DropThrough);
        int horizon=agile?12:6;
        float baseline=Risk(cat,motion,horizon);if(baseline==0)return;
        Vector2 best=motion;float score=baseline;
        float speed=3.8f*settings.MovementScale(true);
        Vector2[] options=flying?new[]{new Vector2(-speed,0),new Vector2(speed,0),new Vector2(0,-speed),new Vector2(0,speed),Vector2.Zero}:new[]{new Vector2(-speed,0),new Vector2(speed,0),Vector2.Zero};
        foreach(var v in options)if(Safe(cat,v,flying)){float risk=Risk(cat,v,horizon);if(risk<score){score=risk;best=v;}}
        if(score>=baseline)return;
        chosen=best;remaining=9;cooldown=agile?45:90;cat.velocity.X=best.X;if(flying)cat.velocity.Y=best.Y;cat.netUpdate=true;
    }
}

using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace PalicoCompanion.Systems;

// A bounded ground charge, with native steps and short ballistic hops. Planning
// and execution share the same next input; flight/teleport recovery is excluded.
internal static class CompanionBarrelNavigation {
    const int Width=CompanionNavigation.Width,Height=CompanionNavigation.Height;
    static readonly Vector2 Half=new(Width/2f,Height/2f);
    internal readonly record struct Move(Vector2 Position,Vector2 Velocity,float GfxOffset);
    // The wider clearance belongs above the body: extending it to the feet
    // incorrectly treats an ordinary step beside the cat as a barrel collision.
    internal static bool Clear(Vector2 center)=>FitsBody(center)&&
        CompanionNavigation.Fits(center-new Vector2(0,56),64,96);
    static bool FitsBody(Vector2 center){
        var position=center-Half;
        if(!WorldGen.InWorld((int)(center.X/16),(int)(center.Y/16),10)||Collision.WetCollision(position,Width,Height))return false;
        if(!Collision.SolidCollision(position,Width,Height))return true;
        // SolidCollision treats slopes as full squares. Clip the body to each
        // touched tile and ask the native point test about its actual shape.
        for(int x=(int)(position.X/16);x<=(int)((position.X+Width-.02f)/16);x++)
        for(int y=(int)(position.Y/16);y<=(int)((position.Y+Height-.02f)/16);y++){
            float left=Math.Max(position.X,x*16)+.02f,right=Math.Min(position.X+Width,x*16+16)-.02f;
            float top=Math.Max(position.Y,y*16)+.02f,bottom=Math.Min(position.Y+Height,y*16+16)-.02f;
            if(Collision.IsWorldPointSolid(new(left,top),true)||Collision.IsWorldPointSolid(new(right,top),true)||
                Collision.IsWorldPointSolid(new(left,bottom),true)||Collision.IsWorldPointSolid(new(right,bottom),true))return false;
        }
        return true;
    }
    internal static bool Contact(Vector2 position,NPC target){
        var area=new Rectangle((int)position.X-18,(int)position.Y-8,Width+36,Height+16);
        if(!area.Intersects(target.Hitbox))return false;
        var from=position+Half;var to=target.Hitbox.ClosestPointInRect(from);
        int count=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(from,to)/2));
        for(int i=0;i<=count;i++)if(Collision.IsWorldPointSolid(Vector2.Lerp(from,to,i/(float)count),true))return false;
        return true;
    }
    static bool Grounded(Vector2 position,Vector2 velocity)=>velocity.Y>=0&&CompanionNavigation.Supported(position);
    static bool Sweep(Vector2 from,Vector2 to){
        int count=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(from,to)/4));
        for(int i=0;i<=count;i++)if(!Clear(Vector2.Lerp(from,to,i/(float)count)+Half))return false;
        return true;
    }
    static bool Integrate(Move move,out Vector2 position,out Vector2 velocity){
        velocity=Collision.TileCollision(move.Position,move.Velocity,Width,Height,false,false);
        // CompanionCat is a regular mod projectile, not Main.projPet. Vanilla
        // resolves its slopes before advancing position (with fall=true).
        var slope=Collision.SlopeCollision(move.Position,velocity,Width,Height,0,true);
        velocity=new Vector2(slope.Z,slope.W);position=new Vector2(slope.X,slope.Y)+velocity;
        return Sweep(move.Position,position);
    }
    static bool FloorBelow(float x,float y,int depth){
        for(int down=0;down<=depth;down+=2){
            var center=new Vector2(x,y+down);
            if(FitsBody(center)&&CompanionNavigation.Supported(center-Half))return true;
        }
        return false;
    }
    // Check the entire arc, including the barrel, and a real landing. A hop may
    // cross a small break in the floor, but must not turn into a long pit jump.
    static bool Lands(Move first,int direction,float speed,float floorY){
        var position=first.Position;var velocity=first.Velocity;
        float unsupported=0,previousX=position.X;
        for(int i=0;i<36;i++){
            if(i>0)velocity=new Vector2(MathHelper.Lerp(velocity.X,direction*speed,.2f),Math.Min(10,velocity.Y+.35f));
            if(!Integrate(new Move(position,velocity,0),out position,out velocity)||position.Y>floorY+32)return false;
            if(Collision.WetCollision(new Vector2(position.X,floorY),Width,Height+32))return false;
            if(FloorBelow(position.X+Half.X,floorY+Half.Y-16,48))unsupported=0;
            else unsupported+=Math.Abs(position.X-previousX);
            previousX=position.X;
            if(unsupported>32)return false;
            if(Grounded(position,velocity))return (position.X-first.Position.X)*direction>=Math.Min(8,speed*2);
            if(Math.Abs(velocity.X)<.1f)return false;
        }
        return false;
    }
    static bool WalksAhead(Vector2 position,Vector2 velocity,int direction,float speed){
        for(int i=0;i<3;i++){
            var previous=position;
            velocity=new Vector2(MathHelper.Lerp(velocity.X,direction*speed,.2f),Math.Min(10,velocity.Y+.35f));
            float step=0,gfx=0;
            if(Grounded(position,velocity))Collision.StepUp(ref position,ref velocity,Width,Height,ref step,ref gfx);
            var move=new Move(position,velocity,gfx);
            if(!Sweep(previous,position)||!Integrate(move,out position,out velocity))return false;
            if(!Grounded(position,velocity))return FloorBelow(position.X+Half.X,previous.Y+Half.Y,32)&&Lands(move,direction,speed,previous.Y);
        }
        return true;
    }
    static bool Next(Vector2 position,Vector2 velocity,int direction,float speed,float floorY,out Move move){
        bool ground=Grounded(position,velocity);
        var input=new Vector2(MathHelper.Lerp(velocity.X,direction*speed,.2f),Math.Min(10,velocity.Y+.35f));
        float step=0,gfx=0;var stepped=position;var steppedInput=input;
        if(ground)Collision.StepUp(ref stepped,ref steppedInput,Width,Height,ref step,ref gfx);
        move=new Move(stepped,steppedInput,gfx);
        if(Sweep(position,stepped)&&Integrate(move,out var next,out var nextVelocity)&&next.Y<=floorY+32){
            bool forward=(next.X-position.X)*direction>=Math.Abs(input.X)-.1f;
            if(!ground||forward&&(Grounded(next,nextVelocity)&&WalksAhead(next,nextVelocity,direction,speed)||
                !Grounded(next,nextVelocity)&&FloorBelow(next.X+Half.X,floorY+Half.Y,32)&&Lands(move,direction,speed,floorY)))return true;
        }
        if(!ground)return false;
        for(int attempt=0;attempt<2;attempt++){
            float impulse=attempt==0?-3.5f:-4.5f;
            move=new Move(position,new Vector2(input.X,impulse),0);
            if(Lands(move,direction,speed,floorY))return true;
        }
        return false;
    }
    internal static bool Plan(Projectile cat,NPC target,float speed,int ticks,float floorY,out Move first,bool fromRest=false){
        return Plan(cat.position,fromRest?Vector2.Zero:cat.velocity,cat.direction,target,speed,ticks,floorY,out first);
    }
    internal static bool Plan(Vector2 position,Vector2 velocity,int facing,NPC target,float speed,int ticks,float floorY,out Move first){
        first=default;
        int direction=CompanionCat.Facing(position+Half,target.Center,facing);
        for(int i=0;i<ticks;i++){
            if(!Clear(position+Half))return false;
            if(Contact(position,target)){
                if(i==0)first=new Move(position,Vector2.Zero,0);
                return true;
            }
            if(Grounded(position,velocity))floorY=position.Y;
            if(!Next(position,velocity,direction,speed,floorY,out var move))return false;
            if(i==0)first=move;
            if(!Integrate(move,out position,out velocity))return false;
        }
        return false;
    }
}

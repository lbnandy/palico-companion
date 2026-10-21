using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PalicoCompanion.Systems;

// Simulation owns the deployment clock. Rendering never advances it or moves
// the projectile, and all body/tool anchors remain unchanged.
public sealed class CompanionFlightVisual {
    public const int DeployTicks=18;
    public int Deployment { get; private set; }
    public int Phase { get; private set; }
    public float Sway { get; private set; }
    public float Amount=>Deployment/(float)DeployTicks;
    public void Update(bool flying,bool paused,bool downed,float vx) {
        if(downed){Deployment=0;Sway=0;return;}
        if(paused)return;
        Deployment=Math.Clamp(Deployment+(flying?1:-1),0,DeployTicks);
        if(Deployment==0){Phase=0;Sway=0;return;}
        Phase=(Phase+1)%120;
        float target=Math.Clamp(-vx*0.25f,-3f,3f)+(float)Math.Sin(Phase*Math.PI/60)*1.2f;
        Sway=MathHelper.Lerp(Sway,target,0.12f);
    }
    public void Draw(SpriteBatch batch,Vector2 feet,int direction,Color frameLight,Color kiteLight) =>
        Draw(batch,feet,direction,frameLight,kiteLight,Amount,Sway,Phase);

    // Back-mounted kite: body and weapon anchors never move. Row shifts keep
    // the cloth on the body's 2px grid while suggesting a gentle change of pitch.
    public static void Draw(SpriteBatch batch,Vector2 feet,int direction,Color frameLight,Color kiteLight,float amount,float sway,int phase=0) {
        var texture=CompanionSprite.Kite;
        if(texture is null||amount<=0)return;
        amount=Math.Clamp(amount,0,1);int dir=direction<0?-1:1;
        var top=feet+new Vector2(-4*dir,-56+(int)Math.Round((1-amount)*6)*2);
        Color cloth=kiteLight*amount;
        for(int row=0;row<texture.Height;row++){
            int lean=(int)Math.Round(sway*(1-row/30f))*2;
            var at=new Vector2((int)top.X-30+lean,(int)top.Y+row*2);
            CompanionDye.Draw(batch,new Terraria.DataStructures.DrawData(texture,at,new Rectangle(0,row,31,1),cloth,0,Vector2.Zero,2,direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None));
        }
        Color wood=new Color(110,83,48).MultiplyRGBA(frameLight)*amount;
        // Small foot rail and shoulder attachments imply a harness behind the cat.
        Rope(batch,feet+new Vector2(-6*dir-8,-2),feet+new Vector2(-6*dir+8,-2),wood);
        Rope(batch,feet+new Vector2(-18*dir,-30),feet+new Vector2(-8*dir,-18),wood);
        float wave=(float)Math.Sin(phase*Math.PI/30);
        Tassel(batch,top+new Vector2(-27+(int)Math.Round(sway)*2,18),wave,frameLight,amount);
        Tassel(batch,top+new Vector2(27+(int)Math.Round(sway)*2,20),-wave,frameLight,amount);
        var last=feet+new Vector2(-6*dir,6);
        for(int i=1;i<=10;i++){
            float bend=(float)Math.Sin(phase*Math.PI/60-i*.42f)*Math.Min(3,i*.4f)-sway*i*.25f;
            var next=feet+new Vector2(-6*dir+(int)Math.Round(bend)*2,6+i*2);
            Rope(batch,last,next,new Color(174,177,157).MultiplyRGBA(frameLight)*amount);
            Rope(batch,last+new Vector2(2,0),next+new Vector2(2,0),new Color(110,131,125).MultiplyRGBA(frameLight)*amount);
            last=next;
        }
    }
    static void Tassel(SpriteBatch batch,Vector2 at,float wave,Color light,float amount){
        Rope(batch,at,at+new Vector2((int)Math.Round(wave)*2,4),new Color(164,139,90).MultiplyRGBA(light)*amount);
        at+=new Vector2((int)Math.Round(wave)*2,4);
        Rope(batch,at,at+new Vector2(0,6),new Color(175,73,54).MultiplyRGBA(light)*amount);
        Rope(batch,at+new Vector2(2,0),at+new Vector2(2,6),new Color(101,48,45).MultiplyRGBA(light)*amount);
    }
    static void Rope(SpriteBatch batch,Vector2 from,Vector2 to,Color color) {
        Vector2 delta=to-from;int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(delta.X),Math.Abs(delta.Y))/2));
        for(int i=0;i<=steps;i++) {
            Vector2 p=from+delta*(i/(float)steps);
            int x=(int)from.X+(int)Math.Round((p.X-from.X)/2)*2;
            int y=(int)from.Y+(int)Math.Round((p.Y-from.Y)/2)*2;
            CompanionDye.Draw(batch,new Terraria.DataStructures.DrawData(TextureAssets.MagicPixel.Value,new Vector2(x,y),new Rectangle(0,0,1,1),color,0,Vector2.Zero,2,SpriteEffects.None));
        }
    }
}

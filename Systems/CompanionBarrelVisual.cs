using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// Authored carrying poses share one fixed foot anchor and replicated phase clock.
internal static class CompanionBarrelVisual {
    const int RecoilWidth=45,RecoilHeight=44,RecoilFrames=7;
    static Texture2D barrelTexture,recoilPoses;
    internal static Texture2D RecoilTexture=>recoilPoses;
    internal static Texture2D Icon {get;private set;}
    internal static void Load(){
        // These pixels are copied once into owned textures. Wait for the native
        // assets here; asynchronous LoadItem/LoadTiles can return a blank placeholder.
        var native=Main.Assets.Request<Texture2D>(TextureAssets.Tile[TileID.Containers].Name,AssetRequestMode.ImmediateLoad).Value;
        var tiles=new Color[native.Width*native.Height];native.GetData(tiles);
        var pixels=new Color[32*34];
        for(int y=0;y<34;y++)for(int x=0;x<32;x++)pixels[y*32+x]=tiles[(y<16?y:y+2)*native.Width+5*36+x%16+x/16*18];
        // Trim only one source pixel from the exposed dark border. Read from
        // the unchanged silhouette so the inner outline cannot be stripped too.
        Color edge=pixels[4*32+8];
        var original=(Color[])pixels.Clone();
        for(int y=0;y<34;y++)for(int x=0;x<32;x++){
            int i=y*32+x;
            if(original[i]==edge&&(x==0||x==31||y==0||y==33||original[i-1].A==0||original[i+1].A==0||original[i-32].A==0||original[i+32].A==0))pixels[i]=Color.Transparent;
        }
        FinishBarrel(pixels);
        edge=new Color(47,39,35);
        // The holding marker denotes the visible base, not transparent padding.
        int bottom=34;
        while(bottom>1&&Array.TrueForAll(pixels[(32*(bottom-1))..(32*bottom)],p=>p.A==0))bottom--;
        barrelTexture=new Texture2D(Main.graphics.GraphicsDevice,32,bottom);barrelTexture.SetData(pixels[..(32*bottom)]);
        // Preserve the native item sprite and its two-pixel source grid.
        var item=Main.Assets.Request<Texture2D>(TextureAssets.Item[ItemID.Barrel].Name,AssetRequestMode.ImmediateLoad).Value;
        var itemPixels=new Color[item.Width*item.Height];item.GetData(itemPixels);
        const int cell=2,padding=3*cell;
        var icon=new Color[item.Width*(item.Height+padding)];
        Array.Copy(itemPixels,0,icon,item.Width*padding,itemPixels.Length);
        int fuseX=item.Width/2/cell*cell,top=0;
        while(top<item.Height&&itemPixels[top*item.Width+fuseX].A==0)top++;
        if(top<item.Height){
            Color fuseColor=new(112,84,56);
            for(int step=0;step<3;step++){
                int x=fuseX+(step==2?cell:0),y=top+padding-(step+1)*cell;
                for(int dy=0;dy<cell;dy++)for(int dx=0;dx<cell;dx++)
                    icon[(y+dy)*item.Width+x+dx]=fuseColor;
            }
        }
        Icon=new Texture2D(Main.graphics.GraphicsDevice,item.Width,item.Height+padding);Icon.SetData(icon);
        LoadRecoilPoses();
    }
    static void LoadRecoilPoses(){
        // Bake a few small backward tilts on the native pixel grid. Rotating a
        // scaled sprite at draw time would make its pixels finer than the cat.
        var source=new Color[31*28];
        CompanionSprite.Atlas.GetData(0,new Rectangle(0,CompanionSpriteLayout.Fall*28,31,28),source,0,source.Length);
        var pixels=new Color[RecoilWidth*RecoilHeight*RecoilFrames];
        for(int frame=0;frame<RecoilFrames;frame++){
            float angle=-frame*.1f,cos=MathF.Cos(angle),sin=MathF.Sin(angle);
            for(int y=0;y<RecoilHeight;y++)for(int x=0;x<RecoilWidth;x++){
                float dx=x+.5f-RecoilWidth/2f,dy=y+.5f-RecoilHeight/2f;
                int sx=(int)MathF.Floor(cos*dx+sin*dy+15.5f),sy=(int)MathF.Floor(-sin*dx+cos*dy+15);
                if(sx>=0&&sx<31&&sy>=0&&sy<28)pixels[(frame*RecoilHeight+y)*RecoilWidth+x]=source[sy*31+sx];
            }
        }
        recoilPoses=new Texture2D(Main.graphics.GraphicsDevice,RecoilWidth,RecoilHeight*RecoilFrames);recoilPoses.SetData(pixels);
    }
    internal static bool RecoilSheath(int index){
        int frame=index/(RecoilWidth*RecoilHeight),x=index%RecoilWidth,y=index/RecoilWidth%RecoilHeight;
        float angle=-frame*.1f,dx=x+.5f-RecoilWidth/2f,dy=y+.5f-RecoilHeight/2f;
        int sy=(int)MathF.Floor(-MathF.Sin(angle)*dx+MathF.Cos(angle)*dy+15);
        return sy>=18;
    }
    static void FinishBarrel(Color[] pixels){
        // Keep the native plank pattern, but group near-identical brown shades
        // and separate the cool metal from the warmer wood.
        Color[] native={new(41,30,21),new(64,48,32),new(64,48,40),new(96,68,48),new(80,60,48),new(168,120,88),new(96,68,56),new(136,100,72),new(72,56,40),new(48,51,49),new(80,80,80),new(128,128,128),new(152,152,152),new(112,84,56)};
        Color[] palette={new(65,47,35),new(83,59,39),new(83,59,39),new(128,88,53),new(103,71,44),new(180,133,81),new(128,88,53),new(153,107,64),new(103,71,44),new(65,72,77),new(92,103,110),new(129,141,145),new(165,174,173),new(153,107,64)};
        var silhouette=(Color[])pixels.Clone();
        for(int y=0;y<34;y++)for(int x=0;x<32;x++){
            int i=y*32+x;if(silhouette[i].A==0)continue;
            bool boundary=x==0||x==31||y==0||y==33||silhouette[i-1].A==0||silhouette[i+1].A==0||silhouette[i-32].A==0||silhouette[i+32].A==0;
            if(boundary){pixels[i]=new Color(47,39,35);continue;}
            int color=Array.IndexOf(native,silhouette[i]);
            if(color>=0)pixels[i]=palette[color];
            // Extend the adjacent wood/metal into the right inner rim so it
            // cannot read as a second outline beside the actual boundary.
            if(x==26&&(color==2||color==9))pixels[i]=pixels[i-1];
        }
    }
    internal static void Unload(){
        var barrel=barrelTexture;var icon=Icon;var recoil=recoilPoses;
        barrelTexture=recoilPoses=Icon=null;
        Main.QueueMainThreadAction(()=>{barrel?.Dispose();icon?.Dispose();recoil?.Dispose();});
    }
    internal static int CarryFrame(int stage,int age,int bodyFrame){
        if(stage==1)return CompanionSpriteLayout.BarrelRaise0+(age<6?0:age<15?1:age<24?2:3);
        int step=bodyFrame-CompanionSpriteLayout.Walk0;
        return CompanionSpriteLayout.BarrelWalk0+(step>=0&&step<CompanionSpriteLayout.WalkFrames?step:0);
    }
    internal static Vector2 BarrelAnchor(int frame){
        int step=frame-CompanionSpriteLayout.BarrelWalk0;
        // Bottom-center of the artist's Barrel marker, translated by the same
        // two source rows as the body. The marked rectangle is never rendered.
        float x=step is 1 or 2 or 3?18.5f:17.5f;
        int y=frame==CompanionSpriteLayout.BarrelRaise0+1?12:frame==CompanionSpriteLayout.BarrelRaise0+2?11:9;
        return new Vector2(x,y);
    }
    internal static int SettleFrame(int age)=>age<CompanionBarrelCharge.ProneTicks?CompanionSpriteLayout.Prone:
        age<CompanionBarrelCharge.ProneTicks+CompanionBarrelCharge.GetUpFrameTicks?CompanionSpriteLayout.GetUp0:CompanionSpriteLayout.GetUp1;
    internal static void Draw(SpriteBatch batch,Vector2 feet,CompanionCat cat,Color light,int stage,int age){
        var atlas=CompanionSprite.Atlas;if(atlas==null)return;
        int direction=cat.Projectile.direction;
        var flip=direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        feet=feet.Floor();
        void Body(int pose)=>CompanionDye.Draw(batch,new DrawData(atlas,feet,new Rectangle(0,pose*28,31,28),light,0,CompanionSpriteLayout.Origin,2f,flip));
        if(stage==5){Body(SettleFrame(age));return;}
        if(stage==3){
            if(age<4||recoilPoses==null)Body(CompanionSpriteLayout.Rise);
            else {
                float lean=MathHelper.SmoothStep(0,1,Math.Clamp((age-4)/5f,0,1))*(1-MathHelper.SmoothStep(0,1,Math.Clamp((age-21)/11f,0,1)));
                int recoilFrame=(int)MathF.Round(lean*(RecoilFrames-1));
                CompanionDye.Draw(batch,new DrawData(recoilPoses,feet-new Vector2(0,22),new Rectangle(0,recoilFrame*RecoilHeight,RecoilWidth,RecoilHeight),light,0,new Vector2(RecoilWidth/2f,RecoilHeight/2f),2f,flip));
            }
            return;
        }
        int pose=CarryFrame(stage,age,cat.Animation.SpriteFrame);
        Body(pose);
        if(pose==CompanionSpriteLayout.BarrelRaise0||barrelTexture==null)return;
        var anchor=BarrelAnchor(pose);
        Vector2 grid=feet-CompanionSpriteLayout.Origin*2;
        // Snap the barrel edge to the body's source grid, including mirroring.
        float left=MathF.Round(anchor.X-barrelTexture.Width/2f);
        if(direction<0)left=CompanionSpriteLayout.Width-left-barrelTexture.Width;
        Vector2 topLeft=grid+new Vector2(left,anchor.Y-barrelTexture.Height)*2;
        batch.Draw(barrelTexture,topLeft,null,light,0,Vector2.Zero,2f,SpriteEffects.None,0);
        Vector2 fuse=topLeft+new Vector2(barrelTexture.Width,10);
        void Rect(Vector2 at,int w,int h,Color color)=>batch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)at.X,(int)at.Y,w,h),color);
        var outline=new Color(49,43,41).MultiplyRGB(light);
        Rect(fuse-new Vector2(2,4),6,4,outline);Rect(fuse-new Vector2(0,8),6,6,outline);
        Rect(fuse-new Vector2(0,4),2,4,new Color(216,183,120).MultiplyRGB(light));Rect(fuse+new Vector2(2,-6),2,4,new Color(216,183,120).MultiplyRGB(light));
        Rect(fuse+new Vector2(0,-12),6,6,new Color(240,121,43));
        Rect(fuse+new Vector2(age%12<6?2:4,-14),2,4,new Color(255,181,69));
        Rect(fuse+new Vector2(2,-10),2,4,new Color(255,239,174));
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace PalicoCompanion.Systems;

// Presentation only: reads the liquid at the rendered cat, never writes tiles,
// navigation, breath, item use or multiplayer authority. No extra packet needed.
public sealed class CompanionDivingVisual {
    public bool InWater { get; private set; }
    public bool Submerged { get; private set; }
    public int Phase { get; private set; }
    public void Update(Projectile cat,bool paused,bool downed) {
        Vector2 feet=cat.Bottom+new Vector2(0,cat.gfxOffY);
        InWater=WaterAt(feet+new Vector2(0,-8));
        Submerged=!downed&&HeadUnderwater(feet,cat.direction);
        if(!Submerged){Phase=0;return;}
        if(paused)return;
        Phase=(Phase+1)%48;
        // Same native dust as Player.CheckDrowning; keep the native rise/pop
        // behavior, at a reduced rate and scale for this smaller character.
        if(!Main.dedServ&&Main.rand.Next(60)==0) {
            Vector2 outlet=feet+new Vector2(16*cat.direction,-33);
            if(WaterAt(outlet-new Vector2(0,8)))
                Dust.NewDust(outlet,2,2,DustID.BreatheBubble,0,0,0,default,.7f);
        }
    }
    internal static bool HeadUnderwater(Vector2 feet,int direction) =>
        WaterAt(feet+new Vector2(2*direction,-24))&&WaterAt(feet+new Vector2(6*direction,-24));
    internal static bool WaterAt(Vector2 point) {
        int x=(int)Math.Floor(point.X/16),y=(int)Math.Floor(point.Y/16);
        if(!WorldGen.InWorld(x,y,1))return false;
        var tile=Main.tile[x,y];
        // Collision.WetCollision's liquid surface calculation, without its
        // global honey/shimmer side effects or center-only entity probe.
        return tile.LiquidType==LiquidID.Water&&tile.LiquidAmount>0&&
            point.Y>=y*16+(256-tile.LiquidAmount)/16f;
    }
    internal static int HeadBob(int pose)=>pose is 5 or 9?-1:pose is 13 or 14 or 15 or 16?1:pose==21?2:0;
    // Native 2-world-pixel cells, aligned with the atlas. Transparent
    // lenses leave every eye/eye-white pixel untouched. The snorkel's lower
    // elbow deliberately reaches the mouth: it is a bitten mouthpiece.
    static readonly string[] Mask={
        "....hhhhhhhh....",
        "...r........r...",
        ".ssr........rss.",
        "...r........r...",
        "...r........r...",
        "....rr.....rr..."
    };
    // Constant 3-cell tube diameter: straight vertical and horizontal runs,
    // joined by a short rounded 90-degree elbow, never a tapered diagonal.
    // The horizontal run sits below the eye whites and meets the lower lip.
    static readonly string[] Snorkel={
        ".....oco",
        ".....obo",
        ".....obo",
        ".....obo",
        ".....obo",
        ".....obo",
        ".....obo",
        ".....obo",
        ".oooobo.",
        "ommbbbo.",
        ".ooooo.."
    };
    public static void DrawGoggles(SpriteBatch batch,Vector2 feet,int pose,int direction,Color light) {
        int bob=HeadBob(pose);
        void Pixel(float px,int py,Color color){
            float left=direction<0?CompanionSpriteLayout.Origin.X-px-1:px-CompanionSpriteLayout.Origin.X;
            var at=feet+new Vector2(left,py+bob-CompanionSpriteLayout.Origin.Y)*2;
            CompanionDye.Draw(batch,new DrawData(TextureAssets.MagicPixel.Value,at,new Rectangle(0,0,1,1),color.MultiplyRGBA(light),0,Vector2.Zero,2,SpriteEffects.None));
        }
        for(int y=0;y<Mask.Length;y++)for(int x=0;x<Mask[y].Length;x++){
            char cell=Mask[y][x];if(cell=='.')continue;
            Color color=cell switch{'h'=>new Color(171,196,184),'r'=>new Color(75,113,124),_=>new Color(38,53,65)};
            Pixel(8+x,12+y,color);
        }
        for(int y=0;y<Snorkel.Length;y++)for(int x=0;x<Snorkel[y].Length;x++){
            char cell=Snorkel[y][x];if(cell=='.')continue;
            Color color=cell switch{'h'=>new Color(147,186,184),'b'=>new Color(75,126,140),'c'=>new Color(206,177,108),'m'=>new Color(60,79,86),_=>new Color(38,47,57)};
            Pixel(17+x,9+y,color);
        }
        // Raised paws pass in front of the mouthpiece. Reuse the existing grip
        // overlay at its exact position, just as held tools do.
        CompanionItemVisual.DrawGrip(batch,feet,pose,direction,light);
        CompanionItemVisual.DrawGrip(batch,feet,pose,direction,light,true);
    }
}

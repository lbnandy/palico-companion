using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// Pixel-space adaptation of native held art to the cat's short, fixed forepaw.
// Cache texture readbacks; never resize art or generate a replacement cat arm.
public sealed class CompanionGrip : ModSystem {
    static readonly Dictionary<(int,int),Vector2> serverOffsets=new();
    static readonly Dictionary<int,Rectangle> serverFrames=new();
    public static Rectangle ItemFrame(int type){if(serverFrames.TryGetValue(type,out var frame))return frame;var item=ContentSamples.ItemsByType[type];return new Rectangle(0,0,Math.Max(1,item.width),Math.Max(1,item.height));}
    public override void Load(){
        if(Mod.FileExists("Assets/ItemFrames.csv"))foreach(var line in System.Text.Encoding.UTF8.GetString(Mod.GetFileBytes("Assets/ItemFrames.csv")).Split('\n')){var c=line.Trim().Split(',');if(c.Length==3)serverFrames[int.Parse(c[0])]=new Rectangle(0,0,int.Parse(c[1]),int.Parse(c[2]));}
        if(Mod.FileExists("Assets/GripOffsets.csv"))foreach(var line in System.Text.Encoding.UTF8.GetString(Mod.GetFileBytes("Assets/GripOffsets.csv")).Split('\n')){
            var c=line.Trim().Split(',');if(c.Length==4&&int.TryParse(c[0],out int type)&&int.TryParse(c[1],out int dir))serverOffsets[(type,dir)]=new(float.Parse(c[2],System.Globalization.CultureInfo.InvariantCulture),float.Parse(c[3],System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    static Vector2 ServerOffset(Projectile p)=> (serverOffsets.TryGetValue((p.type,p.spriteDirection),out var offset)?offset:new Vector2(0,10))*p.scale;
    static readonly Dictionary<(Texture2D,Rectangle,int,int),Vector2> grips=new();
    public override void Unload(){grips.Clear();serverFrames.Clear();serverOffsets.Clear();}
    static Vector2 OpaqueGrip(Texture2D texture,Rectangle frame,Vector2 near) {
        var key=(texture,frame,(int)near.X,(int)near.Y);
        if(grips.TryGetValue(key,out var found))return found;
        var pixels=new Color[frame.Width*frame.Height];texture.GetData(0,frame,pixels,0,pixels.Length);
        Vector2 Closest(Vector2 point) {
            Vector2 result=point;float best=float.MaxValue;
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++) {
                if(pixels[y*frame.Width+x].A<128)continue;
                var candidate=new Vector2(x+.5f,y+.5f);float d=Vector2.DistanceSquared(candidate,point);
                if(d<best){best=d;result=candidate;}
            }
            return result;
        }
        found=Closest(near);
        // Grip a little inside the handle, not a transparent texture corner.
        found=Closest(found+(frame.Size()/2-found).SafeNormalize(Vector2.Zero)*2);
        grips[key]=found;return found;
    }
    static Vector2 Flip(Vector2 p,Rectangle frame,SpriteEffects effects) {
        if((effects&SpriteEffects.FlipHorizontally)!=0)p.X=frame.Width-p.X;
        if((effects&SpriteEffects.FlipVertically)!=0)p.Y=frame.Height-p.Y;
        return p;
    }
    public static Vector2 DrawnGrip(DrawData data) {
        Rectangle frame=data.sourceRect??data.texture.Bounds;
        Vector2 local=OpaqueGrip(data.texture,frame,Flip(data.origin,frame,data.effect));
        return data.position+((Flip(local,frame,data.effect)-data.origin)*data.scale).RotatedBy(data.rotation);
    }
    public static bool ShortStab(Projectile p)=>p.aiStyle==ProjAIStyleID.ShortSword;
    // These vanilla HeldItem branches draw an orb, floating lamp or offset
    // caster, not a handle. In particular Spirit Flame submits a glow layer
    // first: treating its nearest opaque pixel as a grip shifts the whole art.
    internal static bool NativeCastingVisual(Item item)=>item.type is
        ItemID.NebulaArcanum or ItemID.SpiritFlame or ItemID.WeatherPain;
    public static void FitHeld(Projectile p,CompanionNativeUse use) {
        if(!p.active)return;
        // AI_075 assigns a human arm position and UpdatePosition then adds
        // velocity. Replace the arm anchor before movement; beams/real hits
        // inherit the same corrected world position, not a drawing-only shift.
        if(p.type==ProjectileID.SolarWhipSword)p.Center=CompanionSprite.Hand(use.Cat)-p.velocity;
        // Last Prism and other native held projectiles retain their AI position.
        // A floating weapon must not be snapped to an opaque texture pixel.
    }
    public static Vector2 StabGripOffset(Projectile p) {
        if(Main.dedServ)return ServerOffset(p).RotatedBy(p.rotation);
        Main.instance.LoadProjectile(p.type);var texture=TextureAssets.Projectile[p.type].Value;
        var frame=texture.Bounds;
        // Main.DrawProj rotates the diagonal metal shortsword textures by an
        // extra quarter turn. Ruler/Gladius use their native vertical orientation.
        bool diagonal=p.type>=ProjectileID.CopperShortswordStab&&p.type<=ProjectileID.PlatinumShortswordStab;
        var local=OpaqueGrip(texture,frame,diagonal?new Vector2(0,frame.Height):new Vector2(frame.Width/2f,frame.Height));
        var flip=p.spriteDirection<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        float rotation=p.rotation-(diagonal?MathHelper.PiOver4*p.spriteDirection:0);
        return ((Flip(local,frame,flip)-frame.Size()/2)*p.scale).RotatedBy(rotation);
    }
    public static void FitStab(Projectile p,CompanionNativeUse use) {
        if(!p.active||!ShortStab(p))return;
        // A one-pixel wrist motion stays covered by the existing short paw.
        Vector2 wrist=p.velocity.SafeNormalize(Vector2.UnitX)*(float)Math.Sin(Math.Clamp(p.ai[0]/16f,0,1)*Math.PI);
        p.Center=CompanionSprite.Hand(use.Cat)+wrist-StabGripOffset(p);
    }
}

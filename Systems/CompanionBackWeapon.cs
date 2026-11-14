using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Content;

namespace PalicoCompanion.Systems;

// Cosmetic only: item frames and idle/back layers follow native item drawing
// and WeaponOut Lite's pose separation. No item hooks or projectile AI run here.
public sealed class CompanionBackWeapon : ModSystem {
    static readonly Dictionary<(Texture2D,Rectangle),Rectangle> bounds=new();
    static readonly Dictionary<(Texture2D,Rectangle,float),Vector2> silhouettes=new();
    static readonly Dictionary<int,Asset<Texture2D>> stowedTextures=new();
    internal static void RegisterStowedTexture(int type,Asset<Texture2D> texture)=>stowedTextures[type]=texture;
    public override void Unload(){bounds.Clear();silhouettes.Clear();stowedTextures.Clear();}
    internal static (Texture2D Texture,Rectangle Frame) Image(Item item){
        if(stowedTextures.TryGetValue(item.type,out var stowed))return (stowed.Value,stowed.Value.Bounds);
        Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;
        return (texture,Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Bounds);
    }
    internal static bool Visible(CompanionCat cat,CompanionPlayer state) {
        if(!CompanionProfile.Appearance(state).BackWeapon||state.Stowed||state.Vitals.Downed||
            cat.Barrel.Presenting||cat.Barrel.Active)return false;
        var item=state.Gear[0];
        if(item.IsAir||item.damage<=0||item.ammo!=0||item.pick>0||item.axe>0||item.hammer>0||
            item.consumable&&!CompanionWeapons.SafeThrow(item))return false;
        // Match the same buffered pose used by the held-item renderer on clients.
        // Casting with the separate summon slot does not take the main weapon off the back.
        using var pose=new CompanionNativeUse.ReplicaDrawScope(cat.Weapons);
        bool holdingMain=cat.Weapons.Actor.HeldItem.type==item.type;
        if(holdingMain&&cat.Weapons.Busy)return false;
        // Hidden holdouts and returning weapons can outlive itemAnimation.
        foreach(var shot in Main.projectile){
            if(!shot.active||CompanionOwnedShot.Of(shot)!=cat.Weapons)continue;
            if(holdingMain&&shot.whoAmI==cat.Weapons.Actor.heldProj||
                !item.consumable&&item.noUseGraphic&&shot.type==item.shoot&&!shot.minion&&!shot.sentry)return false;
        }
        return true;
    }
    static Rectangle OpaqueBounds(Texture2D texture,Rectangle frame) {
        if(bounds.TryGetValue((texture,frame),out var result))return result;
        var pixels=new Color[frame.Width*frame.Height];texture.GetData(0,frame,pixels,0,pixels.Length);
        int left=frame.Width,top=frame.Height,right=-1,bottom=-1;
        for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){
            if(pixels[y*frame.Width+x].A<16)continue;
            left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
        }
        result=right<left?Rectangle.Empty:new(left,top,right-left+1,bottom-top+1);
        bounds[(texture,frame)]=result;return result;
    }
    internal enum CarryKind { Blade, ShortBlade, Pole, Bow, Rifle, Launcher, Sidearm, Waist }
    internal readonly record struct CarryPose(float Rotation,Vector2 Offset);
    // Keep the small companion / large weapon contrast. Categories choose
    // the carrying pose, while every weapon shares the same silhouette cap.
    internal static readonly Vector2 CarryLimit=new(52,48);
    // Like WeaponOut Lite, classify by native item/projectile behavior before
    // using sprite proportions. Reuse our cached projectile samples; no item
    // hooks or live projectiles are created just to choose a cosmetic pose.
    internal static CarryKind Kind(Item item,Rectangle frame) {
        var shot=CompanionCombatReach.Sample(item);
        // A stack displays one remaining weapon. Small throws sit at the waist;
        // larger throws reuse the back pose instead of individual item offsets.
        if(item.consumable)return Math.Max(frame.Width,frame.Height)*CompanionItemVisual.ItemScale(item)>34?CarryKind.Blade:CarryKind.Waist;
        if(ItemID.Sets.Yoyo[item.type]||shot!=null&&(ProjectileID.Sets.IsAWhip[shot.type]||
            shot.aiStyle is ProjAIStyleID.Yoyo or ProjAIStyleID.Flail or ProjAIStyleID.Boomerang))return CarryKind.Waist;
        if(ItemID.Sets.Spears[item.type]||shot?.aiStyle==ProjAIStyleID.Spear||Item.staff[item.type])return CarryKind.Pole;
        if(item.useStyle is ItemUseStyleID.Rapier or ItemUseStyleID.Thrust||shot?.aiStyle==ProjAIStyleID.ShortSword)return CarryKind.ShortBlade;
        // Reuse native contact-attack metadata for projectile-driven blades.
        // Melee damage/channel alone also describe free-flying knives.
        if(item.useStyle==ItemUseStyleID.Shoot&&item.CountsAsClass(DamageClass.Melee)&&
            (shot?.aiStyle==ProjAIStyleID.HeldProjectile||CompanionCrossMod.Reach(item)!=null))return CarryKind.Blade;
        if(item.useAmmo==AmmoID.Arrow)return frame.Width>frame.Height?CarryKind.Rifle:CarryKind.Bow;
        if(item.useAmmo==AmmoID.Rocket)return CarryKind.Launcher;
        if(item.useStyle==ItemUseStyleID.Shoot&&frame.Width>frame.Height)
            return frame.Width*CompanionItemVisual.ItemScale(item)<=34?CarryKind.Sidearm:CarryKind.Rifle;
        if(item.useStyle is ItemUseStyleID.HoldUp or ItemUseStyleID.Shoot)return CarryKind.Waist;
        return CarryKind.Blade;
    }
    internal static CarryPose Pose(Item item,Rectangle frame) {
        // Angles describe the item's own sprite, not the final silhouette:
        // swords/staves are usually diagonal, guns horizontal and bows upright.
        return Kind(item,frame) switch {
            CarryKind.Pole=>new(-1.35f,new(-15,-24)),
            CarryKind.Bow=>new(-.5f,new(-14,-23)),
            CarryKind.Rifle=>new(-2.27f,new(-13,-24)),
            CarryKind.Launcher=>new(-2.5f,new(-14,-25)),
            CarryKind.ShortBlade=>new(MathHelper.Pi+.35f,new(4,-8)),
            CarryKind.Sidearm=>new(2.05f,new(-13,-15)),
            CarryKind.Waist=>new(.12f,new(-14,-15)),
            // Reverse the ends of a carried sword along the existing diagonal.
            // Non-melee items also use the fallback pose; keep their orientation.
            CarryKind.Blade when !item.consumable&&item.CountsAsClass(DamageClass.Melee)
                =>new(MathHelper.PiOver2,new(-2,-21)),
            _=>new(-MathHelper.PiOver2,new(-13,-23))
        };
    }
    static Vector2 SilhouetteSize(Texture2D texture,Rectangle frame,float rotation){
        var key=(texture,frame,rotation);
        if(silhouettes.TryGetValue(key,out var size))return size;
        var pixels=new Color[frame.Width*frame.Height];texture.GetData(0,frame,pixels,0,pixels.Length);
        float c=MathF.Cos(rotation),s=MathF.Sin(rotation);
        var min=new Vector2(float.MaxValue);var max=new Vector2(float.MinValue);
        for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){
            if(pixels[y*frame.Width+x].A<16)continue;
            var at=new Vector2(x*c-y*s,x*s+y*c);
            min=Vector2.Min(min,at);max=Vector2.Max(max,at);
        }
        // Include the rotated pixel square, but not transparent rectangle
        // corners. Otherwise thin diagonal staves/guns shrink unnecessarily.
        size=max.X<min.X?Vector2.One:max-min+new Vector2(Math.Abs(c)+Math.Abs(s));
        silhouettes[key]=size;return size;
    }
    internal static float Scale(Item item,Texture2D texture,Rectangle frame,CarryPose pose){
        var size=SilhouetteSize(texture,frame,pose.Rotation);
        return Math.Min(CompanionItemVisual.ItemScale(item),Math.Min(CarryLimit.X/size.X,CarryLimit.Y/size.Y));
    }
    internal static void Draw(CompanionCat cat,CompanionPlayer state,Vector2 feet,int bodyPose,Color light) {
        if(!Visible(cat,state))return;
        var item=state.Gear[0];var (texture,frame)=Image(item);
        var opaque=OpaqueBounds(texture,frame);if(opaque.IsEmpty)return;
        var pose=Pose(item,opaque);
        int direction=cat.Projectile.direction;float rotation=pose.Rotation*direction;
        float scale=Scale(item,texture,frame,pose);
        Vector2 origin=opaque.Center.ToVector2();
        if(direction<0)origin.X=frame.Width-origin.X;
        var kind=Kind(item,opaque);
        var offset=pose.Offset+CompanionSpriteLayout.CarryOffset(bodyPose,kind is CarryKind.Waist or CarryKind.Sidearm or CarryKind.ShortBlade);
        var position=(feet+new Vector2(offset.X*direction,offset.Y)).Floor();
        CompanionItemVisual.DrawItem(Main.spriteBatch,item,texture,frame,position,rotation,origin,scale,
            direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None,light,(int)Main.GameUpdateCount);
    }
}

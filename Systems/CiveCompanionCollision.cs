using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using LogSpiralLibrary.CodeLibrary.DataStructures.SequenceStructures.Contents.Melee;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

[JITWhenModsEnabled("CoolerItemVisualEffect")]
internal static class CiveCompanionCollision {
    delegate bool Original(MeleeAction action,Rectangle target);
    static readonly Func<MeleeAction,float> ReadTime;
    static readonly Action<MeleeAction,float> WriteTime;
    static CiveCompanionCollision(){
        var action=Expression.Parameter(typeof(MeleeAction));var time=Expression.Parameter(typeof(float));
        var field=Expression.Field(action,typeof(MeleeAction).GetField("fTimer",BindingFlags.Instance|BindingFlags.NonPublic)!);
        ReadTime=Expression.Lambda<Func<MeleeAction,float>>(field,action).Compile();
        WriteTime=Expression.Lambda<Action<MeleeAction,float>>(Expression.Assign(field,time),action,time).Compile();
    }
    internal static Hook Hook()=>new(typeof(MeleeAction).GetMethod(nameof(MeleeAction.Collide))!,Collide);
    static bool Collide(Original original,MeleeAction action,Rectangle target){
        var use=CompanionNativeUse.Current;
        if(use==null||!ReferenceEquals(action.Owner,use.Actor))return original(action,target);
        // The native collider reads Main.LocalPlayer's weapon texture. Replica
        // scopes normally preserve the human's local identity; this pure geometry
        // call must instead read the actor whose swing is being checked.
        if(!Main.dedServ){int local=Main.myPlayer;try{Main.myPlayer=use.Actor.whoAmI;return original(action,target);}finally{Main.myPlayer=local;}}
        if(!action.Attacktive||!use.State.Player.GetModPlayer<CiveCompanionDefaults>().Textures.TryGetValue(use.Actor.HeldItem.type,out var size))return false;
        return Geometry(action,target,size.ToVector2(),use.Actor.GetAdjustedItemScale(use.Actor.HeldItem));
    }
    // LogSpiralLibrary's base collider gets a diagonal from GetItemVertexes,
    // but that helper requires a GPU texture. Keep the same transforms, five
    // time samples and 48-pixel sweep; the owner supplies only texture dimensions.
    // Native overrides and client/player collision continue through the library.
    internal static bool Geometry(MeleeAction action,Rectangle target,Vector2 textureSize,float itemScale){
        float time=ReadTime(action);
        try{
            for(int i=0;i<5;i++){
                WriteTime(action,time+i*.2f);
                var info=action.StandardInfo;Vector2 origin=action.OffsetOrigin+info.standardOrigin;
                float scale=action.OffsetSize*action.ModifyData.Size*itemScale;
                if(info.VertexStandard.scaler>0)scale*=info.VertexStandard.scaler/textureSize.Length();
                var transform=Matrix.CreateTranslation(-origin.X,origin.Y-1,0)*
                    Matrix.CreateScale(textureSize.X,textureSize.Y,1)*Matrix.CreateRotationZ(info.standardRotation)*
                    Matrix.CreateScale(1,action.Flip?-1:1,1)*Matrix.CreateRotationZ(action.OffsetRotation)*
                    Matrix.CreateScale(1,1/action.KValue,1)*Matrix.CreateRotationZ(action.Rotation)*Matrix.CreateScale(scale);
                Vector2 center=action.OffsetCenter+action.Owner.Center;
                Vector2 start=Vector2.Transform(Vector2.Zero,transform)+center,end=Vector2.Transform(Vector2.One,transform)+center;
                float distance=0;
                if(Collision.CheckAABBvLineCollision(target.TopLeft(),target.Size(),start,end,48,ref distance))return true;
            }
            return false;
        }finally{WriteTime(action,time);}
    }
}

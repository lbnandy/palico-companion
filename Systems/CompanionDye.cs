using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
namespace PalicoCompanion.Systems;
// The native pet renderer uses this same armor shader pipeline. Only companion
// art is submitted here; held items keep their own native drawing/effects.
public static class CompanionDye {
    static Projectile current;
    public static Matrix? WorldMatrixOverride;
    public static bool Accepts(Item item)=>item.IsAir||item.dye>0;
    public static int Shader(CompanionPlayer s)=>s.Dyes[0].IsAir?0:s.Dyes[0].dye;
    public readonly struct Scope:IDisposable {
        readonly Projectile previous;
        public Scope(Projectile cat){previous=current;current=cat;}
        public void Dispose()=>current=previous;
    }
    public static void Draw(SpriteBatch batch,DrawData data){
        var state=current==null?null:Main.player[CompanionCat.OwnerOf(current)].GetModPlayer<CompanionPlayer>();
        int dye=state==null?0:Shader(state);
        if(state!=null&&ReferenceEquals(data.texture,CompanionSprite.Atlas))data.texture=CompanionPalette.Texture(state);
        if(dye==0||batch!=Main.spriteBatch){data.Draw(batch);return;}
        var entity=Main.CurrentDrawnEntity;int prior=Math.Max(0,Main.CurrentDrawnEntityShader);
        var matrix=WorldMatrixOverride??Main.GameViewMatrix.TransformationMatrix;
        try{
            Main.instance.PrepareDrawnEntityDrawing(current,dye,matrix);
            Main.EntitySpriteDraw(data);
        }finally{Main.instance.PrepareDrawnEntityDrawing(entity,prior,matrix);}
    }
    // Caller uses an immediate sprite batch and selects filtering for its UI.
    public static void Portrait(SpriteBatch batch,CompanionPlayer s,Rectangle dest,Rectangle source,Color tint,int? previewColor=null){
        int owner=previewColor.HasValue?256:s.Player.whoAmI,rgb=previewColor??s.HoodColor;
        var texture=CompanionPalette.UiHeadTexture(owner,rgb);
        source=new Rectangle(source.X*2,source.Y*2,source.Width*2,source.Height*2);
        DrawPortraitTexture(batch,s,texture,dest,source,tint);
    }
    static void DrawPortraitTexture(SpriteBatch batch,CompanionPlayer s,Texture2D texture,Rectangle dest,Rectangle source,Color tint){
        int shader=Shader(s);
        if(texture==null)return;
        var data=new DrawData(texture,new Vector2(dest.X,dest.Y),source,tint,0,Vector2.Zero,new Vector2(dest.Width/(float)source.Width,dest.Height/(float)source.Height),SpriteEffects.None);
        try{if(shader>0)GameShaders.Armor.Apply(shader,s.Player,data);data.Draw(batch);}
        finally{if(shader>0)Main.pixelShader.CurrentTechnique.Passes[0].Apply();}
    }
}

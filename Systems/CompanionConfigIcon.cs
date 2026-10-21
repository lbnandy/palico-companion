using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent.UI.Chat;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;

namespace PalicoCompanion.Systems;

// Config headers already use Terraria's rich-text renderer. A dedicated tag
// supplies our portrait without adding a dummy item or replacing native UI.
public sealed class CompanionConfigIcon:ModSystem {
    const string Tag="palicohead";
    public override void Load(){if(!Main.dedServ){ChatManager.Register<HeadTag>(Tag);ChatManager.Register<ItemTag>("palicoitem");}}
    // ChatManager has no public unregister API. Release our handler through
    // its public registration API so the unloaded mod assembly is not retained.
    public override void Unload(){if(!Main.dedServ){ChatManager.Register<PlainTagHandler>(Tag);ChatManager.Register<PlainTagHandler>("palicoitem");}}
    public sealed class ItemTag:ITagHandler {
        public TextSnippet Parse(string text,Color baseColor=default,string options=null)=>
            ItemID.Search.TryGetId(text,out int type)?new ItemSnippet(type){Color=baseColor,DeleteWhole=true}:new TextSnippet(text);
    }
    sealed class ItemSnippet(int type):TextSnippet {
        public override bool UniqueDraw(bool justCheckingString,out Vector2 size,SpriteBatch sb,Vector2 position=default,Color color=default,float scale=1f){
            size=new Vector2(28,24)*scale;
            if(!justCheckingString&&(color.R!=0||color.G!=0||color.B!=0)){
                Main.instance.LoadItem(type);var texture=TextureAssets.Item[type].Value;
                var frame=Main.itemAnimations[type]?.GetFrame(texture)??texture.Bounds;
                float zoom=24f/System.Math.Max(frame.Width,frame.Height)*scale;
                sb.Draw(texture,position+new Vector2(12,8)*scale,frame,Color.White*(color.A/255f),0,frame.Size()/2,zoom,SpriteEffects.None,0);
            }
            return true;
        }
        public override float GetStringLength(DynamicSpriteFont font)=>28*Scale;
    }
    public sealed class HeadTag:ITagHandler {
        public TextSnippet Parse(string text,Color baseColor=default,string options=null)=>new HeadSnippet{Color=baseColor,DeleteWhole=true};
    }
    sealed class HeadSnippet:TextSnippet {
        public override bool UniqueDraw(bool justCheckingString,out Vector2 size,SpriteBatch spriteBatch,Vector2 position=default,Color color=default,float scale=1f){
            size=new Vector2(28,24)*scale;
            var texture=CompanionSprite.HeadUi;
            // Native item snippets likewise skip shadow passes; the art has
            // its own outline. The header always uses the default mod colours.
            if(!justCheckingString&&texture!=null&&(color.R!=0||color.G!=0||color.B!=0)){
                float zoom=24f/texture.Width*scale;
                var at=position+new Vector2(12,8)*scale-new Vector2(texture.Width,texture.Height)*zoom/2;
                spriteBatch.Draw(texture,at,null,Color.White*(color.A/255f),0,Vector2.Zero,zoom,SpriteEffects.None,0);
            }
            return true;
        }
        public override float GetStringLength(DynamicSpriteFont font)=>28*Scale;
    }
}

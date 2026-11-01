using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using ReLogic.Content;
using Microsoft.Xna.Framework;
namespace PalicoCompanion.Systems;
// Sprite frames use a 31x28 source grid, drawn at twice their native size.
public sealed class CompanionSprite : ModSystem {
    public static Texture2D Atlas { get; private set; }
    public static Texture2D Head { get; private set; }
    internal static Texture2D HeadUi { get; private set; }
    public static Texture2D Kite { get; private set; }
    internal static Texture2D CartCat { get; private set; }
    internal static Texture2D Cart { get; private set; }
    public const int Width=CompanionSpriteLayout.Width,Height=CompanionSpriteLayout.Height,Frames=CompanionSpriteLayout.Frames;
    // Keep every authored pose intact, including flight, swimming and item use.
    // Hands and facial expressions are separate overlays; the body is never spliced.
    internal static void DrawBody(SpriteBatch batch,Vector2 feet,int frame,int direction,Color color){
        var effect=direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        var source=new Rectangle(0,frame*Height,Width,Height);
        CompanionDye.Draw(batch,new Terraria.DataStructures.DrawData(Atlas,feet,source,color,0,CompanionSpriteLayout.Origin,CompanionSpriteLayout.Scale,effect));
    }
    public static Vector2 Hand(Projectile cat,bool offHand=false) {
        int frame=cat.ModProjectile is CompanionCat c?c.Animation.SpriteFrame:CompanionSpriteLayout.Idle;
        var offset=(offHand?CompanionSpriteLayout.OffHand:CompanionSpriteLayout.MainHand)[frame];
        return cat.Bottom+new Vector2(offset.X*(cat.direction<0?-1:1),offset.Y+cat.gfxOffY);
    }
    public override void PostSetupContent() {
        if(Main.dedServ)return;
        Main.QueueMainThreadAction(()=> {
            Atlas=ModContent.Request<Texture2D>("PalicoCompanion/Assets/Palico",AssetRequestMode.ImmediateLoad).Value;
            Head=ModContent.Request<Texture2D>("PalicoCompanion/Assets/Palico-head",AssetRequestMode.ImmediateLoad).Value;
            // Native UI art commonly stores each visible pixel as a 2x2 block.
            // Duplicate exactly before linear filtering; do not redraw or blur
            // the head, and do not allocate textures during drawing.
            var headPixels=new Color[Head.Width*Head.Height];Head.GetData(headPixels);
            int uiWidth=Head.Width*2,uiHeight=Head.Height*2;
            var uiPixels=new Color[uiWidth*uiHeight];
            for(int y=0;y<uiHeight;y++)for(int x=0;x<uiWidth;x++)uiPixels[y*uiWidth+x]=headPixels[(y/2)*Head.Width+x/2];
            HeadUi=new Texture2D(Main.graphics.GraphicsDevice,uiWidth,uiHeight);HeadUi.SetData(uiPixels);
            Kite=ModContent.Request<Texture2D>("PalicoCompanion/Assets/FlightKite",AssetRequestMode.ImmediateLoad).Value;
            CartCat=ModContent.Request<Texture2D>("PalicoCompanion/Assets/Palico-cart-cat",AssetRequestMode.ImmediateLoad).Value;
            Cart=ModContent.Request<Texture2D>("PalicoCompanion/Assets/Palico-cart",AssetRequestMode.ImmediateLoad).Value;
            if(CartCat.Width!=64||CartCat.Height!=192||Cart.Width!=64||Cart.Height!=192)
                throw new System.InvalidOperationException("Cat cart must contain six aligned 64x32 frames.");
            if(Kite.Width!=31||Kite.Height!=31)throw new System.InvalidOperationException("Flight kite must use its native 31x31 pixel grid.");
            if(Atlas.Width!=Width||Atlas.Height!=Height*Frames)
                throw new System.InvalidOperationException("Palico atlas dimensions do not match frame layout.");
            CompanionBarrelVisual.Load();
        });
    }
    public override void Unload(){CompanionBarrelVisual.Unload();var owned=HeadUi;HeadUi=null;Atlas=null;Head=null;Kite=null;CartCat=null;Cart=null;if(owned!=null)Main.QueueMainThreadAction(owned.Dispose);}
}

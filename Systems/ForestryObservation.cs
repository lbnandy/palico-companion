using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Observe the native tool hit, not held axe/cursor proximity. Companion tool
// proxies are excluded, so helping cannot generate a new owner instruction.
public sealed class ForestryObservation : ModSystem {
    public override void Load()=>On_Player.ItemCheck_UseMiningTools_ActuallyUseMiningTool+=Hit;
    public override void Unload()=>On_Player.ItemCheck_UseMiningTools_ActuallyUseMiningTool-=Hit;
    static void Hit(On_Player.orig_ItemCheck_UseMiningTools_ActuallyUseMiningTool orig,Player p,Item item,out bool walls,int x,int y) {
        Point root=default;
        bool observe=Main.netMode!=NetmodeID.Server&&p.whoAmI==Main.myPlayer&&p.active&&!p.dead&&
            CompanionNativeUse.Current==null&&item.axe>0&&!p.mouseInterface&&ForestryRoot(x,y,out root);
        // Send before KillTile's vanilla packet: the server can validate a real
        // standing tree even if this very swing is the owner's final blow.
        if(observe){
            if(CompanionNetwork.Client)CompanionNetwork.ChopAt(x,y);
            else p.GetModPlayer<CompanionPlayer>().Trees.Assist(p,root);
        }
        orig(p,item,out walls,x,y);
    }
    static bool ForestryRoot(int x,int y,out Point root)=>TreeAssist.FindRoot(x,y,out root);
}

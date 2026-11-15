using System;
using CalamityMod.Items.Accessories;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;

// These native auras target Main.LocalPlayer from another team member's
// equipment update. Keep their original buffs, cadence and stacking rules.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityTeamAccessories:ModSystem {
    public override void Load(){
        Bind<Affliction>();
        Bind<TrinketofChi>();
    }
    static void Bind<T>() where T:ModItem=>MonoModHooks.Add(typeof(T).GetMethod(nameof(ModItem.UpdateAccessory)),
        (Action<Action<T,Player,bool>,T,Player,bool>)Share);
    static void Share<T>(Action<T,Player,bool> original,T item,Player actor,bool hidden) where T:ModItem {
        var use=CompanionNativeUse.Current;
        var recipient=CompanionNativeUse.HumanPlayer;
        if(use?.Actor!=actor||!use.Present||Main.dedServ||recipient is not {active:true,dead:false}){
            original(item,actor,hidden);return;
        }
        int local=Main.myPlayer,actorTeam=actor.team,recipientTeam=recipient.team;
        try{
            Main.myPlayer=recipient.whoAmI;
            actor.team=use.State.Player.team;
            // Owner and companion are allies even without selecting a team.
            // Only this aura call sees that relation; never change saved teams
            // or let unaffiliated players receive another owner's aura.
            if(ReferenceEquals(recipient,use.State.Player)&&recipientTeam==0)actor.team=recipient.team=1;
            original(item,actor,hidden);
        }finally{Main.myPlayer=local;actor.team=actorTeam;recipient.team=recipientTeam;}
    }
}

using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using CalamityMod;

namespace PalicoCompanion.Systems;

// Calamity's global extra-AI does not carry these native attack flags.
// Companion attacks originate on the server, so peers need the flags for
// native projectile AI and visuals without recomputing the owner's stealth.
[ExtendsFromMod("CalamityMod")]
[JITWhenModsEnabled("CalamityMod")]
public sealed class CalamityCompanionStealth:GlobalProjectile {
    public override void SendExtraAI(Projectile shot,BitWriter bits,BinaryWriter writer){
        bool owned=CompanionOwnedShot.Of(shot)!=null;bits.WriteBit(owned);
        if(!owned)return;
        var state=shot.Calamity();
        bits.WriteBit(state.stealthStrike);bits.WriteBit(state.stealthStrikeSubProjectile);
    }
    public override void ReceiveExtraAI(Projectile shot,BitReader bits,BinaryReader reader){
        if(!bits.ReadBit())return;
        bool strike=bits.ReadBit(),child=bits.ReadBit();
        if(!CompanionNetwork.Client)return;
        var state=shot.Calamity();
        state.stealthStrike=strike;state.stealthStrikeSubProjectile=child;
    }
}

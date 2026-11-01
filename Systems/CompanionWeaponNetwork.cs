using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace PalicoCompanion.Systems;
internal static partial class CompanionNetwork {
    const byte NativeWeaponEffect=47;
    internal static void WeaponEffect(CompanionNativeUse use,int item,byte effect,byte info,float pitch){
        if(!Server)return;
        var p=Packet(NativeWeaponEffect);p.Write((byte)use.State.Player.whoAmI);
        p.Write(((CompanionCat)use.Cat.ModProjectile).NetworkInstance);
        p.Write(item);p.Write(effect);p.Write(info);p.Write(pitch);p.Send();
    }
    static void ReadWeaponEffect(BinaryReader r){
        int owner=r.ReadByte();ulong instance=r.ReadUInt64();int item=r.ReadInt32();byte effect=r.ReadByte(),info=r.ReadByte();
        float pitch=r.ReadSingle();if(!float.IsFinite(pitch))return;
        if(owner>=Main.maxPlayers||item<=0||item>=ItemLoader.ItemCount)return;
        var state=Main.player[owner].GetModPlayer<CompanionPlayer>();
        if(state.Cat?.ModProjectile is not CompanionCat cat||cat.NetworkInstance!=instance)return;
        cat.Weapons.BindReplica(state.Player,state,cat.Projectile);
        CompanionCrossMod.WeaponEffect?.Invoke(cat.Weapons,item,effect,info,pitch);
    }
}

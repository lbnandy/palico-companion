using System;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Cosmetic session data only. Never imports inventory, vitals, permissions or
// work commands from a client. Real gameplay state needs authoritative transactions.
internal readonly record struct CompanionProfile(string Name,bool Bonded,bool Stowed) {
    internal static CompanionProfile Capture(CompanionPlayer s)=>new(s.CatName,s.Bonded,s.Stowed);
    internal void Write(BinaryWriter w) {
        byte[] name=Encoding.UTF8.GetBytes(Name??"");
        if(name.Length>256)name=Encoding.UTF8.GetBytes("呆猫");
        w.Write((ushort)name.Length);w.Write(name);
        w.Write((byte)((Bonded?1:0)|(Stowed?2:0)));
    }
    internal static CompanionProfile Read(BinaryReader r) {
        int length=r.ReadUInt16();if(length>256)throw new InvalidDataException("Profile name too long");
        var bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
        string name=new UTF8Encoding(false,true).GetString(bytes);
        byte flags=r.ReadByte();if((flags&~3)!=0)throw new InvalidDataException("Unknown profile flags");
        // Share the existing UI's name sanitation without touching a real player.
        var cleaned=new CompanionPlayer();
        if(!CompanionNaming.Rename(cleaned,name))throw new InvalidDataException("Empty profile name");
        return new(cleaned.CatName,(flags&1)!=0,(flags&2)!=0);
    }
}

internal static class CompanionProfileNet {
    const byte ProfilePacket=1,Version=1;
    internal static void Send(CompanionPlayer s,int toWho=-1,int ignore=-1) {
        if(Main.netMode==NetmodeID.SinglePlayer)return;
        if(Main.netMode==NetmodeID.MultiplayerClient&&s.Player.whoAmI!=Main.myPlayer)return;
        if(Main.netMode==NetmodeID.Server&&!s.HasNetworkProfile)return;
        var p=s.Mod.GetPacket();p.Write(ProfilePacket);p.Write(Version);p.Write((byte)s.Player.whoAmI);
        (Main.netMode==NetmodeID.Server?s.NetworkProfile:CompanionProfile.Capture(s)).Write(p);
        p.Send(toWho,ignore);
    }
    internal static void Receive(BinaryReader r,int sender) {
        try {
            if(r.ReadByte()!=ProfilePacket||r.ReadByte()!=Version)return;
            int owner=r.ReadByte();
            var profile=CompanionProfile.Read(r);
            // Bind identity to the actual connection, never trust a claimed owner.
            // Initial ModPlayer sync precedes Player.active during the handshake.
            if(owner>=Main.maxPlayers||Main.netMode==NetmodeID.SinglePlayer||
                Main.netMode==NetmodeID.Server&&sender!=owner)return;
            var state=Main.player[owner].GetModPlayer<CompanionPlayer>();
            state.NetworkProfile=profile;state.HasNetworkProfile=true;
            if(Main.netMode==NetmodeID.Server)Send(state,-1,sender);
        } catch(Exception ex) when(ex is IOException or DecoderFallbackException) {
            // Truncated/malformed mod data cannot partially apply a profile.
        }
    }
}

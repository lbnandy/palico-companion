using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace PalicoCompanion.Systems;

// ItemDefinition supplies the native searchable item picker and stable mod names.
// Resolve tile exclusions from placeable items, so ore/gem mod IDs need no strings.
internal static class CompanionExclusions {
    internal static void Apply(CompanionConfig config,CompanionPlayer state){
        state.ExcludedMining.Clear();state.ExcludedPickup.Clear();
        foreach(var item in (config.MiningBlacklist??new()).Take(256))if(item.Type>0)state.ExcludedMining.Add(item.Type);
        foreach(var item in (config.PickupBlacklist??new()).Take(256))if(item.Type>0)state.ExcludedPickup.Add(item.Type);
    }
    internal static void Write(BinaryWriter writer,CompanionPlayer state){
        foreach(var set in new[]{state.ExcludedMining,state.ExcludedPickup}){
            writer.Write((ushort)set.Count);foreach(int type in set.OrderBy(x=>x))writer.Write(type);
        }
    }
    internal static void Read(BinaryReader reader,CompanionPlayer state){
        foreach(var set in new[]{state.ExcludedMining,state.ExcludedPickup}){
            int count=reader.ReadUInt16();if(count>256)throw new InvalidDataException("Too many companion exclusions");
            set.Clear();for(int n=0;n<count;n++){int type=reader.ReadInt32();if(type>=0)set.Add(type);}
        }
    }
    static int GemDrop(int type,int frame)=>type switch {
        TileID.Amethyst=>ItemID.Amethyst,TileID.Topaz=>ItemID.Topaz,TileID.Sapphire=>ItemID.Sapphire,
        TileID.Emerald=>ItemID.Emerald,TileID.Ruby=>ItemID.Ruby,TileID.Diamond=>ItemID.Diamond,
        // Vanilla KillTile_GetItemDrops: all exposed gem colors share a tile ID.
        TileID.ExposedGems=>(frame/18) switch{0=>ItemID.Amethyst,1=>ItemID.Topaz,2=>ItemID.Sapphire,3=>ItemID.Emerald,4=>ItemID.Ruby,5=>ItemID.Diamond,6=>ItemID.Amber,_=>0},
        _=>0
    };
    internal static bool Mine(Player player,Point cell){
        var blocked=player.GetModPlayer<CompanionPlayer>().ExcludedMining;if(blocked.Count==0)return true;
        var tile=Main.tile[cell];int gem=GemDrop(tile.TileType,tile.TileFrameX);
        if(gem!=0)return !blocked.Contains(gem);
        return !blocked.Any(type=>ContentSamples.ItemsByType.TryGetValue(type,out var sample)&&sample.createTile==tile.TileType);
    }
    // A provider without a per-tile filter must not chain across a shared tile
    // type containing an excluded variant. Normal individual mining still works.
    internal static bool CanChain(Player player,int tile)=>!player.GetModPlayer<CompanionPlayer>().ExcludedMining.Any(type=>
        type==GemDrop(tile,0)||ContentSamples.ItemsByType.TryGetValue(type,out var sample)&&sample.createTile==tile);
}

using System;
using System.IO;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// Owner preferences, sent with the existing preference message. These are
// carrying targets, never a limit on using or manually giving resources.
public readonly record struct CompanionStock(int Torches,int Glowsticks,int Platforms,int Ropes,int Healing,int Mana,int Ammo,int Acorns,int Bait) {
    public static CompanionStock Default=>new(30,20,30,30,10,10,200,20,20);
    public CompanionStock Bounded()=>new(Math.Clamp(Torches,0,999),Math.Clamp(Glowsticks,0,999),Math.Clamp(Platforms,0,999),Math.Clamp(Ropes,0,999),Math.Clamp(Healing,0,99),Math.Clamp(Mana,0,99),Math.Clamp(Ammo,0,9999),Math.Clamp(Acorns,0,999),Math.Clamp(Bait,0,999));
    public static CompanionStock Config {get{var c=ModContent.GetInstance<CompanionConfig>();return new CompanionStock(c.StockTorches,c.StockGlowsticks,c.StockPlatforms,c.StockRopes,c.StockHealing,c.StockMana,c.StockAmmo,c.StockAcorns,c.StockBait).Bounded();}}
    public static CompanionStock For(CompanionPlayer s)=>CompanionNetwork.Server?s.NetStock:Config;
    public void Write(BinaryWriter w){var s=Bounded();foreach(int n in new[]{s.Torches,s.Glowsticks,s.Platforms,s.Ropes,s.Healing,s.Mana,s.Ammo,s.Acorns,s.Bait})w.Write((ushort)n);}
    public static CompanionStock Read(BinaryReader r)=>new CompanionStock(r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16(),r.ReadUInt16()).Bounded();
}

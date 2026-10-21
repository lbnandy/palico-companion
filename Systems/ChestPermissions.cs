using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
namespace PalicoCompanion.Systems;

// Permissions belong to this character's companion and remain separate per world.
public sealed class ChestPermissions {
    readonly HashSet<(string world,int x,int y)> entries=new();
    static string World=>Main.ActiveWorldFileData.UniqueId.ToString();
    internal void Write(System.IO.BinaryWriter w){w.Write(entries.Count);foreach(var e in entries){w.Write(e.world);w.Write(e.x);w.Write(e.y);}}
    internal static ChestPermissions Read(System.IO.BinaryReader r){var p=new ChestPermissions();int n=r.ReadInt32();if(n<0||n>10000)throw new System.IO.InvalidDataException("Chest permission count");for(int i=0;i<n;i++)p.Add(r.ReadString(),r.ReadInt32(),r.ReadInt32());return p;}
    public bool Allows(int x,int y)=>entries.Contains((World,x,y));
    internal IEnumerable<Microsoft.Xna.Framework.Point> Points=>entries.Where(e=>e.world==World).Select(e=>new Microsoft.Xna.Framework.Point(e.x,e.y));
    public void Grant(int x,int y)=>entries.Add((World,x,y));
    public void Revoke(int x,int y)=>entries.Remove((World,x,y));
    public void Clear()=>entries.Clear();
    public IEnumerable<Chest> Available()=>entries.Where(e=>e.world==World).Select(e=>Logistics.At(e.x,e.y)).Where(c=>c!=null);
    public List<TagCompound> Save()=>entries.OrderBy(e=>e.world).ThenBy(e=>e.x).ThenBy(e=>e.y)
        .Select(e=>new TagCompound{{"world",e.world},{"x",e.x},{"y",e.y}}).ToList();
    public void Load(TagCompound tag){
        entries.Clear();
        if(tag.ContainsKey("chestPermissions")){
            foreach(var e in tag.GetList<TagCompound>("chestPermissions"))
                Add(e.GetString("world"),e.GetInt("x"),e.GetInt("y"));
        }else if(tag.ContainsKey("chestX")&&tag.ContainsKey("chestY"))
            Add(tag.GetString("chestWorld"),tag.GetInt("chestX"),tag.GetInt("chestY"));
    }
    void Add(string world,int x,int y){if(!string.IsNullOrEmpty(world)&&x>=0&&y>=0)entries.Add((world,x,y));}
}

// A newly placed chest at the same coordinates must not inherit permission.
public sealed class ChestPermissionLifetime:ModSystem {
    public override void Load()=>On_Chest.DestroyChest+=Destroy;
    public override void Unload()=>On_Chest.DestroyChest-=Destroy;
    bool Destroy(On_Chest.orig_DestroyChest orig,int x,int y){
        bool removed=orig(x,y);
        if(removed)DiscoveryChests.Forget(x,y);
        if(removed&&!Main.gameMenu)
            foreach(var player in Main.player)
                if(player.active){var s=player.GetModPlayer<CompanionPlayer>();s.Chests.Revoke(x,y);if(s.Storage.Active&&s.Storage.Target==new Microsoft.Xna.Framework.Vector2(x*16+16,y*16+16))s.Storage.Cancel();}
        return removed;
    }
}

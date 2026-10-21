using System;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;

// A projectile slot/identity may be reused. A server-issued lifetime token
// distinguishes its replacement from a delayed update for the old companion.
public sealed class CompanionRoster:ModSystem {
    static ulong serial;
    static readonly ulong[] expected=new ulong[256],stamp=new ulong[256],highest=new ulong[256];
    static readonly bool[] known=new bool[256];
    internal static ulong Next()=>++serial;
    internal static bool Retired(int owner,ulong token)=>known[owner]&&(expected[owner]==0?token<=highest[owner]:token<expected[owner]);
    public override void OnWorldLoad(){serial=0;Array.Clear(expected);Array.Clear(stamp);Array.Clear(highest);Array.Clear(known);}
    internal static void Confirm(int owner,ulong token,ulong time){
        if(!CompanionNetwork.Client||owner<0||owner>=Main.maxPlayers||time<stamp[owner])return;
        stamp[owner]=time;expected[owner]=token;highest[owner]=Math.Max(highest[owner],token);known[owner]=true;Prune(owner);
    }
    internal static bool Observe(CompanionCat cat,ulong time){
        int owner=cat.OwnerIndex;ulong token=cat.NetworkInstance;
        if(token==0||known[owner]&&token!=expected[owner]&&(time<stamp[owner]||time==stamp[owner]&&token<expected[owner])){cat.Projectile.active=false;return false;}
        if(!known[owner]||time>=stamp[owner]){stamp[owner]=time;expected[owner]=token;highest[owner]=Math.Max(highest[owner],token);known[owner]=true;}
        Prune(owner,cat.Projectile);return cat.Projectile.active;
    }
    static void Prune(int owner,Projectile preferred=null){
        foreach(var p in Main.projectile)if(p.active&&p.ModProjectile is CompanionCat c&&c.OwnerIndex==owner){
            if(!Main.player[owner].active||known[owner]&&(expected[owner]==0||c.NetworkInstance!=expected[owner])||preferred!=null&&p!=preferred)
                p.active=false; // Replica retirement must not send a Kill to the server.
        }
    }
    public override void PostUpdateEverything(){if(CompanionNetwork.Client&&!Main.gameMenu)for(int i=0;i<Main.maxPlayers;i++)if(known[i])Prune(i);}
}

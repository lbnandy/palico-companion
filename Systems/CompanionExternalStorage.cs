using Microsoft.Xna.Framework;
using Terraria;
namespace PalicoCompanion.Systems;

// A storage adapter owns its native permissions, transactions and refreshes.
// The shared scheduler only approaches the selected physical access point.
internal interface ICompanionStorageTarget {
    Vector2 Center {get;}
    bool Valid(CompanionPlayer state);
    bool HasWork(CompanionPlayer state);
    (int Stored,int Supplied) Transfer(CompanionPlayer state);
}
internal interface ICompanionStorageProvider {
    ICompanionStorageTarget Find(CompanionPlayer state,Projectile cat);
    bool Open(out Point access,out Point permission,out Rectangle button);
    bool Permission(Player player,Point access,out Point permission);
}

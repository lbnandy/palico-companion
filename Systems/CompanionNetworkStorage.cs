using Microsoft.Xna.Framework;
namespace PalicoCompanion.Systems;
internal static partial class CompanionNetwork {
    const byte ExternalStorageAccess=32;
    internal static void ExternalPermission(CompanionPlayer state,Point access,bool grant){
        if(Client){var packet=Packet(ExternalStorageAccess);packet.Write(access.X);packet.Write(access.Y);packet.Write(grant);packet.Send();return;}
        if(CompanionCrossMod.Storage?.Permission(state.Player,access,out var point)!=true)return;
        if(grant)state.Chests.Grant(point.X,point.Y);else state.Chests.Revoke(point.X,point.Y);
        state.Storage.Cancel();if(Server)Send(state);
    }
}

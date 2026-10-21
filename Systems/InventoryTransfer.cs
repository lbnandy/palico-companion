using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;
public static class InventoryTransfer
{
    // Mutates source in place. Never discard a remainder; empty slots preserve full Item data.
    public static int Move(Item source, Item[] destination, int limit = int.MaxValue) {
        if (source == null || source.IsAir || limit <= 0) return 0;
        int before = source.stack;
        for (int i=0; i<destination.Length && source.stack>0 && before-source.stack<limit; i++) {
            Item target=destination[i];
            if (target == null || target.IsAir || target.type != source.type || target.stack >= target.maxStack) continue;
            if (!ItemLoader.CanStack(target, source)) continue;
            ItemLoader.StackItems(target, source, out _, false, System.Math.Min(source.stack, System.Math.Min(limit-(before-source.stack), target.maxStack-target.stack)));
        }
        for (int i=0; i<destination.Length && source.stack>0 && before-source.stack<limit; i++) {
            if (destination[i] != null && !destination[i].IsAir) continue;
            destination[i]=ItemLoader.TransferWithLimit(source, System.Math.Min(source.maxStack, limit-(before-source.stack)));
        }
        int moved=before-System.Math.Max(0,source.stack);
        if (source.stack<=0) source.TurnToAir();
        return moved;
    }
    public static Item[] Empty(int count) {
        var result=new Item[count]; for(int i=0;i<count;i++) result[i]=new Item(); return result;
    }
    public static bool HasRoom(Item source, Item[] bag) {
        foreach(var item in bag) if(item.IsAir || (item.type==source.type && item.stack<item.maxStack && ItemLoader.CanStack(item,source))) return true;
        return false;
    }
}

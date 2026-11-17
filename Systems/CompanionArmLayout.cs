using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;

namespace PalicoCompanion.Systems;

// Installed claws are real items. Sparse ItemIO storage preserves mod data and
// unloaded items; old preview layout preferences never owned items.
internal sealed class CompanionArmLayout {
    internal const byte Group=8;
    internal readonly Item[] Items=InventoryTransfer.Empty(169);
    internal int Count=>Items.Count(i=>!i.IsAir);
    internal TagCompound Save()=>new(){["installed"]=Items.Select((item,key)=>(item,key)).Where(p=>!p.item.IsAir)
        .Select(p=>new TagCompound{["position"]=p.key,["item"]=ItemIO.Save(p.item)}).ToList()};
    internal void Load(TagCompound tag){
        foreach(var entry in tag.GetList<TagCompound>("installed")){
            int key=entry.GetInt("position");
            if(key is >=0 and <169&&key!=84)Items[key]=ItemIO.Load(entry.GetCompound("item"));
        }
    }
    internal void Write(BinaryWriter w){
        w.Write((byte)Count);
        for(int key=0;key<Items.Length;key++)if(!Items[key].IsAir){w.Write((byte)key);ItemIO.Send(Items[key],w,true,true);}
    }
    internal static CompanionArmLayout Read(BinaryReader r){
        var result=new CompanionArmLayout();int count=r.ReadByte();
        if(count>7)throw new InvalidDataException("Mechanical claw capacity");
        for(int i=0;i<count;i++){
            int key=r.ReadByte();var item=ItemIO.Receive(r,true,true);
            if(key>=169||key==84||!result.Items[key].IsAir||item.IsAir||item.stack!=1)throw new InvalidDataException("Mechanical claw slot");
            result.Items[key]=item;
        }
        return result;
    }
    // Journey's cursor rules: install one, take one back, or swap a single claw.
    // The existing inventory receipt handles escrow and duplicate packets.
    internal static InventoryOutcome Click(Item[] items,int key,ref Item cursor){
        var installed=items[key];
        if(cursor.IsAir){if(installed.IsAir)return default;cursor=installed;items[key]=new Item();}
        else if(installed.IsAir){items[key]=cursor.Clone();items[key].stack=1;if(--cursor.stack<=0)cursor=new Item();}
        else if(cursor.type==installed.type&&ItemLoaderCanStack(cursor,installed)&&cursor.stack<cursor.maxStack){
            Terraria.ModLoader.ItemLoader.TryStackItems(cursor,installed,out _);
            if(installed.IsAir)items[key]=new Item();
        }
        else if(cursor.type!=installed.type&&cursor.stack==1){items[key]=cursor;cursor=installed;}
        else return default;
        return new(InventoryCue.Grab);
    }
    static bool ItemLoaderCanStack(Item a,Item b)=>Terraria.ModLoader.ItemLoader.CanStack(a,b);
}

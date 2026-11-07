using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;

namespace PalicoCompanion.Systems;

// Vanilla EquipmentLoadout ownership model: inactive sets hold real items;
// the active set is empty because its items live in the existing equipment slots.
internal sealed class CompanionLoadouts {
    internal int Current;
    internal bool Independent;
    const int Count=16;
    internal readonly Item[][] Sets={InventoryTransfer.Empty(Count),InventoryTransfer.Empty(Count),InventoryTransfer.Empty(Count)};
    internal bool Switch(CompanionPlayer s,int index,out string message) {
        message=CompanionText.Message("CompanionLoadouts.Invalid");if(index<0||index>=3)return false;
        if(index==Current){message=CompanionText.Message("CompanionLoadouts.AlreadyActive" ,index+1);return false;}
        var incoming=Sets[index];
        // Preserve items from a stronger world's extra slot. The existing
        // ReturnLocked policy returns them to the bag and disables their effects.
        Sets[Current]=new[]{s.Gear[0],s.Training.Slots[0],s.Training.Slots[1],s.Training.Slots[2],s.Training.Slots[3],CompanionWeapons.Summon(s.Gear),s.Gear[1],s.Gear[2],s.Gear[3],s.Gear[5],s.Gear[6],s.Gear[7],s.Gear[8],s.Gear[9],s.Gear[10],s.Gear[11]};
        for(int slot=0;slot<incoming.Length;slot++)Equip(s,slot,incoming[slot]);
        // Keep real item ownership in exactly one set. Empty slots borrow a
        // shared item, preferring the set we just left over other inactive sets.
        for(int slot=0;slot<incoming.Length;slot++){
            if(!incoming[slot].IsAir)continue;
            for(int offset=0;offset<Sets.Length;offset++){
                int source=(Current+offset)%Sets.Length;
                var item=Sets[source][slot];
                if(source==index||item.IsAir||!item.favorited)continue;
                byte group=(byte)(slot==0||slot>=5?1:3);
                int indexInGroup=slot>=6?(slot<9?slot-5:slot-4):slot==5?CompanionWeapons.SummonSlot:slot==0?0:slot-1;
                if(!CompanionInventoryOperations.Accepts(s,item,group,indexInGroup))continue;
                incoming[slot]=item;Equip(s,slot,item);Sets[source][slot]=new Item();break;
            }
        }
        Sets[index]=InventoryTransfer.Empty(Count);Current=index;
        s.Training.ReturnLocked(s);s.Training.ReconcileEffects(s);
        message=CompanionText.Message("CompanionLoadouts.Switched" ,index+1);return true;
    }
    static void Equip(CompanionPlayer s,int slot,Item item){
        if(slot==0)s.Gear[0]=item;
        else if(slot==5)s.Gear[CompanionWeapons.SummonSlot]=item;
        else if(slot>=6)s.Gear[slot<9?slot-5:slot-4]=item;
        else s.Training.Slots[slot-1]=item;
    }
    internal TagCompound SaveData()=>new(){["format"]=3,["independent"]=Independent,["current"]=Current,["sets"]=Sets.Select(a=>new TagCompound{["items"]=a.Select(ItemIO.Save).ToList()}).ToList()};
    internal void LoadData(TagCompound data) {
        // Unreleased template prototypes were copies, never owned items.
        if(data.GetInt("format") is not (1 or 2 or 3))return;
        Independent=data.GetBool("independent");
        Current=Math.Clamp(data.GetInt("current"),0,2);var sets=data.GetList<TagCompound>("sets");
        for(int i=0;i<Math.Min(3,sets.Count);i++)if(i!=Current){var items=sets[i].GetList<TagCompound>("items");if(items.Count is 5 or 6 or 14 or Count){for(int n=0;n<items.Count;n++)Sets[i][n]=ItemIO.Load(items[n]);if(Sets[i][5].IsAir&&CompanionWeapons.SummonWeapon(Sets[i][0])){Sets[i][5]=Sets[i][0];Sets[i][0]=new Item();}}}
    }
    internal void Write(BinaryWriter w){w.Write((byte)Current);w.Write(Independent);for(int i=0;i<3;i++)if(i!=Current)foreach(var item in Sets[i])ItemIO.Send(item,w,true,true);}
    internal static CompanionLoadouts Read(BinaryReader r) {
        var result=new CompanionLoadouts{Current=r.ReadByte(),Independent=r.ReadBoolean()};if(result.Current>=3)throw new InvalidDataException("Invalid companion loadout");
        for(int i=0;i<3;i++)if(i!=result.Current)for(int n=0;n<Count;n++)result.Sets[i][n]=ItemIO.Receive(r,true,true);
        return result;
    }
}

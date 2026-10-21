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
    internal readonly Item[][] Sets={InventoryTransfer.Empty(5),InventoryTransfer.Empty(5),InventoryTransfer.Empty(5)};
    internal bool Switch(CompanionPlayer s,int index,out string message) {
        message=CompanionText.Message("CompanionLoadouts.Invalid");if(index<0||index>=3)return false;
        if(index==Current){message=CompanionText.Message("CompanionLoadouts.AlreadyActive" ,index+1);return false;}
        var incoming=Sets[index];
        // Preserve items from a stronger world's extra slot. The existing
        // ReturnLocked policy returns them to the bag and disables their effects.
        Sets[Current]=new[]{s.Gear[0],s.Training.Slots[0],s.Training.Slots[1],s.Training.Slots[2],s.Training.Slots[3]};
        s.Gear[0]=incoming[0];for(int i=0;i<4;i++)s.Training.Slots[i]=incoming[i+1];
        Sets[index]=InventoryTransfer.Empty(5);Current=index;
        s.Training.ReturnLocked(s);s.Training.ReconcileEffects(s);
        message=CompanionText.Message("CompanionLoadouts.Switched" ,index+1);return true;
    }
    internal TagCompound SaveData()=>new(){["format"]=1,["current"]=Current,["sets"]=Sets.Select(a=>new TagCompound{["items"]=a.Select(ItemIO.Save).ToList()}).ToList()};
    internal void LoadData(TagCompound data) {
        // Unreleased template prototypes were copies, never owned items.
        if(data.GetInt("format")!=1)return;
        Current=Math.Clamp(data.GetInt("current"),0,2);var sets=data.GetList<TagCompound>("sets");
        for(int i=0;i<Math.Min(3,sets.Count);i++)if(i!=Current){var items=sets[i].GetList<TagCompound>("items");if(items.Count==5)Sets[i]=items.Select(ItemIO.Load).ToArray();}
    }
    internal void Write(BinaryWriter w){w.Write((byte)Current);for(int i=0;i<3;i++)if(i!=Current)foreach(var item in Sets[i])ItemIO.Send(item,w,true,true);}
    internal static CompanionLoadouts Read(BinaryReader r) {
        var result=new CompanionLoadouts{Current=r.ReadByte()};if(result.Current>=3)throw new InvalidDataException("Invalid companion loadout");
        for(int i=0;i<3;i++)if(i!=result.Current)for(int n=0;n<5;n++)result.Sets[i][n]=ItemIO.Receive(r,true,true);
        return result;
    }
}

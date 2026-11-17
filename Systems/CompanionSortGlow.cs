using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
namespace PalicoCompanion.Systems;

// ItemSorting and ItemSlot share global glow arrays. Borrow them only inside
// synchronous native calls; the companion owns its resulting presentation.
internal sealed class CompanionSortGlow {
    static T[] Native<T>(string name)=>(T[])typeof(ItemSlot).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static readonly float[] inventoryHue=Native<float>("inventoryGlowHue"),chestHue=Native<float>("inventoryGlowHueChest");
    static readonly int[] inventoryTime=Native<int>("inventoryGlowTime"),chestTime=Native<int>("inventoryGlowTimeChest");
    float[] hues=Array.Empty<float>();
    int remaining;
    internal void Start(float[] colors){hues=colors;remaining=300;}
    internal void Tick(){if(remaining>0)remaining--;}
    internal static float[] Capture(Action sort,int count,bool chest){
        var oldInventoryHue=(float[])inventoryHue.Clone();var oldChestHue=(float[])chestHue.Clone();
        var oldInventoryTime=(int[])inventoryTime.Clone();var oldChestTime=(int[])chestTime.Clone();
        try{
            Array.Clear(inventoryTime);Array.Clear(chestTime);
            sort();
            var colors=new float[count];Array.Fill(colors,-1f);
            var time=chest?chestTime:inventoryTime;var hue=chest?chestHue:inventoryHue;
            for(int i=0;i<Math.Min(count,time.Length);i++)if(time[i]>0)colors[i]=hue[i];
            return colors;
        }finally{
            oldInventoryHue.CopyTo(inventoryHue,0);oldChestHue.CopyTo(chestHue,0);
            oldInventoryTime.CopyTo(inventoryTime,0);oldChestTime.CopyTo(chestTime,0);
        }
    }
    internal DrawScope Draw(int nativeSlot,int bagSlot)=>new(nativeSlot,
        bagSlot>=0&&bagSlot<hues.Length?hues[bagSlot]:-1f,remaining);
    internal readonly struct DrawScope:IDisposable {
        readonly int slot,time;readonly float hue;
        internal DrawScope(int nativeSlot,float color,int ticks){
            slot=nativeSlot;time=inventoryTime[slot];hue=inventoryHue[slot];
            inventoryTime[slot]=color>=0?ticks:0;inventoryHue[slot]=color;
        }
        public void Dispose(){inventoryTime[slot]=time;inventoryHue[slot]=hue;}
    }
    internal void Write(BinaryWriter writer){writer.Write((ushort)hues.Length);foreach(float hue in hues)writer.Write(hue);}
    internal static float[] Read(BinaryReader reader){
        int count=reader.ReadUInt16();if(count>256)throw new InvalidDataException("Inventory glow capacity");
        var colors=new float[count];for(int i=0;i<count;i++)colors[i]=reader.ReadSingle();return colors;
    }
}

public sealed class CompanionSortGlowSystem:ModSystem {
    public override void UpdateUI(GameTime gameTime){if(!Main.gameMenu)Main.LocalPlayer.GetModPlayer<CompanionPlayer>().SortGlow.Tick();}
}

using Terraria.ModLoader;
namespace PalicoCompanion;
public sealed class PalicoCompanion : Mod
{
    internal static ModKeybind PanelKey, RecallKey, DirectKey, EnvironmentKey;
    public override void HandlePacket(System.IO.BinaryReader reader,int whoAmI){
        long start=reader.BaseStream.Position;byte kind=reader.ReadByte();
        if(kind==1){reader.BaseStream.Position=start;Systems.CompanionProfileNet.Receive(reader,whoAmI);}
        else Systems.CompanionNetwork.Receive(kind,reader,whoAmI);
    }
    public override void Load() {
        PanelKey = KeybindLoader.RegisterKeybind(this, "Panel", "P");
        RecallKey = KeybindLoader.RegisterKeybind(this, "Recall", "K");
        DirectKey = KeybindLoader.RegisterKeybind(this, "Direct", "V");
        EnvironmentKey = KeybindLoader.RegisterKeybind(this, "Environment", Microsoft.Xna.Framework.Input.Keys.None);
    }
    public override void Unload() { PanelKey = RecallKey = DirectKey = EnvironmentKey = null; Systems.CenteredButtonText.Clear(); }
}

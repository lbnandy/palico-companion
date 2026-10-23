using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using PalicoCompanion.Systems;
namespace PalicoCompanion;
public sealed class CompanionCommand : ModCommand
{
    public override string Command=>"palico";
    public override CommandType Type=>CommandType.Chat;
    public override string Usage=>"/palico help";
    public override string Description=>CompanionText.Get("CompanionCommand.Description");
    public override void Action(CommandCaller caller,string input,string[] args) {
        if(Main.netMode!=NetmodeID.SinglePlayer) {caller.Reply(CompanionText.Get("CompanionCommand.SinglePlayerOnly"));return;}
        var s=caller.Player.GetModPlayer<CompanionPlayer>();
        string action=args.Length>0?args[0].ToLowerInvariant():"help";
        switch(action) {
            case "summon": CompanionLife.Follow(s);break;
            case "dismiss": CompanionLife.Stow(s);break;
            case "pause": s.Paused=!s.Paused;break;
            case "recall": s.Recall();break;
            case "bag": CompanionUIFlow.Toggle(s);break;
            case "give": s.GiveHeld();break;
            case "takeall": for(int i=0;i<s.Bag.Length;i++)s.Take(i);break;

            case "pet": s.Pet();break;
            case "follow": CompanionLife.Follow(s);break;
            case "chest":
                s.AuthorizeChest();break;
            case "store": Logistics.Store(caller.Player,s);break;
            case "supply": Logistics.Supply(caller.Player,s);break;
            case "mine": s.Order.Cancel();s.Status=s.Mining.Request(caller.Player,Main.MouseWorld,args.Length>1&&args[1]=="vein",ownerRequested:true);break;
            case "plan":
                if(args.Length<3 || !int.TryParse(args[2],out int length)) {s.Status=CompanionText.Get("CompanionCommand.PlanUsage");break;}
                bool rope=args[1]=="down";
                if(!rope && args[1]!="left" && args[1]!="right") {s.Status=CompanionText.Get("CompanionCommand.PlanUsage");break;}
                s.Status=s.Order.Preview(caller.Player,Main.MouseWorld,rope?ItemID.Rope:ItemID.WoodPlatform,rope?0:args[1]=="left"?-1:1,rope?1:0,length,s.Bag);break;
            case "confirm": s.Mining.Cancel();s.Status=s.Order.Confirm(caller.Player,s.Bag);break;
            case "cancel": s.Mining.Cancel();s.Status=s.Order.Cancel();break;
            case "place":
                if(s.Paused) {s.Status="Paused";break;}
                if(s.Cat is not {} cat) {s.Status="Summon the cat first";break;}
                int type=args.Length>1?args[1] switch {"torch"=>ItemID.Torch,"platform"=>ItemID.WoodPlatform,"rope"=>ItemID.Rope,_=>0}:0;
                var at=Main.MouseWorld.ToTileCoordinates();s.Status=SafeConstruction.TryPlace(caller.Player,cat.Center,s.Bag,at.X,at.Y,type);break;
            case "pickup": s.Pickup=!s.Pickup;break;
            case "combat": s.Combat=!s.Combat;break;
            case "torches": s.Torches=!s.Torches;break;
            case "bridges": s.Bridges=!s.Bridges;break;
            case "ropes": s.Ropes=!s.Ropes;break;
            default: caller.Reply(CompanionText.Get("CompanionCommand.Help"));return;
        }
        s.Report(s.Status);
        caller.Reply(s.CatName+": "+CompanionCopy.Status(s.Status),Color.SandyBrown);
    }
}

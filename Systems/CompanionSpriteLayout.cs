// Frame layout for Assets/Palico.png; hand offsets are world pixels from the feet.
using Microsoft.Xna.Framework;
namespace PalicoCompanion.Systems;
public static class CompanionSpriteLayout {
    public const int Width=31,Height=28,Frames=43;
    public const int StandingWidth=42,StandingHeight=42;
    public const float Scale=2f;
    public static readonly Vector2 Origin=new(15.5f,26f);
    public const int Idle=0;
    public const int Breathe=1;
    public const int Blink=2;
    public const int WalkFrames=6;
    public const int Swing0=22,SwingFrames=6;
    public const int RangedHold=Swing0+3;
    internal const int LegacyBlink=28;
    public const int RestExit=29;
    public const int Prone=30,GetUp0=31,GetUp1=32;
    public const int BarrelRaise0=33,BarrelRaiseFrames=4,BarrelWalk0=37;
    public const int Walk0=3;
    public const int Walk1=4;
    public const int Walk2=5;
    public const int Walk3=6;
    public const int Walk4=7;
    public const int Walk5=8;
    public const int Walk6=9;
    public const int Walk7=10;
    public const int Rise=11;
    public const int Fall=12;
    public const int Land=13;
    public const int RestEnter=14;
    public const int Rest=15;
    public const int RestBlink=16;
    public const int Pet=17;
    public const int WorkWindup=18;
    public const int WorkStrike=19;
    public const int WorkRecover=20;
    public const int Downed=21;
    // Exposed paw pixels only, authored with the imported atlas. Empty regions
    // mean the paw is occluded; legacy poses retain their original grip crop.
    static readonly Rectangle[][] ArtistMainGrip={
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{new(20,20,1,2)},
        new Rectangle[]{},
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{},
        new Rectangle[]{new(19,21,1,2)},
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{new(20,19,2,1)},
        new Rectangle[]{new(22,19,2,2)},
        new Rectangle[]{new(20,20,2,2)},
        new Rectangle[]{new(19,21,1,2)},
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{new(22,16,1,1),new(21,17,2,1)},
        new Rectangle[]{new(19,19,2,1)},
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{new(18,20,2,2)},
        new Rectangle[]{new(19,19,2,1)},
        new Rectangle[]{new(19,22,1,2)},
        new Rectangle[]{new(17,20,2,2)},
        new Rectangle[]{new(18,21,2,2)},
        new Rectangle[]{new(18,22,2,2)},
        new Rectangle[]{new(18,22,2,2)},
        new Rectangle[]{new(18,21,2,2)}
    };
    static readonly Rectangle[][] ArtistOffGrip={
        new Rectangle[]{new(11,20,2,2)},
        new Rectangle[]{new(12,20,2,2)},
        new Rectangle[]{new(14,21,2,2)},
        new Rectangle[]{new(16,21,2,2)},
        new Rectangle[]{},
        new Rectangle[]{new(16,21,2,2)},
        new Rectangle[]{new(14,21,2,2)},
        new Rectangle[]{new(11,20,2,2)},
        new Rectangle[]{new(11,18,2,2)},
        new Rectangle[]{new(13,19,3,1),new(14,20,2,1)},
        new Rectangle[]{new(15,20,2,2)},
        new Rectangle[]{new(13,21,2,2)},
        new Rectangle[]{new(11,20,2,2)},
        new Rectangle[]{new(10,16,2,2)},
        new Rectangle[]{new(11,18,2,2)},
        new Rectangle[]{new(11,20,2,2)},
        new Rectangle[]{new(12,20,2,2)},
        new Rectangle[]{new(11,18,2,2)},
        new Rectangle[]{new(13,22,2,2)},
        new Rectangle[]{new(11,20,2,2)},
        new Rectangle[]{new(13,22,2,2)},
        new Rectangle[]{new(14,22,2,2)},
        new Rectangle[]{new(14,22,2,2)},
        new Rectangle[]{new(13,22,2,2)}
    };
    static int ArtistPoseIndex(int pose)=>
        pose==WorkWindup?8:pose==WorkStrike?9:pose==WorkRecover?10:pose==Walk6?1:pose==Walk7?2:pose==LegacyBlink?16:pose==Blink?16:pose==Idle?0:pose==Breathe?19:
        pose>=Walk0&&pose<Walk0+WalkFrames?1+pose-Walk0:
        pose>=Swing0&&pose<Swing0+SwingFrames?7+pose-Swing0:
        pose==Rise?13:pose==Fall?14:pose==Land?15:pose==Pet?17:pose==Downed?18:
        pose==RestEnter?20:pose==Rest?21:pose==RestBlink?22:pose==RestExit?23:-1;
    internal static bool TryFaceOffset(int pose,out Point offset) {
        pose=pose==WorkWindup?Swing0+1:pose==WorkStrike?Swing0+2:pose==WorkRecover?Swing0+3:pose;
        offset=new Point(pose==Breathe?-1:pose is 4 or 5 or 6 or 23 or 24?1:0,
            pose==Rise?-1:pose==Downed?3:pose is RestEnter or Rest or RestBlink or RestExit?1:0);
        return ArtistPoseIndex(pose)>=0;
    }
    // Torso attachment movement measured on the authored poses, in source
    // pixels relative to idle. Share the same pose aliases as hand crops.
    // Arms can swing independently; carried equipment follows shoulder/waist.
    internal static Vector2 CarryOffset(int pose,bool waist) {
        int index=ArtistPoseIndex(pose);
        Point offset=waist?index switch {
            2=>new(2,0),3 or 4=>new(3,0),5=>new(2,0),6=>new(1,0),
            8 or 17=>new(0,-1),9 or 10=>new(1,0),13=>new(0,-2),
            19=>new(-1,0),20 or 23=>new(0,1),21 or 22=>new(1,2),_=>Point.Zero
        }:index switch {
            2 or 3 or 5 or 9 or 10=>new(1,0),4=>new(2,0),8=>new(1,-1),
            13=>new(-1,-2),14 or 20 or 21 or 22 or 23=>new(0,1),19=>new(-1,0),_=>Point.Zero
        };
        return new Vector2(offset.X,offset.Y)*Scale;
    }
    public static Rectangle[] GripRegions(int pose,bool offhand) {
        int index=ArtistPoseIndex(pose);
        return index<0?null:(offhand?ArtistOffGrip:ArtistMainGrip)[index];
    }
    // Near-side paw silhouette, including its border and wrist. Hand ownership
    // and depth are independent: MainHand is the far-side weapon anchor.
    // Mirroring the whole pose preserves near/far depth for either facing.
    static readonly Rectangle[][] ArtistNearHand={
        new Rectangle[]{new(11,19,2,1),new(10,20,4,2),new(11,22,2,1)},
        new Rectangle[]{new(12,19,2,1),new(11,20,4,2),new(12,22,2,1)},
        new Rectangle[]{new(14,20,2,1),new(13,21,4,2),new(14,23,2,1)},
        new Rectangle[]{new(16,20,2,1),new(15,21,4,2),new(16,23,2,1)},
        new Rectangle[]{},
        new Rectangle[]{new(16,20,2,1),new(15,21,4,2),new(16,23,2,1)},
        new Rectangle[]{new(14,20,2,1),new(13,21,4,2),new(14,23,2,1)},
        new Rectangle[]{new(11,19,2,1),new(10,20,4,2),new(11,22,2,1)},
        new Rectangle[]{new(11,17,2,1),new(10,18,4,2),new(11,20,2,1)},
        new Rectangle[]{new(13,18,3,1),new(12,19,5,1),new(13,20,4,1),new(14,21,2,1)},
        new Rectangle[]{new(15,19,2,1),new(14,20,4,2),new(15,22,2,1)},
        new Rectangle[]{new(13,20,2,1),new(12,21,4,2),new(13,23,2,1)},
        new Rectangle[]{new(11,19,2,1),new(10,20,4,2),new(11,22,2,1)},
        new Rectangle[]{new(10,15,2,1),new(9,16,4,2),new(10,18,2,1)},
        new Rectangle[]{new(11,17,2,1),new(10,18,4,2),new(11,20,2,1)},
        new Rectangle[]{new(11,19,2,1),new(10,20,4,2),new(11,22,2,1)},
        new Rectangle[]{new(12,19,2,1),new(11,20,4,2),new(12,22,2,1)},
        new Rectangle[]{new(11,17,2,1),new(10,18,4,2),new(11,20,2,1)},
        new Rectangle[]{new(13,21,2,1),new(12,22,4,2),new(13,24,2,1)},
        new Rectangle[]{new(11,19,2,1),new(10,20,4,2),new(11,22,2,1)},
        new Rectangle[]{new(13,21,2,1),new(12,22,4,2),new(13,24,2,1)},
        new Rectangle[]{new(14,21,2,1),new(13,22,4,2),new(14,24,2,1)},
        new Rectangle[]{new(14,21,2,1),new(13,22,4,2),new(14,24,2,1)},
        new Rectangle[]{new(13,21,2,1),new(12,22,4,2),new(13,24,2,1)}
    };
    public static Rectangle[] NearHandRegions(int pose) {
        int index=ArtistPoseIndex(pose);
        return index<0?null:ArtistNearHand[index];
    }
    public static readonly Vector2[] MainHand={
        new(7.0f,-10.0f),
        new(5.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(10.0f,-10.0f),
        new(6.0f,-12.0f),
        new(7.0f,-10.0f),
        new(6.0f,-10.0f),
        new(8.0f,-8.0f),
        new(7.0f,-10.0f),
        new(10.0f,-10.0f),
        new(13.0f,-18.0f),
        new(9.0f,-13.0f),
        new(7.0f,-10.0f),
        new(7.0f,-8.0f),
        new(7.0f,-6.0f),
        new(7.0f,-6.0f),
        new(9.0f,-13.0f),
        new(11.0f,-13.0f),
        new(15.0f,-12.0f),
        new(11.0f,-10.0f),
        new(8.0f,-6.0f),
        new(7.0f,-10.0f),
        new(11.0f,-13.0f),
        new(15.0f,-12.0f),
        new(11.0f,-10.0f),
        new(8.0f,-8.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-8.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f),
        new(7.0f,-10.0f)
    };
    public static readonly Vector2[] OffHand={
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-5.0f,-10.0f),
        new(-5.0f,-10.0f),
        new(-1.0f,-8.0f),
        new(3.0f,-8.0f),
        new(4.0f,-12.0f),
        new(3.0f,-8.0f),
        new(-1.0f,-8.0f),
        new(-5.0f,-10.0f),
        new(-1.0f,-8.0f),
        new(-9.0f,-18.0f),
        new(-7.0f,-14.0f),
        new(-7.0f,-10.0f),
        new(-3.0f,-6.0f),
        new(-1.0f,-6.0f),
        new(-1.0f,-6.0f),
        new(-7.0f,-14.0f),
        new(-7.0f,-14.0f),
        new(-2.0f,-12.0f),
        new(1.0f,-10.0f),
        new(-3.0f,-6.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-14.0f),
        new(-2.0f,-12.0f),
        new(1.0f,-10.0f),
        new(-3.0f,-8.0f),
        new(-7.0f,-10.0f),
        new(-5.0f,-10.0f),
        new(-3.0f,-6.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f),
        new(-7.0f,-10.0f)
    };
}

// Frame layout for Assets/Palico.png; hand offsets are world pixels from the feet.
using Microsoft.Xna.Framework;
namespace PalicoCompanion.Systems;
public static class CompanionSpriteLayout {
    public const int Width=31,Height=28,Frames=22;
    public const int StandingWidth=42,StandingHeight=42;
    public const float Scale=2f;
    public static readonly Vector2 Origin=new(15.5f,26f);
    public const int Idle=0;
    public const int Breathe=1;
    public const int Blink=2;
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
    public static readonly Vector2[] MainHand={
        new(12.0f,-10f),
        new(12.0f,-10f),
        new(12.0f,-10f),
        new(12.0f,-10f),
        new(10.0f,-10f),
        new(10.0f,-12f),
        new(12.0f,-10f),
        new(14.0f,-10f),
        new(14.0f,-10f),
        new(12.0f,-12f),
        new(12.0f,-10f),
        new(12.0f,-12f),
        new(14.0f,-12f),
        new(12.0f,-8f),
        new(12.0f,-8f),
        new(10.0f,-8f),
        new(10.0f,-8f),
        new(12.0f,-12f),
        new(14.0f,-16f),
        new(16.0f,-12f),
        new(14.0f,-10f),
        new(10.0f,-6f)
    };
    public static readonly Vector2[] OffHand={
        new(-12.0f,-11.0f),
        new(-12.0f,-13.0f),
        new(-12.0f,-11.0f),
        new(-12.0f,-11.0f),
        new(-10.0f,-11.0f),
        new(-10.0f,-13.0f),
        new(-12.0f,-11.0f),
        new(-14.0f,-11.0f),
        new(-14.0f,-11.0f),
        new(-12.0f,-13.0f),
        new(-12.0f,-11.0f),
        new(-12.0f,-13.0f),
        new(-14.0f,-13.0f),
        new(-12.0f,-9.0f),
        new(-12.0f,-9.0f),
        new(-10.0f,-9.0f),
        new(-10.0f,-9.0f),
        new(-10.0f,-13.0f),
        new(-12.0f,-13.0f),
        new(-10.0f,-11.0f),
        new(-12.0f,-11.0f),
        new(-10.0f,-7.0f)
    };
}

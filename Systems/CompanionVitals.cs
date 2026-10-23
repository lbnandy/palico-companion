using System;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
namespace PalicoCompanion.Systems;
// Independent companion health: no player damage or permanent item loss.
public sealed class CompanionVitals {
    public const int LegacyMaxHealth=120, RecoveryTime=1800;
    public int MaxHealth=LegacyMaxHealth, MaxMana=20;
    public int Health=LegacyMaxHealth, Mana=20, Recovery, Immunity, QuietTicks;
    public int RecoveryTotal=600;
    public int RecoveryProgress=>Math.Clamp(RecoveryTotal-Recovery,0,RecoveryTotal);
    public int PotionDelay;
    public int ManaSickness, ManaDelay, LifeCounter, ManaCounter;
    public float LifeSteal=80;
    public float MagicMultiplier=>1f-Player.manaSickLessDmg*Math.Clamp(ManaSickness/(float)Player.manaSickTime,0,Player.manaSickTimeMax/(float)Player.manaSickTime);
    public static int RespawnTicks(bool expert,bool boss=false)=>(expert?900:600)*(boss?2:1);
    public int EffectiveDefense { get; internal set; }
    public void Sync(Player owner,Item[] gear) {
        // Native regeneration assumes at least the player's starting 20 mana.
        // Retain that floor for modded owners below the native starting value.
        var balance=CompanionBalanceConfig.Current;
        int health=Math.Max(1,Math.Clamp(owner.statLifeMax2,100,100000)*balance.HealthRatio/100),mana=Math.Clamp(owner.statManaMax2,20,100000);
        // Preserve injury/resource fractions: swapping buffs or armor never refills us.
        if(health!=MaxHealth){Health=Downed?0:Math.Max(1,(int)((long)Health*health/Math.Max(1,MaxHealth)));MaxHealth=health;}
        if(mana!=MaxMana){Mana=MaxMana>0?(int)((long)Mana*mana/MaxMana):0;MaxMana=mana;}
        Health=Math.Clamp(Health,0,MaxHealth);Mana=Math.Clamp(Mana,0,MaxMana);
        EffectiveDefense=Math.Clamp((int)owner.statDefense,0,10000)*balance.DefenseRatio/100;
    }
    public int AttackDamage(Item[] gear,int experience) => Attack(gear,experience);
    public bool Downed => Recovery>0;
    // Legacy armor remains saved and removable, but cannot be equipped again.
    public static bool Accepts(Item item,int slot) => item.IsAir || slot==0 && CompanionWeapons.Supports(item);
    public static int Attack(Item[] gear,int experience) => Math.Max(1,CompanionWeapons.Active(gear).damage);
    internal static int AfterDefense(int damage,int defense,float penetration=0)=>Math.Max(1,
        (int)Math.Floor(damage-Math.Max(0,defense-penetration)*(Main.masterMode?1f:Main.expertMode?.75f:.5f)));
    public bool Hurt(int damage,int defense,bool boss=false) => HurtFinal(AfterDefense(damage,defense),boss,damage>0);
    internal bool HurtFinal(int damage,bool boss,bool valid=true) {
        if(!valid||damage<=0 || Downed || Immunity>0)return false;
        Health=Math.Max(0,Health-damage);Immunity=60;QuietTicks=0;LifeCounter=0;
        if(Health==0)Recovery=RecoveryTotal=RespawnTicks(Main.expertMode,boss);
        return true;
    }
    // Health keeps its existing baseline; mana uses the installed tML runtime.
    // No copying owner accessories, personal buffs or debuffs onto a second actor.
    public void Tick(float velocityX=0,float velocityY=0,bool magicUse=false,bool resting=false,bool expert=false,Player manaActor=null) {
        LifeSteal=Math.Min(expert?70:80,LifeSteal+(expert?.5f:.6f));
        if(PotionDelay>0)PotionDelay--;
        if(ManaSickness>0)ManaSickness--;
        if(Immunity>0)Immunity--;
        if(Recovery>0){if(--Recovery==0){Health=MaxHealth;Mana=MaxMana;Immunity=120;QuietTicks=LifeCounter=ManaCounter=ManaDelay=0;}return;}
        QuietTicks=Math.Min(QuietTicks+1,3600);
        int steps=Math.Min(QuietTicks/300,6)+(QuietTicks>=2400?1:0)+(QuietTicks>=3000?1:0)+(QuietTicks>=3600?1:0);
        float lifeRate=steps*(velocityX!=0?0.5f:1.25f)*(MaxHealth/400f*0.85f+0.15f);
        if(resting){QuietTicks=Math.Min(QuietTicks+3,3600);lifeRate*=1.3f;}
        if(expert)lifeRate*=0.5f; // Companion has no Well Fed buff of its own.
        if(Health<MaxHealth){LifeCounter+=(int)Math.Round(lifeRate);Health=Math.Min(MaxHealth,Health+LifeCounter/120);LifeCounter%=120;}
        else LifeCounter=0;
        if(MaxMana<=0){Mana=ManaCounter=ManaDelay=0;return;}
        if(manaActor==null)return;
        // Reuse the installed game's regeneration on the existing companion
        // actor. The delay setup lives in Player.Update rather than this method.
        manaActor.statMana=Mana;manaActor.statManaMax=manaActor.statManaMax2=MaxMana;
        manaActor.velocity=new Microsoft.Xna.Framework.Vector2(velocityX,velocityY);
        manaActor.maxRegenDelay=(1f-Mana/(float)MaxMana)*168f+31.5f;
        manaActor.manaRegenDelay=ManaDelay;manaActor.manaRegenCount=ManaCounter;
        if(magicUse)manaActor.manaRegenDelay=(int)manaActor.maxRegenDelay;
        // Resource-only update: do not emit a human player's full-mana effect.
        int owner=manaActor.whoAmI;
        try{manaActor.whoAmI=-1;manaActor.UpdateManaRegen();}
        finally{manaActor.whoAmI=owner;}
        Mana=Math.Clamp(manaActor.statMana,0,MaxMana);
        ManaDelay=Math.Max(0,(int)Math.Ceiling(manaActor.manaRegenDelay));
        ManaCounter=Math.Max(0,manaActor.manaRegenCount);
    }
}

using Microsoft.Xna.Framework;
using PalicoCompanion.Systems;
int checks=0;
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
CompanionPose Pose(int tick)=>new(){Time=tick,Position=new(tick*4,100),Velocity=new(4,0),Direction=1,Motion=CatMotion.Walk,WalkPhase=(tick/3.0)%8,Frame=3};
void Trajectory(bool jitter){
 var r=new CompanionReplica();var a=new CompanionAnimation();int next=0,changed=0,between=0,lastFrame=0;float prior=0,maxStep=0,minStep=float.MaxValue;double squareError=0;int samples=0;bool reversed=false;
 int[] delays=jitter?new[]{0,3,1,5,2,0,4,1}:new[]{0};int due=0;
 for(int local=0;local<600;local++){
  bool received=false;
  if(local>=due){r.Receive(Pose(next),local);next+=6;due=next+delays[(next/6)%delays.Length];received=true;}
  var q=r.Advance(local);a.PlayReplica(q,r.Elapsed);
  if(local>30){float step=q.Position.X-prior;maxStep=Math.Max(maxStep,step);minStep=Math.Min(minStep,step);squareError+=(step-4)*(step-4);samples++;reversed|=step<-.001f;if(a.SpriteFrame!=lastFrame){changed++;if(!received)between++;}}
  prior=q.Position.X;lastFrame=a.SpriteFrame;
 }
 Console.WriteLine($"METRIC jitter={jitter} min={minStep:F4} max={maxStep:F4} rms={Math.Sqrt(squareError/samples):F4}");
 Check(!reversed,$"jitter={jitter}: no backward correction during constant forward travel");
 Check(maxStep<=5.01f,$"jitter={jitter}: largest frame movement {maxStep:F3} stays bounded");
 Check(minStep>=3.879f&&maxStep<=4.121f,$"jitter={jitter}: ordinary arrival jitter stays within three percent speed correction");
 Check(changed>=170&&between>=80,$"jitter={jitter}: gait advances between packets ({changed} changes / {between} between)");
 Check(Math.Abs(r.Position.X-(599-CompanionReplica.Delay)*4)<30,$"jitter={jitter}: playback does not accumulate drift");
}
Trajectory(false);Trajectory(true);
var stopped=new CompanionReplica();stopped.Receive(Pose(0),0);for(int i=0;i<80;i++)stopped.Advance(i);Check(stopped.Position.X<=24,"packet outage extrapolation stops after six ticks");
var old=stopped.Position;stopped.Receive(Pose(0),90);Check(stopped.Position==old,"duplicate snapshot cannot rewind presentation");
var teleport=Pose(100);teleport.Position=new(9000,3000);teleport.Epoch=1;stopped.Receive(teleport,100);Check(stopped.Position==teleport.Position&&stopped.Resets==2,"explicit teleport snaps immediately instead of traversing terrain");
var work=new CompanionReplica();var tool=Pose(0);tool.Motion=CatMotion.Mine;tool.Tool=1;tool.ToolDuration=30;tool.ToolProgress=.2f;tool.ItemAnimation=24;tool.ItemMax=30;work.Receive(tool,0);CompanionPose sample=default;for(int i=0;i<17;i++)sample=work.Advance(i);Check(sample.ToolProgress>.3f&&sample.ItemAnimation<24,"tool progress and held item timer advance between packets");
var same=teleport;same.Epoch=2;same.Position=new(9100,3000);stopped.Receive(same,101);Check(stopped.Position==same.Position,"teleport within same server tick still applies epoch");
var arc=new CompanionReplica();float maxError=0;for(int i=0;i<120;i++){if(i%6==0){var q=Pose(i);q.Position=new(i*2,100-i*6+.175f*i*i);q.Velocity=new(2,-6+.35f*i);q.Motion=CatMotion.Flight;q.Flying=true;arc.Receive(q,i);}var at=arc.Advance(i);double t=at.Time+arc.Elapsed;if(i>12&&i<174)maxError=Math.Max(maxError,Math.Abs(at.Position.Y-(float)(100-6*t+.175*t*t)));}Check(maxError<1.6f,$"airborne parabola follows server samples within {maxError:F3} pixels");
var fishAnim=new CompanionAnimation();var fish=Pose(0);fish.Motion=CatMotion.Mine;fish.Frame=CompanionSpriteLayout.WorkWindup;fishAnim.PlayReplica(fish,0);Check(fishAnim.SpriteFrame==fish.Frame,"native fishing gesture retains received windup pose");
foreach(double rate in new[]{.99,1.01}){
 var drift=new CompanionReplica();int nextTick=0;float previous=0;bool backwards=false;
 for(int i=0;i<3600;i++){
  while(nextTick<=i*rate){drift.Receive(Pose(nextTick),i);nextTick+=6;}
  var q=drift.Advance(i);backwards|=q.Position.X<previous-.001f;previous=q.Position.X;
 }
 Check(!backwards&&Math.Abs(drift.Position.X/4-(3599*rate-CompanionReplica.Delay))<8,$"one minute clock drift rate={rate} remains bounded without reversal");
}
var recovered=new CompanionReplica();float beforeResume=0;bool reversal=false;
for(int i=0;i<240;i++){
 if(i%6==0&&(i<60||i>=100))recovered.Receive(Pose(i),i);
 var q=recovered.Advance(i);reversal|=q.Position.X<beforeResume-.001f;beforeResume=q.Position.X;
}
Check(!reversal&&Math.Abs(recovered.Position.X/4-(239-CompanionReplica.Delay))<8,"buffer resumes after a forty-tick outage without reversal or lasting delay");
var aiming=new CompanionReplica();var aimA=Pose(0);aimA.WeaponAim=new(100,200);var aimB=Pose(6);aimB.WeaponAim=new(160,260);aiming.Receive(aimA,0);aiming.Receive(aimB,6);CompanionPose aimPose=default;for(int i=0;i<17;i++)aimPose=aiming.Advance(i);Check(aimPose.WeaponAim.X>100&&aimPose.WeaponAim.X<160&&Math.Abs(aimPose.WeaponAim.Y-aimPose.WeaponAim.X-100)<.001,"weapon aim interpolates on the same timeline as the companion body");
foreach(bool channel in new[]{false,true}){
 var casting=new CompanionReplica();int hidden=0;
 for(int i=0;i<180;i++){
  if(i%6==0){var q=Pose(i);q.ItemMax=10;q.ItemAnimation=channel?2:10-i%10;q.Channel=channel;casting.Receive(q,i);}
  var q2=casting.Advance(i);if(i>24&&q2.ItemAnimation==0)hidden++;
 }
 Check(hidden==0,$"continuous casting channel={channel} remains visible across snapshot boundaries ({hidden} hidden frames)");
 var end=Pose(180);end.ItemMax=12;casting.Receive(end,180);
 for(int i=180;i<210;i++)casting.Advance(i);
 Check(casting.Advance(210).ItemAnimation==0,$"authoritative stop channel={channel} clears the held weapon");
}
var missing=new CompanionReplica();var lastCast=Pose(0);lastCast.ItemMax=12;lastCast.ItemAnimation=2;lastCast.Channel=true;missing.Receive(lastCast,0);
for(int i=0;i<60;i++)sample=missing.Advance(i);
Check(sample.ItemAnimation==0,"packet loss does not hold a short casting animation forever");
// Body facing and native held-item facing can differ while a tether returns.
var held=new CompanionReplica();var ha=Pose(0);ha.ItemDirection=1;ha.ItemMax=20;ha.ItemAnimation=15;ha.ItemRotation=.2f;
var hb=ha;hb.Time=6;hb.Direction=-1;hb.ItemAnimation=9;hb.ItemRotation=.8f;
held.Receive(ha,0);held.Receive(hb,6);for(int i=0;i<17;i++)sample=held.Advance(i);
Check(sample.ItemRotation>.2f&&sample.ItemRotation<.8f,"body turning does not interrupt same-facing weapon interpolation");
var flip=new CompanionReplica();hb=ha;hb.Time=6;hb.ItemDirection=-1;hb.ItemAnimation=9;hb.ItemRotation=-2.6f;
flip.Receive(ha,0);flip.Receive(hb,6);for(int i=0;i<17;i++)sample=flip.Advance(i);
Check(sample.ItemDirection==1&&sample.ItemRotation==ha.ItemRotation,"weapon facing flip never interpolates through the opposite aim");
Console.WriteLine($"checks={checks} failures=0");

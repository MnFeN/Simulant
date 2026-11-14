using Simulant.Content.U7a.Presets;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Simulant.Tests
{
    internal static class CrystallizeTimeTests
    {
        public static void NetworkRoutesCollectPuddlesAndDodgeBothWaves()
        {
            Exception failure=null;
            const double origin=32;
            var random=new Random(7359);
            using (var done=new ManualResetEventSlim())
            using (var scheduler=new SimScheduler((error,_)=>{failure=error;done.Set();}))
            using (var movement=new SimMovementController(scheduler))
            using (var casts=new SimCastController(scheduler))
            using (var behavior=new SimActorBehaviorController(scheduler,movement,casts))
            {
                foreach (var wave in new[]{CrystalWave.LeftUp,CrystalWave.LeftDown,CrystalWave.RightUp,CrystalWave.RightDown})
                foreach (var purple in new[]{CrystalPurple.MainDiagonal,CrystalPurple.OtherDiagonal})
                {
                    var plan=new CrystallizeTimePlan(0,CrystalDebuff.RedIce,wave,purple,random);
                    var pools=Enumerable.Range(1,4).Select(n=>CrystallizeTimePlan.Circle(plan.PuddleDefault(n),0.3f).Sample(random)).ToArray();
                    var taken=new bool[4];
                    var actors=Enumerable.Range(0,8).Select(slot=>
                    {
                        var actor=new SimActor(new EntityTestWorld().CreateCharacter(2),casts,movement);
                        actor.Pos=(plan.Debuffs[slot]==CrystalDebuff.RedWind?plan.SouthRest:plan.NorthRest).Sample(random);
                        actor.Entity.OnActorMove=(pos,_,__)=>actor.Entity.Pos3D=pos;
                        behavior.Add(actor,new SimActorBehaviorOptions{AttackTarget=false,IdleRadius=0});
                        foreach (var move in plan.RewindRoute(slot,pools))
                            scheduler.Schedule(move.Start-origin,()=>actor.MoveForMechanic(move.Area,move.Arrival-origin,20,urgent:true));
                        return actor;
                    }).ToArray();
                    void CheckPools()
                    {
                        for (var index=0;index<4;index++)
                        {
                            if (taken[index]) continue;
                            for (var member=0;member<4;member++)
                            {
                                var slot=plan.BlueOrder[member];
                                if (Vector2.Distance(actors[slot].Pos,pools[index])>1) continue;
                                Assert(index==member,$"{wave} {purple} 蓝色队员误吃了其他白圈。");
                                taken[index]=true;
                                var leave=plan.LeavePuddle(actors[slot].Pos,scheduler.ElapsedSeconds+origin);
                                scheduler.Schedule(leave.Start-origin,()=>actors[slot].MoveForMechanic(leave.Area,leave.Arrival-origin,20,urgent:true));
                                break;
                            }
                        }
                        if (scheduler.ElapsedSeconds<11.5) scheduler.ScheduleAfter(0.1,CheckPools);
                    }
                    scheduler.Schedule(0.5,CheckPools);
                    for (var w=0;w<2;w++)
                    for (var s=0;s<4;s++)
                    {
                        var t=CrystallizeTimePlan.WaveHits[w][s]-origin;
                        if (t<0) continue;
                        var position=plan.WavePosition(w,s);
                        var direction=plan.WaveDirection(w);
                        var heading=(float)Math.Atan2(direction.X,direction.Y);
                        scheduler.Schedule(t,()=>
                        {
                            for (var slot=0;slot<8;slot++)
                                Assert(!ActionArea.Rectangle(10,40).Contains(actors[slot].Pos,position,heading),$"{wave} {purple} 在 {t+origin:0.000} 秒光波击中 {CrystallizeTimePlan.Roles[slot]}：{actors[slot].Pos}。");
                        });
                    }
                    scheduler.Schedule(11.8,()=>
                    {
                        Assert(taken.All(t=>t),$"{wave} {purple} 未及时吃完白圈。");
                        for (var slot=0;slot<8;slot++) Assert(plan.Rewind(slot).Contains(actors[slot].Pos),$"{wave} {purple} 未在回返记录前完成排队。");
                    });
                }
                scheduler.Schedule(12,()=>done.Set());scheduler.Start();
                Assert(done.Wait(15000),"光波与白圈实际移动检查超时。");
                scheduler.Cancel();
                Assert(failure==null,"光波与白圈实际移动检查失败："+failure);
            }
        }
        public static void AssignmentsAndRandomAreasPreserveMechanics()
        {
            var random=new Random(7358);
            foreach (var priority in new[]{"12345678","34125678","43215678","23184567"})
            foreach (var wave in new[]{CrystalWave.LeftUp,CrystalWave.LeftDown,CrystalWave.RightUp,CrystalWave.RightDown})
            foreach (var purple in new[]{CrystalPurple.MainDiagonal,CrystalPurple.OtherDiagonal})
            foreach (var selected in new[]{CrystalDebuff.RedIce,CrystalDebuff.RedWind,CrystalDebuff.BlueWater,CrystalDebuff.BlueEarth,CrystalDebuff.BlueDark,CrystalDebuff.BlueIce})
            for (var self=0;self<8;self++)
            {
                var plan=new CrystallizeTimePlan(self,selected,wave,purple,random,priority);
                Assert(plan.Debuffs[self]==selected,"自身点名必须服从选项。");
                Assert(plan.Debuffs.Count(d=>d==CrystalDebuff.RedIce)==2 && plan.Debuffs.Count(d=>d==CrystalDebuff.RedWind)==2,"红冰与红风应各两人。");
                Assert(plan.BlueOrder.Distinct().Count()==4 && plan.BlueOrder.All(i=>!plan.IsRed(i)),"四个蓝色头标应互不重复。");
                Assert(plan.BlueOrder.Select(i=>plan.Debuffs[i]).SequenceEqual(new[]{CrystalDebuff.BlueEarth,CrystalDebuff.BlueWater,CrystalDebuff.BlueIce,CrystalDebuff.BlueDark}),
                    "红组优先级不能改变蓝色点名的头标和吃圈顺序。");
                foreach (var kind in new[]{CrystalDebuff.RedIce,CrystalDebuff.RedWind})
                {
                    var left=plan.Red(kind,-1);
                    var right=plan.Red(kind,1);
                    var order=priority=="12345678"?new[]{0,1,2,3,4,5,6,7}:
                        priority=="34125678"?new[]{2,3,0,1,4,5,6,7}:
                        priority=="43215678"?new[]{3,2,1,0,4,5,6,7}:new[]{2,0,1,4,5,6,7,3};
                    Assert(Array.IndexOf(order,left)<Array.IndexOf(order,right),"红色同类必须按所选职能顺序高左低右。");
                }
                Assert(plan.WaterGroup.Length==4 && plan.EarthGroup.Length==5,"水分摊四人、土分摊五人。");
                for (var sample=0;sample<50;sample++)
                {
                    var initial=Enumerable.Range(0,8).Select(i=>plan.Initial(i).Sample(random)).ToArray();
                    foreach (var sand in new[]{plan.Hourglass(0),plan.Hourglass(3)})
                        Assert(initial.All(p=>Vector2.Distance(p,sand)>12),"初始随机区域应避开第一组沙漏。");
                    var water=initial[plan.Slot(CrystalDebuff.BlueWater)];
                    Assert(Enumerable.Range(0,8).Where(i=>Vector2.Distance(initial[i],water)<=6).OrderBy(i=>i).SequenceEqual(plan.WaterGroup.OrderBy(i=>i)),"初始随机区域不能改变水分摊人数。");
                    var wind=plan.Wind(-plan.NorthSlowSide).Sample(random);
                    var riders=Enumerable.Range(0,3).Select(_=>CrystallizeTimePlan.WindRider(wind).Sample(random)).ToArray();
                    foreach (var rider in riders)
                    {
                        Assert(Vector2.Distance(rider,wind)<3,"蓝冰和红风之间应保持在冰封内圈内。");
                        var landing=rider+Vector2.Normalize(rider-wind)*30;
                        Assert(Vector2.Distance(landing,CrystallizeTimePlan.Center)<19,"随机击退角度不能把假队员送到场外。");
                        Assert(Vector2.Distance(landing,plan.Stack.Midpoint())/6+0.4<1.8,"击退落地到土分摊必须留够跑步时间。");
                    }
                    Assert(riders.All(p=>riders.All(q=>Vector2.Distance(p,q)<3)),"蓝色三人的独立随机不能踩出冰封内圈。");
                    var rewinds=Enumerable.Range(0,8).Select(i=>plan.Rewind(i).Sample(random)).ToArray();
                    var x=CrystallizeTimePlan.FrontFour(rewinds,0,plan.FirstSide);
                    var y=CrystallizeTimePlan.FrontFour(rewinds,1,plan.SecondSide);
                    Assert(x[0]<2 && y[0]<2 && x[0]!=y[0],"两次圣翼都应由不同坦克挡在最前方。");
                    Assert(!x.Take(4).Intersect(y.Take(4)).Any(),"两次圣翼的前四人必须互补。");
                    foreach (var pos in rewinds)
                    {
                        var first=pos+plan.WaveDirection(0)*20;
                        var second=first+plan.WaveDirection(1)*20;
                        Assert(Vector2.Distance(pos,CrystallizeTimePlan.Center)<20 && Vector2.Distance(first,CrystallizeTimePlan.Center)<20 && Vector2.Distance(second,CrystallizeTimePlan.Center)<20,"回返与连续击退的三个位置都必须在场内。");
                    }
                    var spreads=Enumerable.Range(0,8).Select(i=>plan.Spread(i).Sample(random)).ToArray();
                    for (var i=0;i<8;i++)
                    {
                        Assert(Vector2.Distance(spreads[i],CrystallizeTimePlan.Center)<20 && Vector2.Distance(spreads[i],CrystallizeTimePlan.Crystal)>5,"分散不能出界或打到水晶。");
                        for (var j=i+1;j<8;j++) Assert(Vector2.Distance(spreads[i],spreads[j])>5,"碎灵一击随机分散不得重叠。");
                        Assert(Vector2.Distance(rewinds[i],spreads[i])/6+0.4<3.05,"记录回返后必须有时间展开分散。");
                    }
                }
            }
        }
        private static void Assert(bool condition,string message) { if (!condition) throw new Exception(message); }
    }
}

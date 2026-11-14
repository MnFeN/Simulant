using Simulant.ACT;
using Simulant.Core.Entity;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal sealed partial class CrystallizeTimeLogic
    {
        private AreaMechanic Targeted(string name, uint action, int target, int[] expected, int helper, ActionArea? area = null)
        {
            var shape=area ?? ActionArea.FromAction(action);
            var center=Vector2.Zero;
            return new AreaMechanic
            {
                Name=name, Area=shape, RequiredPlayers=expected.Length,
                CenterAtResolve=snapshot => center=snapshot.Get(target).Position,
                Visual=result => Effect(helper,action,target),
                Failed=result => Report(result.DescribeFailure(name,expected,target)
                    .WithAttribution(Responsible(result,expected,target), target == Self ? "可能是自身站歪了" : null)),
                Check=members => shape.Contains(CrystallizeTimePlan.Crystal,center,0) ? "技能击中了北侧水晶。" :
                    members.Select(m=>m.Slot).OrderBy(i=>i).SequenceEqual(expected.OrderBy(i=>i)) ? null : "参与者不符合当前点名分工。"
            };
        }

        private int[] Responsible(MechanicResult result, int[] expected, int target)
        {
            var actual=result.Participants.Select(m=>m.Slot).ToArray();
            // 固定假队员作为参照；只有实际漏参与、多参与或自身点名覆盖错误时才归因给玩家。
            if (expected.Contains(Self) != actual.Contains(Self) || target == Self) return new[] {Self};
            return Array.Empty<int>();
        }

        private AreaMechanic Avoid(string name, ActionArea area, Vector2 center, float heading = 0)
            => new AreaMechanic
            {
                Name=name, Area=area, Center=center, Heading=heading, MaxPlayers=0,
                FailureSeverity=MechanicFailureSeverity.Warning,
                Failed=result => Report(MechanicError.Fixed(name,Array.Empty<int>(),result.Participants.Select(m=>m.Slot))
                    .WithAttribution(result.Participants.Select(m=>m.Slot),"自身进入伤害范围"))
            };

        private void ResolveSand(int wave)
        {
            EntityManager.RunMechanic(new MechanicGroup(Enumerable.Range(0,6).Where(i=>_plan.HourglassWave(i)==wave)
                .Select(i=>Avoid($"第 {wave+1} 组沙漏",ActionArea.FromAction(40299),_plan.Hourglass(i))).ToArray()) {Name="沙漏范围判定"});
        }
        private void ResolveWave(int wave,int step) => EntityManager.RunMechanic(Avoid($"第 {wave+1} 组光波第 {step+1} 次",
            ActionArea.FromAction(step==0?40252u:40253u),_plan.WavePosition(wave,step),Heading(_plan.WaveDirection(wave))));
        private void ResolveWater()
        {
            var slot=_plan.Slot(CrystalDebuff.BlueWater); ClearStatus(slot,2461);
            EntityManager.RunMechanic(Targeted("黑暗大水四人分摊",40271,slot,_plan.WaterGroup,0));
        }
        private void ResolveEarth()
        {
            var slot=_plan.Slot(CrystalDebuff.BlueEarth); ClearStatus(slot,2454);
            EntityManager.RunMechanic(Targeted("黑暗神圣五人分摊",40277,slot,_plan.EarthGroup,0));
        }

        private void ResolveIceWindDark()
        {
            var mechanics=new List<SimMechanic>();
            for (var slot=0;slot<8;slot++)
            {
                var target=slot; var debuff=_plan.Debuffs[slot];
                if (debuff == CrystalDebuff.BlueWater || debuff == CrystalDebuff.BlueEarth) continue;
                ClearStatus(slot,CrystallizeTimePlan.Status(debuff));
                if (debuff == CrystalDebuff.BlueDark)
                    mechanics.Add(Targeted("黑暗爆炎分散",40274,slot,new[]{slot},slot));
                else if (debuff == CrystalDebuff.RedWind)
                {
                    var expected=_plan.RedSide[slot] == -_plan.NorthSlowSide ? _plan.WaterGroup : new[]{slot};
                    var mechanic=Targeted("黑暗大风击退",40280,slot,expected,slot,ActionArea.Circle(15));
                    mechanic.Visual=result =>
                    {
                        Effect(target,40280,target);
                        foreach (var member in result.Participants.Where(m=>m.Slot!=target))
                        {
                            var actor=Party[member.Slot]; var direction=member.Position-result.Center;
                            if (direction.LengthSquared()<0.0001f) direction=Vector2.UnitY;
                            actor.SuspendBehavior(0.9);
                            actor.Knockback(Heading(direction),30,0.8f);
                        }
                    };
                    mechanics.Add(mechanic);
                }
                else
                {
                    // 该动作没有预兆行；内圈按上游机制定义取 3 米，外圈仍读取技能表。
                    var mechanic=Avoid(CrystallizeTimePlan.Roles[slot]+" 的黑暗冰封",ActionArea.FromAction(40279,innerRadius:3),Party[slot].Pos);
                    mechanic.Check=members => ActionArea.FromAction(40279,innerRadius:3).Contains(CrystallizeTimePlan.Crystal,Party[target].Pos,0)?"黑暗冰封击中了北侧水晶。":null;
                    mechanic.Visual=result => Effect(target,40279,target);
                    mechanics.Add(mechanic);
                }
            }
            EntityManager.RunMechanic(new MechanicGroup(mechanics.ToArray()) {Name="冰风暗同时结算"});
            Scheduler.ScheduleAfter(0.9,CheckArena,"黑暗大风击退落点");
        }

        private void WatchHeads()
        {
            var time=Now;
            if (time>50.8) return;
            for (var index=0;index<2;index++)
            {
                if (_heads[index]==null || _headHits[index]>=2) continue;
                var side=index==0?-1:1; var position=_plan.HeadPosition(side,time);
                _heads[index].Entity.ActorMove(new Vector3(position,0),Heading(_plan.HeadPosition(side,time+0.1)-position),30);
                var radius=_headHits[index]==0?2f:1f;
                var touched=Enumerable.Range(0,8).Where(slot=>!(_cleansed[slot] && _plan.IsRed(slot)) && Vector2.Distance(Party[slot].Pos,position)<=radius)
                    .OrderBy(slot=>Vector2.DistanceSquared(Party[slot].Pos,position)).ToArray();
                if (touched.Length!=0) TouchHead(index,touched[0]);
            }
            for (var index=0;index<4;index++)
            {
                if (_puddles[index]==null || _taken[index]) continue;
                var touched=Enumerable.Range(0,8).Where(slot=>!_cleansed[slot] && !_plan.IsRed(slot) && Vector2.Distance(Party[slot].Pos,_puddlePositions[index])<=1)
                    .OrderBy(slot=>Vector2.DistanceSquared(Party[slot].Pos,_puddlePositions[index])).ToArray();
                if (touched.Length!=0) TakePuddle(index,touched[0]);
            }
            Scheduler.ScheduleAfter(0.1,WatchHeads,"龙头与白圈接触检查");
        }

        private void TouchHead(int head,int slot)
        {
            var side=head==0?-1:1;
            var expected=_plan.Red(_headHits[head]==0?CrystalDebuff.RedIce:CrystalDebuff.RedWind,side);
            var center=Party[slot].Pos;
            var area=ActionArea.FromAction(40241);
            EntityManager.RunMechanic(new AreaMechanic
            {
                Name="龙头接触爆炸", Area=area, Center=center, Exclude=Party[slot], MaxPlayers=0,
                Check=members=>slot!=expected?"该龙头应由 "+CrystallizeTimePlan.Roles[expected]+" 接触。":
                    area.Contains(CrystallizeTimePlan.Crystal,center,0)?"龙头爆炸击中了北侧水晶。":null,
                Visual=result=>Helper(16+head,center).Execute(40241),
                Failed=result=>Report(MechanicError.Rule(CrystallizeTimePlan.Roles[slot]+" 的龙头接触",new[]{slot}.Concat(result.Participants.Select(m=>m.Slot)),result.Reason)
                    .WithAttribution(slot==Self?new[]{slot}:result.Participants.Where(m=>m.Slot==Self).Select(m=>m.Slot),
                        slot==Self?"自身接触了错误的龙头，或接触位置波及队友、水晶":"自身进入了龙头爆炸范围")),
                Succeeded=result=>
                {
                    _cleansed[slot]=true; ClearStatus(slot,3263); _headHits[head]++;
                    if (_headHits[head]==2)
                    {
                        _heads[head].Execute(40244);
                        Scheduler.ScheduleAfter(0.7,()=>_heads[head].Entity.DisableDraw(),"龙头消失");
                        Move(slot,_plan.SouthRest,32.1,urgent:true);
                    }
                    else _heads[head].Entity.ActorControl(Simulant.Game.ActorControlCategory.SetModelScale,4);
                    var number=_plan.PuddleNumber(slot);
                    Scheduler.ScheduleAfter(1,()=>SpawnPuddle(number,center),"龙头留下白圈");
                }
            });
        }

        private void SpawnPuddle(int number,Vector2 center)
        {
            var index=number-1;
            _puddlePositions[index]=center;
            _puddles[index]=EntityManager.SpawnEObj(new EventObjectSpawnSpec {Index=(byte)index,BaseId=0x1EBD41,
                TargetableFlags=5,EventId=0x800375BF,Pos=new Vector3(center,0),SharedGroupState=(uint)(0x500003+index*0x10000)});
        }

        private void TakePuddle(int index,int slot)
        {
            _taken[index]=true; _cleansed[slot]=true;
            ClearStatus(slot,3264);
            var puddle=_puddles[index];
            puddle.SetEventState(7);
            // 先让已吃掉的白圈消退；停止模拟时也由实体管理器回收，不遗留 EObj 槽位。
            Scheduler.ScheduleAfter(2,()=>EntityManager.Delete(puddle),"回收已消退的白圈");
            if (slot!=_plan.BlueOrder[index])
            {
                Report(MechanicError.Targeted($"{index+1} 号白圈",new[]{_plan.BlueOrder[index]},new[]{slot},slot)
                    .WithAttribution(new[]{slot},"自身吃了其他编号的白圈"));
                throw new SimMechanicFailedException("白圈分配","蓝色队员未按头标吃圈。");
            }
            if (slot!=Self)
            {
                // 光波还未离开右侧时先等候，之后跨进已爆炸的条带；南侧圈要在纵向第一道前撤出。
                var leave=_plan.LeavePuddle(Party[slot].Pos,Now);
                Scheduler.Schedule(_origin+leave.Start,()=>Move(slot,leave.Area,leave.Arrival,urgent:true),"吃白圈后撤出下一道光波");
            }
        }

        private void CheckCleansed(IEnumerable<int> slots,string name)
        {
            var missing=slots.Where(slot=>!_cleansed[slot]).ToArray();
            if (missing.Length==0) return;
            Report(MechanicError.Rule(name,missing,"倒计时结束前未解除龙咒。")
                .WithAttribution(missing,"自身没有及时撞龙头或吃白圈"));
            throw new SimMechanicFailedException(name,"龙咒尚未解除。");
        }

        private void CheckArena()
        {
            var outside=Enumerable.Range(0,8).Where(i=>Vector2.Distance(Party[i].Pos,Center)>20).ToArray();
            if (outside.Length==0) return;
            Report(MechanicError.Rule("击退落点",outside,"落点超出场地。")
                .WithAttribution(outside,"自身击退前的位置或方向不正确"));
            throw new SimMechanicFailedException("击退落点","有人落到场外。");
        }

        private void ResolveSpiritTaker()
        {
            var target=_random.Next(8);
            var mechanic=Targeted("碎灵一击分散",40289,target,new[]{target},0);
            mechanic.Check=members=>ActionArea.FromAction(40289).Contains(CrystallizeTimePlan.Crystal,Party[target].Pos,0)?"碎灵一击击中了北侧水晶。":null;
            mechanic.Visual=result=>_gaia.Entity.ExecuteActionEffect(40289,Party[target].Entity);
            EntityManager.RunMechanic(mechanic);
        }

        private void ResolveWing(int wave)
        {
            var positions=Party.Select(actor=>actor.Pos).ToArray();
            var order=CrystallizeTimePlan.FrontFour(positions,wave,wave==0?_plan.FirstSide:_plan.SecondSide);
            var front=order.Take(4).ToArray();
            var wrongTank=order[0]>=2;
            var repeated=wave==0?Array.Empty<int>():front.Intersect(_firstFront).ToArray();
            if (wave==0) _firstFront=front;
            Scheduler.ScheduleAfter(1.4,()=> { foreach (var slot in front) Status(slot,2941,6); },"圣翼前四人魔法易伤");
            foreach (var actor in Party)
            {
                actor.SuspendBehavior(wave==0?6.9:2.4);
                actor.Knockback(Heading(_plan.WaveDirection(wave)),20,0.8f);
            }
            // 先提交全队击退动画，再报告排队错误。
            if (wrongTank || repeated.Length!=0)
            {
                var offenders=repeated.Concat(wrongTank?new[]{order[0]}:Array.Empty<int>()).Distinct().ToArray();
                Report(MechanicError.Rule($"第 {wave+1} 次圣翼排队",front,
                    wrongTank?"最前方必须是坦克。":"两次击退的前四人应互补，重复进入前排会受到易伤伤害。")
                    .WithAttribution(offenders,"自身排队位置不正确"));
                throw new SimMechanicFailedException("圣翼排队","坦克或前四人顺序错误。");
            }
            Scheduler.ScheduleAfter(0.9,CheckArena,"圣翼击退落点检查");
        }

        private void ResolveAkhMorn(int hit)
        {
            foreach (var slot in CrystallizeTimePlan.LeftParty) Status(slot,4164,4);
            foreach (var slot in CrystallizeTimePlan.RightParty) Status(slot,3323,4);
            EntityManager.RunMechanic(new MechanicGroup(
                Targeted($"死亡轮回第 {hit+1} 次左组分摊",40248,0,CrystallizeTimePlan.LeftParty,0),
                Targeted($"死亡轮回第 {hit+1} 次右组分摊",40303,1,CrystallizeTimePlan.RightParty,1)) {Name="死亡轮回左右分摊"});
        }
        private void ResolveMornAfah() => EntityManager.RunMechanic(Targeted("无尽顿悟八人分摊",40250,0,Enumerable.Range(0,8).ToArray(),0));
    }
}

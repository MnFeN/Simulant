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
        private AreaMechanic Targeted(string name, uint action, int target, int[] expected, int helper,
            ActionArea? area = null, Action<MechanicResult> visual = null)
        {
            var shape = area ?? ActionArea.FromAction(action);
            return ProtectCrystal(new AreaMechanic
            {
                Name = name,
                Area = shape,
                RequiredPlayers = expected.Length,
                CenterAtResolve = snapshot => snapshot.Get(target).Position,
                Visual = visual ?? (result => Effect(helper, action, target)),
                Failed = result => Report(result.DescribeFailure(name, expected, target)
                    .WithAttribution(Responsible(result, expected, target), target == Self ? "可能是自身站歪了" : null), result),
                Check = members => members.Select(m => m.Slot).OrderBy(i => i).SequenceEqual(expected.OrderBy(i => i))
                    ? null : "参与者不符合当前点名分工。"
            }, target);
        }

        private AreaMechanic ProtectCrystal(AreaMechanic mechanic, int target)
        {
            var centerAtResolve = mechanic.CenterAtResolve;
            var check = mechanic.Check;
            var visual = mechanic.Visual;
            var failed = mechanic.Failed;
            var hit = false;
            mechanic.CenterAtResolve = snapshot =>
            {
                var center = centerAtResolve?.Invoke(snapshot) ?? mechanic.Center;
                hit = !_crystalBroken && mechanic.Area.IntersectsCircle(_crystal.Pos, _crystal.Entity.HitboxRadius, center, mechanic.Heading);

                if (hit)
                    mechanic.FailureSeverity = MechanicFailureSeverity.Fatal;

                return center;
            };
            mechanic.Check = members =>
            {
                var reason = check?.Invoke(members);
                return hit ? (reason == null ? "" : reason + " ") + "点名范围击碎了北侧水晶。" : reason;
            };
            mechanic.Visual = result =>
            {
                visual?.Invoke(result);

                if (hit)
                    BreakCrystal();
            };
            mechanic.Failed = result =>
            {
                if (hit)
                    Report(MechanicError.Rule(mechanic.Name, new[]
                    {
                        target
                    }, result.Reason).WithAttribution(target == Self ? new[]
                    {
                        Self
                    } : Array.Empty<int>(), "自身点名范围击中了北侧水晶"), result);
                else
                    failed?.Invoke(result);
            };
            return mechanic;
        }

        private void BreakCrystal()
        {
            if (_crystalBroken)
                return;

            _crystalBroken = true;
            _crystal.StopCasting();
            _crystal.Entity.SetNetworkVitals(0);
            // 回放 4786.223 秒水晶破碎：死亡动画、状态 2，同时 MapEffect 0x2E 切换到 0x10。
            _crystal.Entity.ActorControl(Simulant.Game.ActorControlCategory.DeathAnimation);
            _crystal.Entity.ActorControl(Simulant.Game.ActorControlCategory.SetStatus, 2);
            Map(0x2E, 0x10);
        }

        private int[] Responsible(MechanicResult result, int[] expected, int target)
        {
            var actual = result.Participants.Select(m => m.Slot).ToArray();

            // 固定假队员作为参照；只有实际漏参与、多参与或自身点名覆盖错误时才归因给玩家。
            if (expected.Contains(Self) != actual.Contains(Self) || target == Self)
                return new[]
                {
                    Self
                };

            return Array.Empty<int>();
        }

        private AreaMechanic Avoid(string name, ActionArea area, Vector2 center, float heading = 0)
            => new AreaMechanic
            {
                Name = name,
                Area = area,
                Center = center,
                Heading = heading,
                MaxPlayers = 0,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Failed = result => Report(MechanicError.Fixed(name, Array.Empty<int>(), result.Participants.Select(m => m.Slot))
                        .WithAttribution(result.Participants.Select(m => m.Slot), "自身进入伤害范围"), result)
            };

        private void ResolveSand(int wave)
        {
            EntityManager.RunMechanic(new MechanicGroup(Enumerable.Range(0, 6).Where(i => _plan.HourglassWave(i) == wave)
                .Select(i => Avoid($"第 {wave + 1} 组沙漏", ActionArea.FromAction(40299), _plan.Hourglass(i))).ToArray())
            {
                Name = "沙漏范围判定"
            });
        }

        private void ResolveWave(int wave, int step) => EntityManager.RunMechanic(Avoid($"第 {wave + 1} 组光波第 {step + 1} 次",
            ActionArea.FromAction(step == 0 ? 40252u : 40253u), _plan.WavePosition(wave, step), Heading(_plan.WaveDirection(wave))));
        private void ResolveWater()
        {
            var slot = _plan.Slot(CrystalDebuff.BlueWater);
            ClearStatus(slot, 2461);
            EntityManager.RunMechanic(Targeted("黑暗狂水四人分摊", 40271, slot, _plan.WaterGroup, 0));
        }

        private void ResolveEarth()
        {
            var slot = _plan.Slot(CrystalDebuff.BlueEarth);
            ClearStatus(slot, 2454);
            EntityManager.RunMechanic(Targeted("黑暗神圣五人分摊", 40277, slot, _plan.EarthGroup, 0));
        }

        private void ResolveIceWindDark()
        {
            foreach (var slot in _plan.WaterGroup.Where(i => !_plan.IsRed(i) && i != Self))
                Party[slot].StopMoving();

            var mechanics = new List<SimMechanic>();

            for (var slot = 0; slot < 8; slot++)
            {
                var target = slot;
                var debuff = _plan.Debuffs[slot];

                if (debuff == CrystalDebuff.BlueWater || debuff == CrystalDebuff.BlueEarth)
                    continue;

                ClearStatus(slot, CrystallizeTimePlan.Status(debuff));

                if (debuff == CrystalDebuff.BlueDark)
                    mechanics.Add(Targeted("暗炎喷发分散", 40274, slot, new[]
                    {
                    slot
                    }, slot));
                else if (debuff == CrystalDebuff.RedWind)
                {
                    var expected = _plan.RedSide[slot] == -_plan.NorthSlowSide ? _plan.WaterGroup : new[]
                    {
                        slot
                    };
                    var mechanic = Targeted("黑暗暴风击退", 40280, slot, expected, slot, ActionArea.Circle(15));
                    var visual = mechanic.Visual;

                    mechanic.Visual = result =>
                    {
                        visual(result);

                        foreach (var member in result.Participants.Where(m => m.Slot != target))
                        {
                            var actor = Party[member.Slot];
                            var direction = member.Position - result.Center;

                            if (direction.LengthSquared() < 0.0001f)
                                direction = Vector2.UnitY;

                            actor.SuspendBehavior(0.9);
                            actor.Knockback(Heading(direction), 30, 0.8f);
                        }
                    };

                    mechanics.Add(mechanic);
                }
                else
                {
                    // 该动作没有预兆行；内圈按上游机制定义取 3 米，外圈仍读取技能表。
                    var mechanic = Avoid(CrystallizeTimePlan.Roles[slot] + " 的黑暗冰封", ActionArea.FromAction(40279, innerRadius: 3), Party[slot].Pos);
                    mechanic.Visual = result => Effect(target, 40279, target);
                    mechanics.Add(ProtectCrystal(mechanic, slot));
                }
            }

            EntityManager.RunMechanic(new MechanicGroup(mechanics.ToArray())
            {
                Name = "冰风暗同时结算"
            });
        }

        private void WatchHeads()
        {
            var time = Now;

            if (time > 50.8)
                return;

            for (var index = 0; index < 2; index++)
            {
                if (_heads[index] == null || _headHits[index] >= 2)
                    continue;

                var side = index == 0 ? -1 : 1;
                var position = _plan.HeadPosition(side, time);
                _heads[index].Entity.ActorMove(new Vector3(position, 0), Heading(_plan.HeadPosition(side, time + 0.1) - position), 30);
                var radius = _headHits[index] == 0 ? 2f : 1f;
                var touched = Enumerable.Range(0, 8).Where(slot => !(_cleansed[slot] && _plan.IsRed(slot)) && Vector2.Distance(Party[slot].Pos, position) <= radius)
                    .OrderBy(slot => Vector2.DistanceSquared(Party[slot].Pos, position)).ToArray();

                if (touched.Length != 0)
                    TouchHead(index, touched[0], position);
            }

            for (var index = 0; index < 4; index++)
            {
                if (_puddles[index] == null || _taken[index])
                    continue;

                var touched = Enumerable.Range(0, 8).Where(slot => !_cleansed[slot] && !_plan.IsRed(slot) && Vector2.Distance(Party[slot].Pos, _puddlePositions[index]) <= 1)
                    .OrderBy(slot => Vector2.DistanceSquared(Party[slot].Pos, _puddlePositions[index])).ToArray();

                if (touched.Length != 0)
                    TakePuddle(index, touched[0]);
            }

            Scheduler.ScheduleAfter(0.1, WatchHeads, "龙头与白圈接触检查");
        }

        private void TouchHead(int head, int slot, Vector2 center)
        {
            var side = head == 0 ? -1 : 1;
            var expected = _plan.Red(_headHits[head] == 0 ? CrystalDebuff.RedIce : CrystalDebuff.RedWind, side);
            var area = ActionArea.FromAction(40241);
            var name = $"{CrystallizeTimePlan.Roles[slot]} 的第 {_headHits[head] + 1} 次龙头接触爆炸";

            EntityManager.RunMechanic(ProtectCrystal(new AreaMechanic
            {
                Name = name,
                Area = area,
                Center = center,
                RequiredPlayers = 1,
                Check = members => slot != expected ? "该龙头应由 " + CrystallizeTimePlan.Roles[expected] + " 接触。" : null,
                Visual = result =>
                {
                    var helper = _helpers[16 + head];
                    // 辅助施法者立即定位到本次碰撞处；ActorMove 的插值尚未结束时不能播放原地特效。
                    helper.Entity.ActorSetPos(new Vector3(center, 0), 0);
                    helper.Execute(40241);
                },
                Failed = result =>
                {
                    var extra = result.Participants.Where(m => m.Slot != slot).Select(m => m.Slot).ToArray();
                    var wrongTarget = slot != expected;
                    var offenders = wrongTarget ? new[]
                    {
                        slot
                    }.Concat(extra) : extra;
                    var detail = wrongTarget ? "该次龙头应由 " + CrystallizeTimePlan.Roles[expected] + " 接触" :
                        CrystallizeTimePlan.Roles[slot] + " 正确接触龙头";
                    if (extra.Length != 0)
                        detail += "；" + string.Join(" ", extra.Select(i => CrystallizeTimePlan.Roles[i])) + " 未避开本次爆炸";

                    Report(result.DescribeFailure(name, new[]
                    {
                        expected
                    }, slot)
                        .WithDetail(detail)
                        .WithAttribution(offenders, wrongTarget && slot == Self ? "自身接触了其他队员应处理的龙头" : "自身未避开龙头爆炸"), result);
                },
                Succeeded = result =>
                {
                    _cleansed[slot] = true;
                    ClearStatus(slot, 3263);
                    _headHits[head]++;

                    if (_headHits[head] == 2)
                    {
                        _heads[head].Execute(40244);
                        Scheduler.ScheduleAfter(0.7, () => _heads[head].Entity.DisableDraw(), "龙头消失");
                        Move(slot, _plan.SouthRest, 32.1, urgent: true);
                    }
                    else
                    {
                        _heads[head].Entity.ActorControl(Simulant.Game.ActorControlCategory.SetModelScale, 4);
                        foreach (var move in _plan.RedIceRoute(side, Now))
                            Scheduler.Schedule(_origin + move.Start, () => Move(slot, move.Area, move.Arrival, urgent: true), "红冰撞头后撤离");
                    }

                    var number = _plan.PuddleNumber(slot);
                    Scheduler.ScheduleAfter(1, () => SpawnPuddle(number, center), "龙头留下白圈");
                }
            }, slot));
        }

        private void SpawnPuddle(int number, Vector2 center)
        {
            var index = number - 1;
            _puddlePositions[index] = center;
            _puddles[index] = EntityManager.SpawnEObj(new EventObjectSpawnSpec
            {
                Index = (byte)index,
                BaseId = 0x1EBD41,
                TargetableFlags = 5,
                EventId = 0x800375BF,
                Pos = new Vector3(center, 0),
                SharedGroupState = (uint)(0x500003 + index * 0x10000)
            });
        }

        private void TakePuddle(int index, int slot)
        {
            _taken[index] = true;
            _cleansed[slot] = true;
            ClearStatus(slot, 3264);
            var puddle = _puddles[index];
            puddle.SetEventState(7);
            // 先让已吃掉的白圈消退；停止模拟时也由实体管理器回收，不遗留 EObj 槽位。
            Scheduler.ScheduleAfter(2, () => EntityManager.Delete(puddle), "回收已消退的白圈");

            if (slot != _plan.BlueOrder[index])
            {
                Report(MechanicError.Targeted($"{index + 1} 号白圈", new[]
                {
                    _plan.BlueOrder[index]
                }, new[]
                {
                    slot
                }, slot)
                    .WithAttribution(new[]
                {
                    slot
                }, "自身吃了其他编号的白圈"));
                throw new SimMechanicFailedException("白圈分配", "蓝色队员未按头标吃圈。");
            }

            if (slot != Self)
            {
                // 光波还未离开右侧时先等候，之后跨进已爆炸的条带；南侧圈要在纵向第一道前撤出。
                var leave = _plan.LeavePuddle(Party[slot].Pos, Now);
                Scheduler.Schedule(_origin + leave.Start, () => Move(slot, leave.Area, leave.Arrival, urgent: true), "吃白圈后撤出下一道光波");
            }
        }

        private void CheckCleansed(IEnumerable<int> slots, string name)
        {
            var missing = slots.Where(slot => !_cleansed[slot]).ToArray();

            if (missing.Length == 0)
                return;

            Report(MechanicError.Rule(name, missing, "倒计时结束前未解除龙咒。")
                .WithAttribution(missing, "自身没有及时撞龙头或吃白圈"));
            throw new SimMechanicFailedException(name, "龙咒尚未解除。");
        }

        private void CheckArena()
        {
            var outside = Enumerable.Range(0, 8).Where(i => Vector2.DistanceSquared(Party[i].Pos, Center) > 400).ToArray();
            if (outside.Length != 0)
            {
                Report(MechanicError.Rule("电网即死", outside, "超出场地 20 米半径，触碰电网。")
                    .WithAttribution(outside, "自身走出场地，或击退前站位、防击退时机不正确"));
                throw new SimMechanicFailedException("电网即死", "有人触碰场外电网。");
            }

            Scheduler.ScheduleAfter(0.1, CheckArena, "电网即死检查");
        }

        private void ResolveSpiritTaker()
        {
            var area = ActionArea.FromAction(40289);
            var target = CrystallizeTimePlan.SpiritTakerTarget(Party.Select(actor => actor.Pos).ToArray(), Self,
                area, _crystal.Pos, _crystal.Entity.HitboxRadius, _random);
            var mechanic = Targeted("碎灵一击分散", 40289, target, new[]
            {
                target
            }, 0, area, result =>
            {
                _gaia.Entity.ExecuteActionEffect(40289, Party[target].Entity);

                // 主目标承受跳跃，范围内的其他人向外击退 40 米。
                foreach (var member in result.Participants.Where(member => member.Slot != target))
                {
                    var direction = member.Position - result.Center;

                    if (direction.LengthSquared() < 0.0001f)
                        direction = member.Position - Center;

                    if (direction.LengthSquared() < 0.0001f)
                        direction = Vector2.UnitY;

                    Party[member.Slot].Knockback(Heading(direction), 40, 0.8f);
                }
            });
            EntityManager.RunMechanic(mechanic);
        }

        private void BeginWing(int wave)
        {
            var positions = Party.Select(actor => actor.Pos).ToArray();
            var order = CrystallizeTimePlan.FrontFour(positions, wave, wave == 0 ? _plan.FirstSide : _plan.SecondSide);
            var immune = Party.Select(actor => actor.IsKnockbackImmune).ToArray();
            _shiva.Execute(40332);

            foreach (var actor in Party)
                actor.SuspendBehavior(wave == 0 ? 6.9 : 2.4);

            // 回放：ActionEffect 为 53.241 / 57.697 秒，击退移动从 54.625 / 59.065 秒开始。
            // 先锁定此次技能的排队和免疫结果，等翅膀打下来再位移，期间状态到期不改变结果。
            Scheduler.ScheduleAfter(wave == 0 ? 1.384 : 1.368, () => ResolveWing(wave, order, immune), "圣翼命中与击退");
        }

        private void ResolveWing(int wave, int[] order, bool[] immune)
        {
            var front = order.Take(4).ToArray();
            var tankCount = _plan.IsY && wave == 1 ? 2 : 1;
            var wrongTank = !_plan.WingTanksCorrect(order, wave);
            // Y 字的重复前排伤害按已有减伤处理，只检查第一次首位、第二次前两位的坦克。
            var repeated = wave == 0 || _plan.IsY ? Array.Empty<int>() : front.Intersect(_firstFront).ToArray();

            if (wave == 0)
                _firstFront = front;

            foreach (var slot in front)
                Status(slot, 2941, 6);

            for (var slot = 0; slot < Party.Count; slot++)
                if (!immune[slot])
                    Party[slot].Knockback(Heading(_plan.WaveDirection(wave)), 20, 0.8f, ignoreImmunity: true);

            // 先提交全队击退动画，再报告排队错误。
            if (wrongTank || repeated.Length != 0)
            {
                var offenders = repeated.Concat(wrongTank ? order.Take(tankCount).Where(slot => slot >= 2) : Array.Empty<int>()).ToList();
                if (wrongTank && Self < 2 && !order.Take(tankCount).Contains(Self))
                    offenders.Add(Self);

                Report(MechanicError.Rule($"第 {wave + 1} 次圣翼排队", front,
                    wrongTank ? (tankCount == 2 ? "最前两位必须都是坦克。" : "最前方必须是坦克。") : "两次击退的前四人应互补，重复进入前排会受到易伤伤害。")
                    .WithAttribution(offenders.Distinct(), "自身排队位置不正确"));
                throw new SimMechanicFailedException("圣翼排队", "坦克或前四人顺序错误。");
            }
        }
    }
}

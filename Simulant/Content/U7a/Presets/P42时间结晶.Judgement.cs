using Simulant.Content.U7a.Abilities;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal sealed partial class CrystallizeTimeLogic
    {
        private readonly double[] _puddleReadyAt = new double[4];

        private AreaMechanic Targeted(string name, uint action, int target, int[] expected, int helper,
            ActionArea? area = null, Action<MechanicResult> visual = null)
        {
            var mechanic = new AreaMechanic(name, area ?? ActionArea.FromAction(action), centerSlot: target)
            {
                RequiredPlayers = expected.Length,
                Check = members => AreaMechanic.CheckPartyAssignment(members, expected)
            };
            return WithTargetEffects(mechanic, action, target, expected, helper, visual);
        }

        private AreaMechanic WithTargetEffects(AreaMechanic mechanic, uint action, int target, int[] expected, int helper,
            Action<MechanicResult> visual = null)
        {
            mechanic.Visual = result =>
            {
                if (action == 黑暗狂水.ActionId || action == 40277)
                    EntityManager.MarkUnderfilledStack(result.HitActors.ToArray(), expected.Length, _gaia);
                if (visual != null)
                    visual(result);
                else
                    Effect(helper, action, target, result.HitActors);
            };
            mechanic.Failed = result => Report(result.DescribeFailure(mechanic.Name, expected, target)
                .WithAttribution(Responsible(result, expected, target), target == Self ? "可能是自身站歪了" : null), result);
            return ProtectCrystal(mechanic, target);
        }

        private AreaMechanic ProtectCrystal(AreaMechanic mechanic, int target)
            => _crystal.Protect(mechanic, target, Self, Report);

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

        private AreaMechanic Avoid(string name, ActionArea area, Vector2 center, float heading = 0, int? target = null)
            => new AreaMechanic(name, area, center)
            {
                Heading = heading,
                MaxPlayers = 0,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Failed = result => Report(result.DescribeFailure(name, Array.Empty<int>(), target)
                        .WithAttribution(result.Participants.Select(m => m.Slot), "自身进入伤害范围"), result)
            };

        private void ResolveSand(int wave)
        {
            EntityManager.RunMechanic(new MechanicGroup(Enumerable.Range(0, 6).Where(i => _plan.HourglassWave(i) == wave)
                .Select(i => Avoid($"第 {wave + 1} 组沙漏", ActionArea.FromAction(40299), _plan.Hourglass(i))))
            {
                Name = "沙漏范围判定"
            });
        }

        private void ResolveWave(int wave, int step) => EntityManager.RunMechanic(Avoid($"第 {wave + 1} 组光波第 {step + 1} 次",
            ActionArea.FromAction(step == 0 ? 40252u : 40253u), _plan.WavePosition(wave, step), Heading(_plan.WaveDirection(wave))));
        private void ResolveWater()
        {
            var slot = _plan.Slot(CrystalDebuff.BlueWater);
            var expected = _plan.WaterGroup;
            var water = 黑暗狂水.CreateStack(EntityManager, "黑暗狂水四人分摊", slot, expected);
            EntityManager.RunMechanic(WithTargetEffects(water, 黑暗狂水.ActionId, slot, expected, 0));
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

                ClearStatus(slot, P42时间结晶Plan.Status(debuff));

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
                        var center = Party[target].Pos;
                        visual(result);
                        SimKnockback.Resolve(Scheduler, result.HitActors, KnockbackEffect.Radial(center, 30, 0.8f),
                            exclude: Party[target], suspendFor: 0.9);
                    };

                    mechanics.Add(mechanic);
                }
                else
                {
                    // 该动作没有预兆行；内圈按上游机制定义取 3 米，外圈仍读取技能表。
                    var mechanic = Avoid(P42时间结晶Plan.Roles[slot] + " 的黑暗冰封", ActionArea.FromAction(40279, innerRadius: 3), Party[slot].JudgementPosition, target: slot);
                    mechanic.Visual = result => Effect(target, 40279, target, result.HitActors);
                    mechanics.Add(ProtectCrystal(mechanic, slot));
                }
            }

            EntityManager.RunMechanic(new MechanicGroup(mechanics)
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
                _heads[index].ActorMove(new Vector3(position, 0), Heading(_plan.HeadPosition(side, time + 0.1) - position), 30);
                var radius = _headHits[index] == 0 ? 2f : 1f;
                var touched = FindContact(position, radius, slot => !(_cleansed[slot] && _plan.IsRed(slot)));

                if (touched.HasValue)
                    TouchHead(index, touched.Value, position);
            }

            for (var index = 0; index < 4; index++)
            {
                if (_puddles[index] == null || _taken[index] || time < _puddleReadyAt[index])
                    continue;

                var touched = FindContact(_puddlePositions[index], 1, slot => !_cleansed[slot] && !_plan.IsRed(slot));

                if (touched.HasValue)
                    TakePuddle(index, touched.Value);
            }

            Scheduler.ScheduleAfter(0.1, WatchHeads, "龙头与白圈接触检查");
        }

        private int? FindContact(Vector2 center, float radius, Func<int, bool> eligible)
            => Enumerable.Range(0, Party.Count).Where(eligible)
                .Select(slot => (Slot: slot, Position: Party[slot].JudgementPosition))
                .Where(member => Vector2.Distance(member.Position, center) <= radius)
                .OrderBy(member => Vector2.DistanceSquared(member.Position, center))
                .Select(member => (int?)member.Slot).FirstOrDefault();

        private void TouchHead(int head, int slot, Vector2 center)
        {
            var side = head == 0 ? -1 : 1;
            var expected = _plan.Red(_headHits[head] == 0 ? CrystalDebuff.RedIce : CrystalDebuff.RedWind, side);
            var area = ActionArea.FromAction(40241);
            var name = $"{P42时间结晶Plan.Roles[slot]} 的第 {_headHits[head] + 1} 次龙头接触爆炸";

            EntityManager.RunMechanic(ProtectCrystal(new AreaMechanic(name, area, center)
            {
                RequiredPlayers = 1,
                Check = members => slot != expected ? "该龙头应由 " + P42时间结晶Plan.Roles[expected] + " 接触。" : null,
                Visual = result =>
                {
                    var helper = _helpers[16 + head];
                    // 辅助施法者立即定位到本次碰撞处；ActorMove 的插值尚未结束时不能播放原地特效。
                    helper.ActorSetPos(new Vector3(center, 0), 0);
                    // 实战的圣龙气息用 ActionEffect8，单人命中也记录 16 行。
                    helper.Execute(40241, result.HitActors, packetTargetCapacity: 8);
                },
                Failed = result =>
                {
                    var extra = result.Participants.Where(m => m.Slot != slot).Select(m => m.Slot).ToArray();
                    var wrongTarget = slot != expected;
                    // 点名身份相符不等于处理正确；自身撞头波及假队员时，不能反过来归责受击者。
                    var offenders = wrongTarget || slot == Self ? new[] { slot } : extra;
                    var detail = wrongTarget ? "该次龙头应由 " + P42时间结晶Plan.Roles[expected] + " 接触" :
                        P42时间结晶Plan.Roles[slot] + " 接触了分配给自己的龙头，但本次处理失败";
                    if (extra.Length != 0)
                        detail += "；接触爆炸波及 " + string.Join(" ", extra.Select(i => P42时间结晶Plan.Roles[i]));

                    var selfHint = slot != Self ? "自身未避开龙头爆炸" : wrongTarget ? "自身接触了其他队员应处理的龙头" :
                        "自身可能接触龙头过晚或接触位置不当，导致爆炸波及队友或水晶";

                    Report(result.DescribeFailure(name, new[] { expected }, slot)
                        .WithDetail(detail)
                        .WithAttribution(offenders, selfHint), result);
                },
                Succeeded = result =>
                {
                    CleanseDragonCurse(slot);
                    _headHits[head]++;

                    if (_headHits[head] == 2)
                    {
                        _heads[head].Execute(40244);
                        Scheduler.ScheduleAfter(0.7, () =>
                        {
                            _heads[head].Entity.DisableDraw();
                            EntityManager.SetEnemyListVisibility(_heads[head], false);
                        }, "龙头消失", finishOnMechanicFailure: true);
                        foreach (var move in _plan.RedWindRoute(Party[slot].Pos, Now))
                            Scheduler.Schedule(_origin + move.Start, () => Move(slot, move.Area, move.Arrival, urgent: true), "红风撞头后沿南侧撤离");
                    }
                    else
                    {
                        _heads[head].ActorControl(Simulant.Game.ActorControlCategory.SetModelScale, 4);
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
            // 从实际生成完成起计时，前两秒只显示白圈，不接受接触。
            _puddleReadyAt[index] = Now + 2;
        }

        private void TakePuddle(int index, int slot)
        {
            _taken[index] = true;
            CleanseDragonCurse(slot);
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

        private ushort DragonCurseStatus(int slot) => _plan.IsRed(slot) ? (ushort)3263 : (ushort)3264;

        private void CleanseDragonCurse(int slot)
        {
            _cleansed[slot] = true;
            ClearStatus(slot, DragonCurseStatus(slot));
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
            var outside = Enumerable.Range(0, 8).Where(i => Vector2.DistanceSquared(Party[i].JudgementPosition, Center) > 400).ToArray();
            if (outside.Length != 0)
            {
                foreach (var slot in outside)
                    Report(MechanicError.Boundary("电网", slot));

                throw new SimMechanicFailedException("电网即死", "有人触碰场外电网。", alreadyReported: true);
            }

            Scheduler.ScheduleAfter(0.1, CheckArena, "电网即死检查");
        }

        private void ResolveSpiritTaker()
        {
            var hit = new 碎灵一击(EntityManager, Self, _crystal, Random);
            var target = hit.Target;
            var mechanic = Targeted(碎灵一击.Name, 碎灵一击.ActionId, target.Slot, new[] { target.Slot }, 0,
                hit.Area, hit.KnockbackFrom(target.Actor.Pos));
            mechanic.Started = _ => _gaia.ExecuteActionEffect(碎灵一击.ActionId, target.Actor);
            hit.Resolve(mechanic);
        }

        private void BeginWing(int wave)
        {
            var positions = Party.Select(actor => actor.JudgementPosition).ToArray();
            var order = P42时间结晶Plan.FrontFour(positions, wave, wave == 0 ? _plan.FirstSide : _plan.SecondSide);
            var front = order.Take(4).ToArray();

            // 回放：ActionEffect 为 53.241 / 57.697 秒，击退移动从 54.625 / 59.065 秒开始。
            SimKnockback.Resolve(Scheduler, Party, KnockbackEffect.DirectionalFromRow(Heading(_plan.WaveDirection(wave)), 108),
                movementAt: _origin + (wave == 0 ? 54.625 : 59.065),
                beforeMovement: () =>
                {
                    if (wave == 0)
                        _firstFront = front;
                    foreach (var slot in front)
                        Status(slot, 2941, 6);
                },
                afterMovement: () => CheckWingOrder(wave, order, front), name: "圣翼命中与击退");
            _shiva.Execute(40332);

            foreach (var actor in Party)
                actor.SuspendBehavior(wave == 0 ? 6.9 : 2.4);
        }

        private void CheckWingOrder(int wave, int[] order, int[] front)
        {
            var tankCount = _plan.IsY && wave == 1 ? 2 : 1;
            var wrongTank = !_plan.WingTanksCorrect(order, wave);
            // Y 字的重复前排伤害按已有减伤处理，只检查第一次首位、第二次前两位的坦克。
            var repeated = wave == 0 || _plan.IsY ? Array.Empty<int>() : front.Intersect(_firstFront).ToArray();

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

            if (wave == 1)
                // 等击退落地及延迟的电网检查完成，避免尚未判定出界就先显示成功。
                Scheduler.ScheduleAfter(0.9 + EntityManager.SelfJudgementAdvance, () =>
                {
                    if (_mistakes != 0 || !Party[Self].IsActive)
                        return;

                    try
                    {
                        VfxManager.ShowLockOn(Party[Self].Address, "m0489trg_a0c");
                    }
                    catch (Exception ex)
                    {
                        _host.LogWarning("[时间结晶] 成功提示特效显示失败：" + ex.Message);
                    }
                }, "两次圣翼击退完成提示");
        }
    }
}

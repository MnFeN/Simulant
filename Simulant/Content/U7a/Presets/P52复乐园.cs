using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(5.2)]
    internal sealed class P52复乐园 : SimPresetBase
    {
        private const ParadiseTowerRule DefaultTowerRule = ParadiseTowerRule.FirstWing;
        private const ParadiseTowerGroups DefaultTowerGroups = ParadiseTowerGroups.H12D12D34;

        public override int TerritoryId => 1238;
        public override string Name => "P5-2 复乐园";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 23);
        public override PhaseData Phase => U7a.P5;
        public override int Level => 100;
        public override Type SimLogicType => typeof(ParadiseRegainedLogic);
        public override WaymarkLayout Waymarks => WaymarkLayout.Center(100, 100).Circle("A14C32", true, 8);
        public override string Description => 
            "复乐园 / 光与暗的孤翼。双 T 换仇，依次引导扇形和远近连线。其余人按所选方式分配三座双人塔。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new ComboBoxOption<ParadiseTowerRule>(nameof(ParadiseRegainedLogic.TowerRule), "踩塔分配方式", DefaultTowerRule, new Map<ParadiseTowerRule>
            {
                [ParadiseTowerRule.FirstWing] = "按首翼（后、危险侧、安全侧）",
                [ParadiseTowerRule.LeftRight] = "按左右（后、左、右）",
                [ParadiseTowerRule.SpawnOrder] = "按出塔顺序（后、第二塔、第三塔）"
            }, "首塔为后。左右以从首塔面向场中为准。"),
            new ComboBoxOption<ParadiseTowerGroups>(nameof(ParadiseRegainedLogic.TowerGroups), "踩塔者顺序", DefaultTowerGroups, new Map<ParadiseTowerGroups>
            {
                [ParadiseTowerGroups.H12D12D34] = "H12 D12 D34",
                [ParadiseTowerGroups.H12D34D12] = "H12 D34 D12",
                [ParadiseTowerGroups.D12H12D34] = "D12 H12 D34",
                [ParadiseTowerGroups.D12D34H12] = "D12 D34 H12",
                [ParadiseTowerGroups.D34H12D12] = "D34 H12 D12",
                [ParadiseTowerGroups.D34D12H12] = "D34 D12 H12"
            }, "三组依次对应所选分配方式的三座塔。"),
            new ComboBoxOption<ParadiseTower>(nameof(ParadiseRegainedLogic.FirstTower), "第一塔位置", ParadiseTower.Random, new Map<ParadiseTower>
            {
                [ParadiseTower.Random] = "随机",
                [ParadiseTower.South] = "南",
                [ParadiseTower.Northeast] = "东北",
                [ParadiseTower.Northwest] = "西北"
            }),
            U7aExerciseOptions.Turn("塔出现顺序"),
            new ComboBoxOption<ParadiseWings>(nameof(ParadiseRegainedLogic.Wings), "光暗顺序", ParadiseWings.Random, new Map<ParadiseWings>
            {
                [ParadiseWings.Random] = "随机",
                [ParadiseWings.DarkFirst] = "先暗后光",
                [ParadiseWings.LightFirst] = "先光后暗"
            })
        };

        internal static (ParadiseTowerRule Rule, ParadiseTowerGroups Groups) ConfiguredAssignment(Configuration configuration)
        {
            var saved = configuration?.Presets.Find(preset => preset.Id == typeof(P52复乐园).FullName);
            return (ReadOption(nameof(ParadiseRegainedLogic.TowerRule), DefaultTowerRule),
                ReadOption(nameof(ParadiseRegainedLogic.TowerGroups), DefaultTowerGroups));

            T ReadOption<T>(string name, T fallback) where T : struct, Enum
            {
                var value = saved?.Options.Find(option => option.Name == name)?.Value;
                return Enum.TryParse(value, out T parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;
            }
        }
    }

    internal sealed class ParadiseRegainedLogic : U7aP5ExerciseLogic
    {
        [SimOption] public ParadiseTower FirstTower { get; set; }
        [SimOption] public ExerciseTurn Rotation { get; set; }
        [SimOption] public ParadiseWings Wings { get; set; }
        [SimOption] public ParadiseTowerRule TowerRule { get; set; }
        [SimOption] public ParadiseTowerGroups TowerGroups { get; set; }

        private ParadiseRegainedRound _round;

        public ParadiseRegainedLogic(PluginHost host, SimPresetBase preset) : base(host, preset, FulgentBladeHelper)
        {
        }

        protected override void PreparePlan()
            => _round = new ParadiseRegainedRound(this, 0, 1, FirstTower, Rotation, Wings, TowerRule, TowerGroups);

        protected override SimPosition InitialPartyPosition(int slot)
            => Spot(Center + new Vector2((slot & 1) == 0 ? -1.5f : 1.5f, slot < 2 ? -8 : 5), 0.8f);

        protected override SimTimeline BuildExercise()
        {
            var timeline = _round.Build();
            timeline.At(28.35, Complete, "复乐园结束");
            return timeline;
        }
    }

    internal abstract partial class U7aP5ExerciseLogic
    {
        protected sealed class ParadiseRegainedRound
        {
            private const double SecondTowerHit = 17.890;
            private const double SecondCleave = 18.081;
            private const double TowerStatusDelay = 0.60;

            private readonly U7aP5ExerciseLogic _fight;
            private readonly double _offset;
            private readonly int _occurrence;
            private readonly P52复乐园Plan _plan;
            private readonly ActionArea _towerArea;
            private readonly int[] _towerCounts = new int[3];
            private readonly double[] _magicVulnerableUntil = new double[8];
            private readonly PartySnapshot[] _busterSnapshots = new PartySnapshot[2];
            private readonly int[] _tetherTargets = new int[2];
            private int _initialTank;
            private int _shownTether;
            private int _trackingWing;
            private int _firstCleaveTarget;
            private float _openingHeading;

            internal ParadiseRegainedRound(U7aP5ExerciseLogic fight, double offset, int occurrence,
                ParadiseTower firstTower, ExerciseTurn rotation, ParadiseWings wings, ParadiseTowerRule rule, ParadiseTowerGroups groups)
            {
                _fight = fight;
                _offset = offset;
                _occurrence = occurrence;
                _plan = new P52复乐园Plan(Center, firstTower, rotation, wings, rule, groups);
                _towerArea = ActionArea.FromAction(0x9D80);
                for (var wave = 0; wave < 3; wave++)
                    _towerCounts[wave] = -1;
                _shownTether = -1;
                _trackingWing = -1;
            }

            internal SimTimeline Build()
            {
                var opening = _plan.Dark(0) ? 0x9D29u : 0x9D79u;
                var firstTowerHit = _occurrence == 1 ? 14.368 : 14.387;
                var firstCleave = _occurrence == 1 ? 14.317 : 14.453;
                var thirdTowerHit = _occurrence == 1 ? 21.358 : 21.388;
                var timeline = new SimTimeline();
                if (_occurrence == 1)
                    _fight.AddDialogue(timeline, 0, 35, _fight.BossNameId, "潘多拉的复乐园台词");

                timeline.At(0, () =>
                {
                    _initialTank = _fight.GetInitialTank();
                    _openingHeading = _fight.BossHeading;
                    _fight.LockBossFacing();
                    _fight.CastBoss(0x9D7F, 3.7f);
                }, "复乐园读条");
                timeline.At(4.008, () => _fight.BossEffect(0x9D7F, 3.1f), "复乐园结算");
                timeline.At(4.803, () =>
                {
                    ShowTower(0);
                    Move(_initialTank, _plan.FirstPosition(0), 9, firstTowerHit);
                    foreach (var slot in _plan.TowerPair(0))
                        Move(slot, Spot(_plan.TowerCenter(0)), 8.5, firstTowerHit);
                }, "第一塔出现");
                if (_occurrence == 1)
                {
                    _fight.AddDialogue(timeline, 5.949, 39, RyneNameId, "琳的复乐园台词");
                    _fight.AddDialogue(timeline, 11.015, 40, GaiaNameId, "盖娅的复乐园台词");
                }
                timeline.At(7.014, () =>
                {
                    _firstCleaveTarget = _fight.MainTank;
                    _fight.Pandora.ActorControl(ActorControlCategory.SetModelState, 4);
                    StartTether(0);
                    MoveAll(FirstPosition, 13.75, Math.Max(firstTowerHit, firstCleave));
                }, "首翼亮起与连线");
                timeline.At(7.117, () => _fight.CastBoss(opening, 6.6f), "光与暗的孤翼读条");
                timeline.At(8.300, () => ShowTower(1), "第二塔出现");
                timeline.At(11.751, () => ShowTower(2), "第三塔出现");
                timeline.At(11.4, () => UseTankAction(TankForWing(1), provoke: true), "另一名坦克挑衅");
                timeline.At(12.5, () =>
                {
                    if (_fight.MainTank == TankForWing(1))
                        UseTankAction(_initialTank, provoke: false);
                }, "原一仇退避");
                timeline.At(14.001, () => _fight.BossEffect(opening, 0.2f), "光与暗的孤翼读条结算");
                AddTowerResolution(timeline, 0, 14.249, firstTowerHit);
                timeline.At(firstCleave, () => ResolveCleave(0), "第一翼扇形");
                timeline.At(Math.Max(firstTowerHit, firstCleave), () => MoveAll(SecondPosition, 17.62, thirdTowerHit, true),
                    "穿场到后两塔及第二翼引导位");
                timeline.At(15.140, () => ResolveTether(0), "第一翼连线死刑");
                timeline.At(15.528, () => StartTether(1), "切换第二翼连线");
                AddTowerResolution(timeline, 1, 17.777, SecondTowerHit);
                timeline.At(SecondCleave, () => ResolveCleave(1), "第二翼扇形");
                timeline.At(18.797, () => _fight.Pandora.ActorControl(ActorControlCategory.SetModelState, 0), "孤翼模型恢复");
                timeline.At(18.860, () => ResolveTether(1), "第二翼连线死刑");
                timeline.At(19.486, () => _fight.Pandora.CancelTether(), "孤翼连线结束");
                AddTowerResolution(timeline, 2, 21.268, thirdTowerHit);
                timeline.At(thirdTowerHit + 0.05, () =>
                {
                    Move(TankForWing(1), Spot(Center + new Vector2(0, -8), 0.4f), 23.40, 23.40, true);
                    Move(_initialTank, Spot(Center + new Vector2(0, 8), 0.4f), 23.40, 23.40, true);
                    _fight.Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly;
                }, "三塔结束后由当前一仇拉正");
                if (_occurrence == 1)
                {
                    _fight.AddDoubleAuto(timeline, 23.628, 24.383);
                    _fight.AddDoubleAuto(timeline, 26.794, 27.530);
                }
                return timeline;
            }

            private void ShowTower(int wave)
            {
                var slot = _plan.TowerMapSlot(wave);
                _fight.Map(slot, 2, 1);
                _fight.Map(slot - 3, 1, 4);
                _towerCounts[wave] = 0;
                RefreshTower(wave);
            }

            private void RefreshTower(int wave)
            {
                var previous = _towerCounts[wave];
                if (previous < 0)
                    return;
                var count = Math.Min(2, PartySnapshot.Capture(_fight.Party).InArea(_towerArea, _plan.TowerCenter(wave)).Count);
                if (count != previous)
                {
                    var slot = _plan.TowerMapSlot(wave);
                    if (count == 0)
                        _fight.Map(slot, 0x100, 1);
                    else if (count == 1)
                        _fight.Map(slot, 0x20, 0x10);
                    else
                        _fight.Map(slot, 0x80, 0x40);
                    _towerCounts[wave] = count;
                }
                _fight.Scheduler.ScheduleAfter(0.12, () => RefreshTower(wave), "刷新双人塔人数演出");
            }

            private void AddTowerResolution(SimTimeline timeline, int wave, double hide, double hit)
            {
                var tower = _plan.TowerCenter(wave);
                timeline.At(hide, () =>
                {
                    _towerCounts[wave] = -1;
                    _fight.Helper(1, tower, Heading(Center - tower));
                    _fight.Map(_plan.TowerMapSlot(wave), 8, 4);
                }, $"第 {wave + 1} 塔结束预兆");
                timeline.At(hit, () => ResolveTower(wave, tower), $"第 {wave + 1} 塔结算");
            }

            private void ResolveTower(int wave, Vector2 point)
            {
                var name = $"第 {wave + 1} 座双人塔";
                var snapshot = PartySnapshot.Capture(_fight.Party);
                var vulnerable = snapshot.Members.Where(member => _fight.Now < _magicVulnerableUntil[member.Slot]).Select(member => member.Slot).ToArray();
                var expected = _plan.TowerPair(wave);
                var mechanic = new AreaMechanic(name, _towerArea, point)
                {
                    LockedSnapshot = snapshot,
                    MinPlayers = 2,
                    Check = members => members.Any(member => vulnerable.Contains(member.Slot)) ? "有队员在魔法受伤加重期间踩塔。" : null,
                    Visual = result =>
                    {
                        var helper = _fight.Helpers[1];
                        var filled = result.Participants.Count >= 2;
                        helper.ExecuteActionEffect(filled ? 0x9D80u : 0x9D81u, helper, Heading(Center - point),
                            filled ? result.HitActors : snapshot.Members.Select(member => member.Actor), 8, 2.1f);
                        if (filled)
                            ApplyMagicVulnerability(result, 7, helper, TowerStatusDelay);
                    },
                    Failed = result =>
                    {
                        var inside = result.Participants.Any(member => member.Slot == _fight.Self);
                        var selfError = inside ? vulnerable.Contains(_fight.Self) : expected.Contains(_fight.Self);
                        _fight.Report(result.DescribeFailure(name, expected).WithAttribution(selfError ? new[] { _fight.Self } : Array.Empty<int>()), result);
                    }
                };
                _fight.Run(mechanic);
            }

            private void StartTether(int wave)
            {
                if (_shownTether >= 0)
                    _fight.Pandora.CancelTether();
                _shownTether = -1;
                _trackingWing = wave;
                RefreshTether(wave);
            }

            private void RefreshTether(int wave)
            {
                if (_trackingWing != wave)
                    return;
                var snapshot = PartySnapshot.Capture(_fight.Party);
                ShowTether(wave, FindTetherTarget(snapshot, wave, wave == 0 ? _firstCleaveTarget : _fight.MainTank));
                _fight.Scheduler.ScheduleAfter(0.12, () => RefreshTether(wave), "按距离更新孤翼连线");
            }

            private int FindTetherTarget(PartySnapshot snapshot, int wave, int cleaveTarget)
            {
                var select = snapshot.Members.Where(member => member.Slot != cleaveTarget);
                var order = _plan.Dark(wave) ? 1 : -1;
                return select.OrderBy(member => order * Vector2.DistanceSquared(Center, member.Position)).First().Slot;
            }

            private void ShowTether(int wave, int target)
            {
                if (_shownTether == target)
                    return;
                _fight.Pandora.TetherTo(_fight.Party[target], _plan.Dark(wave) ? 1u : 2u);
                _shownTether = target;
            }

            private void ResolveCleave(int wave)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                _busterSnapshots[wave] = snapshot;
                var target = wave == 0 ? _firstCleaveTarget : _fight.MainTank;
                _tetherTargets[wave] = FindTetherTarget(snapshot, wave, target);
                ShowTether(wave, _tetherTargets[wave]);
                _trackingWing = -1;
                var expected = TankForWing(wave);
                var dark = _plan.Dark(wave);
                var action = dark ? 0x9D7Bu : 0x9D7Au;
                var name = $"第 {wave + 1} 翼{(dark ? "暗" : "光")}扇形死刑";
                _fight.FaceBoss(Heading(snapshot.Get(target).Position - Center));
                var facing = _fight.BossHeading;
                var area = ActionArea.FromAction(action, coneAngleDegrees: 225);
                var mechanic = new AreaMechanic(name, area, Center)
                {
                    LockedSnapshot = snapshot,
                    Heading = P52复乐园Plan.CleaveHeading(facing, dark),
                    FailureSeverity = MechanicFailureSeverity.Warning,
                    MaxPlayers = 1,
                    Check = _ => target == expected ? null : "本次扇形没有由应有的坦克承伤，请检查换仇时机。",
                    Visual = result =>
                    {
                        _fight.Pandora.ExecuteActionEffect(action, _fight.Pandora, facing, result.HitActors,
                            result.Participants.Count <= 1 ? 1 : 8, 3.6f);
                        _fight.ApplyStatus(result, PhysicalVulnerability, 4, _fight.Pandora, 0.8);
                    },
                    Failed = result =>
                    {
                        var wrongSwap = target != expected && _fight.Self == TankForWing(1);
                        var ownSplash = _fight.Self == target && HitsPlannedPosition(result, area, wave, target);
                        var stoodInCleave = target == expected && _fight.Self != target && result.Participants.Any(member => member.Slot == _fight.Self);
                        _fight.Report(result.DescribeBaitFailure(name, new[] { expected }, expected, target)
                            .WithAttribution(wrongSwap || ownSplash || stoodInCleave ? new[] { _fight.Self } : Array.Empty<int>()), result);
                    }
                };
                _fight.Run(mechanic);
            }

            private void ResolveTether(int wave)
            {
                var snapshot = _busterSnapshots[wave];
                var actual = _tetherTargets[wave];
                var expected = TankForWing(1 - wave);
                var dark = _plan.Dark(wave);
                var action = dark ? 0x9BC8u : 0x9BC7u;
                var name = $"第 {wave + 1} 翼{(dark ? "最近" : "最远")}连线死刑";
                var helper = _fight.Helper(0, Center, _openingHeading);
                var area = ActionArea.FromAction(action);
                var vulnerable = snapshot.Members.Where(member => _fight.Now < _magicVulnerableUntil[member.Slot]).Select(member => member.Slot).ToArray();
                var mechanic = new AreaMechanic(name, area, snapshot.Get(actual).Position)
                {
                    LockedSnapshot = snapshot,
                    FailureSeverity = MechanicFailureSeverity.Warning,
                    MaxPlayers = 1,
                    Check = members => actual != expected ? "坦克需要按亮翼引导最近或最远连线。" :
                        members.Any(member => vulnerable.Contains(member.Slot)) ? "有队员带着魔法受伤加重吃到连线死刑。" : null,
                    Visual = result =>
                    {
                        helper.ExecuteActionEffect(action, _fight.Party[actual], _openingHeading, result.HitActors,
                            result.Participants.Count <= 1 ? 1 : 8, 1.1f);
                        ApplyMagicVulnerability(result, 4, helper, 0.48);
                    },
                    Failed = result =>
                    {
                        var wrongBait = actual != expected && (_fight.Self == actual || _fight.Self == expected);
                        var ownSplash = _fight.Self == actual && HitsPlannedPosition(result, area, wave, actual);
                        _fight.Report(result.DescribeBaitFailure(name, new[] { expected }, expected, actual)
                            .WithAttribution(wrongBait || ownSplash ? new[] { _fight.Self } : Array.Empty<int>()), result);
                    }
                };
                _fight.Run(mechanic);
            }

            private bool HitsPlannedPosition(MechanicResult result, ActionArea area, int wave, int target)
                => result.Participants.Any(member => member.Slot != target && area.Contains(
                    (wave == 0 ? FirstPosition(member.Slot) : SecondPosition(member.Slot)).Pos, result.Center, result.Heading));

            private void ApplyMagicVulnerability(MechanicResult result, float duration, SimActor source, double delay)
            {
                var until = _fight.Now + delay + duration;
                foreach (var member in result.Participants)
                    _magicVulnerableUntil[member.Slot] = Math.Max(until, _magicVulnerableUntil[member.Slot]);
                _fight.ApplyStatus(result, MagicVulnerability, duration, source, delay);
            }

            private int TankForWing(int wave) => wave == 0 ? _initialTank : 1 - _initialTank;
            private int PlanSlot(int slot) => slot < 2 && _initialTank == 1 ? 1 - slot : slot;
            private SimPosition FirstPosition(int slot) => _plan.FirstPosition(PlanSlot(slot));
            private SimPosition SecondPosition(int slot) => _plan.SecondPosition(PlanSlot(slot));

            private void Move(int slot, SimPosition position, double arrive, double hold, bool urgent = false)
                => _fight.Move(slot, position, _offset + arrive, _offset + hold, urgent);

            private void MoveAll(Func<int, SimPosition> positions, double arrive, double hold, bool urgent = false)
                => _fight.MoveAll(positions, _offset + arrive, _offset + hold, urgent);

            private void UseTankAction(int slot, bool provoke)
            {
                if (slot != _fight.Self)
                    _fight.EntityManager.UsePartyEnmityAction(_fight.Party[slot], provoke, provoke ? _fight.Pandora : _fight.Party[1 - slot]);
            }
        }
    }
}

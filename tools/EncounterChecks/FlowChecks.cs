extern alias SimulantPlugin;
using Simulant.Content.Local;
using Simulant.Game.Data;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ActionRow = SimulantPlugin::Lumina.Excel.Sheets.Action;

namespace Simulant.EncounterChecks
{
    internal static class FlowChecks
    {
        internal static void Run(string tower, GameDataService data)
        {
            Tower(tower, "双治疗", 2, true);
            Tower(tower, "双近战", 1, true);
            Tower(tower, "双治疗", 0, true);
            Tower(tower, "双近战", 2, false);
            Signals(data);
            Console.WriteLine("复乐园双人、单人、空塔及开场未结算四条路线通过。事件过滤、一次性触发、条件组合和轮次收尾通过。");
        }

        private static void Tower(string json, string pair, int membersEntering, bool castResolves)
        {
            var encounter = LocalEncounter.Parse(json);
            var choice = encounter.Choices.Single();
            var randomValue = choice.Resolve("*", new Random(29));
            Program.Check(choice.Values.Contains(randomValue) && choice.Resolve("*", new Random(29)) == randomValue, "随机选项种子");
            foreach (var step in encounter.AllEvents)
            {
                step.At *= 0.05;
                step.Timeout *= 0.05;
            }
            var positions = encounter.PartyPositions.ToArray();
            var actual = new List<LocalEvent>();
            bool? grade = null;
            using (var scheduler = new SimScheduler())
            using (var finished = new ManualResetEventSlim())
            {
                LocalFlow flow = null;
                var log = new SimCombatLog(() => false, null, null);
                log.AbilityResolved += (_, action) => flow.Emit(new LocalSignal { Type = "castResolved", Actor = "pandora", ActionId = action });
                bool InTower(LocalCondition c)
                    => c.MatchesArea(c.Slots.Select(slot => positions[slot]), c.Position, c.Heading ?? 0);
                void Run(LocalEvent step)
                {
                    actual.Add(step);
                    if (step.Op == "cast" && step.Actor == "pandora")
                    {
                        // A different action and a second identical resolution must never open extra towers.
                        flow.Emit(new LocalSignal { Type = "castResolved", Actor = "pandora", ActionId = 40320 });
                        flow.Emit(new LocalSignal { Type = "castResolved", Actor = "tower", ActionId = step.ActionId });
                        if (castResolves)
                            scheduler.ScheduleAfter(0.01, () =>
                            {
                                log.Ability(null, step.ActionId, null, null, null, 0, null, null);
                                log.Ability(null, step.ActionId, null, null, null, 0, null, null);
                            });
                    }
                    if (step.Op == "move")
                        scheduler.ScheduleAfter(0.02, () =>
                        {
                            foreach (var slot in step.Slots.Take(membersEntering)) positions[slot] = step.Position;
                        });
                    if (step.Op == "judge")
                    {
                        grade = flow.Test(step.Condition);
                        flow.Record(step.CheckId, grade.Value);
                    }
                    if (step.Op == "finish") finished.Set();
                }
                flow = new LocalFlow(scheduler, Run, InTower, new Dictionary<string, string> { [choice.Id] = pair });
                flow.Play(encounter.Timeline);
                scheduler.Start();
                Program.Check(finished.Wait(TimeSpan.FromSeconds(4)), "南塔未结束");
            }
            var moves = actual.Where(e => e.Op == "move").ToArray();
            var effects = actual.Where(e => e.Op == "cast" && e.Actor == "tower").Select(e => e.ActionId).ToArray();
            Program.Check(actual.Count(e => e.Op == "finish") == 1, "南塔收尾次数");
            if (!castResolves)
            {
                Program.Check(moves.Length == 0 && effects.Length == 0 && grade == null &&
                    actual.Any(e => e.Message?.Contains("未观察到") == true), "开场未结算路线");
                return;
            }
            Program.Check(moves.Single().Slots.SequenceEqual(pair == "双治疗" ? new[] { 2, 3 } : new[] { 4, 5 }), "选项决定进塔职能");
            Program.Check(grade == (membersEntering == 0 ? (bool?)null : membersEntering == 2), "实际踩塔判定");
            Program.Check(effects.Single() == (membersEntering == 2 ? 40320u : 40321u), "成功与失败结算技能");
            Program.Check(actual.Any(e => e.Message?.Contains("等待超时") == true) == (membersEntering == 0), "空塔超时提示");
            Program.Check(actual.Count(e => e.Op == "mapEffect" && e.MapSlot == 53) == 2, "塔只打开和关闭一次");
        }

        private static void Signals(GameDataService data)
        {
            const string json = @"{
              ""schemaVersion"":3,""id"":""test.signals"",""name"":""信号"",""territoryId"":755,
              ""actors"":[{""id"":""boss"",""baseId"":8528,""nameId"":7131,""level"":80,""position"":[0,0,0],""heading"":0}],
              ""timeline"":[
                {""at"":0,""op"":""trigger"",""event"":{""type"":""castStarted"",""actor"":""boss"",""actionId"":10509},""timeout"":1,
                 ""then"":[{""at"":0,""op"":""hint"",""text"":""cast""}]},
                {""at"":0,""op"":""trigger"",""event"":{""type"":""checkResolved"",""check"":""low"",""passed"":true},""timeout"":1,
                 ""then"":[{""at"":0,""op"":""hint"",""text"":""grade""},{""at"":0,""op"":""finish""}]},
                {""at"":0,""op"":""trigger"",""if"":{""type"":""hpAtMost"",""actor"":""boss"",""percent"":30},""timeout"":1,
                 ""then"":[{""at"":0,""op"":""judge"",""id"":""low"",""text"":""低血量"",""if"":{""type"":""all"",""conditions"":[
                    {""type"":""not"",""condition"":{""type"":""checkResult"",""check"":""low"",""passed"":false}},
                    {""type"":""any"",""conditions"":[{""type"":""hasStatus"",""actor"":""boss"",""statusId"":1529},{""type"":""hpAtMost"",""actor"":""boss"",""percent"":30}]}]}}]},
                {""at"":0.01,""op"":""cast"",""actor"":""boss"",""actionId"":10509},
                {""at"":0.02,""op"":""health"",""actor"":""boss"",""percent"":30},
                {""at"":0.25,""op"":""hint"",""text"":""late""}
              ]}";
            var encounter = LocalEncounter.Parse(json);
            var output = new List<string>();
            double health = 100;
            using (var scheduler = new SimScheduler())
            using (var done = new ManualResetEventSlim())
            {
                LocalFlow flow = null;
                var log = new SimCombatLog(() => false, null, null);
                void StartCast(uint id) => flow.Emit(new LocalSignal { Type = "castStarted", Actor = "boss", ActionId = id });
                log.CastStarted += (_, id) => StartCast(id);
                void Run(LocalEvent e)
                {
                    if (e.Op == "cast")
                    {
                        if (data == null) StartCast(e.ActionId);
                        else log.Cast(null, data.GetSheet<ActionRow>().GetRow(e.ActionId), null, 3, null, null);
                    }
                    if (e.Op == "health") health = e.Percent;
                    if (e.Op == "judge") flow.Record(e.CheckId, flow.Test(e.Condition));
                    if (e.Op == "hint") output.Add(e.Message);
                    if (e.Op == "finish") output.Add("finish");
                }
                flow = new LocalFlow(scheduler, Run, c => c.Type == "hpAtMost" && health <= c.Percent, null);
                flow.Record("other", true);
                flow.Play(encounter.Timeline);
                scheduler.Schedule(0.005, () => flow.Emit(new LocalSignal { Type = "checkResolved", Check = "low", Passed = false }));
                scheduler.Schedule(0.30, done.Set);
                scheduler.Start();
                Program.Check(done.Wait(TimeSpan.FromSeconds(3)), "触发调度器未完成");
            }
            Program.Check(output.SequenceEqual(new[] { "cast", "grade", "finish" }), "事件过滤、条件轮询、命名判定和终止后跳过节点");
        }
    }
}

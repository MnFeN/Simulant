using Simulant.Content.Local;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.EncounterChecks
{
    // Drive the imported script and its real control flow with observable party positions, HP and statuses.
    internal static class GarudaChecks
    {
        internal static void Verify(string json)
        {
            Selectors();
            Parallel.ForEach(new[] { "示范", "抢刺羽", "提前击破", "截西线", "无人接仇恨" }, scenario => Run(json, scenario));
            Console.WriteLine("迦楼罗实际脚本通过示范、抢首仇、提前击破、无层截线和无人接怪五条路线。");
        }

        private static void Selectors()
        {
            Vector2?[] party = { new Vector2(5, 0), new Vector2(-5, 0), null, new Vector2(10, 0) };
            var order = new[] { 3, 1, 0 };
            var draws = new List<int>();
            var targets = new LocalTargets(s => s < party.Length ? party[s] : null, _ => Vector2.Zero,
                (_, first) => first ? new[] { 3 } : order, (_, slot) => draws.Add(slot));
            var e = new LocalEvent { Op = "selectTarget", Key = "t", Actor = "boss", By = "nearest", Rank = 2 };
            targets.Start(e);
            Program.Check(targets.Holder("t") == 1, "等距按职能顺序、不选缺席者");
            e.By = "farthest"; e.Rank = 1;
            targets.Start(e);
            Program.Check(targets.Holder("t") == 3, "最远距离");
            e.By = "enmity"; e.Op = "trackTether";
            targets.Start(e);
            order = new[] { 1, 0, 3 };
            targets.Refresh();
            Program.Check(targets.Holder("t") == 1, "仇恨变化时换线");
            targets.Resolve("t");
            order = new[] { 0 };
            targets.Refresh();
            Program.Check(targets.Holder("t") == 1 && draws.SequenceEqual(new[] { 3, 1, -1 }), "收线后锁定持有者");
            e.Op = "selectTarget"; e.Rank = 2;
            targets.Start(e);
            Program.Check(targets.Holder("t") == -1, "名次不足无目标");
            e.Op = "trackTether"; e.By = "nearest"; e.Rank = 1; e.Role = 3; e.InterceptRadius = 1;
            party[0] = new Vector2(2, 2); party[1] = null;
            targets.Start(e); targets.Refresh();
            Program.Check(targets.Holder("t") == 3, "靠近来源但偏离线段不截线");
            party[0] = new Vector2(4, 0);
            targets.Resolve("t");
            Program.Check(targets.Holder("t") == 0, "结算前最后一次移动纳入截线");
            targets.Clear();
            Program.Check(targets.Holder("t") == -1 && !targets.Tracking, "重开清除旧持线者");
        }

        private static void Run(string json, string scenario)
        {
            const double scale = 0.12;
            var file = LocalEncounter.Parse(json);
            foreach (var e in file.AllEvents)
            {
                e.At *= scale;
                e.Timeout *= scale;
                if (e.Op == "forParty")
                {
                    e.Interval *= (float)scale;
                    e.Duration *= (float)scale;
                }
            }
            var party = file.PartyPositions.ToArray();
            var positions = file.Actors.ToDictionary(a => a.Id, a => new Vector2(a.Spawn.Pos.X, a.Spawn.Pos.Y));
            var status = new Dictionary<(int, uint), int>();
            var grades = new Dictionary<string, bool>();
            var hits = new List<(uint Action, int Slot)>();
            var first = -1;
            var garudaOrder = new[] { 0, 1 };
            uint hp = 40000;
            bool bubble = false;
            string error = null;
            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((ex, node) => { error = node + " " + ex; done.Set(); }))
            {
                var targets = new LocalTargets(s => party[s], id => positions[id],
                    (id, fixate) => id == "spiny" ? new[] { first } : garudaOrder, (_, __) => { });
                LocalFlow flow = null;
                int Slot(string key) => key == "$member" ? flow.Member : targets.Current(key);
                int Stacks(int slot, uint id) => status.TryGetValue((slot, id), out var value) ? value : 0;
                bool World(LocalCondition c)
                {
                    switch (c.Type)
                    {
                        case "hpAtMost": return hp == 0;
                        case "objectActive": return bubble;
                        case "hasStatus":
                            int s = c.Actor != null ? 8 : c.Target == null ? c.Role : Slot(c.Target);
                            return Stacks(s, c.StatusId) >= Math.Max(1, (int)c.MinStacks);
                        case "areaCount":
                            var center = c.CenterRole >= 0 ? party[c.CenterRole] : c.Actor == null ? c.Position : positions[c.Actor];
                            var slots = c.Target == null ? c.Slots : new[] { Slot(c.Target) };
                            return c.MatchesArea(slots.Where(i => i >= 0).Select(i => party[i]), center, 0);
                        case "targetIs": return Slot(c.Target) == c.Role;
                        default: throw new Exception(c.Type);
                    }
                }
                void Hit(int slot, uint damage)
                {
                    if (hp == 0) return;
                    if (first < 0) first = slot;
                    hp -= Math.Min(hp, damage);
                }
                void RunNode(LocalEvent e)
                {
                    int slot = e.Target == null ? e.Role : Slot(e.Target);
                    if (e.Target != null && slot < 0) return;
                    switch (e.Op)
                    {
                        case "selectTarget": case "trackTether": targets.Start(e); break;
                        case "resolveTether": targets.Resolve(e.Key); break;
                        case "partyHit": if (scenario != "无人接仇恨") Hit(slot, e.Damage); break;
                        case "move": foreach (var s in e.Slots) party[s] = e.Position; break;
                        case "place": positions[e.Actor] = e.Position; break;
                        case "object":
                            bubble = true;
                            scheduler.ScheduleAfter(e.Duration * scale, () => bubble = false);
                            break;
                        case "status":
                            var recipient = e.Target == null && slot < 0 ? 8 : slot;
                            status[(recipient, e.StatusId)] = e.Duration == 0 ? 0 : e.AddStacks > 0 ?
                                Stacks(recipient, e.StatusId) + e.AddStacks : Math.Max(1, (int)e.Stacks);
                            break;
                        case "cast": hits.Add((e.ActionId, slot)); break;
                        case "judge":
                            grades[e.CheckId] = flow.Test(e.Condition);
                            flow.Record(e.CheckId, grades[e.CheckId]);
                            break;
                    }
                }
                flow = new LocalFlow(scheduler, RunNode, World, null);
                flow.Play(file.Timeline);
                if (scenario == "抢刺羽")
                {
                    scheduler.Schedule(20.5 * scale, () => Hit(0, 1000));
                    scheduler.Schedule(115 * scale, () => garudaOrder = new[] { 1, 0 });
                }
                if (scenario == "提前击破") scheduler.Schedule(29 * scale, () => Hit(0, 40000));
                if (scenario == "截西线") scheduler.Schedule(123 * scale, () => party[4] = new Vector2(86, 100));
                for (double tick = 20.1; tick < 126; tick += 0.1)
                    scheduler.Schedule(tick * scale, targets.Refresh);
                scheduler.Schedule(128 * scale, done.Set);
                scheduler.Start();
                Program.Check(done.Wait(TimeSpan.FromSeconds(25)) && error == null, scenario + " 调度失败 " + error);
            }
            var cyclones = hits.Where(h => h.Action == 11077).Select(h => h.Slot).ToArray();
            var lines = hits.Where(h => h.Action == 11081).Select(h => h.Slot).ToArray();
            void Check(bool ok, string message) => Program.Check(ok, scenario + " " + message);
            Check(lines.SequenceEqual(new[] { scenario == "截西线" ? 4 : 1, 7 }), "双线实际受击职能 " + string.Join(",", lines));
            Check(hits.Last(h => h.Action == 11088).Slot == (scenario == "抢刺羽" ? 1 : 0), "换仇后下行突风打当前一仇");
            if (scenario == "无人接仇恨")
                Check(cyclones.Length == 0 && !grades.ContainsKey("刺羽两层"), "无仇恨、未击破，无气旋和风罩");
            else
            {
                Check(cyclones.Length == (scenario == "提前击破" ? 1 : 2) &&
                    cyclones.All(s => s == (scenario == "抢刺羽" ? 0 : 1)), "气旋接收者和次数");
                Check(grades["刺羽两层"] == (scenario != "提前击破"), "按击破时实际层数判定");
                Check(grades["刺羽接怪"] == (scenario != "抢刺羽"), "实际机制和示范分工分别判定");
            }
            Check(grades["西线结算"] == (scenario != "截西线") && grades["东线结算"], "无层截线失败，其余有效持线者消层");
            Check(grades["觉醒"] == (scenario == "示范" || scenario == "抢刺羽"), "按实际四次供能决定觉醒");
        }
    }
}

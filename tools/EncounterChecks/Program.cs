using Simulant.Content.Local;
using Simulant.Game.Data;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Simulant.EncounterChecks
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var directory = Path.Combine(Path.GetTempPath(), "simulant-encounters-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (args.Length > 1)
                    throw new ArgumentException("用法：Simulant.EncounterChecks.exe [游戏安装路径]");
                using (var data = args.Length == 1 ? new GameDataService(args[0]) : null)
                {
                    Run(directory, data);
                    Console.WriteLine(data == null ? "未指定游戏目录，跳过本机资源表检查。" : "本机资源表检查通过。");
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static void Run(string directory, GameDataService data)
        {
            void Validate(LocalEncounter encounter)
            {
                if (data != null)
                    encounter.ValidateResources(data);
            }
            string Example(string name) => LocalEncounterLibrary.Read(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "examples", name + ".json"));
            bool NoReplace(string name) => throw new Exception("意外要求替换 " + name);
            var library = new LocalEncounterLibrary(directory);
            var opening = Example("o8s-trine-opening");
            var preset = library.Import(opening, Validate, NoReplace);
            Check(preset.TerritoryId == 755 && preset.Phase.Weather == 79 && preset.Phase.BGM == 533, "O8S 场地参数");
            Check(preset.Encounter.Actors.Single().Spawn.BNpcBaseId == 8528, "O8S 本体");
            library.Import(opening, Validate, NoReplace);
            var changed = opening.Replace("开场读条", "更新后的读条");
            Check(library.Import(changed, Validate, _ => false) == null, "放弃替换");
            void Reject(string before, string after, string field)
                => ExpectInvalid(() => library.Import(opening.Replace(before, after), Validate, NoReplace), field);
            Reject("\"actor\": \"boss\"", "\"actor\": \"bos\"", "timeline[0].actor");
            Reject("\"schemaVersion\": 1", "\"schemaVersion\": 4", "schemaVersion");
            Reject("\"op\": \"cast\"", "\"op\": \"other\"", "timeline[0].op");
            Reject("\"level\": 80", "\"level\": 256", "actors[0].level");
            Reject("\"level\": 80", "\"level\": 80, \"level\": 81", "actors[0].level");
            Reject("\"level\": 80", "\"level\": 80, \"typo\": 1", "actors[0].typo");
            if (data != null)
                Reject("\"actionId\": 10509", "\"actionId\": 4294967295", "timeline[0].actionId");
            ExpectInvalid(() => library.Import(changed, _ => throw new InvalidDataException("资源行缺失"), NoReplace), "资源行缺失");
            Check(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()) == opening, "无效更新保留原件");
            var updated = library.Import(changed, Validate, _ => true);
            Check(updated.ConfigurationKey == preset.ConfigurationKey && updated.Name != preset.Name, "更新保留选择键");
            var arena = library.Import(Example("uwu-garuda-arena"), Validate, NoReplace);
            var full = Example("uwu-garuda-opening");
            var oldGaruda = @"{""schemaVersion"":1,""id"":""example.uwu.garuda-opening"",""name"":""迦楼罗开场读条"",""territoryId"":777,
                ""actors"":[{""id"":""garuda"",""baseId"":8722,""nameId"":1644,""level"":70,""position"":[100,100,0],""heading"":0}],
                ""timeline"":[{""at"":6.3,""op"":""cast"",""actor"":""garuda"",""actionId"":11091}]}";
            library.Import(oldGaruda, Validate, NoReplace);
            var garuda = library.Import(full, Validate, _ => true);
            Check(!arena.IsImplemented && arena.TerritoryId == 777 && garuda.TerritoryId == 777 &&
                new[] { arena, garuda }.All(p => p.Phase.Weather == 28 && p.Phase.BGM == 544), "预览与练习使用迦楼罗场景");
            Check(garuda.Options.Select(o => o.PropertyName).SequenceEqual(new[] { "PlayerSlot", "IncludeParty", "SwapRoles", "ShowHints" }) &&
                preset.Options.Count == 0, "绝神兵与旧格式的模拟参数");
            Check(garuda.Waymarks["4"].Value.X > 100 && garuda.Waymarks["4"].Value.Y > 100 &&
                garuda.Waymarks["B"].Value.X > garuda.Waymarks["D"].Value.X, "东南集合与东西标点");
            Check(garuda.Encounter.AssignedSlot(4, true) == 5 && garuda.Encounter.AssignedSlot(5, true) == 4 &&
                garuda.Encounter.AssignedSlot(1, true) == 1 && garuda.Encounter.AssignedSlot(4, false) == 4 &&
                garuda.Encounter.RoleText("{D1} → {D2}", true) == "D2 → D1", "近战对调后按实际职能播报和判定");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"roles\": [\"D1\", \"D2\"]", "\"roles\": [\"D1\", \"D1\"]")), "roleSwap.roles");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"groundAt\": \"MT\"", "\"groundAt\": \"T3\"")), "groundAt");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"iconId\": 16", "\"iconId\": 0")), "iconId");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"op\": \"tether\"", "\"op\": \"other\"")), ".op");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"by\": \"firstEnmity\"", "\"by\": \"other\"")), ".by");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"by\": \"nearest\"", "\"by\": \"nearest\", \"rank\": 0")), ".rank");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"interceptRadius\": 1.5", "\"interceptRadius\": 0")), ".interceptRadius");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"target\": \"西线\"", "\"target\": \"未注册\"")), ".target");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"target\": \"西线\"", "\"target\": \"$member\"")), ".target");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"enmity\": {}, ", "")), ".maxHp");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"interval\": 0.15", "\"interval\": 0.01")), ".interval");
            ExpectInvalid(() => LocalEncounter.Parse(full.Replace("\"op\": \"resolveTether\", \"key\": \"西线\"", "\"op\": \"resolveTether\", \"key\": \"other\"")), ".key");
            VerifyPhase(garuda.Encounter, data);
            GarudaChecks.Verify(full);

            var tower = Example("fru-south-tower");
            var fru = library.Import(tower, Validate, NoReplace);
            Check(fru.Options.Any(o => o.PropertyName == "choice.tower-pair") &&
                fru.Options.Any(o => o.PropertyName == "ShowHints"), "v3 声明选项和子轴提示");
            FlowChecks.Run(tower, data);
            void RejectTower(string before, string after, string field)
                => ExpectInvalid(() => library.Import(tower.Replace(before, after), Validate, NoReplace), field);
            RejectTower("\"actor\": \"tower\"", "\"actor\": \"missing\"", ".then[1].actor");
            RejectTower("\"check\": \"south-tower\"", "\"check\": \"missing\"", ".if.check");
            RejectTower("\"default\": \"双治疗\"", "\"default\": \"missing\"", "options[0].default");
            RejectTower("\"min\": 2", "\"min\": 3", ".if.max");
            RejectTower("\"radius\": 3", "\"radius\": 0", ".if.shape");
            RejectTower("\"slot\": 53", "\"slot\": 54", ".slot");
            RejectTower("\"timeout\": 8", "\"timeout\": 0", ".timeout");
            RejectTower("\"roles\": [\"D1\", \"D2\"]", "\"roles\": [\"D1\", \"T5\"]", ".roles");
            RejectTower("\"then\": [", "\"then\": [], \"then\": [", ".then");
            if (data != null)
                RejectTower("\"actionId\": 40320", "\"actionId\": 4294967295", ".then[1].actionId");
            var finish = "{\"at\": 2, \"op\": \"finish\"}";
            string Operation(string fields) => "{\"at\": 2, " + fields + "}";
            var extra = new[] {
                Operation("\"op\":\"knockback\",\"roles\":[\"MT\"],\"knockbackId\":156,\"position\":[100,100]"),
                Operation("\"op\":\"knockback\",\"roles\":[\"H1\"],\"knockbackId\":156,\"heading\":0"),
                Operation("\"op\":\"judge\",\"id\":\"test-status\",\"if\":{\"type\":\"hasStatus\",\"role\":\"MT\",\"statusId\":1529},\"text\":\"状态\"")
            };
            // These nodes are deliberately inserted into just one child timeline.
            var offset = tower.IndexOf(finish, StringComparison.Ordinal);
            var extended = tower.Insert(offset, string.Join(",", extra) + ",");
            Validate(LocalEncounter.Parse(extended));
            ExpectInvalid(() => LocalEncounter.Parse(extended.Replace("test-status", "south-tower")), ".id");
            if (data != null)
            {
                ExpectInvalid(() => Validate(LocalEncounter.Parse(extended.Replace("\"knockbackId\":156", "\"knockbackId\":65535"))), ".knockbackId");
                ExpectInvalid(() => Validate(LocalEncounter.Parse(extended.Replace("\"statusId\":1529", "\"statusId\":65535"))), ".statusId");
            }
            ExpectInvalid(() => LocalEncounter.Parse(oldGaruda.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")
                .Replace("\"op\":\"cast\"", "\"op\":\"trackTether\"")), ".op");

            File.WriteAllText(Path.Combine(directory, "enc-broken.json"), "{");
            var fresh = new LocalEncounterLibrary(directory);
            var warnings = new List<string>();
            fresh.Load(Validate, warnings.Add);
            Check(fresh.Presets.Count() == 4 && warnings.Single().Contains("enc-broken.json"), "重启恢复及损坏文件隔离");
            Check(fresh.Presets.Single(p => p.ConfigurationKey == fru.ConfigurationKey).Encounter.AllEvents.Count ==
                fru.Encounter.AllEvents.Count, "无效更新后恢复原版 v3 子轴");
            Check(fresh.Presets.Single(p => p.ConfigurationKey == updated.ConfigurationKey).Name == updated.Name, "恢复更新后的文件");

            // 与重试轮同时观测。若被停止的调度器继续播放，后面的 6.3 秒内就能观察到旧回调。
            int stale = 0;
            using (var armed = new ManualResetEventSlim())
            using (var stopped = new SimScheduler())
            {
                var flow = new LocalFlow(stopped, _ => Interlocked.Increment(ref stale), _ => false, null);
                var pending = LocalEncounter.Parse(tower).Timeline[0];
                pending.Timeout = 0.2;
                flow.Play(new[] { pending });
                stopped.Schedule(0, armed.Set);
                stopped.Play(preset.Encounter.BuildTimeline(_ => Interlocked.Increment(ref stale)));
                stopped.Start();
                Check(armed.Wait(TimeSpan.FromSeconds(2)), "停止前已注册事件监听");
            }
            VerifyCast(preset.Encounter, 3, "boss", 10509);
            VerifyCast(garuda.Encounter, 6.3, "garuda", 11091);
            Check(stale == 0, "停止后取消旧节点和触发器超时分支");
            Console.WriteLine("文件导入、替换与恢复通过。O8S 3 秒、绝神兵 6.3 秒读条调度和取消通过。");
        }

        private static void VerifyCast(LocalEncounter encounter, double at, string actor, uint action)
        {
            using (var signal = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                var actual = (Actor: "", Action: 0u, Time: 0d);
                var timeline = encounter.BuildTimeline(e =>
                {
                    if (e.Op != "cast")
                        return;
                    actual = (e.Actor, e.ActionId, scheduler.ElapsedSeconds);
                    signal.Set();
                });
                Check(encounter.Timeline.First(e => e.Op == "cast").At == at, "读条节点计划时间");
                scheduler.Play(timeline);
                scheduler.Start();
                Check(signal.Wait(TimeSpan.FromSeconds(at + 3)) && actual.Actor == actor &&
                    actual.Action == action && actual.Time >= at, encounter.Name + " 读条回调");
            }
        }

        private static void VerifyPhase(LocalEncounter encounter, GameDataService data)
        {
            var timeline = new List<LocalEvent>();
            var schedule = encounter.BuildTimeline(timeline.Add);
            foreach (var node in schedule.Snapshot().OrderBy(e => e.Seconds))
                node.Action();
            Check(timeline.Count == encounter.Timeline.Count && timeline.Last().Op == "finish" && timeline.Last().At == 182,
                "播放完整 P1 至狂暴收尾");
            var casts = timeline.Where(e => e.Op == "cast").ToArray();
            Check(casts.Count(e => e.ActionId == 11080) == 2 && casts.Count(e => e.ActionId == 11081) == 2 &&
                casts.Count(e => e.ActionId == 11091) == 5 && casts.Last().ActionId == 11093, "烈风刃、双线、螺旋气流与狂暴");
            Check(timeline.Count(e => e.Op == "trackTether" && e.TetherId == 4) == 2 &&
                encounter.AllEvents.Any(e => e.ObjectId == 2002792) && encounter.AllEvents.Any(e => e.StatusId == 1529), "双线、风罩与觉醒");
            var waves = casts.Where(e => e.ActionId == 11085).GroupBy(e => e.At).ToArray();
            Check(waves.Length == 6, "六轮飞翎雨");
            foreach (var wave in waves)
                Check(wave.Select(e => e.Role).Distinct().Count() == 8, "飞翎雨锁定八个职能");
            var positions = encounter.PartyPositions.ToArray();
            foreach (var e in timeline)
            {
                if (e.Op == "move")
                    foreach (var slot in e.Slots)
                        positions[slot] = e.Position;
                if (e.Op == "check")
                    foreach (var slot in e.Slots)
                        Check(System.Numerics.Vector2.Distance(positions[slot], e.Position) <= e.Radius, e.Message);
            }
            if (data != null)
            {
                var busyUntil = new Dictionary<string, double>();
                foreach (var e in casts)
                {
                    Check(!busyUntil.TryGetValue(e.Actor, out var end) || end <= e.At + 0.0001, e.Actor + " 读条冲突");
                    busyUntil[e.Actor] = e.At + e.CastSeconds(data);
                }
            }
            Console.WriteLine($"迦楼罗 P1 的 {timeline.Count} 个主轴节点、八职能示范点和关键机制检查通过。");
        }

        private static void ExpectInvalid(Action action, string field)
        {
            try { action(); }
            catch (InvalidDataException e) when (e.Message.Contains(field)) { return; }
            throw new Exception("应拒绝无效文件并报告 " + field);
        }

        internal static void Check(bool ok, string message)
        {
            if (!ok)
                throw new Exception(message);
        }
    }
}

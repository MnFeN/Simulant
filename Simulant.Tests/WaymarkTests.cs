using Simulant.Content.U5a;
using Simulant.Content.U6b;
using Simulant.Content.U7a;
using Simulant.Content.U7b;
using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Simulation.Runtime;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Tests
{
    internal static class WaymarkTests
    {
        public static void InitialLayoutsKeepCoordinatesAndRotateTogether()
        {
            Near(U7a.Waymarks["A"], 100, 90);
            Near(U7a.Waymarks["1"], 92.928932f, 92.928932f);
            Near(U7a.Waymarks["2"], 107.071068f, 92.928932f);
            Near(U6b.Waymarks["A"], 100, 87);
            Near(U6b.Waymarks["1"], 109.19239f, 90.80761f);
            Near(U6b.Waymarks["4"], 90.80761f, 90.80761f);

            var expected = new[]
            {
                new Vector2(100, 88),
                new Vector2(112, 100),
                new Vector2(100, 112),
                new Vector2(88, 100),
                new Vector2(88, 88),
                new Vector2(112, 88),
                new Vector2(112, 112),
                new Vector2(88, 112)
            };

            for (var i = 0; i < expected.Length; i++)
                Near(U7b.Waymarks["ABCD1234"[i].ToString()], expected[i].X, expected[i].Y);

            Near(U5a.Waymarks["A"], 100, 86.25f);
            Near(U5a.Waymarks["B"], 118, 100);
            Near(U5a.Waymarks["3"], 115.5f, 98);
            Near(U5a.Waymarks["4"], 115.5f, 102);

            var rotated = WaymarkLayout.Center(10, 20, 3).Rotate(Deg(90)).Circle("ABCD", true, 2);
            Near(rotated["A"], 12, 20, 3);
            Near(rotated["B"], 10, 18, 3);
            var points = WaymarkLayout.Center(10, 20, 3).At("A", 0, -2).Polar("B", 2, Deg(-90));
            points.Rotate(Deg(90));
            Near(points["A"], 12, 20, 3);
            Near(points["B"], 10, 18, 3);
            points.Rotate(Math.PI);
            Near(points["A"], 10, 18, 3);
            Near(points["B"], 8, 20, 3);

            var between = WaymarkLayout.Center(10, 20, 3).Circle("ABCD", false, Math.Sqrt(2));
            Near(between["A"], 9, 19, 3);
            Near(between["D"], 11, 19, 3);
            between.Rotate(Deg(90));
            Near(between["A"], 11, 19, 3);
            Near(between["D"], 11, 21, 3);

            var cycling = WaymarkLayout.Center(0, 0).Circle("ABCD1234", true, 2, 3, 4);
            var radii = new[]
            {
                2f,
                3f,
                4f,
                2f,
                3f,
                4f,
                2f,
                3f
            };

            for (var i = 0; i < radii.Length; i++)
                Assert(Math.Abs(cycling["ABCD1234"[i].ToString()].Value.Length() - radii[i]) < 0.0001f,
                    "半径应按给定顺序循环，末尾不足一轮时也必须正确分配。");

            Throws<ArgumentException>(() => WaymarkLayout.Center(0, 0).Circle("ABCD", true));
            Throws<ArgumentOutOfRangeException>(() => WaymarkLayout.Center(0, 0).Circle("ABCD", true, 2, -1));
        }

        public static void SessionOwnsPlacementAndIdempotentCleanup()
        {
            GameMarkers.Reset();

            try
            {
                using (var probe = new SessionProbe
                {
                    Waymarks = U7a.Waymarks
                }

                )
                using (var session = new SimSession(new PluginHost(), new SessionTestPreset(probe)))
                {
                    probe.StartAction = () => Assert(GameMarkers.WaymarkCommands.Count == 1,
                        "首个机制节点前应已提交初始标点。");
                    session.Start();
                    SessionTests.Wait(probe.Started, "标点后的模拟初始化");
                    session.Stop();
                    session.Stop();
                    Assert(GameMarkers.WaymarkCommands.Count == 2 &&
                        GameMarkers.WaymarkCommands[0].SequenceEqual(Enumerable.Range(0, 8).Select(U7a.Waymarks.Position)) &&
                        GameMarkers.WaymarkCommands[1] == null && !session.HasPendingCleanup,
                        "停止应清除标点，重复停止不能再次提交清理。");
                    probe.StartAction = null;
                    session.Start();
                    session.Dispose();
                    Assert(GameMarkers.WaymarkCommands.Count == 4 && !session.HasPendingCleanup,
                        "再次开始应重新放置，立即取消也必须清除标点。");
                }

                using (var probe = new SessionProbe())
                using (var session = new SimSession(new PluginHost(), new SessionTestPreset(probe)))
                {
                    session.Start();
                    session.Stop();
                    Assert(GameMarkers.WaymarkCommands.Count == 4, "没有布局的预设不能修改或删除场地标点。");
                }
            }
            finally
            {
                GameMarkers.Reset();
            }
        }

        public static void FailedPlacementAndCleanupRetainRetryResponsibility()
        {
            GameMarkers.Reset();

            try
            {
                using (var probe = new SessionProbe
                {
                    Waymarks = U7b.Waymarks
                }

                )
                using (var session = new SimSession(new PluginHost(), new SessionTestPreset(probe)))
                {
                    GameMarkers.OnWaymarkCommand = _ =>
                    {
                        throw new InvalidOperationException("模拟标点回调失败。");
                    };

                    Throws<InvalidOperationException>(session.Start);
                    Assert(probe.StartCount == 0 && probe.StopCount == 1 && session.HasPendingCleanup &&
                        GameMarkers.WaymarkCommands.Count == 2,
                        "部分放置失败要立即回滚；清理仍失败时保留责任，不能启动机制。");
                    Throws<InvalidOperationException>(session.Start);
                    Assert(probe.StartCount == 0 && probe.StopCount == 1, "标点未清理完不能重启或重复预设停止逻辑。");
                    GameMarkers.OnWaymarkCommand = null;
                    session.Stop();
                    Assert(!session.HasPendingCleanup && probe.StopCount == 1, "再次停止应只重试剩余标点清理。");
                }
            }
            finally
            {
                GameMarkers.Reset();
            }
        }

        public static void InterruptedSceneKeepsWaymarksUntilExplicitStop()
        {
            GameMarkers.Reset();

            try
            {
                using (var reported = new ManualResetEventSlim())
                using (var probe = new SessionProbe
                {
                    Waymarks = U6b.Waymarks
                }

                )
                using (var session = new SimSession(new PluginHost(), new SessionTestPreset(probe)))
                {
                    probe.StartAction = () =>
                    {
                        throw new InvalidOperationException("模拟机制异常。");
                    };

                    GameChat.OnChatCommand = _ => reported.Set();
                    session.Start();
                    SessionTests.Wait(reported, "机制异常后保留现场");
                    Assert(GameMarkers.WaymarkCommands.Count == 1 && session.HasPendingCleanup,
                        "机制中断只能停止流程，不能提前删除用于复盘的场地标点。");
                    session.Stop();
                    Assert(GameMarkers.WaymarkCommands.Count == 2 && !session.HasPendingCleanup,
                        "显式停止时必须清理保留的场地标点。");
                }
            }
            finally
            {
                GameMarkers.Reset();
            }
        }

        public static void SessionDoesNotClearWaymarksAfterWorldChange()
        {
            GameMarkers.Reset();
            try
            {
                using (var probe = new SessionProbe { Waymarks = U7a.Waymarks })
                {
                    var host = new PluginHost();
                    using (var session = new SimSession(host, new SessionTestPreset(probe)))
                    {
                        session.Start();
                        host.WorldGeneration++;
                        session.Stop();
                        Assert(GameMarkers.WaymarkCommands.Count == 1 && !session.HasPendingCleanup,
                            "外部切图后必须放弃旧场地标点的清理责任，避免清除新地图的标点。");
                    }
                }
            }
            finally
            {
                GameMarkers.Reset();
            }
        }

        private static void Near(Vector3? actual, float x, float y, float z = 0)
            => Assert(actual.HasValue && Vector3.Distance(actual.Value, new Vector3(x, y, z)) < 0.0001f,
                $"标点坐标不符：预期 ({x}, {y}, {z})，实际 {actual}。");

        private static void Throws<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new Exception("预期抛出异常：" + typeof(T).Name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}

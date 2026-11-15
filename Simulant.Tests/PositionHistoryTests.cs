using Simulant.Simulation.Runtime;
using System;
using System.Numerics;

namespace Simulant.Tests
{
    internal static class PositionHistoryTests
    {
        public static void OnlySelfUsesHistoryAndPresentationKeepsScheduledTime()
        {
            var spawner = new Simulant.Core.Entity.EntitySpawner();
            using (var done = new System.Threading.ManualResetEventSlim())
            using (var scheduler = new Simulant.Simulation.Timeline.SimScheduler())
            {
                var manager = new SimEntityManager(spawner)
                {
                    Scheduler = scheduler,
                    SelfJudgementAdvance = 0.35
                };
                var self = manager.Attach(spawner.World.CreateCharacter(0));
                var other = manager.Attach(spawner.World.CreateCharacter(2));
                manager.Party.Add(self);
                manager.Party.Add(other);
                self.Pos = Vector2.Zero;
                other.Pos = Vector2.Zero;
                Simulant.Simulation.Mechanics.MechanicResult result = null;
                double presented = 0;
                scheduler.Schedule(0.05, () =>
                {
                    self.Pos = new Vector2(10, 0);
                    other.Pos = new Vector2(10, 0);
                });
                scheduler.Schedule(0.1, () => manager.RunMechanic(new Simulant.Simulation.Mechanics.AreaMechanic
                {
                    Area = Simulant.Simulation.Geometry.ActionArea.Circle(2),
                    Center = Vector2.Zero,
                    Visual = value =>
                    {
                        result = value;
                        presented = scheduler.ElapsedSeconds;
                        done.Set();
                    }
                }));
                scheduler.Start();
                try
                {
                    if (!done.Wait(3000) || result == null || presented < 0.1 || result.Participants.Count != 1 ||
                        result.Participants[0].Actor != self || self.Pos.X != 10 || other.JudgementPosition.X != 10)
                        throw new Exception("只有自身判定应取历史位置，假队员和实时坐标不变，演出不能提前。");
                }
                finally
                {
                    scheduler.Dispose();
                    manager.Clear();
                }
            }
        }

        public static void SamplesEarlierPositionWithoutInterpolatingTeleports()
        {
            var history = new PositionHistory();
            history.Add(0, Vector2.Zero);
            history.Add(0.2, new Vector2(1, 0));
            history.Add(0.4, new Vector2(100, 0));
            if (history.At(0.6 - 0.35).X != 1 || history.At(-1).X != 0 || history.At(0.4).X != 100)
                throw new Exception("应取提前量对应的历史位置，传送不能插值或使用未来样本。");
        }

        public static void BoundedHistoryRetainsRecentSamplesAndConfigurationCopiesAdvance()
        {
            var history = new PositionHistory();
            for (var i = 0; i < 1000; i++)
                history.Add(i * 0.02, new Vector2(i, 0));
            if (history.At(19).X != 950 || history.At(-1).X != 872)
                throw new Exception("环形记录必须保留最近的固定数量样本。");
            var config = new Configuration();
            if (config.Copy().SelfJudgementAdvance != 0.35)
                throw new Exception("默认提前量应为 0.35 秒。");
            config.SelfJudgementAdvance = 0;
            if (config.Copy().SelfJudgementAdvance != 0)
                throw new Exception("关闭提前判定的设置必须保留。");
        }
    }
}

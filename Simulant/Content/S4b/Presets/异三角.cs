using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.S4b.Presets
{
    internal class 异三角 : SimPresetBase
    {
        public override int TerritoryId { get; } = 755;
        public override string Name { get; } = "本体 异三角";
        public override string Author { get; } = "阿洛";
        public override DateTime LastUpdated { get; } = new DateTime(2026, 6, 1);
        public override PhaseData Phase { get; } = O8s.本体P1;
        public override int Level => 80;
        public override Type SimLogicType { get; } = typeof(O8s_异三角Logic);
        public override string Description { get; } = "新玩具";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
        };

    }

    internal class O8s_异三角Logic : SimLogicBase
    {
        public O8s_异三角Logic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void OnStop()
        {
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            _remainingTriangles = _candidateTriangles
                .Select((triangle, i) => (triangle, (byte)(10 + i)))
                .ToList();

            var boss = EntityManager.SpawnBNpc(8528, 7131, 80);
            boss.Pos3D = new Vector3(0, 0, 0);
            boss.Heading = (float)Math.PI;
            boss.SetReadyToDraw();
            boss.EnableDraw();

            // 准备 3 秒后，以本体读条为机制时间轴的零点。
            var mechanic = new SimTimeline();
            mechanic.At(0, () => boss.Cast(0x290D), "异三角读条");
            mechanic.At(3.2, () => StartTriangles(2), "第一组异三角");
            mechanic.At(5.2, () => StartTriangles(2), "第二组异三角");
            mechanic.At(7.2, () => StartTriangles(3), "第三组异三角");
            mechanic.At(20, () => EntityManager.Delete(boss), "回收本体");
            timeline.At(3, mechanic);
        }

        private void StartTriangles(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Play(SingleTriangleTimeline());
            }
        }
        
        private SimTimeline SingleTriangleTimeline()
        {
            (Triangle triangle, byte index) = GetRandomTriangleFromList();
            var eobj = EntityManager.SpawnEObj(triangle.CreateSpawnSpec(index));
            var timeline = new SimTimeline();
            timeline.At(1.1, () => eobj.PlayAnimation(0x20), "异三角出现");
            timeline.At(2.6, () => eobj.PlayAnimation(0x80), "异三角旋转");
            timeline.At(9.6, () => eobj.PlayAnimation(0x8), "异三角消失");
            timeline.At(9.7, () =>
            {
                foreach (var pos in triangle.ResolveAbilityPos())
                    DummyExecute(pos, 0f, 0x290E, 3f); // 实际爆炸的圆形 AoE
            }, "异三角爆炸");
            return timeline;
        }

        private List<Triangle> _candidateTriangles = new List<Triangle>
        {
            new Triangle(14.4338f, -5.0000f, TriangleDirection.Left),
            new Triangle(11.5470f, 10.0000f, TriangleDirection.Right),
            new Triangle(-11.5470f, -10.0000f, TriangleDirection.Left),
            new Triangle(-14.4338f, 5.0000f, TriangleDirection.Right),
            new Triangle(2.8868f, -15.0000f, TriangleDirection.Right),
            new Triangle(0.0000f, 0.0000f, TriangleDirection.Both),
            new Triangle(-2.8868f, 15.0000f, TriangleDirection.Left),
        };

        private readonly Random _random = new Random();
        private readonly object _randomLock = new object();
        private List<(Triangle Triangle, byte ObjectIndex)> _remainingTriangles;

        /// <summary>
        /// 从候选三角形列表中随机选择一个。
        /// </summary>
        /// <returns></returns>
        private (Triangle triangle, byte index) GetRandomTriangleFromList()
        {
            lock (_randomLock)
            {
                if (_remainingTriangles == null || _remainingTriangles.Count == 0)
                    throw new InvalidOperationException("没有可用的异三角候选位置。");

                int i = _random.Next(_remainingTriangles.Count);
                var item = _remainingTriangles[i];
                _remainingTriangles.RemoveAt(i);

                return (item.Triangle, item.ObjectIndex);
            }
        }

        public enum TriangleDirection
        {
            Left,  // 1E84B4
            Right, // 1E84B3
            [RandomEnum(Left, Right)]
            Both
        }

        public class Triangle
        {
            public Vector2 PosXY;
            public bool IsLeft;

            public Triangle(float x, float y, TriangleDirection direction)
            {
                PosXY = new Vector2(x, y);
                IsLeft = direction.TryResolveRandom() == TriangleDirection.Left;
            }

            public EventObjectSpawnSpec CreateSpawnSpec(byte objectIndex)
            {
                return new EventObjectSpawnSpec
                {
                    Index = objectIndex,
                    BaseId = IsLeft ? 2000052u : 2000051u,
                    SharedTimelineState = 0x2,
                    Pos = new Vector3(PosXY.X, PosXY.Y, 0),
                };
            }

            /// <summary>
            /// 计算三个实际释放小圆形技能的位置。
            /// </summary>
            public List<Vector3> ResolveAbilityPos()
            {
                var r = (IsLeft ? 1 : -1) * (float)(10.0 / Math.Sqrt(3.0));

                return new List<Vector3>
                {
                    new Vector3(PosXY.X - r, PosXY.Y, 0f),
                    new Vector3(PosXY.X + r / 2f, PosXY.Y - 5f, 0f),
                    new Vector3(PosXY.X + r / 2f, PosXY.Y + 5f, 0f),
                };
            }
        }

    }
}

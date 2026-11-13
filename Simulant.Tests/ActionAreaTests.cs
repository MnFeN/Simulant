using Simulant.Game.ExtractedCsv;
using Simulant.Game.ExtractedCsv.Rows;
using Simulant.Simulation.Geometry;
using System;
using System.Numerics;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;

namespace Simulant.Tests
{
    internal static class ActionAreaTests
    {
        public static void CirclesAndDonutsUseClosedBoundaries()
        {
            var circle = ActionArea.Circle(5);
            var origin = new Vector2(100, 100);
            Assert(circle.Contains(origin, origin, 0), "圆心应在圆形范围内。");
            Assert(circle.Contains(new Vector2(103, 104), origin, 0), "半径为 5 的圆必须包含 3、4、5 直角三角形对应的边界点。");
            Assert(!circle.Contains(new Vector2(103.001f, 104), origin, 0), "圆周外的点不得被算入范围。");
            Assert(circle.Contains(new Vector2(95, 100), origin, 2), "圆形范围不能受到朝向影响。");

            var donut = ActionArea.Donut(3, 5);
            Assert(!donut.Contains(origin, origin, 0), "圆环中心必须安全。");
            Assert(!donut.Contains(new Vector2(102.999f, 100), origin, 0), "内圆以内必须安全。");
            Assert(donut.Contains(new Vector2(103, 100), origin, 0), "圆环内边界必须命中。");
            Assert(donut.Contains(new Vector2(103, 104), origin, 0), "圆环外边界必须命中。");
            Assert(!donut.Contains(new Vector2(105.001f, 100), origin, 0), "圆环外侧不得命中。");
            Assert(ActionArea.Donut(0, 5).Contains(origin, origin, 0), "内半径为零的圆环应包含圆心。");

            var huge = ActionArea.Circle(float.MaxValue);
            Assert(!huge.Contains(new Vector2(float.MaxValue, float.MaxValue), Vector2.Zero, 0), "距离平方溢出单精度时仍须正确排除圆外点。");
            Assert(!huge.Contains(new Vector2(-float.MaxValue, 0), new Vector2(float.MaxValue, 0), 0), "有限坐标相减不能因单精度溢出而误判。");
        }

        public static void RectanglesUseActHeadingAndFullDimensions()
        {
            var rectangle = ActionArea.Rectangle(8, 4);
            var origin = new Vector2(100, 100);
            foreach (var corner in new[] { new Vector2(98, 100), new Vector2(102, 100), new Vector2(98, 108), new Vector2(102, 108) })
                Assert(rectangle.Contains(corner, origin, 0), "矩形四角应包含在闭合边界内。");
            Assert(!rectangle.Contains(new Vector2(102.001f, 104), origin, 0), "矩形宽度参数是完整宽度，不能当成半宽。");
            Assert(!rectangle.Contains(new Vector2(100, 108.001f), origin, 0), "矩形长度参数是向前长度，不能翻倍。");
            Assert(!rectangle.Contains(new Vector2(100, 99.999f), origin, 0), "矩形不能延伸到原点后方。");

            var east = (float)(Math.PI / 2);
            Assert(rectangle.Contains(new Vector2(108, 102), origin, east), "ACT 的 π/2 朝向应使矩形向 +X 延伸。");
            Assert(rectangle.Contains(new Vector2(100, 98), origin, east), "旋转后的后边界须容纳浮点朝向舍入误差。");
            Assert(!rectangle.Contains(new Vector2(99.999f, 100), origin, east), "旋转后的原点后方不得命中。");
            Assert(rectangle.Contains(new Vector2(92, 98), origin, -east), "负朝向应使矩形向 -X 延伸。");
        }

        public static void ConesHandleAcuteHalfPlaneAndReflexAngles()
        {
            var cone = ActionArea.Cone(10, 90);
            var origin = new Vector2(100, 100);
            Assert(cone.Contains(origin, origin, 0), "扇形顶点必须命中。");
            Assert(cone.Contains(new Vector2(105, 105), origin, 0), "90 度扇形必须包含右侧 45 度边界。");
            Assert(cone.Contains(new Vector2(95, 105), origin, 0), "90 度扇形必须包含左侧 45 度边界。");
            Assert(!cone.Contains(new Vector2(105.001f, 105), origin, 0), "扇形角度边界外的点不得命中。");
            Assert(cone.Contains(new Vector2(100, 110), origin, 0), "扇形半径边界必须命中。");
            Assert(!cone.Contains(new Vector2(100, 110.001f), origin, 0), "角度正确但超过扇形半径的点不得命中。");
            Assert(!cone.Contains(new Vector2(100, 99), origin, 0), "锐角扇形后方不得命中。");
            Assert(cone.Contains(new Vector2(110, 100), origin, (float)(Math.PI / 2)), "扇形必须遵循 ACT 朝向。");

            var halfPlane = ActionArea.Cone(10, 180);
            Assert(halfPlane.Contains(new Vector2(110, 100), origin, 0), "180 度扇形应包含右侧边界。");
            Assert(halfPlane.Contains(new Vector2(90, 100), origin, 0), "180 度扇形应包含左侧边界。");
            Assert(!halfPlane.Contains(new Vector2(100, 99.999f), origin, 0), "180 度扇形不能误包后半场。");

            var reflex = ActionArea.Cone(10, 270);
            Assert(reflex.Contains(new Vector2(101, 99), origin, 0), "270 度扇形应包含后侧 135 度边界。");
            Assert(reflex.Contains(new Vector2(102, 99), origin, 0), "大于 180 度的扇形必须包含部分后半场。");
            Assert(!reflex.Contains(new Vector2(101, 98.999f), origin, 0), "270 度扇形仍须保留后方安全区。");
            Assert(!reflex.Contains(new Vector2(100, 99), origin, 0), "270 度扇形正后方不得命中。");

            var full = ActionArea.Cone(10, 360);
            Assert(full.Contains(new Vector2(100, 90), origin, 0), "360 度扇形应包含正后方圆周。");
            Assert(!full.Contains(new Vector2(100, 89.999f), origin, 0), "360 度扇形仍应受半径约束。");
        }

        public static void InvalidAreasAndCoordinatesFailClearly()
        {
            foreach (var value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Expect<ArgumentOutOfRangeException>(() => ActionArea.Circle(value), "无效圆形半径必须拒绝。");
                Expect<ArgumentOutOfRangeException>(() => ActionArea.Rectangle(1, value), "无效矩形宽度必须拒绝。");
                Expect<ArgumentOutOfRangeException>(() => ActionArea.Rectangle(value, 1), "无效矩形长度必须拒绝。");
                Expect<ArgumentOutOfRangeException>(() => ActionArea.Cone(1, value), "无效扇形张角必须拒绝。");
            }
            Expect<ArgumentOutOfRangeException>(() => ActionArea.Cone(1, 360.01f), "大于一圈的扇形张角必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.Donut(-1, 5), "负内半径必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.Donut(5, 5), "相等的内外半径必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.Donut(6, 5), "大于外半径的内半径必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.Donut(float.NaN, 5), "非有限内半径必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.Donut(1, float.PositiveInfinity), "非有限外半径必须拒绝。");
            Expect<InvalidOperationException>(() => default(ActionArea).Contains(Vector2.Zero, Vector2.Zero, 0), "未初始化的范围不得默默返回空判定。");

            var circle = ActionArea.Circle(1);
            Expect<ArgumentOutOfRangeException>(() => circle.Contains(new Vector2(float.NaN, 0), Vector2.Zero, 0), "非有限判定点必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => circle.Contains(Vector2.Zero, new Vector2(0, float.PositiveInfinity), 0), "非有限原点必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => circle.Contains(Vector2.Zero, Vector2.Zero, float.NaN), "非有限朝向必须拒绝。");
        }

        public static void ActionRowsUseVerifiedShapesAndCasterHitboxes()
        {
            foreach (var shape in new byte[] { 2, 3, 4, 5, 12, 13 })
            {
                WithAction(new ActionRow { ShapeType = shape, ScaleY = 10, ScaleX = 2 }, actionId =>
                {
                    var area = ActionArea.FromAction(actionId, 2, coneAngleDegrees: 90);
                    var addsHitbox = shape == 3 || shape == 4 || shape == 5;
                    Assert(area.Contains(new Vector2(0, 11), Vector2.Zero, 0) == addsHitbox,
                        "只有 CastType 3、4、5 应向技能范围加上施法者碰撞半径。");
                    Assert(area.Contains(new Vector2(0, addsHitbox ? 12 : 10), Vector2.Zero, 0), "技能表范围的前方边界应命中。");
                    Assert(!area.Contains(new Vector2(0, addsHitbox ? 12.001f : 10.001f), Vector2.Zero, 0), "技能表范围的前方边界外不得命中。");
                    if (shape == 4 || shape == 12)
                    {
                        Assert(area.Contains(new Vector2(2, 5), Vector2.Zero, 0), "技能表 ScaleX 为矩形半宽，边界应命中。");
                        Assert(!area.Contains(new Vector2(2.001f, 5), Vector2.Zero, 0), "施法者碰撞半径不得改变矩形宽度。");
                        Assert(!area.Contains(new Vector2(0, -0.001f), Vector2.Zero, 0), "技能表矩形不得向后延伸。");
                    }
                });
            }
            WithAction(new ActionRow { ShapeType = 10, ScaleY = 10 }, actionId =>
            {
                var area = ActionArea.FromAction(actionId, 2, innerRadius: 3);
                Assert(area.Contains(new Vector2(0, 10), Vector2.Zero, 0), "圆环外半径应使用 EffectRange。");
                Assert(!area.Contains(new Vector2(0, 11), Vector2.Zero, 0), "圆环外半径不得添加施法者碰撞半径。");
            });
        }

        public static void ActionRowsResolveStandardOmenDimensions()
        {
            WithAction(new ActionRow { ShapeType = 13, ScaleY = 10, OmenId = 1, Omen = new Omen { Name = "vfx/omen/eff/gl_fan090_1bf.avfx" } }, actionId =>
            {
                var area = ActionArea.FromAction(actionId);
                Assert(area.Contains(new Vector2(5, 5), Vector2.Zero, 0), "fan090 表示完整张角 90 度。");
                Assert(!area.Contains(new Vector2(5.001f, 5), Vector2.Zero, 0), "不得将 fan090 当成半张角。");
            });
            foreach (var name in new[] { "gl_sircle_1005bf", "er_sicle_1005f", "circle_1005_test", "circle1005_test" })
            {
                WithAction(new ActionRow { ShapeType = 10, ScaleY = 20, OmenId = 1, Omen = new Omen { Name = name } }, actionId =>
                {
                    var area = ActionArea.FromAction(actionId);
                    Assert(!area.Contains(new Vector2(0, 9.999f), Vector2.Zero, 0), "圆环预兆中的内外半径比例必须按 EffectRange 缩放。");
                    Assert(area.Contains(new Vector2(0, 10), Vector2.Zero, 0), "缩放后的圆环内边界应命中。");
                });
            }
            WithAction(new ActionRow { ShapeType = 10, ScaleY = 40, OmenId = 1, Omen = new Omen { Name = "gl_sircle_1907af" } }, actionId =>
            {
                var area = ActionArea.FromAction(actionId);
                Assert(!area.Contains(new Vector2(0, 14.7f), Vector2.Zero, 0), "圆环比例计算不能将内半径截断为整数。");
                Assert(area.Contains(new Vector2(0, 14.75f), Vector2.Zero, 0), "圆环比例计算必须保留小数距离。");
            });
        }

        public static void ActionRowsRequireExplicitUnknownDimensions()
        {
            foreach (var name in new[] { "", "custom_omen", "m0532om_fan01x", "gl_fan000_test", "gl_fan361_test", "gl_fan0900_test", "gl_fan090_fan120_test" })
            {
                WithAction(new ActionRow { ShapeType = 13, ScaleY = 10, OmenId = 1, Omen = new Omen { Name = name } }, actionId =>
                {
                    var error = Expect<InvalidOperationException>(() => ActionArea.FromAction(actionId), "未知或歧义扇形名称不能回退到猜测张角。");
                    Assert(error.Message.Contains("coneAngleDegrees"), "缺失扇形数据时应提示具体的补充参数。");
                });
            }
            foreach (var name in new[] { "", "custom_omen", "m0532om_don03x", "gl_sircle_1034bf", "gl_sircle_0000bf", "gl_sircle_1000bf", "gl_sircle_10050bf", "gl_sircle_1005_sircle_2010bf" })
            {
                WithAction(new ActionRow { ShapeType = 10, ScaleY = 10, OmenId = 1, Omen = new Omen { Name = name } }, actionId =>
                {
                    var error = Expect<InvalidOperationException>(() => ActionArea.FromAction(actionId), "未知或歧义圆环名称不能回退为圆形。");
                    Assert(error.Message.Contains("innerRadius"), "缺失圆环数据时应提示具体的补充参数。");
                });
            }
            WithAction(new ActionRow { ShapeType = 13, ScaleY = 10, OmenId = 0, Omen = new Omen { Name = "gl_fan090_1bf" } }, actionId =>
                Expect<InvalidOperationException>(() => ActionArea.FromAction(actionId), "没有预兆编号时不得使用无效的预兆元数据。"));
        }

        public static void ActionRowsValidateOverridesAndUnsupportedTypes()
        {
            WithAction(new ActionRow { ShapeType = 13, ScaleY = 10, OmenId = 1, Omen = new Omen { Name = "gl_fan090_1bf" } }, actionId =>
            {
                var area = ActionArea.FromAction(actionId, coneAngleDegrees: 45);
                Assert(area.Contains(new Vector2(3, 9), Vector2.Zero, 0), "显式扇形角度范围以内应命中。");
                Assert(!area.Contains(new Vector2(4, 9), Vector2.Zero, 0), "显式扇形角度应优先于预兆名称。");
                Expect<ArgumentOutOfRangeException>(() => ActionArea.FromAction(actionId, coneAngleDegrees: float.NaN), "显式角度也必须验证有限值。");
            });
            WithAction(new ActionRow { ShapeType = 10, ScaleY = 10, OmenId = 1, Omen = new Omen { Name = "gl_sircle_1005bf" } }, actionId =>
            {
                Assert(ActionArea.FromAction(actionId, innerRadius: 2).Contains(new Vector2(0, 3), Vector2.Zero, 0), "显式圆环内半径应优先于预兆名称。");
                Expect<ArgumentOutOfRangeException>(() => ActionArea.FromAction(actionId, innerRadius: 11), "显式内半径不能超过外半径。");
            });
            WithAction(new ActionRow { ShapeType = 13, ScaleY = 10 }, actionId =>
                Assert(ActionArea.FromAction(actionId, coneAngleDegrees: 180).Contains(new Vector2(0, 5), Vector2.Zero, 0), "没有预兆的技能可以显式补充张角。"));
            WithAction(new ActionRow { ShapeType = 8, ScaleY = 10 }, actionId =>
            {
                var error = Expect<NotSupportedException>(() => ActionArea.FromAction(actionId), "不支持的 CastType 不能自动回退为圆形。");
                Assert(error.Message.Contains("CastType=8"), "不支持的类型应在错误中保留类型编号。");
            });
            Expect<InvalidOperationException>(() => ActionArea.FromAction(uint.MaxValue), "超出技能表索引范围的编号必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.FromAction(1, -1), "负施法者碰撞半径必须拒绝。");
            Expect<ArgumentOutOfRangeException>(() => ActionArea.FromAction(1, float.PositiveInfinity), "非有限施法者碰撞半径必须拒绝。");
        }

        private static void WithAction(ActionRow row, System.Action<uint> action)
        {
            const int actionId = 2000000001;
            var table = CsvManager.Instance.Get<ActionRow>();
            var hadPrevious = table.TryGetValue(actionId, out var previous);
            table[actionId] = row;
            try { action(actionId); }
            finally
            {
                if (hadPrevious) table[actionId] = previous;
                else table.Remove(actionId);
            }
        }

        private static T Expect<T>(System.Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T exception) { return exception; }
            throw new Exception(message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}

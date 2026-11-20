using System;
using System.IO;
using System.Numerics;

namespace Simulant.Core.Environment
{
    internal static class StudioAvfx
    {
        // Base size of the mesh encoded below, in meters.
        internal const float EdgeLength = 10f;
        internal const int TriggerCount = 12;

        internal static byte[] Build(StudioSettings settings) => Build(settings, false);

        internal static byte[] BuildDecalFloor()
        {
            var settings = StudioSettings.Default;
            settings.DrawLayer = StudioDrawLayer.Base;
            return Build(settings, true);
        }

        private static byte[] Build(StudioSettings settings, bool decal)
        {
            var useLighting = settings.EnvironmentLight != 0 || settings.DirectionalLight != 0;
            var life = Fields(("Val", -1f), ("ValR", 0f), ("Type", 0));
            var identity = Fields(("ACT", 0), ("ACTR", 0));
            var white = Fields(("RGB", Fields(("KeyC", 1), ("BvPr", 0), ("BvPo", 0), ("Keys", Key(1f, 1f, 1f)))));
            var noTexture = Fields(("bEna", 0), ("TxNo", -1));
            var timeline = Fields(("LpSt", 0), ("LpEd", 0), ("BnNo", -1), ("TICn", 1), ("CpCn", 0),
                ("Item", Fields(("bEna", 1), ("StTm", 0), ("EdTm", -1), ("BdNo", -1), ("EfNo", -1), ("EmNo", 0), ("Plfm", 0))));
            // 粒子持续跟随发射器的位置和缩放。
            var initialParticle = Fields(("bEnb", 1), ("TgtB", 0), ("LoDr", 0), ("CrTm", 1), ("CrCn", 1), ("CrPr", 100),
                ("PICd", 2), ("PICo", 0), ("ICbS", 1), ("ICbR", 1), ("ICbP", 1), ("ICbB", 0), ("ICSK", 0),
                ("IPbV", 0), ("IPbL", 0), ("bOvr", 0), ("OvrV", 60), ("OvrR", 0), ("PrLk", -1), ("StFr", 0),
                ("bStN", 0), ("BIAX", 0f), ("BIAY", 0f), ("BIAZ", 0f), ("GenD", 0f), ("bGD", 0));
            var emitter = Fields(("SdNo", -1), ("LpSt", 0), ("LpEd", 0), ("ClCn", 30), ("EfNo", -1), ("bAD", new byte[1]),
                ("EVT", 0), ("RBDT", 2), ("CCOT", 0), ("ROT", 2), ("PrCn", 1), ("EmCn", 0), ("Life", life),
                ("CrC", Curve(1f)), ("CrI", Curve(100f)), ("ARs", Curve(1f)), ("Col", white),
                ("Pos", identity), ("Rot", identity), ("Scl", identity), ("ItPr", initialParticle));

            // Decal 投影到地图表面，LightModel 绘制内嵌网格。色调由运行时实例控制。
            var particle = Fields(("LpSt", 0), ("LpEd", 0), ("PrVT", decal ? 10 : 13), ("RBDT", 2), ("RoOT", 2), ("CCOT", 0), ("RMT", 0),
                ("CulT", 0), ("EnvT", 0), ("DirT", settings.DirectionalLight != 0 ? 1 : 0), ("UVPT", 0), ("DwPr", decal ? 0 : 1),
                ("DsDt", settings.DepthTest ? 1 : 0), ("DsDw", settings.DepthWrite ? 1 : 0), ("DsSp", 0),
                ("Coll", -1), ("bS11", 0), ("bATM", 0), ("bAFg", 0), ("bNea", 0), ("bFar", 0), ("NeSt", 0f),
                ("NeEd", 1f), ("FaSt", 50f), ("FaEd", 60f), ("FaBP", 0f), ("UvSN", 1), ("EvAR", settings.EnvironmentLight),
                ("DlAR", settings.DirectionalLight), ("LBAR", 0), ("DOTy", 0), ("DpOf", 0f), ("bSCt", 0), ("Life", life), ("ARs", Curve(1f)),
                ("Scl", identity), ("Rot", identity), ("Pos", identity), ("Col", white),
                ("UvSt", Fields(("CUvT", 0))), ("Data", decal ? Fields(("SS", 1f)) : Fields(("MNO", 0))),
                ("TC1", noTexture), ("TC2", noTexture),
                ("TN", noTexture), ("TR", noTexture), ("TD", noTexture), ("TP", noTexture));

            var body = Fields(
                ("Ver", 0x20110913), ("bDFP", 1), ("bFG", 0), ("bTS", 0), ("bASH", 0), ("bCBC", 1), ("bCul", 1),
                ("CBPx", 0f), ("CBPy", 0f), ("CBPz", 0f), ("CBSx", decal ? 2f : EdgeLength), ("CBSy", decal ? 2f : EdgeLength * 2), ("CBSz", decal ? 2f : EdgeLength),
                ("ZBMs", 1f), ("ZBMd", 0f), ("bCmS", 0), ("bFEL", 0), ("bOSt", 0), ("SPFR", 0.15f), ("SKO", decal ? 0f : -80f),
                ("DwLy", (int)settings.DrawLayer), ("DwOT", 0), ("DLST", 0f), ("PL1S", 0f), ("PL2S", 0f),
                ("RvPx", 0f), ("RvPy", 0f), ("RvPz", 0f), ("RvRx", 0f), ("RvRy", 0f), ("RvRz", 0f),
                ("RvSx", 1f), ("RvSy", 1f), ("RvSz", 1f), ("RvR", 1f), ("RvG", 1f), ("RvB", 1f),
                ("AFXe", 0), ("AFXi", (float)Math.PI), ("AFXo", (float)Math.PI),
                ("AFYe", 0), ("AFYi", (float)Math.PI), ("AFYo", (float)Math.PI),
                ("AFZe", 0), ("AFZi", (float)Math.PI), ("AFZo", (float)Math.PI), ("bGFE", 0), ("GFIM", 1f),
                ("ScCn", 1), ("TlCn", 1), ("EmCn", 1), ("PrCn", 1), ("EfCn", 0), ("BdCn", 0), ("TxCn", 0), ("MdCn", decal ? 0 : 1),
                ("Schd", Scheduler()), ("TmLn", timeline), ("Emit", emitter), ("Ptcl", particle));
            if (!decal)
            {
                var mesh = Model(useLighting, settings.FloorType == StudioFloorType.Model);
                body = Encode(writer =>
                {
                    writer.Write(body);
                    WriteField(writer, "Modl", mesh);
                });
            }
            return Fields(("AVFX", body));
        }

        private static byte[] Scheduler()
        {
            var item = Fields(("bEna", 1), ("StTm", 0), ("TlNo", 0));
            var inactive = Fields(("bEna", 1), ("StTm", 0), ("TlNo", -1));
            return Encode(writer =>
            {
                writer.Write(Fields(("ItCn", 1), ("TrCn", TriggerCount), ("Item", item)));
                // 保留已验证的原生触发槽排列。
                for (var count = 1; count <= TriggerCount; count++)
                {
                    var trigger = Encode(data =>
                    {
                        data.Write(item);
                        for (var index = 0; index < count; index++)
                            data.Write(inactive);
                    });
                    WriteField(writer, "Trgr", trigger);
                }
            });
        }

        private static byte[] Model(bool useLighting, bool includeFloor)
        {
            // 固定网格使用 IEEE binary16 坐标。实际尺寸由场景实例的 XYZ 缩放控制。
            const ushort left = 0xC500, right = 0x4500, floor = 0, ceiling = 0x4900;
            var vertices = new (ushort X, ushort Y, ushort Z)[]
            {
                (left, floor, left), (right, floor, left), (right, floor, right), (left, floor, right),
                (left, ceiling, left), (right, ceiling, left), (right, ceiling, right), (left, ceiling, right)
            };
            // 棚内表面的法线朝内。受光时各面使用自己的顶点，避免相邻面的法线被插值。
            var faces = new (ushort[] Corners, Vector3 Normal, Vector3 Tangent)[]
            {
                (new ushort[] { 0, 3, 2, 1 }, Vector3.UnitY, Vector3.UnitZ),
                (new ushort[] { 0, 1, 5, 4 }, Vector3.UnitZ, Vector3.UnitX),
                (new ushort[] { 1, 2, 6, 5 }, -Vector3.UnitX, Vector3.UnitZ),
                (new ushort[] { 2, 3, 7, 6 }, -Vector3.UnitZ, -Vector3.UnitX),
                (new ushort[] { 3, 0, 4, 7 }, Vector3.UnitX, -Vector3.UnitZ),
                (new ushort[] { 4, 5, 6, 7 }, -Vector3.UnitY, Vector3.UnitX)
            };
            var firstFace = includeFloor ? 0 : 1;
            var positions = Encode(writer =>
            {
                if (useLighting)
                {
                    for (var faceIndex = firstFace; faceIndex < faces.Length; faceIndex++)
                    {
                        var face = faces[faceIndex];
                        foreach (var corner in face.Corners)
                            WriteVertex(writer, vertices[corner], face.Normal, face.Tangent);
                    }
                }
                else
                {
                    foreach (var vertex in vertices)
                        WriteVertex(writer, vertex, Vector3.Zero, Vector3.Zero);
                }
            });

            var indices = Encode(writer =>
            {
                var triangleOrder = new[] { 0, 1, 2, 0, 2, 3 };
                for (var faceIndex = firstFace; faceIndex < faces.Length; faceIndex++)
                {
                    var corners = faces[faceIndex].Corners;
                    var first = (faceIndex - firstFace) * 4;
                    foreach (var corner in triangleOrder)
                        writer.Write(useLighting ? (ushort)(first + corner) : corners[corner]);
                }
            });
            return Fields(("VDrw", positions), ("VIdx", indices));
        }

        private static void WriteVertex(BinaryWriter writer, (ushort X, ushort Y, ushort Z) position,
            Vector3 normal, Vector3 tangent)
        {
            const ushort one = 0x3C00;
            writer.Write(position.X);
            writer.Write(position.Y);
            writer.Write(position.Z);
            writer.Write(one);
            if (normal == Vector3.Zero)
            {
                writer.Write(0UL);
            }
            else
            {
                WriteDirection(writer, normal);
                WriteDirection(writer, tangent);
            }
            writer.Write(uint.MaxValue);
            writer.Write(0UL); // 四组 UV。
            writer.Write(0UL);
        }

        private static void WriteDirection(BinaryWriter writer, Vector3 direction)
        {
            writer.Write((byte)(128f + 127f * direction.X));
            writer.Write((byte)(128f + 127f * direction.Y));
            writer.Write((byte)(128f + 127f * direction.Z));
            writer.Write((byte)128);
        }

        private static byte[] Curve(float value)
            => Fields(("KeyC", 1), ("BvPr", 0), ("BvPo", 0), ("RanT", 0), ("Keys", Key(1f, 1f, value)));

        private static byte[] Key(float x, float y, float z) => Encode(writer =>
        {
            writer.Write((short)0); // 帧
            writer.Write((short)1); // 插值类型
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        });

        private static byte[] Fields(params (string Tag, object Value)[] fields) => Encode(writer =>
        {
            foreach (var field in fields)
                WriteField(writer, field.Tag, field.Value);
        });

        private static void WriteField(BinaryWriter writer, string tag, object value)
        {
            for (var index = 0; index < 4; index++)
                writer.Write(index < tag.Length ? (byte)tag[tag.Length - 1 - index] : (byte)0);

            switch (value)
            {
                case int number:
                    writer.Write(sizeof(int));
                    writer.Write(number);
                    break;
                case float number:
                    writer.Write(sizeof(float));
                    writer.Write(number);
                    break;
                case byte[] data:
                    writer.Write(data.Length);
                    writer.Write(data);
                    for (var padding = (4 - data.Length % 4) % 4; padding != 0; padding--)
                        writer.Write((byte)0);
                    break;
                default:
                    throw new ArgumentException("无效的 AVFX 字段类型。", nameof(value));
            }
        }

        private static byte[] Encode(Action<BinaryWriter> write)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                write(writer);
                return buffer.ToArray();
            }
        }
    }
}

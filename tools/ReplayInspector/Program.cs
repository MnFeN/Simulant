using Simulant.Replay;
using System;
using System.Globalization;
using System.IO;

namespace Simulant.ReplayTool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("回放只读检查和转换工具");
                Console.WriteLine("Simulant.ReplayInspector.exe <回放.dat> [--output <新目录>] [--profile CN7.57|CN7.58]");
                Console.WriteLine("Simulant.ReplayInspector.exe convert <回放.dat> --profile CN7.58 [--start-ms <毫秒>] [--end-ms <毫秒>] [--output <新目录>]");
                Console.WriteLine("Simulant.ReplayInspector.exe generate <timeline.json> --output <新C#文件>");
                Console.WriteLine("Simulant.ReplayInspector.exe health <回放.dat> --actor-id <十六进制ID> --start-ms <毫秒> --end-ms <毫秒> [--output <新目录>]");
                return args.Length == 0 ? 2 : 0;
            }

            try
            {
                var convert = args[0] == "convert";
                var generate = args[0] == "generate";
                var health = args[0] == "health";
                var inputIndex = convert || generate || health ? 1 : 0;
                if (args.Length <= inputIndex)
                    throw new ArgumentException("缺少输入文件。");

                var input = args[inputIndex];
                string output = null, profile = null;
                var startMs = 0u;
                var endMs = uint.MaxValue;
                var actorId = 0u;
                for (var i = inputIndex + 1; i < args.Length; i++)
                {
                    var option = args[i];
                    if (++i >= args.Length)
                        throw new ArgumentException("缺少参数值：" + option);

                    switch (option)
                    {
                        case "--output":
                            output = args[i];
                            break;
                        case "--profile" when !generate:
                            profile = args[i];
                            break;
                        case "--start-ms" when convert || health:
                            startMs = ParseMilliseconds(args[i], option);
                            break;
                        case "--end-ms" when convert || health:
                            endMs = ParseMilliseconds(args[i], option);
                            break;
                        case "--actor-id" when health:
                            var value = args[i].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? args[i].Substring(2) : args[i];
                            if (!uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out actorId) || actorId == 0)
                                throw new ArgumentException("--actor-id 需要有效的十六进制实体 ID。");
                            break;
                        default:
                            throw new ArgumentException("无法识别参数：" + option);
                    }
                }

                if (generate)
                {
                    if (output == null)
                        throw new ArgumentException("请指定 --output 和新的 C# 文件名。");
                    ReplayConversion.Generate(input, output);
                    Console.WriteLine("C# 时间轴底稿：" + Path.GetFullPath(output));
                    return 0;
                }

                if (output == null)
                    output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports",
                        Path.GetFileNameWithoutExtension(input) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "Z");

                if (convert)
                {
                    ReplayConversion.Convert(input, output, profile, startMs, endMs);
                    Console.WriteLine("导出完成：" + Path.GetFullPath(output));
                    Console.WriteLine("时间轴位于 timeline.json 和 timeline.g.cs，整份回放的调查清单位于 summary.md 和 TSV 文件。");
                    return 0;
                }

                if (health)
                {
                    if (actorId == 0 || endMs == uint.MaxValue)
                        throw new ArgumentException("血量提取需要 --actor-id 和 --end-ms。");
                    ReplayHealthExport.Export(input, output, profile, actorId, startMs, endMs);
                    Console.WriteLine("血量样本：" + Path.Combine(Path.GetFullPath(output), "health.tsv"));
                    return 0;
                }

                var result = ReplayInspector.Export(input, output, profile);
                Console.WriteLine(FormattableString.Invariant($"读取完成：{result.PacketCount} 条记录，{result.OpcodeCount} 种 opcode，最大时间 {result.MaxTimestampMs / 1000.0:F3} 秒。"));
                Console.WriteLine("原文件 SHA-256：" + result.Sha256);
                Console.WriteLine("输出目录：" + result.OutputDirectory);
                Console.WriteLine("请先查看 summary.md 与 opcodes.csv；全部记录位于 packets.csv。");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("回放处理失败：" + ex.Message);
                return 1;
            }
        }

        private static uint ParseMilliseconds(string value, string option)
        {
            if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
                throw new ArgumentException(option + " 需要非负的整数毫秒。");

            return milliseconds;
        }
    }
}

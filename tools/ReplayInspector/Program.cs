using Simulant.Replay;
using System;
using System.IO;

namespace Simulant.ReplayTool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("回放只读检查工具\n用法：Simulant.ReplayInspector.exe <回放.dat> [--output <新输出目录>] [--profile CN7.57]\n不指定版本时只输出候选；指定版本仍需核对录制版本，不会自动把内部构建号当作补丁号。");
                return args.Length == 0 ? 2 : 0;
            }
            try
            {
                string output = null, profile = null;
                for (var i = 1; i < args.Length; i++)
                {
                    if ((args[i] != "--output" && args[i] != "--profile") || i + 1 >= args.Length)
                        throw new ArgumentException("无法识别或缺少值的参数：" + args[i]);
                    var option = args[i++];
                    if (option == "--output") output = args[i];
                    else profile = args[i];
                }
                if (output == null)
                    output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports",
                        Path.GetFileNameWithoutExtension(args[0]) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "Z");
                var result = ReplayInspector.Export(args[0], output, profile);
                Console.WriteLine("读取完成：{0} 条记录，{1} 种 opcode，最大时间 {2:F3} 秒。", result.PacketCount, result.OpcodeCount, result.MaxTimestampMs / 1000.0);
                Console.WriteLine("原文件 SHA-256：" + result.Sha256);
                Console.WriteLine("输出目录：" + result.OutputDirectory);
                Console.WriteLine("请先查看 summary.md 与 opcodes.csv；全部记录位于 packets.csv。此阶段不生成或执行模拟时间轴。");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("回放读取失败：" + ex.Message);
                return 1;
            }
        }
    }
}

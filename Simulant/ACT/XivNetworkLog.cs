using FFXIV_ACT_Plugin.Logfile;
using System;
using System.Reflection;

namespace Simulant.ACT
{
    internal static class XivNetworkLog
    {
        private static readonly FieldInfo ContainerField = typeof(FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)
            .GetField("_iocContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>通过 FF14 解析插件写入网络日志，由它生成完整行和校验值。<br />
        /// · id 为日志行的数字编号，payload 为竖线分隔的内容字段。<br />
        /// · timestamp 默认为当前时间。
        /// </summary>
        internal static void WriteLine(int id, string payload, DateTimeOffset? timestamp = null)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            if (payload.IndexOf('\r') >= 0 || payload.IndexOf('\n') >= 0)
                throw new ArgumentException("网络日志内容必须在同一行。", nameof(payload));

            var parser = XivPluginInterop.plugin;
            if (parser?.PluginStarted != true)
                throw new InvalidOperationException("FF14 解析插件尚未启动。");

            var services = ContainerField?.GetValue(parser) as IServiceProvider;
            var output = services?.GetService(typeof(ILogOutput)) as ILogOutput
                ?? throw new InvalidOperationException("无法获取 FF14 解析插件的日志写入服务。");

            output.WriteLine((LogMessageType)id, timestamp?.LocalDateTime ?? DateTime.Now, payload);
        }
    }
}

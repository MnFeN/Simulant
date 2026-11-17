using System;
using System.Linq;
using Advanced_Combat_Tracker;

namespace Simulant.ACT
{
    public static class NamazuInterop
    {
        internal const string UpdateGuide = "https://1824544011.cdn.123clouddisk.com/1824544011/Triggernometry_Release_CN/PostNamazu.dll";

        public static NamazuPlugin Plugin { get; private set; }

        public static bool IsReady => Plugin?.IsReady == true;

        public static void Init()
        {
            DeInit();

            if (ActGlobals.oFormActMain == null)
                throw new Exception($"[{nameof(NamazuInterop)}] 未找到 ActGlobals.oFormActMain。");

            var pluginData = ActGlobals.oFormActMain.ActPlugins
                .FirstOrDefault(x =>
                    x.pluginObj != null &&
                    x.pluginObj.GetType().ToString() == "PostNamazu.PostNamazu")
                ?? throw new Exception($"[{nameof(NamazuInterop)}] 未找到 PostNamazu 插件，或插件未启用。");

            if (pluginData.cbEnabled?.Checked != true)
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] PostNamazu 插件未启用，请先在 ACT 插件列表中启用鲶鱼精邮差。");

            var rawPlugin = pluginData.pluginObj;
            var version = rawPlugin.GetType().Assembly.GetName().Version;
            var minimumVersion = new Version(1, 3, 6, 7);

            if (version == null || version < minimumVersion)
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] PostNamazu 版本过低（当前版本：{version?.ToString() ?? "未知"}，最低要求：{minimumVersion}）。请按 {UpdateGuide} 中的说明更新鲶鱼精邮差。");

            var plugin = new NamazuPlugin(rawPlugin);
            var readinessError = plugin.GetReadinessError();

            if (readinessError != null)
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] {readinessError}");

            Plugin = plugin;
        }

        public static void DeInit()
        {
            Plugin = null;
        }
    }
}

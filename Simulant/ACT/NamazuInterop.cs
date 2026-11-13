using System;
using System.Linq;
using Advanced_Combat_Tracker;

namespace Simulant.ACT
{
    public static class NamazuInterop
    {
        public static object RawPlugin { get; private set; }
        public static NamazuPlugin Plugin { get; private set; }

        // private static GreyMagicCallScheduler _callScheduler;

        public static bool IsReady => Plugin?.IsReady == true;// && _callScheduler != null;

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
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] PostNamazu 版本过低（当前版本：{version?.ToString() ?? "未知"}，最低要求：{minimumVersion}）。请在 Triggernometry 远程库中的自检工具箱中尝试检查鲶鱼精邮差版本。");

            var plugin = new NamazuPlugin(rawPlugin);
            var readinessError = plugin.GetReadinessError();
            if (readinessError != null)
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] {readinessError}");

            // var scheduler = new GreyMagicCallScheduler(plugin.Memory, frameLeaseMs: 10);

            RawPlugin = rawPlugin;
            Plugin = plugin;
            // _callScheduler = scheduler;
        }

        public static void DeInit()
        {
            // var scheduler = _callScheduler;
            // _callScheduler = null;
            // scheduler?.Dispose();

            RawPlugin = null;
            Plugin = null;
        }

        /*
        /// <summary>
        ///   尝试在 GreyMagic FrameLock 窗口内同步执行 Call 等操作。<br />
        ///   若短期内多次调用，会自动尝试在同一帧内调用，注意避免传入高延迟动作阻塞 UI。<br />
        /// </summary>
        /// <remarks>
        ///   用于降低连续直接调用 CallInjected64 时因等待 hook 入口再次触发而产生的高延迟。<br />
        ///   如果只是调用函数，可直接使用 <see cref="Simulant.Game.IntPtrExtensions"/> 中包装的 IntPtr.Call(params object[] args)。
        /// </remarks>
        public static void ExecuteInFrameLock(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!IsReady)
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] PostNamazu 插件未准备就绪。");

            _callScheduler.Execute(action);
        }

        /// <summary>
        ///   尝试在 GreyMagic FrameLock 窗口内同步执行 Call 等操作。<br />
        ///   若短期内多次调用，会自动尝试在同一帧内调用，注意避免传入高延迟动作阻塞 UI。<br />
        /// </summary>
        /// <remarks>
        ///   用于降低连续直接调用 CallInjected64 时因等待 hook 入口再次触发而产生的高延迟。<br />
        ///   如果只是调用函数，可直接使用 <see cref="Simulant.Game.IntPtrExtensions"/> 中包装的 IntPtr.Call<T>(params object[] args)。
        /// </remarks>
        public static T ExecuteInFrameLock<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            if (!IsReady)
                throw new InvalidOperationException($"[{nameof(NamazuInterop)}] PostNamazu 插件未准备就绪。");

            T result = default;
            _callScheduler.Execute(() => result = func());
            return result;
        }
        */
    }
}

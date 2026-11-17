using System;
using System.Reflection;

namespace Simulant.ACT
{
    /// <summary>
    /// Wrapper for PostNamazu.PostNamazu
    /// </summary>
    public class NamazuPlugin
    {
        private readonly dynamic _plugin;

        private GreyMagicExternalProcessMemory _memory;
        public GreyMagicExternalProcessMemory Memory
        {
            get
            {
                var current = _plugin.Memory;

                if (_memory?.RawMemory != current)
                {
                    _memory = current == null ? null : new GreyMagicExternalProcessMemory(current);
                }

                return _memory;
            }
        }

        private NamazuScanner _sigScanner;
        public NamazuScanner SigScanner
        {
            get
            {
                var current = _plugin.SigScanner;

                if (_sigScanner?.RawScanner != current)
                {
                    _sigScanner = current == null ? null : new NamazuScanner(current);
                }

                return _sigScanner;
            }
        }

        public bool IsReady => GetReadinessError() == null;

        private readonly PropertyInfo _stateProperty;
        private readonly FieldInfo _callSchedulerField;

        public NamazuPlugin(object plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

            var pluginType = plugin.GetType();
            _stateProperty = pluginType.GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMemberException($"PostNamazu 缺少工作状态接口 State。请在 {NamazuInterop.UpdateGuide} 更新鲶鱼精邮差。");
            _callSchedulerField = pluginType.GetField("CallScheduler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingFieldException($"PostNamazu 缺少函数调用调度器 CallScheduler。请在 {NamazuInterop.UpdateGuide} 更新鲶鱼精邮差。");
        }

        /// <summary>按 PostNamazu 的工作状态及模拟所需资源检查就绪情况；不要求开启 HTTP 服务。</summary>
        internal string GetReadinessError()
        {
            var state = _stateProperty.GetValue(_plugin)?.ToString();

            switch (state)
            {
                case "Ready":
                    break;
                case "NotReady":
                    return "PostNamazu 尚未连接游戏进程，请确认游戏已启动且 FFXIV 解析插件已识别进程。";
                case "Waiting":
                    return "PostNamazu 正在启动、扫描或切换游戏进程，请等待鲶鱼精邮差就绪后重新初始化模拟器。";
                case "Failure":
                    return "PostNamazu 地址扫描或游戏进程注入失败，请检查鲶鱼精邮差日志。";
                default:
                    return $"PostNamazu 工作状态无法识别（{state ?? "未知"}），请检查鲶鱼精邮差版本及日志。";
            }

            // 卸载时 State 可能尚未更新，不能仅凭 Ready 判断资源仍然有效。
            var memory = Memory;

            if (memory == null || !memory.IsProcessOpen || memory.Process?.HasExited != false)
                return "PostNamazu 的游戏进程连接已失效，请等待鲶鱼精邮差重新连接后初始化模拟器。";

            if (SigScanner == null)
                return "PostNamazu 的地址扫描器尚未就绪，请检查鲶鱼精邮差日志。";

            if (_callSchedulerField.GetValue(_plugin) == null)
                return "PostNamazu 的函数调用调度器尚未就绪或已停止，请重新启用鲶鱼精邮差后初始化模拟器。";

            return null;
        }

        /// <summary> 使用自动复用的 FrameLock 窗口，在指定的函数地址调用 CallInjected64。</summary>
        public void Call(IntPtr ptr, params object[] args)
            => _plugin.Call(ptr, args);

        /// <summary> 使用自动复用的 FrameLock 窗口，在指定的函数地址调用 CallInjected64。</summary>
        public T Call<T>(IntPtr ptr, params object[] args)
            where T : struct
            => _plugin.Call<T>(ptr, args);
    }
}

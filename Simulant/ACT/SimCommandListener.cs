using Advanced_Combat_Tracker;
using System;
using System.Text.RegularExpressions;

namespace Simulant.ACT
{
    /// <summary>从 ACT 解析后的实时默语日志中提取模拟命令。</summary>
    internal sealed class SimCommandListener
    {
        private static readonly Regex CommandPattern = new Regex(
            @"^.{15}\S+ 00:0038::(?i) *sim *(?<cmd>.+?) *$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        private readonly Action<string> _logError;
        private FormActMain _source;
        private volatile bool _listening;

        internal SimCommandListener(Action<string> logError)
        {
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        /// <summary>在 ACT 日志回调线程同步通知；处理者应快速返回，模拟操作交给已有调度器执行。</summary>
        internal event Action<string> Received;

        // 启停由宿主的订阅锁串行调用，绑定失败必须向初始化入口传播。
        internal void Start(FormActMain source)
        {
            if (source == null || source.IsDisposed)
                throw new InvalidOperationException("无法订阅解析日志：ACT 主窗口尚未就绪或已经关闭。");

            if (_listening && ReferenceEquals(_source, source))
                return;

            Stop();
            source.OnLogLineRead += OnLogLineRead;
            _source = source;
            _listening = true;
        }

        internal void Stop()
        {
            _listening = false;
            if (_source == null)
                return;

            _source.OnLogLineRead -= OnLogLineRead;
            _source = null;
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (!_listening || isImport || string.IsNullOrEmpty(logInfo?.logLine))
                return;

            try
            {
                var match = CommandPattern.Match(logInfo.logLine);
                if (!match.Success)
                    return;

                Dispatch(match.Groups["cmd"].Value);
            }
            catch (Exception error)
            {
                ReportError("读取模拟命令日志失败：" + error);
            }
        }

        /// <summary>分发从默语读取的模拟命令；停止监听后丢弃待处理命令。</summary>
        internal void Dispatch(string command)
        {
            var handlers = Received;
            if (!_listening || string.IsNullOrWhiteSpace(command) || handlers == null)
                return;

            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                if (!_listening)
                    return;
                try
                {
                    handler(command);
                }
                catch (Exception error)
                {
                    ReportError("处理模拟命令失败：" + error);
                }
            }
        }

        private void ReportError(string message)
        {
            try
            {
                _logError(message);
            }
            catch
            {
                // 插件处理或日志输出失败不能中断 ACT 向其他订阅者分发日志。
            }
        }
    }
}

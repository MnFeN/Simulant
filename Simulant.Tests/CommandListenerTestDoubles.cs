using System;

// 只替换 ACT 事件源，测试链接正式的命令监听实现。
namespace Advanced_Combat_Tracker
{
    public sealed class LogLineEventArgs
    {
        public string logLine;
    }

    public delegate void LogLineEventDelegate(bool isImport, LogLineEventArgs logInfo);

    public sealed class FormActMain
    {
        private LogLineEventDelegate _handlers;
        public bool IsDisposed { get; set; }
        internal bool RejectSubscription { get; set; }
        internal int SubscriberCount => _handlers?.GetInvocationList().Length ?? 0;

        public event LogLineEventDelegate OnLogLineRead
        {
            add
            {
                if (RejectSubscription)
                    throw new InvalidOperationException("测试：拒绝日志订阅。");

                _handlers += value;
            }
            remove
            {
                _handlers -= value;
            }
        }

        internal void Emit(bool isImport, string line)
        {
            _handlers?.Invoke(isImport, new LogLineEventArgs { logLine = line });
        }
    }
}

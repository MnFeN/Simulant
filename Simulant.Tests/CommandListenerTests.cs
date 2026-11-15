using Advanced_Combat_Tracker;
using Simulant.ACT;
using System;
using System.Collections.Generic;

namespace Simulant.Tests
{
    internal static class CommandListenerTests
    {
        private const string Prefix = "[12:34:56.789] ChatLog 00:0038::";

        public static void ExtractsCommandsWithoutChangingArguments()
        {
            var source = new FormActMain();
            var commands = new List<string>();
            var listener = new SimCommandListener(message => { });
            listener.Received += commands.Add;
            listener.Start(source);
            source.Emit(false, Prefix + "   SiM   ac 亲疏自行   ");
            source.Emit(false, Prefix + "sim ac 沉稳咏唱");
            Assert(commands.Count == 2 && commands[0] == "ac 亲疏自行" && commands[1] == "ac 沉稳咏唱",
                "命令应忽略 sim 大小写并去除外围空格，保留参数内容。");
            listener.Stop();
        }

        public static void IgnoresImportsOtherChannelsAndEmptyCommands()
        {
            var source = new FormActMain();
            var count = 0;
            var listener = new SimCommandListener(message => { });
            listener.Received += command => count++;
            listener.Start(source);
            source.Emit(true, Prefix + "sim ac 亲疏自行");
            source.Emit(false, Prefix.Replace("0038", "000A") + "sim ac 亲疏自行");
            source.Emit(false, Prefix + "sim");
            source.Emit(false, Prefix + " sim   ");
            source.Emit(false, Prefix + "不是 sim ac 亲疏自行");
            source.Emit(false, "00|2026-11-15|0038||sim ac 亲疏自行|");
            source.Emit(false, null);
            Assert(count == 0, "导入日志、其他频道、空命令及其他格式不能触发命令事件。");
            listener.Stop();
        }

        public static void ReinitializationDoesNotDuplicateAndStopUnsubscribes()
        {
            var first = new FormActMain();
            var second = new FormActMain();
            var count = 0;
            var listener = new SimCommandListener(message => { });
            listener.Received += command => count++;
            listener.Start(first);
            listener.Start(first);
            Assert(first.SubscriberCount == 1, "重复初始化不能重复订阅。");
            first.Emit(false, Prefix + "sim test");
            listener.Start(second);
            Assert(first.SubscriberCount == 0 && second.SubscriberCount == 1, "换宿主时必须解除旧订阅。");
            first.Emit(false, Prefix + "sim test");
            second.Emit(false, Prefix + "sim test");
            listener.Stop();
            listener.Stop();
            second.Emit(false, Prefix + "sim test");
            Assert(count == 2 && second.SubscriberCount == 0, "停止后不能继续收到命令。");
        }

        public static void SubscriptionFailurePropagatesAndCanBeRetried()
        {
            var listener = new SimCommandListener(message => { });
            ExpectFailure(() => listener.Start(null));
            ExpectFailure(() => listener.Start(new FormActMain { IsDisposed = true }));
            var source = new FormActMain { RejectSubscription = true };
            ExpectFailure(() => listener.Start(source));
            Assert(source.SubscriberCount == 0, "绑定失败不能留下有效订阅。");
            source.RejectSubscription = false;
            listener.Start(source);
            Assert(source.SubscriberCount == 1, "绑定失败后应允许重新初始化。");
            listener.Stop();
        }

        public static void HandlerFailureDoesNotEscapeOrSkipOtherHandlers()
        {
            var source = new FormActMain();
            var errors = new List<string>();
            var listener = new SimCommandListener(errors.Add);
            var count = 0;
            listener.Received += command => throw new InvalidOperationException("测试异常");
            listener.Received += command => count++;
            listener.Start(source);
            source.Emit(false, Prefix + "sim test");
            Assert(count == 1 && errors.Count == 1, "单个处理者失败不能中断其他处理者或逃逸到 ACT。");
            listener.Stop();
        }

        public static void InterfaceCommandsShareDispatchAndRespectListenerLifetime()
        {
            var source = new FormActMain();
            var commands = new List<string>();
            var listener = new SimCommandListener(message => { });
            listener.Received += commands.Add;
            listener.Dispatch("ac 冲刺");
            listener.Start(source);
            listener.Dispatch("ac 冲刺");
            source.Emit(false, Prefix + "sim ac 亲疏自行");
            listener.Stop();
            listener.Dispatch("ac 沉稳咏唱");
            Assert(commands.Count == 2 && commands[0] == "ac 冲刺" && commands[1] == "ac 亲疏自行",
                "界面与默语应走同一事件；初始化前或停止后不能分发命令。");
        }

        private static void ExpectFailure(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new Exception("订阅失败必须向初始化入口报告，不能静默跳过。");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}

namespace Simulant.Tests.Native
{
    using Simulant.ACT;
    using Simulant.Core.Entity;
    using Simulant.Game;
    using Simulant.Game.FFCS.Client.Game.Character;
    using Simulant.Game.FFCS.Client.Game.Object;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using CoreCharacter = Simulant.Core.Entity.Character;

    internal static class NativeCallsTests
    {
        private static int Main()
        {
            try
            {
                VerifyLayoutAndArguments();
                VerifyValidation();
                VerifyFailureCleanup();
                VerifyCharacterIdentity();
                Console.WriteLine("原生技能演出验证通过：4 项。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("原生技能演出验证失败：\n" + error);
                return 1;
            }
        }

        private static void VerifyLayoutAndArguments()
        {
            Assert(Marshal.SizeOf(typeof(ActionEffectHandler.Header)) == 0x28 &&
                Marshal.OffsetOf(typeof(ActionEffectHandler.Header), "AnimationTargetId").ToInt32() == 0 &&
                Marshal.OffsetOf(typeof(ActionEffectHandler.Header), "RotationInt").ToInt32() == 0x1A &&
                Marshal.OffsetOf(typeof(ActionEffectHandler.Header), "NumTargets").ToInt32() == 0x21,
                "技能头部必须保持 FFCS 的大小和关键字段偏移。");
            using (var scene = new Scene())
            {
                var target = new GameObjectId { ObjectId = 0x10FF1111, Type = 2 };
                var position = new Vector3(100, 7, 105);
                ActionEffectHandler.ReceiveVisual(0x40FF2222, new IntPtr(0x330000), 0xBAD6, -1.25f, position, target);
                var header = scene.Plugin.Headers.Single();
                Assert(header.AnimationTargetId.Id == target.Id && scene.Plugin.Targets.Single() == target.Id,
                    "主目标和目标数组必须保留完整 64 位身份。");
                Assert(header.ActionId == 0xBAD6 && header.SpellId == 0xBAD6 && header.ActionType == 1 && header.NumTargets == 1 &&
                    header.RotationInt == PacketCodec.EncodeUShortHeading(-1.25f) && header.GlobalSequence != 0,
                    "技能编号、朝向、类型、目标数量与结算序号必须完整。");
                Assert(header.BallistaEntityId == 0xE0000000 && header.Flags == 0 && header.SourceSequence == 0 &&
                    header.AnimationLock == 0 && header.AnimationVariation == 0,
                    "纯演出不得伪造玩家技能序号或强制玩家动画锁。");
                Assert(scene.Plugin.Positions.Single() == position, "原生坐标必须保持 XYZ，不能再次交换高度。");
                Assert(scene.Memory.Bytes.Freed.SequenceEqual(scene.Memory.Bytes.Allocations), "成功后必须释放远程缓冲区。");
                ActionEffectHandler.ReceiveVisual(0x40FF2222, new IntPtr(0x330000), 0xBAD9, 0, position, target);
                Assert(scene.Plugin.Headers[1].GlobalSequence != header.GlobalSequence, "连续演出应使用不同结算序号。");
                Throws<ArgumentException>(() => scene.Plugin.Call(ActionEffectPlugin.Function, 0x40FF2222u,
                    new IntPtr(0x330000), position, header, new byte[64], target));
            }
        }

        private static void VerifyValidation()
        {
            using (var scene = new Scene())
            {
                var target = new GameObjectId { ObjectId = 123 };
                Action<uint, IntPtr, uint, float, Vector3, GameObjectId> send = ActionEffectHandler.ReceiveVisual;
                Throws<ArgumentOutOfRangeException>(() => send(1, new IntPtr(1), 0, 0, Vector3.Zero, target));
                Throws<ArgumentOutOfRangeException>(() => send(1, new IntPtr(1), 65536, 0, Vector3.Zero, target));
                Throws<ArgumentException>(() => send(0, new IntPtr(1), 1, 0, Vector3.Zero, target));
                Throws<ArgumentException>(() => send(1, IntPtr.Zero, 1, 0, Vector3.Zero, target));
                Throws<ArgumentException>(() => send(1, new IntPtr(1), 1, 0, Vector3.Zero, new GameObjectId { ObjectId = 0xE0000000 }));
                Throws<ArgumentOutOfRangeException>(() => send(1, new IntPtr(1), 1, float.NaN, Vector3.Zero, target));
                Throws<ArgumentOutOfRangeException>(() => send(1, new IntPtr(1), 1, 0, new Vector3(float.PositiveInfinity), target));
                ActionEffectHandler.ReceiveFuncPtr = IntPtr.Zero;
                Throws<InvalidOperationException>(() => send(1, new IntPtr(1), 1, 0, Vector3.Zero, target));
                Assert(scene.Memory.Bytes.Allocations.Count == 0 && scene.Plugin.Headers.Count == 0,
                    "无效调用必须在分配内存和进入原生函数之前拒绝。");
            }
        }

        private static void VerifyFailureCleanup()
        {
            foreach (var failure in new[] { "分配为空", "清零短写", "头部写入", "原生调用" })
            {
                using (var scene = new Scene())
                {
                    scene.Memory.Failure = failure;
                    scene.Plugin.FailCall = failure == "原生调用";
                    Throws<InvalidOperationException>(() => ActionEffectHandler.ReceiveVisual(1, new IntPtr(1), 1, 0,
                        Vector3.Zero, new GameObjectId { ObjectId = 2 }));
                    Assert(scene.Memory.Bytes.Allocations.SequenceEqual(scene.Memory.Bytes.Freed),
                        "失败后必须释放已取得的全部远程分配：" + failure);
                    Assert(scene.Plugin.Headers.Count == (failure == "原生调用" ? 1 : 0),
                        "参数写入失败时不能进入原生函数：" + failure);
                }
            }
        }

        private static void VerifyCharacterIdentity()
        {
            using (var scene = new Scene())
            {
                var sourceAddress = new IntPtr(0x300000);
                var targetAddress = new IntPtr(0x400000);
                var observations = new EntityObservation?[4];
                observations[2] = new EntityObservation(sourceAddress, 0x40FF1234, ObjectKind.BattleNpc);
                observations[0] = new EntityObservation(targetAddress, 0x10001234, ObjectKind.Pc);
                var registry = new EntityRegistry(slot => observations[slot], observations.Length);
                var source = (CoreCharacter)registry.Get(2);
                var target = (CoreCharacter)registry.Get(0);
                scene.Memory.Bytes.Put(sourceAddress + 0x8C, (ushort)2);
                scene.Memory.Bytes.Put(sourceAddress + 0xC0, 0.75f);
                scene.Memory.Bytes.Put(targetAddress + 0x8C, (ushort)0);
                scene.Memory.Bytes.Put(targetAddress + 0xB0, new Vector3(101, 3, 109));
                source.ExecuteActionEffect(0xBAD6, target);
                Assert(scene.Plugin.Positions.Single() == new Vector3(101, 3, 109) &&
                    scene.Plugin.Headers.Single().AnimationTargetId.ObjectId == target.Id,
                    "Character 入口必须传当前目标的原生位置和身份。");
                Throws<ArgumentNullException>(() => source.ExecuteActionEffect(1, null));
                scene.Memory.Bytes.Put(sourceAddress + 0x8C, (ushort)200);
                Throws<InvalidOperationException>(() => source.ExecuteActionEffect(1, target));
                scene.Memory.Bytes.Put(sourceAddress + 0x8C, (ushort)2);
                observations[0] = new EntityObservation(targetAddress, 0x10005678, ObjectKind.Pc);
                Throws<InvalidOperationException>(() => source.ExecuteActionEffect(1, target));
                registry.Reset();
                Throws<InvalidOperationException>(() => source.ExecuteActionEffect(1, target));
                Assert(scene.Plugin.Headers.Count == 1 && scene.Memory.Bytes.Allocations.Count == 1,
                    "本地角色、失效目标和失效施法者不得触发额外的分配或原生调用。");
            }
        }

        internal static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }

        private sealed class Scene : IDisposable
        {
            internal readonly ActionEffectMemory Memory = new ActionEffectMemory();
            internal readonly ActionEffectPlugin Plugin;
            private readonly NamazuPlugin _previousPlugin;
            private readonly IntPtr _previousFunction;

            internal Scene()
            {
                _previousPlugin = NamazuInterop.Plugin;
                _previousFunction = ActionEffectHandler.ReceiveFuncPtr;
                Plugin = new ActionEffectPlugin(Memory);
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { new NamazuPlugin(Plugin) });
                ActionEffectHandler.ReceiveFuncPtr = ActionEffectPlugin.Function;
            }

            public void Dispose()
            {
                ActionEffectHandler.ReceiveFuncPtr = _previousFunction;
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { _previousPlugin });
            }
        }
    }

    public sealed class ActionEffectMemory
    {
        public readonly PartyMemory Bytes = new PartyMemory();
        public string Failure;
        public T Read<T>(IntPtr address) where T : struct => Bytes.Read<T>(address);
        public IntPtr AllocateMemory(int size)
        {
            NativeCallsTests.Assert(size == 0x80, "单目标演出只需一个 128 字节的远程缓冲区。");
            return Failure == "分配为空" ? IntPtr.Zero : Bytes.AllocateMemory(size);
        }
        public int WriteBytes(IntPtr address, byte[] data)
        {
            if (Failure == "清零短写") Bytes.ShortWriteAddress = address;
            return Bytes.WriteBytes(address, data);
        }
        public void Write<T>(IntPtr address, T value) where T : struct
        {
            if (Failure == "头部写入" && typeof(T) == typeof(ActionEffectHandler.Header))
                throw new InvalidOperationException("模拟技能头部写入失败。");
            Bytes.Write(address, value);
        }
        public bool FreeMemory(IntPtr address) => Bytes.FreeMemory(address);
    }

    public sealed class ActionEffectPlugin
    {
        public string State => "Ready";
        public readonly object CallScheduler = new object();
        private readonly object PluginUi = new object();
        public static readonly IntPtr Function = new IntPtr(0x202);
        public readonly ActionEffectMemory Memory;
        public bool FailCall;
        public readonly List<ActionEffectHandler.Header> Headers = new List<ActionEffectHandler.Header>();
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<ulong> Targets = new List<ulong>();
        public ActionEffectPlugin(ActionEffectMemory memory) { Memory = memory; }
        public void Call(IntPtr function, params object[] args)
        {
            if (args.Length != 6 || !(args[0] is uint) || args.Skip(1).Any(arg => !(arg is IntPtr)))
                throw new ArgumentException("技能原生入口只接受施法者 ID 和五个指针。");
            NativeCallsTests.Assert(function == Function, "必须调用技能结算入口。");
            var buffer = (IntPtr)args[2];
            NativeCallsTests.Assert(Memory.Bytes.Allocations.Contains(buffer) && !Memory.Bytes.Freed.Contains(buffer),
                "原生函数只能读取尚未释放的远程缓冲区。");
            NativeCallsTests.Assert((IntPtr)args[3] == buffer + 0x10 && (IntPtr)args[4] == buffer + 0x38 &&
                (IntPtr)args[5] == buffer + 0x78, "头部、效果和目标数组指针必须指向正确偏移。");
            NativeCallsTests.Assert(Memory.Bytes.ReadBytes(buffer + 0x38, 0x40).All(value => value == 0),
                "八项效果必须全部为空，不能产生伤害或状态。");
            Headers.Add(Memory.Read<ActionEffectHandler.Header>((IntPtr)args[3]));
            Positions.Add(Memory.Read<Vector3>(buffer));
            Targets.Add(Memory.Read<ulong>((IntPtr)args[5]));
            if (FailCall) throw new InvalidOperationException("模拟技能原生调用失败。");
        }
    }
}

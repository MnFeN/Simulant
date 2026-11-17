using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Graphics.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Simulant.Core
{
    internal static class VfxManager
    {
        private sealed class Effect
        {
            public VfxObject Native;
            public object Owner;
            public DateTime ExpiresAt;
        }

        private static readonly object Gate = new object();
        private static readonly List<Effect> Effects = new List<Effect>();
        private static GreyMagicExternalProcessMemory _memory;
        private static IntPtr _processHandle;
        private static Timer _timer;
        private static long _world;

        public static void ShowOmen(string name, Vector3 position, float heading, Vector3 scale,
            double duration, object owner, Vector4? color = null, CancellationToken cancellation = default)
        {
            lock (Gate)
            {
                cancellation.ThrowIfCancellationRequested();
                CheckProcess();
                GameFunctions.CheckStatus(VfxObject.RunFuncPtr, VfxObject.FadeOutFuncPtr);
                const string pool = "Client.System.Scheduler.Instance.VfxObject";
                var native = VfxObject.CreateStatic("vfx/omen/eff/" + name + ".avfx", pool);
                if (native.Ptr == IntPtr.Zero)
                    throw new InvalidOperationException("无法创建预兆：" + name);

                var effect = new Effect { Native = native, Owner = owner };
                Effects.Add(effect);
                if (_timer == null)
                {
                    var world = _world;
                    _timer = new Timer(_ => Expire(world), null, 100, Timeout.Infinite);
                }

                try
                {
                    var rotation = Quaternion.CreateFromYawPitchRoll(0, 0, heading);
                    native.Position.Set(new Vector3(position.X, position.Z, position.Y));
                    native.Rotation.Set(new Quaternion(rotation.X, rotation.Z, rotation.Y, rotation.W));
                    native.Scale.Set(new Vector3(scale.X, scale.Z, scale.Y));
                    if (color.HasValue)
                        native.Color.Set(color.Value);
                    native.Run(0f, -1);
                    effect.ExpiresAt = DateTime.UtcNow.AddSeconds(duration);
                }
                catch
                {
                    effect.ExpiresAt = DateTime.UtcNow;
                    throw;
                }
            }
        }

        public static void ShowLockOn(IntPtr target, string name)
            => ShowActor("vfx/lockon/eff/" + name + ".avfx", target, target);

        public static void ShowActor(string path, IntPtr source, IntPtr target)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase) || path.IndexOf('\0') >= 0)
                throw new ArgumentException("实体特效需要有效的 .avfx 资源路径。", nameof(path));

            // 当前的点名和实体预览由游戏自行结束。
            lock (Gate)
                if (VfxObject.CreateActor(path, source, target, -1f, 0, 0, 0).Ptr == IntPtr.Zero)
                    throw new InvalidOperationException("无法创建实体特效：" + path);
        }

        public static void Remove(object owner)
        {
            lock (Gate)
            {
                if (Effects.Count == 0)
                    return;

                CheckProcess();
                RemoveWhere(effect => Equals(effect.Owner, owner));
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                if (Effects.Count == 0)
                    return;

                CheckProcess();
                RemoveWhere(_ => true);
            }
        }

        public static void InvalidateWorld()
        {
            lock (Gate)
                Reset();
        }

        private static void CheckProcess()
        {
            var memory = NamazuInterop.Plugin?.Memory;
            var handle = memory?.IsProcessOpen == true ? memory.ProcessHandle : IntPtr.Zero;
            if (ReferenceEquals(memory?.RawMemory, _memory?.RawMemory) && handle == _processHandle)
                return;

            Reset();
            _memory = memory;
            _processHandle = handle;
        }

        private static void Reset()
        {
            _world++;
            _timer?.Dispose();
            _timer = null;
            Effects.Clear();
            _memory = null;
            _processHandle = IntPtr.Zero;
        }

        private static void Expire(long world)
        {
            Exception failure = null;
            lock (Gate)
            {
                if (world != _world)
                    return;

                try
                {
                    CheckProcess();
                    var now = DateTime.UtcNow;
                    RemoveWhere(effect => effect.ExpiresAt <= now);
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    if (world == _world)
                        _timer?.Change(100, Timeout.Infinite);
                }
            }

            if (failure != null)
                PluginHost.Instance?.LogWarning("清理到期预兆失败：" + failure.Message);
        }

        private static void RemoveWhere(Predicate<Effect> matches)
        {
            Exception firstError = null;
            foreach (var effect in Effects.FindAll(matches))
            {
                try
                {
                    effect.Native.FadeOut(10f);
                    Effects.Remove(effect);
                }
                catch (Exception error)
                {
                    firstError = firstError ?? error;
                    effect.ExpiresAt = DateTime.UtcNow.AddSeconds(1);
                }
            }

            if (Effects.Count == 0)
            {
                _world++;
                _timer?.Dispose();
                _timer = null;
            }

            if (firstError != null)
                throw new InvalidOperationException("未能清理部分预兆。", firstError);
        }
    }
}

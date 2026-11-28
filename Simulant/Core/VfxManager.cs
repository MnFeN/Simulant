using Simulant.Game;
using Simulant.Game.FFCS.Client.Graphics.Scene;
using Simulant.Game.FFCS.Client.System.Resource.Handle;
using SimuMagic;
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
            public DateTime? ExpiresAt;
            public ResourceHandle Resource;
            public uint ResourceId;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Vector4? Color;
        }

        private static readonly object Gate = new object();
        private static readonly List<Effect> Effects = new List<Effect>();
        private static ExternalProcessMemory _memory;
        private static IntPtr _processHandle;
        private static Timer _timer;
        private static long _world;
        private const string StaticVfxPool = "Client.System.Scheduler.Instance.VfxObject";

        public static void ShowOmen(string name, Vector3 position, float heading, Vector3 scale,
            double duration, object owner, Vector4? color = null, CancellationToken cancellation = default)
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(0, 0, heading);
            ShowStaticCore("vfx/omen/eff/" + name + ".avfx", new Vector3(position.X, position.Z, position.Y),
                new Quaternion(rotation.X, rotation.Z, rotation.Y, rotation.W), new Vector3(scale.X, scale.Z, scale.Y),
                owner, color, duration, cancellation: cancellation);
        }

        internal static void ShowGroundEffect(string path, Vector3 position, float heading, object owner)
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(heading, 0, 0);
            ShowStaticCore(path, new Vector3(position.X, position.Z, position.Y), rotation, Vector3.One, owner, null);
        }

        internal static void ShowStatic(string path, ResourceHandle resource, Vector3 position, Quaternion rotation,
            Vector3 scale, object owner, Vector4? color = null)
        {
            if (resource.IsNull())
                throw new ArgumentException("静态特效需要已加载的资源句柄。", nameof(resource));

            ShowStaticCore(path, position, rotation, scale, owner, color, resource: resource);
        }

        private static void ShowStaticCore(string path, Vector3 position, Quaternion rotation, Vector3 scale, object owner,
            Vector4? color, double? duration = null, ResourceHandle resource = default, CancellationToken cancellation = default)
        {
            lock (Gate)
            {
                cancellation.ThrowIfCancellationRequested();
                CheckProcess();
                GameFunctions.CheckStatus(VfxObject.RunFuncPtr, VfxObject.FadeOutFuncPtr);
                bool hasResource = !resource.IsNull();
                if (hasResource)
                    GameFunctions.CheckStatus(VfxObject.VTablePtr);

                var resourceId = hasResource ? resource.Id.Get() : 0u;
                var native = VfxObject.CreateStatic(path, StaticVfxPool);
                if (native.IsNull())
                    throw new InvalidOperationException("无法创建静态特效：" + path);

                var effect = new Effect
                {
                    Native = native,
                    Owner = owner,
                    Resource = resource,
                    ResourceId = resourceId,
                    Position = position,
                    Rotation = rotation,
                    Scale = scale,
                    // 播放失败时，定时特效会在下一轮计时中清理。
                    ExpiresAt = duration.HasValue ? DateTime.UtcNow : (DateTime?)null
                };
                Effects.Add(effect);
                if (duration.HasValue && _timer == null)
                {
                    var world = _world;
                    _timer = new Timer(_ => Expire(world), null, 100, Timeout.Infinite);
                }

                try
                {
                    native.Position.Set(position);
                    native.Rotation.Set(rotation);
                    native.Scale.Set(scale);
                    if (color.HasValue)
                        native.Color.Set(color.Value);
                    native.Run(0f, -1);
                    if (duration.HasValue)
                        effect.ExpiresAt = DateTime.UtcNow.AddSeconds(duration.Value);
                }
                catch
                {
                    if (!duration.HasValue)
                    {
                        native.FadeOut(0f);
                        Effects.Remove(effect);
                    }

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

        internal static bool HasEffect(object owner)
        {
            lock (Gate)
            {
                CheckProcess();
                return Effects.Exists(effect => Equals(effect.Owner, owner));
            }
        }

        internal static bool UpdateStatic(object owner, Vector3 position, Quaternion rotation, Vector3 scale, Vector4 color)
        {
            lock (Gate)
            {
                CheckProcess();
                var effect = Effects.Find(item => Equals(item.Owner, owner) && !item.Resource.IsNull());
                var connection = GameInterop.Connection;
                if (effect == null || connection == null || !GameInterop.IsReady)
                    return false;

                var transformChanged = effect.Position != position || effect.Rotation != rotation || effect.Scale != scale;
                var colorChanged = effect.Color != color;
                if (!transformChanged && !colorChanged)
                    return true;

                var matched = false;
                connection.RunInFrame(() =>
                {
                    if (!ReferenceEquals(connection, GameInterop.Connection) ||
                        !effect.Native.MatchesResource(effect.Resource, effect.ResourceId))
                        return;

                    if (transformChanged)
                    {
                        var scene = effect.Native.Ptr.As<Game.FFCS.Client.Graphics.Scene.Object>();
                        scene.Position.Set(position);
                        scene.Rotation.Set(rotation);
                        scene.Scale.Set(scale);
                        scene.IsTransformChanged = true;
                        effect.Native.Ptr.As<DrawObject>().UpdateTransforms(true);
                    }

                    if (colorChanged)
                    {
                        effect.Native.VfxResourceInstance.SetColor(color);
                        effect.Native.Color.Set(color);
                    }
                    matched = true;
                });

                if (!matched)
                    Effects.Remove(effect);
                else
                {
                    effect.Position = position;
                    effect.Rotation = rotation;
                    effect.Scale = scale;
                    effect.Color = color;
                }

                return matched;
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
            try
            {
                lock (Gate)
                {
                    CheckProcess();
                    // 旧地图的预兆指针可能已失效。内存资源实例挂在全局图形场景，按资源身份清理。
                    Effects.RemoveAll(effect => effect.Resource.IsNull());
                    StopTimer();
                    RemoveWhere(_ => true);
                }
            }
            catch (Exception error)
            {
                PluginHost.Instance?.LogWarning("换图时清理静态特效失败：" + (error.InnerException?.Message ?? error.Message));
            }
        }

        internal static void InvalidateProcess()
        {
            lock (Gate)
                Reset();
        }

        private static void CheckProcess()
        {
            var memory = GameInterop.Connection?.Memory;
            var handle = memory?.IsProcessOpen == true ? memory.ProcessHandle : IntPtr.Zero;
            if (ReferenceEquals(memory, _memory) && handle == _processHandle)
                return;

            Reset();
            _memory = memory;
            _processHandle = handle;
        }

        private static void Reset()
        {
            StopTimer();
            Effects.Clear();
            _memory = null;
            _processHandle = IntPtr.Zero;
        }

        private static void StopTimer()
        {
            _world++;
            _timer?.Dispose();
            _timer = null;
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
                    if (effect.Resource.IsNull())
                        effect.Native.FadeOut(10f);
                    else
                        RemoveStatic(effect);
                    Effects.Remove(effect);
                }
                catch (Exception error)
                {
                    firstError = firstError ?? error;
                    if (effect.ExpiresAt.HasValue)
                        effect.ExpiresAt = DateTime.UtcNow.AddSeconds(1);
                }
            }

            if (_timer != null && !Effects.Exists(effect => effect.ExpiresAt.HasValue))
                StopTimer();

            if (firstError != null)
                throw new InvalidOperationException("未能清理部分特效。", firstError);
        }

        private static void RemoveStatic(Effect effect)
        {
            var connection = GameInterop.Connection;
            if (connection == null || !GameInterop.IsReady)
                return;

            GameFunctions.CheckStatus(VfxObject.FadeOutFuncPtr, VfxObject.VTablePtr);
            connection.RunInFrame(() =>
            {
                if (ReferenceEquals(connection, GameInterop.Connection) &&
                    effect.Native.MatchesResource(effect.Resource, effect.ResourceId))
                    effect.Native.FadeOut(0f);
            });
        }
    }
}

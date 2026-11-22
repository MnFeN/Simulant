using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Game.InstanceContent;
using Simulant.Game.FFCS.Client.Graphics.Environment;
using Simulant.Game.FFCS.Client.LayoutEngine;
using System;

namespace Simulant.Core.Environment
{
    internal sealed class EnvironmentService
    {
        private readonly PluginHost _host;

        public EnvironmentService(PluginHost host)
        {
            _host = host;
        }

        public void SetWeather(byte weatherId, float transitionTime = 1f)
        {
            var envManager = EnvManager.Instance;
            envManager.Ptr.ThrowIfZero(nameof(SetWeather), 0, "设置天气");
            WriteWeather(envManager, weatherId, transitionTime);
        }

        private static void WriteWeather(EnvManager envManager, byte weatherId, float transitionTime)
        {
            envManager.ActiveWeather.Set(weatherId);
            envManager.TransitionTime.Set(transitionTime);
        }

        /// <summary>读取已加载的区域及其当前生效的天气和场地效果列表。</summary>
        internal bool TryReadCurrent(out LiveEnvironment current)
        {
            current = default;
            var connection = GameInterop.Connection;
            if (connection == null || !_host.PluginReady || !_host.GameDataReady ||
                _host.IsInitializing || _host.IsSwitchingTerritory || _host.Disposed)
                return false;

            var game = GameMain.Instance;
            if (game.IsNull() || EnvManager.InstancePtrPtr == IntPtr.Zero || game.TerritoryLoadState.Get() != 2)
                return false;

            var territory = game.CurrentTerritoryTypeId.Get();
            var manager = EnvManager.Instance;
            if (territory == 0 || manager.IsNull())
                return false;

            var framework = EventFramework.InstancePtrPtr == IntPtr.Zero ? default : EventFramework.Instance;
            var director = framework.IsNull() ? default : framework.ContentDirector;
            var list = director.IsNull() ? default : director.MapEffects;
            var count = list.IsNull() ? 0 : Math.Min((int)list.ItemCount.Get(), ContentDirector.MapEffectList.Capacity);
            var weather = manager.ActiveWeather.Get();
            if (game.TerritoryLoadState.Get() != 2 || territory != game.CurrentTerritoryTypeId.Get() ||
                !ReferenceEquals(connection, GameInterop.Connection))
                return false;

            current = new LiveEnvironment(connection, territory, manager, weather, director, list, count);
            return true;
        }

        internal bool TrySetCurrentWeather(LiveEnvironment shown, byte weather)
        {
            if (!TryReadCurrent(out var current) || !shown.IsSameRegion(current))
                return false;

            // 与 Hyperborea 的天气编辑行为一致，使用半秒过渡。
            WriteWeather(current.Manager, weather, 0.5f);
            return true;
        }

        /// <summary>读取槽位保存的状态、场景正在播放的位和最近启动位。</summary>
        internal (ushort? Recorded, ushort? Playing, ushort? LastStarted)?[] ReadMapEffectPlayback(LiveEnvironment shown, (int Index, uint LayoutId)[] slots)
        {
            var result = new (ushort? Recorded, ushort? Playing, ushort? LastStarted)?[slots.Length];
            var connection = GameInterop.Connection;
            if (connection == null || !TryReadCurrent(out var current) || !shown.IsSameLayout(current))
                return result;

            var memory = connection.Memory;
            var worldAddress = LayoutWorld.InstancePtrPtr;
            var world = worldAddress == IntPtr.Zero ? default : memory.Read<IntPtr>(worldAddress).As<LayoutWorld>();
            var active = world.IsNull() ? default : world.ActiveLayout;
            var global = active.IsNull() ? default : world.GlobalLayout;
            if (!ReferenceEquals(GameInterop.Connection, connection))
                return result;

            var reader = !active.IsNull() && memory.Read<int>(active.InitState.Ptr) >= 7
                ? new SharedGroupTimelineReader(memory, active, global)
                : null;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Index >= current.SlotCount)
                    continue;

                var item = current.MapEffects.GetItem(slot.Index);
                if (memory.Read<uint>(item.LayoutId.Ptr) != slot.LayoutId)
                    continue;

                var state = memory.Read<ushort>(item.State.Ptr);
                ushort? recorded = state != 0 || (memory.Read<byte>(item.Flags.Ptr) & ContentDirector.MapEffectItem.TimelineAppliedFlag) != 0
                    ? (ushort?)state : null;
                var playback = reader?.ReadPlayback(slot.LayoutId);
                result[i] = (recorded, playback?.Playing, playback?.LastStarted);
            }

            return ReferenceEquals(GameInterop.Connection, connection) &&
                TryReadCurrent(out var after) && current.IsSameLayout(after) &&
                (world.IsNull() || memory.Read<IntPtr>(worldAddress) == world.Ptr &&
                    world.ActiveLayout.Ptr == active.Ptr && (reader == null || world.GlobalLayout.Ptr == global.Ptr)) &&
                ReferenceEquals(GameInterop.Connection, connection)
                ? result : new (ushort? Recorded, ushort? Playing, ushort? LastStarted)?[slots.Length];
        }

        internal bool TryPlayCurrentMapEffect(LiveEnvironment shown, int slot, uint layoutId, ushort mask)
        {
            if (layoutId == 0 || !TryReadCurrent(out var current) || !shown.IsSameLayout(current) ||
                slot < 0 || slot >= current.SlotCount || current.MapEffects.GetItem(slot).LayoutId.Get() != layoutId)
                return false;

            return PlayMapEffect(current.Director, (uint)slot, mask);
        }

        internal static bool TryReadDefaultMapEffects(ContentDirector director, int requiredCount, out ushort?[] flags)
        {
            flags = Array.Empty<ushort?>();
            var list = director.IsNull() ? default : director.MapEffects;
            if (list.IsNull())
                return requiredCount == 0;

            var count = Math.Min((int)list.ItemCount.Get(), ContentDirector.MapEffectList.Capacity);
            if (count < requiredCount)
                return false;

            var defaults = new ushort?[count];
            for (var slot = 0; slot < count; slot++)
                if (list.GetItem(slot).LayoutId.Get() != 0)
                    defaults[slot] = 4;

            flags = defaults;
            return true;
        }

        /// <summary>播放指定场地效果槽位的动画，并返回是否成功。</summary>
        public bool PlayMapEffect(uint slot, ushort timelineIndex, bool logFailure = true)
        {
            var contentDirector = EventFramework.Instance.ContentDirector;

            if (contentDirector.IsNull())
            {
                if (logFailure)
                    _host.LogWarning($"当前地图中不存在 ContentDirector，无法播放场地动画（槽位 {slot}，动画 {timelineIndex}）。");
                return false;
            }

            return PlayMapEffect(contentDirector, slot, timelineIndex, logFailure);
        }

        private bool PlayMapEffect(ContentDirector director, uint slot, ushort timelineIndex, bool logFailure = true)
        {
            bool success = director.PlayMapEffectTimeline(slot, timelineIndex);

            if (!success && logFailure)
            {
                _host.LogWarning($"播放场地动画失败（槽位 {slot}，动画 {timelineIndex}）。");
            }

            return success;
        }

        public void SetBgm(ushort songId) // 参考 perchbirdd/OrchestrionPlugin
        {
            // bgm.Scenes 相当于连续布局的 BGMScene[12]，播放 bgm 的优先级高到低排列
            // 这里只写最高优先级的 scene 0
            const int sceneId = 0;

            var bgm = BGMSystem.Instance();

            if (bgm.IsNull())
                throw new Exception("BGMSystem 为空指针");

            var scenes = bgm.Scenes;

            if (scenes.IsNull())
                throw new Exception("BGM scenes 数组为空指针");

            var scene = scenes[sceneId];

            // songId == 0 且 scene == 0 时，恢复原有 BGM
            if (songId == 0)
                scene.SceneFlags.Set((BGMSystem.SceneFlags)4);

            scene.BgmId.Set(songId);
            scene.PlayingBgmId.Set(songId);
            scene.PreviousBgmId.Set(songId);
            scene.Timer.Set(0f);
            scene.TimerEnabled.Set(0);
        }
    }

    internal readonly struct LiveEnvironment
    {
        private readonly GameConnection _connection;

        internal LiveEnvironment(GameConnection connection, uint territoryId, EnvManager manager, byte weatherId,
            ContentDirector director, ContentDirector.MapEffectList mapEffects, int slotCount)
        {
            _connection = connection;
            TerritoryId = territoryId;
            Manager = manager;
            WeatherId = weatherId;
            Director = director;
            MapEffects = mapEffects;
            SlotCount = slotCount;
        }

        internal uint TerritoryId { get; }
        internal EnvManager Manager { get; }
        internal byte WeatherId { get; }
        internal ContentDirector Director { get; }
        internal ContentDirector.MapEffectList MapEffects { get; }
        internal int SlotCount { get; }

        internal bool IsSameRegion(LiveEnvironment other) => ReferenceEquals(_connection, other._connection) &&
            TerritoryId == other.TerritoryId && Manager.Ptr == other.Manager.Ptr;

        internal bool IsSameLayout(LiveEnvironment other) => IsSameRegion(other) &&
            Director.Ptr == other.Director.Ptr && MapEffects.Ptr == other.MapEffects.Ptr && SlotCount == other.SlotCount;
    }
}

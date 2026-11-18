using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Graphics.Environment;
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
            envManager.ActiveWeather.Set(weatherId);
            envManager.TransitionTime.Set(transitionTime);
        }

        /// <summary>播放指定场地效果槽位的动画，并返回是否成功。</summary>
        public bool PlayMapEffect(uint slot, ushort timelineIndex)
        {
            var contentDirector = EventFramework.Instance.ContentDirector;

            if (contentDirector.IsNull())
            {
                _host.LogWarning($"当前地图中不存在 ContentDirector，无法播放场地动画（槽位 {slot}，动画 {timelineIndex}）。");
                return false;
            }

            bool success = contentDirector.PlayMapEffectTimeline(slot, timelineIndex);

            if (!success)
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
}

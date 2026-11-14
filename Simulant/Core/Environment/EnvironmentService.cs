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

        /// <returns> 是否调用成功。</returns>
        public bool PlayMapEffect(uint slot, ushort flag)
        {
            var contentDirector = EventFramework.Instance.ContentDirector;

            if (contentDirector.IsNull())
            {
                _host.LogWarning($"当前地图中不存在 ContentDirector，无法调用 MapEffect ({slot}, {flag})");
                return false;

            }

            bool success = contentDirector.MapEffect(slot, flag);

            if (!success)
            {
                _host.LogWarning($"当前地图中 MapEffect ({slot}, {flag}) 调用失败。");
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

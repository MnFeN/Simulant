using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Core.Zone
{
    internal sealed partial class ZoneService
    {
        public async Task<bool> EnterPhase(PhaseData phaseData)
        {
            try
            {
                if (!IsInSimulatedTerritory)
                    throw new InvalidOperationException("当前不在模拟区域中，无法进入阶段。");

                var openingEntrance = _openingEntrance;
                var applied = await ApplyPhaseData(phaseData);
                await openingEntrance;
                return applied;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _host.LogError("进入阶段失败：" + ex.ToString());
                return false;
            }
        }

        private Task<bool> ApplyPhaseData(PhaseData phaseData)
        {
            int worldVersion;
            uint territoryId;
            Vector3? spawn;
            bool placeBeforeReady;
            GreenScreenStage greenScreen;

            lock (_stateGate)
            {
                if (!_isInSimulatedTerritory)
                    throw new OperationCanceledException();

                worldVersion = _worldVersion;
                territoryId = _simulatedTerritoryId;
                greenScreen = territoryId == GreenScreenStage.TerritoryId ? _greenScreen : null;
                var recommended = _spawnData?.Recommended;
                var phaseSpawn = phaseData.Spawn;
                spawn = phaseSpawn ?? (greenScreen != null ? TerritorySpawnData.GreenScreenPosition : recommended?.Position);
                placeBeforeReady = greenScreen == null && (phaseSpawn.HasValue || recommended?.Circle != null);
            }

            var clock = Stopwatch.StartNew();
            _host.LogSim($"进入阶段 {phaseData.Name}：天气 {phaseData.Weather}，BGM {phaseData.BGM}，坐标 {spawn?.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "null"}");

            void CheckCurrent(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                EnsureWorld(worldVersion);
            }

            void SetSpawn(CancellationToken token)
            {
                CheckCurrent(token);

                if (!spawn.HasValue)
                    return;

                PlacePlayer(spawn.Value, () => CheckCurrent(token));
            }

            var needsDirector = false;

            foreach (var flag in phaseData.MapEffectFlags)
                needsDirector |= flag.HasValue;

            string lastState = null;

            bool Ready(CancellationToken token)
            {
                CheckCurrent(token);
                var ready = IsMapReady(territoryId, out var state);

                if (needsDirector)
                {
                    var framework = EventFramework.Instance;
                    var directorReady = !framework.IsNull() && !framework.ContentDirector.IsNull();
                    ready &= directorReady;
                    state += $"；场地效果控制器已创建={directorReady}";
                }

                if (state != lastState)
                {
                    lastState = state;
                    _host.LogVerbose(FormattableString.Invariant($"[场地初始化] 区域 {territoryId}，{clock.Elapsed.TotalSeconds:F3} 秒：{state}"));
                }

                return ready;
            }

            void Apply(CancellationToken token)
            {
                CheckCurrent(token);
                if (greenScreen != null)
                {
                    greenScreen.ActivateFloor();
                    SetSpawn(token);
                    _host.LogRuntime("区域 1 已加载绿幕地板，角色已放置到场地内。");
                }
                else if (!placeBeforeReady)
                {
                    SetSpawn(token);
                }

                _host.EnvironmentService.SetWeather(phaseData.Weather);
                CheckCurrent(token);
                _host.EnvironmentService.SetBgm(phaseData.BGM);
            }

            return _phaseApplication.ApplyAsync(token =>
            {
                if (placeBeforeReady)
                    SetSpawn(token);
            }, Apply, Ready, async token =>
            {
                // 地图就绪后，副本的部分场地组仍可能在加载。
                await _phaseApplication.ApplyMapEffectsAsync(phaseData.MapEffectFlags, (slot, mask) =>
                {
                    CheckCurrent(token);
                    return _host.EnvironmentService.PlayMapEffect(slot, mask, logFailure: false);
                }, token);

                CheckCurrent(token);
                _host.LogVerbose(FormattableString.Invariant($"[场地初始化] 区域 {territoryId}，{clock.Elapsed.TotalSeconds:F3} 秒：地图已就绪，已提交阶段「{phaseData.Name}」的天气、BGM 和场地效果。"));
            });
        }
    }
}

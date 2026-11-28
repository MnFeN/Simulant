using Simulant.Core;
using Simulant.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    /// <summary>本机玩家的职业技能结算及其模拟生命周期。</summary>
    internal abstract class JobActions
    {
        private static readonly Dictionary<Job, JobActions> ByJob = new JobActions[]
        {
            PaladinActions.Instance,
            WhiteMageActions.Instance
        }.ToDictionary(actions => actions.ClassJob);

        protected Job ClassJob { get; }

        protected JobActions(Job job) => ClassJob = job;

        internal static JobActions For(Job job) => ByJob.TryGetValue(job, out var actions) ? actions : null;

        /// <summary>处理职业技能请求。未实现的技能返回 false。</summary>
        internal abstract bool TryHandleAction(ActionRequest request);

        /// <summary>开始模拟时，当前职业的重置，如某些职业满量谱。</summary>
        internal abstract void OnSimStart();

        /// <summary>本机技能服务结束时清理职业状态，包括停止模拟、换图和断线。</summary>
        /// <param name="canWritePlayerState">是否仍可写入本次模拟玩家的客户端状态。</param>
        internal abstract void OnSimEnd(bool canWritePlayerState);

        internal virtual void OnAutoAttack()
        {
        }
    }

    /// <summary>为每个职业实现提供单例和对应类型的量谱。</summary>
    internal abstract class JobActions<TJob, TGauge> : JobActions
        where TJob : JobActions<TJob, TGauge>
        where TGauge : struct, IMemoryObject
    {
        /// <summary>由泛型类型初始化时调用职业类的私有无参构造函数，只创建一次。</summary>
        internal static TJob Instance { get; } = (TJob)Activator.CreateInstance(typeof(TJob), nonPublic: true);

        protected JobActions(Job job) : base(job)
        {
        }

        protected TGauge GetGauge() => PlayerActionRules.GetGauge<TGauge>(ClassJob);
    }
}

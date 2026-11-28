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

        internal static void ResetAll()
        {
            foreach (var actions in ByJob.Values)
                actions.OnReset();
        }

        /// <summary>处理职业技能请求。未实现的技能返回 false。</summary>
        internal abstract bool TryHandleAction(ActionRequest request);

        internal virtual void OnSimStart()
        {
        }

        internal virtual void OnAutoAttack()
        {
        }

        protected virtual void OnReset()
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

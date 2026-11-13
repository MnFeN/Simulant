using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation
{
    public abstract class SimLogicBase
    {
        protected readonly PluginHost _host;
        protected readonly SimPresetBase _preset;

        internal SimEntityManager EntityManager { get; set; }
        internal SimScheduler Scheduler { get; set; }

        // 如果修改构造函数传参，需要同步修改 SimSession 构造函数中的 CreateInstance 调用参数
        public SimLogicBase(PluginHost host, SimPresetBase preset)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        }

        internal void Prepare()
        {
            _host.LogSim($"正在开始模拟：{_preset.Name}（{_preset.Phase.Name}）");
            LoadOptionsFrom(_preset.Options);
        }

        internal void Start()
        {
            var timeline = new SimTimeline();
            BuildTimeline(timeline);
            // 准备入口从当前时刻开始；网络实体的后续机制由就绪回调排入。
            Scheduler.ScheduleAfter(0, () => Play(timeline), "开始时间轴");
            _host.LogSim("模拟已开始。");
        }

        internal void Stop()
        {
            try
            {
                _host.LogSim($"正在停止模拟：{_preset.Name}（{_preset.Phase.Name}）");
                OnStop();
            }
            catch (Exception ex)
            {
                _host.LogError($"模拟停止失败：{ex}");
            }
            finally
            {
                _preparingParty = false;
                EntityManager.Clear();
                _host.LogSim("模拟已停止。");
            }
        }

        /// <summary>在调度线程上定义准备节点；网络实体就绪后再排入机制时间轴。时间单位为秒，节点应为短同步动作。</summary>
        protected abstract void BuildTimeline(SimTimeline timeline);
        protected virtual void OnStop() { }

        /// <summary>子轴以当前节点的计划时间为原点，因此父节点迟到不会逐层积累漂移。</summary>
        protected void Play(SimTimeline timeline, double delaySeconds = 0)
            => Scheduler.Play(timeline, delaySeconds);

        /// <summary>把一次机制接入子时间轴；开始、判定和清理由当前会话管理。</summary>
        protected SimTimeline Mechanic(SimMechanic mechanic)
        {
            if (mechanic == null) throw new ArgumentNullException(nameof(mechanic));
            return new SimTimeline().At(0, () => EntityManager.RunMechanic(mechanic), mechanic.Name);
        }

        internal void LoadOptionsFrom(IEnumerable<SimOptionBase> options)
        {
            // 传入的所有 UI 选项构建为字典，过滤掉非 SimOption<T> 子类（没有 PropertyName）
            var dict = (options ?? Enumerable.Empty<SimOptionBase>())
                .Where(o => !string.IsNullOrEmpty(o.PropertyName))
                .ToDictionary(o => o.PropertyName);

            // 当前 SimLogicBase 子类中所有标记 SimOptionAttribute 的属性
            var simOptionProps = GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.GetCustomAttribute<SimOptionAttribute>() != null);

            foreach (var prop in simOptionProps)
            {
                if (!dict.TryGetValue(prop.Name, out var option))
                    throw new InvalidOperationException($"配置选项 {GetType().Name} 的标记属性 {prop.Name} 在界面中未找到对应选项。");

                option.ApplyTo(this);

                var optionValue = prop.GetValue(this);
                _host.LogSim($"应用当前模拟选项：{prop.Name} = {optionValue}");
            }
        }

        #region Party / Roles

        protected List<SimActor> Party => EntityManager.Party;
        protected virtual SimActor MT => EntityManager.GetPartyMember(1);
        protected virtual SimActor ST => EntityManager.GetPartyMember(2);
        protected virtual SimActor H1 => EntityManager.GetPartyMember(3);
        protected virtual SimActor H2 => EntityManager.GetPartyMember(4);
        protected virtual SimActor D1 => EntityManager.GetPartyMember(5);
        protected virtual SimActor D2 => EntityManager.GetPartyMember(6);
        protected virtual SimActor D3 => EntityManager.GetPartyMember(7);
        protected virtual SimActor D4 => EntityManager.GetPartyMember(8);

        protected enum JobRole
        {
            None, Tank, Healer, Melee, PhysicalRanged, MagicalRanged
        }

        protected Dictionary<JobRole, List<Job>> JobsByRole = new Dictionary<JobRole, List<Job>>()
        {
            [JobRole.None] = new List<Job>(),
            [JobRole.Tank] = new List<Job> { Job.PLD, Job.WAR, Job.DRK, Job.GNB },
            [JobRole.Healer] = new List<Job> { Job.WHM, Job.AST, Job.SGE, Job.SCH },
            [JobRole.Melee] = new List<Job> { Job.MNK, Job.DRG, Job.NIN, Job.SAM, Job.RPR, Job.VPR },
            [JobRole.PhysicalRanged] = new List<Job> { Job.BRD, Job.MCH, Job.DNC },
            [JobRole.MagicalRanged] = new List<Job> { Job.BLM, Job.SMN, Job.RDM, Job.PCT },
        };

        protected struct AutoRole
        {
            internal JobRole Role;
            internal Job Job;
            /// <summary>
            /// 自动分配职能时，从对应职能的职业列表中选择职业的顺序。
            /// </summary>
            internal bool IsAscending;
            internal bool IsFixedJob;

            public static AutoRole Asc(JobRole role)
                => new AutoRole { Role = role, IsAscending = true, IsFixedJob = false };

            public static AutoRole Desc(JobRole role)
                => new AutoRole { Role = role, IsAscending = false, IsFixedJob = false };

            public static AutoRole Fixed(Job job)
                => new AutoRole { Role = JobRole.None, Job = job, IsFixedJob = true };
        }

        /// <summary>
        /// 默认为 MT ST H1 H2 D1 D2 D3 D4 的常规小队配置，子类可重写以实现不同的队伍构成和职业分配顺序。
        /// </summary>
        protected virtual List<AutoRole> PartyRoles() => new List<AutoRole>()
        {
            AutoRole.Asc(JobRole.Tank),
            AutoRole.Desc(JobRole.Tank),
            AutoRole.Asc(JobRole.Healer),
            AutoRole.Desc(JobRole.Healer),
            AutoRole.Asc(JobRole.Melee),
            AutoRole.Desc(JobRole.Melee),
            AutoRole.Asc(JobRole.PhysicalRanged),
            AutoRole.Asc(JobRole.MagicalRanged),
        };

        private bool _preparingParty;

        /// <summary>按职能顺序准备网络玩家，全部就绪后在调度线程继续；自己只借用，不重复生成。</summary>
        protected void GeneratePartyMembers(int myPartyIdx, Action onReady)
        {
            SimTimeline.ValidateAction(onReady, nameof(onReady));
            if (_preparingParty || Party.Count != 0)
                throw new InvalidOperationException("本次模拟已准备小队，不能重复追加队员；请停止并完成清理后重新开始。");
            var partyRoles = PartyRoles();

            var isOutOfRange = myPartyIdx < 1 || myPartyIdx > partyRoles.Count;
            if (isOutOfRange)
                throw new ArgumentOutOfRangeException(nameof(myPartyIdx), $"小队职能序号必须在小队范围内（1-{partyRoles.Count}）。");

            var myself = _host.EntityProvider.GetMyself();
            if (myself == null || !myself.IsAlive)
                throw new InvalidOperationException("未找到有效的自身玩家，无法准备模拟小队。");
            var myJob = myself.Job;
            var usedJobs = new HashSet<Job> { myJob };

            List<Job?> jobs = new List<Job?>();

            for (int partyIdx = 1; partyIdx <= partyRoles.Count; partyIdx++)
            {
                if (partyIdx == myPartyIdx)
                {
                    jobs.Add(null);
                    continue;
                }

                var autoRole = partyRoles[partyIdx - 1];

                if (autoRole.IsFixedJob)
                {
                    jobs.Add(autoRole.Job);
                    usedJobs.Add(autoRole.Job);
                    continue;
                }

                var role = autoRole.Role;
                var jobList = JobsByRole[role].Cast<Job?>().ToList();

                Job? job = null;

                if (autoRole.IsAscending)
                    job = jobList.FirstOrDefault(j => j.HasValue && !usedJobs.Contains(j.Value))
                        ?? jobList.FirstOrDefault();
                else
                    job = jobList.LastOrDefault(j => j.HasValue && !usedJobs.Contains(j.Value))
                        ?? jobList.LastOrDefault();

                _ = job ?? throw new InvalidOperationException($"无法为队员 # {partyIdx} 的职能 {role} 分配合适的职业。");

                jobs.Add(job);
                usedJobs.Add(job.Value);
            }

            var position = myself.Pos3D;
            var heading = myself.Heading;
            var members = new SimActor[jobs.Count];
            members[myPartyIdx - 1] = EntityManager.Attach(myself);
            EntityManager.SetTemporaryName(members[myPartyIdx - 1], SimPartyNames.Get(myPartyIdx, myJob));
            var remaining = jobs.Count - 1;
            _preparingParty = true;

            void CompleteParty()
            {
                Party.AddRange(members);
                if (members.Length > 1) EntityManager.ShowParty(_host.PartyService);
                _preparingParty = false;
                onReady();
            }

            if (remaining == 0)
            {
                CompleteParty();
                return;
            }

            for (var i = 0; i < jobs.Count; i++)
            {
                if (!jobs[i].HasValue) continue;
                var slot = i;
                EntityManager.SpawnNetworkPlayer(new NetworkPlayerSpawnSpec
                {
                    Job = jobs[i].Value,
                    Name = SimPartyNames.Get(i + 1, jobs[i].Value),
                    AppearanceSource = myself,
                    Pos = position,
                    Heading = heading,
                }, player =>
                {
                    members[slot] = player;
                    if (--remaining == 0) CompleteParty();
                });
            }
        }

        #endregion Party / Roles

        protected void Dummy(Vector3 pos, float heading, Action<SimActor> onReady)
        {
            // 不可见模型仍承载技能特效；就绪时只关闭选中能力，不关闭游戏绘制。
            EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 9020,
                Level = 100,
                Pos = pos,
                Heading = heading,
                Targetable = false,
            }, onReady);
        }

        protected void DummyCast(Vector3 pos, float heading, uint abilityId, float despawnSeconds,
            float? omenDelay = 0, Action onCast = null)
        {
            ValidateLifetime(despawnSeconds);
            if (onCast != null) SimTimeline.ValidateAction(onCast, nameof(onCast));
            Dummy(pos, heading, dummy =>
            {
                dummy.Cast(abilityId, omenDelay);
                Scheduler.ScheduleAfter(despawnSeconds, () => EntityManager.Delete(dummy), "回收读条假人");
                onCast?.Invoke();
            });
        }

        protected void DummyExecute(Vector3 pos, float heading, uint abilityId, float despawnSeconds, Action onExecuted = null)
        {
            ValidateLifetime(despawnSeconds);
            if (onExecuted != null) SimTimeline.ValidateAction(onExecuted, nameof(onExecuted));
            Dummy(pos, heading, dummy =>
            {
                dummy.Execute(abilityId);
                Scheduler.ScheduleAfter(despawnSeconds, () => EntityManager.Delete(dummy), "回收技能假人");
                onExecuted?.Invoke();
            });
        }

        private static void ValidateLifetime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
        }

    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SimOptionAttribute : Attribute
    {
    }
}

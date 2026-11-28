using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using GeneralStatusId = Simulant.Simulation.Runtime.PlayerActions.GeneralActions.StatusId;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    /// <summary>参与一次模拟的角色。释放时停止其行为，原生实体的回收由资源所有者负责。</summary>
    public sealed partial class SimActor : IDisposable
    {
        private readonly SimCastController _casts;
        private readonly SimMovementController _movement;
        private int _detached;
        internal SimEntityManager Manager { get; set; }
        internal EntityIdentity? PetOwner { get; set; }
        internal SimActorBehaviorController BehaviorController { get; set; }
        internal SimActorBehaviorController BossBehaviorController { get; set; }
        internal bool MovementLocked { get; set; }

        internal SimActor(Character entity, SimCastController casts, SimMovementController movement)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _casts = casts ?? throw new ArgumentNullException(nameof(casts));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        public Character Entity { get; }
        public bool IsActive => Volatile.Read(ref _detached) == 0 && Entity.IsAlive;
        /// <summary>是否有本会话管理的本地或网络移动；不反映直接调用 Entity.ActorMove 后的状态。</summary>
        public bool IsMoving => _movement.IsMoving(this);
        public bool IsKnockbackImmune => IsActive && Entity.IsKnockbackImmune;
        public bool IsInvulnerable => IsActive && Entity.IsInvulnerable;
        public uint Id => Entity.Id;
        public IntPtr Address => Entity.Address;

        /// <summary>扣除已经判定的伤害，返回实际损失的生命值。</summary>
        public uint TakeDamage(uint damage)
        {
            EnsureActive();
            var hp = Entity.HP;
            var lost = Math.Min(damage, hp);
            if (lost == 0)
                return 0;

            SetHp(hp - lost);
            if (lost == hp)
            {
                StopCasting();
                StopMoving();
                ActorControl(ActorControlCategory.DeathAnimation);
            }

            return lost;
        }

        /// <summary>为已接受复活的模拟角色恢复血蓝并播放原生复活动画。</summary>
        public bool Revive(uint hp, ushort? mp = null)
        {
            EnsureActive();
            if (Entity.HP != 0 || hp == 0)
                return false;

            Entity.SetNetworkVitals(hp, mp);
            ActorControl(ActorControlCategory.RaiseAnimation);
            Manager?.RemoveStatus(this, (ushort)GeneralStatusId.复活);

            var playerActions = PluginHost.Instance?.PlayerActions.Entities;
            if (!ReferenceEquals(playerActions, Manager))
                playerActions?.RemoveStatus(this, (ushort)GeneralStatusId.复活);
            return true;
        }

        internal void SetHp(uint hp)
        {
            var previous = Entity.HP;
            Entity.SetNetworkVitals(hp);
            if (hp >= previous)
                return;

            Manager?.BreakShields(Entity);
            var playerActions = PluginHost.Instance?.PlayerActions.Entities;
            if (!ReferenceEquals(playerActions, Manager))
                playerActions?.BreakShields(Entity);
        }

        /// <summary>网络 Boss 对当前一仇的行为，默认为关闭。转向和追击均通过 ActorMove 同步。</summary>
        public EnmityFollowMode FollowTopEnmity
        {
            get => BossBehaviorController?.GetEnmityFollowMode(this) ?? EnmityFollowMode.Disabled;
            set
            {
                EnsureActive();
                if (BossBehaviorController == null)
                    throw new InvalidOperationException("只有本会话生成的网络战斗 NPC 可以跟随一仇。");

                BossBehaviorController.SetEnmityFollowMode(this, value);
            }
        }

        private PositionHistory _judgementHistory;
        private SimScheduledAction _judgementSample;
        private SimScheduler _judgementClock;
        private double _judgementAdvance;

        /// <summary>判定专用坐标；仅自身使用历史位置，移动和演出仍读取 Pos。</summary>
        public Vector2 JudgementPosition => _judgementHistory?.At(_judgementClock.ElapsedSeconds - _judgementAdvance) ?? Pos;
        public Vector3 JudgementPosition3D => _judgementHistory?.PositionAt(_judgementClock.ElapsedSeconds - _judgementAdvance) ?? Pos3D;
        public bool JudgementInvulnerable => _judgementHistory?.InvulnerabilityAt(_judgementClock.ElapsedSeconds - _judgementAdvance) ?? IsInvulnerable;
        public float JudgementHeading => _judgementHistory?.HeadingAt(_judgementClock.ElapsedSeconds - _judgementAdvance) ?? Heading;

        internal void StartJudgementSampling(SimScheduler scheduler, double advance)
        {
            _judgementClock = scheduler;
            _judgementAdvance = advance;
            _judgementHistory = new PositionHistory();
            SampleJudgementPosition();
        }

        private void SampleJudgementPosition()
        {
            if (!IsActive)
                return;

            // 状态表一次读取，不在高频采样中调用原生 HasStatus 函数。
            var position = Entity.Pos3D;
            _judgementHistory.Add(_judgementClock.ElapsedSeconds, position, Entity.ReadInvulnerability(), Entity.Heading);
            _judgementSample = _judgementClock.ScheduleAfter(0.02, SampleJudgementPosition, "记录自身判定位置");
        }

        public Vector2 Pos
        {
            get
            {
                EnsureActive();
                return Entity.Pos;
            }

            set
            {
                EnsureActive();
                Entity.Pos = value;
            }
        }

        public Vector3 Pos3D
        {
            get
            {
                EnsureActive();
                return Entity.Pos3D;
            }

            set
            {
                EnsureActive();
                Entity.Pos3D = value;
            }
        }

        /// <summary>直接读取或初始化朝向；网络角色在机制中转向应使用 FaceNetwork，由客户端更新模型。</summary>
        public float Heading
        {
            get
            {
                EnsureActive();
                return Entity.Heading;
            }

            set
            {
                EnsureActive();
                Entity.Heading = value;
            }
        }

        public Job Job
        {
            get
            {
                EnsureActive();
                return Entity.Job;
            }

            set
            {
                EnsureActive();
                Entity.Job = value;
            }
        }

        /// <summary>读条并播放技能；指定目标时沿用原生带目标演出，不产生实际伤害。</summary>
        /// <param name="omenPosition">可选的预兆落点（ACT 坐标），用于同帧移动包尚未更新实体的情况。</param>
        /// <param name="omenHeading">可选的预兆朝向；未指定时在显示预兆时读取实体朝向。</param>
        /// <param name="castTime">可选的实际咏唱秒数，未指定时使用技能表时长减 0.3 秒。</param>
        /// <param name="effectDelay">读条结束至结算演出的秒数；默认 0.3 秒，瞬发默认 0。</param>
        /// <param name="executeOnFinish">是否自动播放结算；由机制判定节点显式执行时设为 false。</param>
        /// <param name="packetTargetCapacity">自动结算时用于生成日志的实战结算包目标容量。</param>
        public void Cast(uint actionId, float? omenDelay = 0, SimActor target = null,
            Vector3? omenPosition = null, float? omenHeading = null, float? castTime = null, float? effectDelay = null,
            bool executeOnFinish = true, int? packetTargetCapacity = null)
        {
            BehaviorController?.ManualAction(this);
            if (BossBehaviorController != null)
                _movement.StopEnmityPursuit(this);

            _casts.Cast(this, actionId, omenDelay, target, omenPosition, omenHeading, castTime, effectDelay, executeOnFinish, packetTargetCapacity);
        }

        public void Execute(uint actionId, IEnumerable<SimActor> hitTargets = null, int? packetTargetCapacity = null)
        {
            BehaviorController?.ManualAction(this);
            _casts.Execute(this, actionId, hitTargets: hitTargets, packetTargetCapacity: packetTargetCapacity);
        }

        public void ConfigureBehavior(SimActorBehaviorOptions options)
        {
            EnsureActive();

            if (BehaviorController == null)
                throw new InvalidOperationException("该角色不是本会话生成的网络假玩家。");

            BehaviorController.Configure(this, options);
        }

        /// <summary>
        /// 提交目的范围和目标时间（会话的绝对秒数）；实体自行安排出发、途中停顿与提前到达。
        /// 可用 holdUntil 指定机制结束前保持站位，省略时保持至目标时间。
        /// northHeading 是本机制相对北的场地朝向；接收指令时固定下来，不随其他机制改变。
        /// 紧急移动立即直达，不绕路、贪刀或等待读条；Vector2 可直接作为零容差点位传入。
        /// 目标时间仅用于规划和冲刺，赶不上时继续移动；机制是否成功由实际参与情况判定。
        /// gapClose 允许 T 和近战先突进当前 Boss，再前往安全范围；会等待击退等强制移动结束。
        /// </summary>
        public void MoveForMechanic(SimPosition destination, double arriveBy, double? holdUntil = null,
            bool urgent = false, double northHeading = Math.PI, bool gapClose = false)
            => MoveForMechanic(destination, arriveBy, holdUntil, urgent, northHeading, gapClose, null);

        /// <summary>立即出发，依次经过指定的绝对坐标，再前往最终站位。赶时间时仍保留途经点。</summary>
        public void MoveForMechanic(SimPosition destination, IReadOnlyList<Vector2> waypoints, double arriveBy,
            double? holdUntil = null, double northHeading = Math.PI)
            => MoveForMechanic(destination, arriveBy, holdUntil, true, northHeading, false, waypoints);

        private void MoveForMechanic(SimPosition destination, double arriveBy, double? holdUntil, bool urgent,
            double northHeading, bool gapClose, IReadOnlyList<Vector2> waypoints)
        {
            EnsureActive();

            if (BehaviorController == null)
                throw new InvalidOperationException("机制走位只支持本会话生成的网络假玩家。");

            BehaviorController.MoveForMechanic(this, destination, arriveBy, holdUntil ?? arriveBy, urgent, northHeading, gapClose,
                waypoints: waypoints);
        }

        /// <summary>等当前首领可选中后执行职业突进，并保持在实际落点直到指定会话时刻。</summary>
        internal void GapCloseForMechanic(double holdUntil)
        {
            EnsureActive();

            if (BehaviorController == null)
                throw new InvalidOperationException("机制突进只支持本会话生成的网络假玩家。");

            BehaviorController.MoveForMechanic(this, Pos, holdUntil, holdUntil, urgent: true,
                northHeading: Math.PI, gapClose: true, gapCloseOnly: true);
        }

        /// <summary>
        /// 持续追随动态站位范围，until 为会话绝对秒数；范围内的随机偏移只选一次。
        /// 立即移动，来不及时最多使用 1.3 倍速；不因目标临时改变而抛出到位超时，交由机制判定。
        /// 新移动指令、强制移动或停止会取消跟随。
        /// </summary>
        public void FollowForMechanic(Func<SimPosition> destination, double until, double northHeading = Math.PI)
        {
            EnsureActive();

            if (BehaviorController == null)
                throw new InvalidOperationException("机制跟随只支持本会话生成的网络假玩家。");

            BehaviorController.FollowForMechanic(this, destination, until, northHeading);
        }

        /// <summary>取消本会话的读条及其预兆，保留角色、移动及实体上不同技能编号的外部读条。</summary>
        public void StopCasting() => _casts.Cancel(this);

        /// <summary>暂停自动攻击和移动，让原生击退、眩晕等强制动作完整执行。</summary>
        public void SuspendBehavior(double seconds)
        {
            EnsureActive();

            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "行为暂停时间必须为非负有限数。");

            BehaviorController?.Suspend(this, seconds);
            BossBehaviorController?.SuspendBoss(this, seconds);
            _casts.Cancel(this);
            _movement.Stop(this);
        }

        internal void ApplyKnockback(float heading, Vector2? center, float distance, float duration)
        {
            EnsureActive();
            _movement.Knockback(this, heading, distance, duration, center);
        }

        /// <summary>
        /// 在 ACT 的 X/Y 平面内移动，速度单位为世界单位每秒，并保持当前高度。
        /// 可指定模型适用的移动动画；正常到达后在调度线程执行同步回调，停止或换目标时取消旧回调。
        /// 调度线程已经领取的回调可以执行完毕。
        /// </summary>
        public void MoveTo(Vector2 target, float speed = 6, bool faceMovement = true,
            ushort? animationId = null, Action onArrived = null)
        {
            BehaviorController?.ManualMove(this);
            _movement.MoveTo(this, target, speed, faceMovement, animationId, onArrived);
        }

        /// <summary>
        /// 控制网络角色表中的角色向 ACT 的 X/Y 目标移动，沿用当前高度；原生客户端管理移动动画。
        /// 首包在调度线程发送，随后每 100 毫秒按经过时间发送沿途位置并检查实际到达，30 秒未到达则报告超时。
        /// speedParam 是每秒距离乘以 10 后的包内速度参数，默认 30；替换或停止会取消旧的同步到达回调。
        /// </summary>
        public void MoveNetworkTo(Vector2 target, byte speedParam = 30, Action onArrived = null)
        {
            BehaviorController?.ManualMove(this);
            _movement.MoveNetworkTo(this, target, speedParam, onArrived);
        }

        /// <summary>停止本会话的移动，再通过 ActorMove 在当前位置转向，同时更新模型与目标圈。</summary>
        public void FaceNetwork(float heading)
        {
            EnsureActive();

            if (float.IsNaN(heading) || float.IsInfinity(heading))
                throw new ArgumentOutOfRangeException(nameof(heading), "朝向必须为有限数。");

            var slot = Entity.Identity.Slot;

            if (slot < 2 || slot >= 200 || (slot & 1) != 0)
                throw new InvalidOperationException("网络转向只支持网络角色，不能用于自身玩家或本地备用实体。");

            StopMoving();
            ActorMove(Entity.Pos3D, heading);
        }

        /// <summary>停止本会话的本地或网络移动；不会接管直接调用 Entity.ActorMove 产生的移动。</summary>
        public void StopMoving()
        {
            BehaviorController?.ManualMove(this);
            _movement.Stop(this);
        }

        public void SetReadyToDraw()
        {
            if (IsActive)
                Entity.SetReadyToDraw();
        }

        public void EnableDraw()
        {
            if (IsActive)
                Entity.EnableDraw();
        }

        public void Redraw()
        {
            if (IsActive)
                Entity.Redraw();
        }

        /// <summary>解除关联并停止本会话的读条、本地及网络移动；保留实体及直接 ActorMove 产生的移动。</summary>
        internal void Detach()
        {
            _judgementSample?.Cancel();
            BehaviorController?.Remove(this);
            BossBehaviorController?.RemoveBoss(this);

            if (Interlocked.Exchange(ref _detached, 1) != 0)
            {
                // 首次解除关联时原生停止可能失败，仍允许重试本次移动的清理。
                _movement.Stop(this);
                return;
            }

            // 外部预兆回调失败时，也必须停止移动。
            try
            {
                _casts.Cancel(this);
            }
            finally
            {
                _movement.Stop(this);
            }
        }

        /// <summary>解除关联并停止本会话的行为；不回收实体，也不接管直接调用 Entity.ActorMove 产生的移动。</summary>
        public void Dispose() => Detach();

        private void EnsureActive()
        {
            if (!IsActive)
                throw new InvalidOperationException("该角色已不再参与本次模拟。");
        }
    }
}

using Simulant.Core.Entity;
using System;
using System.Numerics;
using System.Threading;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    /// <summary>参与一次模拟的角色。释放时停止其行为，原生实体的回收由资源所有者负责。</summary>
    public sealed class SimActor : IDisposable
    {
        private readonly SimCastController _casts;
        private readonly SimMovementController _movement;
        private int _detached;

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
        public uint Id => Entity.Id;
        public IntPtr Address => Entity.Address;

        public Vector2 Pos
        {
            get { EnsureActive(); return Entity.Pos; }
            set { EnsureActive(); Entity.Pos = value; }
        }

        public Vector3 Pos3D
        {
            get { EnsureActive(); return Entity.Pos3D; }
            set { EnsureActive(); Entity.Pos3D = value; }
        }

        /// <summary>直接读取或初始化朝向；网络角色在机制中转向应使用 FaceNetwork，由客户端更新模型。</summary>
        public float Heading
        {
            get { EnsureActive(); return Entity.Heading; }
            set { EnsureActive(); Entity.Heading = value; }
        }

        public Job Job
        {
            get { EnsureActive(); return Entity.Job; }
            set { EnsureActive(); Entity.Job = value; }
        }

        public void Cast(uint actionId, float? omenDelay = 0) => _casts.Cast(this, actionId, omenDelay);
        public void Execute(uint actionId) => _casts.Execute(this, actionId);

        /// <summary>取消本会话的读条及其预兆，保留角色、移动及实体上不同技能编号的外部读条。</summary>
        public void StopCasting() => _casts.Cancel(this);

        /// <summary>
        /// 在 ACT 的 X/Y 平面内移动，速度单位为世界单位每秒，并保持当前高度。
        /// 可指定模型适用的移动动画；正常到达后在调度线程执行同步回调，停止或换目标时取消旧回调。
        /// 调度线程已经领取的回调可以执行完毕。
        /// </summary>
        public void MoveTo(Vector2 target, float speed = 6, bool faceMovement = true,
            ushort? animationId = null, Action onArrived = null)
            => _movement.MoveTo(this, target, speed, faceMovement, animationId, onArrived);

        /// <summary>
        /// 控制网络角色表中的角色向 ACT 的 X/Y 目标移动，沿用当前高度；原生客户端管理移动动画。
        /// 首包在调度线程发送，随后每 100 毫秒按经过时间发送沿途位置并检查实际到达，30 秒未到达则报告超时。
        /// speedParam 是每秒距离乘以 10 后的包内速度参数，默认 30；替换或停止会取消旧的同步到达回调。
        /// </summary>
        public void MoveNetworkTo(Vector2 target, byte speedParam = 30, Action onArrived = null)
            => _movement.MoveNetworkTo(this, target, speedParam, onArrived);

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
            Entity.ActorMove(Entity.Pos3D, heading);
        }

        /// <summary>停止本会话的本地或网络移动；不会接管直接调用 Entity.ActorMove 产生的移动。</summary>
        public void StopMoving() => _movement.Stop(this);
        public void SetReadyToDraw() { if (IsActive) Entity.SetReadyToDraw(); }
        public void EnableDraw() { if (IsActive) Entity.EnableDraw(); }
        public void Redraw() { if (IsActive) Entity.Redraw(); }

        /// <summary>解除关联并停止本会话的读条、本地及网络移动；保留实体及直接 ActorMove 产生的移动。</summary>
        internal void Detach()
        {
            if (Interlocked.Exchange(ref _detached, 1) != 0)
            {
                // 首次解除关联时原生停止可能失败，仍允许重试本次移动的清理。
                _movement.Stop(this);
                return;
            }
            // 外部预兆回调失败时，也必须停止移动。
            try { _casts.Cancel(this); }
            finally { _movement.Stop(this); }
        }

        /// <summary>解除关联并停止本会话的行为；不回收实体，也不接管直接调用 Entity.ActorMove 产生的移动。</summary>
        public void Dispose() => Detach();

        private void EnsureActive()
        {
            if (!IsActive) throw new InvalidOperationException("该角色已不再参与本次模拟。");
        }
    }
}

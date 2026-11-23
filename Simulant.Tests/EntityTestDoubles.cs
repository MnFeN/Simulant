using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

// 仅替换原生适配器、游戏数据行和特效和标记边界；测试
// 链接实际的注册表、模拟角色、读条、预兆和移动控制器，以及调度器。
namespace Simulant.Core.Entity
{
    public abstract class EntityBase
    {
        private readonly EntityRegistry _registry;
        public EntityIdentity Identity { get; }
        public bool IsAlive => _registry.IsAlive(this);
        public uint Id { get; }
        public IntPtr Address { get; }
        public Simulant.Game.FFCS.Client.Game.Object.ObjectKind ObjectKind { get; }

        internal EntityBase(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
        {
            _registry = registry;
            Identity = identity;
            Id = observation.EntityId;
            Address = observation.Address;
            ObjectKind = observation.ObjectKind;
        }

        public void SetReadyToDraw()
        {
        }

        public void EnableDraw()
        {
        }

        public void Redraw()
        {
        }

        internal (Simulant.Game.ActorControlCategory Category, uint A1, uint A2, uint A3, uint A4) LastActorControl { get; private set; }

        public IntPtr ActorControl(Simulant.Game.ActorControlCategory category, uint a1 = 0, uint a2 = 0, uint a3 = 0, uint a4 = 0)
        {
            LastActorControl = (category, a1, a2, a3, a4);
            return IntPtr.Zero;
        }
    }

    public sealed partial class Character : EntityBase
    {
        private readonly StatusTestCharacter _native = new StatusTestCharacter();
        internal StatusTestCharacter Native
        {
            get
            {
                if (!IsAlive)
                    throw new InvalidOperationException("状态控制器访问了已失效的实体。");

                return _native;
            }
        }

        private Vector2 _position;
        private float _height;
        private float _heading;
        internal readonly List<Vector2> PositionWrites = new List<Vector2>();
        internal readonly List<int> WriteThreads = new List<int>();
        internal Action<Vector2> OnPositionWritten;
        internal int PositionReads;
        internal readonly List<NetworkMove> NetworkMoves = new List<NetworkMove>();
        internal int NativeKnockbackCalls;
        internal float NativeKnockbackHeading, NativeKnockbackDistance, NativeKnockbackDuration;
        internal Action<Vector3, float, byte> OnActorMove;
        internal readonly List<uint> CastStarts = new List<uint>();
        internal readonly List<uint> FinalizedCasts = new List<uint>();
        internal readonly List<ushort> PlayedTimelines = new List<ushort>();
        internal readonly List<ushort> PlayedBaseTimelines = new List<ushort>();
        internal ushort BaseTimelineId { get; private set; }

        private long _baseTimelineVersion;
        internal int BaseTimelineResetCount;

        internal Character(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation)
        {
        }

        public Vector2 Pos
        {
            get
            {
                PositionReads++;
                return _position;
            }

            set
            {
                if (!IsAlive)
                    throw new InvalidOperationException("移动控制器写入了已失效的实体。");

                _position = value;
                PositionWrites.Add(value);
                WriteThreads.Add(Thread.CurrentThread.ManagedThreadId);
                OnPositionWritten?.Invoke(value);
            }
        }

        public float Heading
        {
            get => _heading;
            set
            {
                if (!IsAlive)
                    throw new InvalidOperationException("移动控制器旋转了已失效的实体。");

                _heading = value;
            }
        }

        public Vector3 Pos3D
        {
            get
            {
                PositionReads++;
                return new Vector3(_position.X, _position.Y, _height);
            }

            set
            {
                Pos = new Vector2(value.X, value.Y);
                _height = value.Z;
            }
        }

        public Simulant.Game.FFCS.Client.Game.Character.Job Job { get; set; }
        public bool HasStatus(uint statusId)
        {
            for (var slot = 0; slot < 60; slot++)
                if (Native.StatusManager.GetStatus(slot).StatusId.Get() == statusId)
                    return true;
            return false;
        }

        public uint HP { get; set; } = 10000;
        public uint MaxHP { get; set; } = 10000;
        public uint MP { get; set; } = 10000;
        public uint MaxMp { get; set; } = 10000;
        public void SetNetworkVitals(uint hp, ushort? mp = null)
        {
            HP = hp;
            if (mp.HasValue)
                MP = mp.Value;
        }

        public void ActorSetPos(Vector3 position, float heading)
        {
            Pos3D = position;
            Heading = heading;
        }

        public bool IsTargetable { get; set; } = true;
        public bool IsWeaponDrawn { get; set; }

        public void SetNetworkTarget(Character target)
        {
        }

        internal void SetCastTarget(Character target)
        {
        }

        internal List<uint> ActionEffects { get; } = new List<uint>();

        public void ExecuteActionEffect(uint actionId, Character target)
        {
            ActionEffects.Add(actionId);
        }

        public void ExecuteActionEffect(uint actionId, Character target, float? heading, float animationLock = 0)
            => ExecuteActionEffect(actionId, target);

        public void ExecuteActionEffectAt(uint actionId, Vector3 position, float heading, float animationLock = 0)
        {
            ActionEffects.Add(actionId);
        }

        internal byte[] NameBytes = new byte[64];
        internal Action BeforeNameWrite, AfterNameWrite;
        internal byte[] ReadNameBytes()
        {
            if (!IsAlive)
                throw new InvalidOperationException("名字读取访问了已失效的实体。");

            return (byte[])NameBytes.Clone();
        }

        internal void SetNameBytes(byte[] bytes)
        {
            if (!IsAlive)
                throw new InvalidOperationException("改名访问了已失效的实体。");

            BeforeNameWrite?.Invoke();
            NameBytes = (byte[])bytes.Clone();
            AfterNameWrite?.Invoke();
        }

        internal sealed class NetworkMove
        {
            internal Vector3 Position;
            internal float Heading;
            internal byte SpeedParam;
            internal Simulant.Game.FFCS.Client.Game.Network.ActorMoveFlags Flags;
            internal int Thread;
        }

        public void ActorMove(Vector3 position, float heading, byte speedParam = 20,
            Simulant.Game.FFCS.Client.Game.Network.ActorMoveFlags flags = Simulant.Game.FFCS.Client.Game.Network.ActorMoveFlags.None)
        {
            if (!IsAlive)
                throw new InvalidOperationException("移动控制器向已失效的实体发送了移动包。");

            NetworkMoves.Add(new NetworkMove
            {
                Position = position,
                Heading = heading,
                SpeedParam = speedParam,
                Flags = flags,
                Thread = System.Threading.Thread.CurrentThread.ManagedThreadId
            });
            OnActorMove?.Invoke(position, heading, speedParam);
        }

        public void Knockback(float heading, float distance, float duration)
        {
            NativeKnockbackCalls++;
            NativeKnockbackHeading = heading;
            NativeKnockbackDistance = distance;
            NativeKnockbackDuration = duration;
        }

        public float HitboxRadius { get; set; }

        private uint _castingActionId;
        internal Action BeforeCastingActionIdRead;
        public uint CastingActionId
        {
            get
            {
                BeforeCastingActionIdRead?.Invoke();
                return _castingActionId;
            }

            set
            {
                _castingActionId = value;
            }
        }

        internal float CastDuration;
        internal float DisplayCastDuration;

        public void SetCastInfo(uint actionId, float castTime, byte actionType = 1, float extraCastTime = 0)
        {
            if (!IsAlive)
                throw new InvalidOperationException("读条控制器写入了已失效的实体。");

            CastingActionId = actionId;
            CastStarts.Add(actionId);
            CastDuration = castTime;
            DisplayCastDuration = castTime + extraCastTime;
        }

        public bool FinalizeCast(uint expectedActionId)
        {
            if (!IsAlive)
                throw new InvalidOperationException("读条控制器结束了已失效实体的读条。");

            if (CastingActionId == 0 || CastingActionId != expectedActionId)
                return false;

            FinalizedCasts.Add(CastingActionId);
            CastingActionId = 0;
            return true;
        }

        public void PlayBaseTimeline(ushort timelineId, bool interrupt = true)
        {
            if (!IsAlive)
                throw new InvalidOperationException("移动控制器播放了已失效实体的动画。");

            if (timelineId == 0)
                return;

            BaseTimelineId = timelineId;
            _baseTimelineVersion++;
            PlayedBaseTimelines.Add(timelineId);
        }

        public void ResetBaseTimeline()
        {
            if (!IsAlive)
                throw new InvalidOperationException("移动控制器重置了已失效实体的动画。");

            BaseTimelineId = 0;
            _baseTimelineVersion++;
            BaseTimelineResetCount++;
        }

        internal long PlayOwnedBaseTimeline(ushort timelineId)
        {
            PlayBaseTimeline(timelineId);
            return _baseTimelineVersion;
        }

        internal bool ResetOwnedBaseTimeline(ushort timelineId, long version)
        {
            if (!IsAlive)
                throw new InvalidOperationException("移动控制器清理了已失效实体的动画。");

            if (BaseTimelineId != timelineId || _baseTimelineVersion != version)
                return false;

            ResetBaseTimeline();
            return true;
        }

        public void PlayBlendTimeline(ushort timelineId)
        {
            PlayedTimelines.Add(timelineId);
        }
    }

    public sealed class EventObject : EntityBase
    {
        internal EventObject(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation)
        {
        }
    }

    internal sealed class StatusTestCharacter
    {
        internal readonly Simulant.Game.FFCS.Client.Game.StatusManager StatusManager = new Simulant.Game.FFCS.Client.Game.StatusManager();
    }
}

namespace Simulant.Game.FFCS.Client.Game
{
    // 只替换原生状态读写，状态所有权、重试和实体生命周期使用正式实现。
    public sealed class StatusTestField<T>
    {
        internal T Value;
        public T Get() => Value;
    }

    public sealed class Status
    {
        public readonly StatusTestField<ushort> StatusId = new StatusTestField<ushort>();
        public readonly StatusTestField<ushort> Param = new StatusTestField<ushort>();
        public readonly StatusTestField<float> RemainingTime = new StatusTestField<float>();
        public readonly StatusTestField<Object.GameObjectId> SourceObject = new StatusTestField<Object.GameObjectId>();
    }

    public sealed class Conditions
    {
        public static IntPtr InstancePtr = new IntPtr(1), SetFuncPtr = new IntPtr(2);
        private static readonly Conditions Current = new Conditions();
        public readonly StatusTestField<bool> SufferingStatusAffliction = new StatusTestField<bool>();
        public static Conditions Instance() => Current;
        public void SetMovementLocked(bool locked) => SufferingStatusAffliction.Value = locked;
    }

    public sealed class StatusManager
    {
        public static IntPtr SetStatusFuncPtr = new IntPtr(1), RemoveStatusFuncPtr = new IntPtr(2);
        private readonly Status[] _values = new Status[60];
        internal int SetCalls, RemoveCalls;
        internal Action<int> AfterSet, BeforeRemove, AfterRemove;
        public StatusManager()
        {
            for (var i = 0; i < _values.Length; i++)
                _values[i] = new Status();
        }

        public Status GetStatus(int index) => _values[index];
        internal const int StatusSize = 0x10;
        internal byte[] ReadStatusTable()
        {
            var table = new byte[_values.Length * StatusSize];
            for (var i = 0; i < _values.Length; i++)
                BitConverter.GetBytes(_values[i].StatusId.Get()).CopyTo(table, i * StatusSize);

            return table;
        }

        internal static ushort ReadStatusId(byte[] table, int offset) => BitConverter.ToUInt16(table, offset);
        public bool SetStatus(int index, ushort id, float remaining, ushort param, Object.GameObjectId source, bool refresh)
        {
            SetCalls++;
            var status = _values[index];
            var identityChanged = status.StatusId.Get() != id || status.SourceObject.Get().Id != source.Id;
            status.StatusId.Value = id;
            status.Param.Value = param;
            status.RemainingTime.Value = remaining;
            status.SourceObject.Value = source;
            AfterSet?.Invoke(index);
            return identityChanged;
        }

        public void RemoveStatus(int index)
        {
            RemoveCalls++;
            BeforeRemove?.Invoke(index);
            _values[index].StatusId.Value = 0;
            AfterRemove?.Invoke(index);
        }
    }
}

namespace Simulant.Core.Party
{
    // 会话测试只替换原生小队展示与恢复，恢复失败后保留同一所有者。
    internal sealed class SimPartyService
    {
        private object _owner;
        internal Action BeforeRestore;
        internal int ShowCount;
        internal int RestoreAttempts;
        internal int EnmitySetCount;
        internal int EnmityRefreshCount;
        internal Action OnRefreshEnmity;
        internal static IntPtr SelectedTarget, SoftSelectedTarget;
        internal uint EnmityTarget;
        internal IReadOnlyList<int> EnmityValues;
        internal readonly Dictionary<uint, IReadOnlyList<int>> EnmityByTarget = new Dictionary<uint, IReadOnlyList<int>>();
        internal IReadOnlyList<Simulant.Simulation.Runtime.SimActor> Members;

        internal bool IsOwnedBy(object owner) => ReferenceEquals(_owner, owner) && _owner != null;
        internal void Show(object owner, IReadOnlyList<Simulant.Simulation.Runtime.SimActor> members)
        {
            if (_owner != null && !ReferenceEquals(_owner, owner))
                throw new InvalidOperationException("模拟小队已由另一会话持有。");

            _owner = owner;
            Members = members;
            ShowCount++;
        }

        internal void Restore(object owner)
        {
            if (!IsOwnedBy(owner))
                return;

            RestoreAttempts++;
            BeforeRestore?.Invoke();
            _owner = null;
        }

        internal void RemoveMember(object owner, uint id)
        {
            if (IsOwnedBy(owner))
                Restore(owner);
        }

        internal void SetEnmity(object owner, uint targetId,
            IReadOnlyList<Simulant.Simulation.Runtime.SimActor> members, IReadOnlyList<int> values)
        {
            if (!IsOwnedBy(owner))
                throw new InvalidOperationException("测试小队尚未被本会话接管。");

            EnmityTarget = targetId;
            EnmityValues = values;
            EnmityByTarget[targetId] = values.ToArray();
            EnmitySetCount++;
        }

        internal static IntPtr GetSelectedTarget(bool preferSoftTarget = false)
        {
            if (preferSoftTarget && SoftSelectedTarget != IntPtr.Zero)
                return SoftSelectedTarget;

            return SelectedTarget != IntPtr.Zero ? SelectedTarget : SoftSelectedTarget;
        }

        internal void RefreshEnmity(object owner)
        {
            if (!IsOwnedBy(owner))
                throw new InvalidOperationException("小队恢复后仍调用了仇恨刷新。");

            EnmityRefreshCount++;
            OnRefreshEnmity?.Invoke();
        }
    }
}

namespace Simulant.Core
{
    internal static class VfxManager
    {
        internal sealed class Call
        {
            public string Tag;
            public bool IsRemoval;
            public Vector3 Position;
            public float Heading;
            public Vector3 Scale;
            public double Duration;
        }

        internal static readonly List<Call> Commands = new List<Call>();
        internal static Action<Call> OnCommand;

        public static void ShowOmen(string name, Vector3 position, float heading, Vector3 scale, double duration, object owner)
            => Record(new Call { Tag = (string)owner, Position = position, Heading = heading, Scale = scale, Duration = duration });

        public static void Remove(object owner) => Record(new Call { Tag = (string)owner, IsRemoval = true });

        private static void Record(Call command)
        {
            Commands.Add(command);
            OnCommand?.Invoke(command);
        }

        internal static void Reset()
        {
            Commands.Clear();
            OnCommand = null;
        }
    }
}

namespace Simulant.Core
{
    internal static class GameChat
    {
        internal static readonly List<string> ChatCommands = new List<string>();
        internal static Action<string> OnChatCommand;

        public static void SendEcho(string text, bool playSound = false)
        {
            if (!playSound)
                throw new InvalidOperationException("会话错误提示应播放提示音。");
            ChatCommands.Add(text);
            OnChatCommand?.Invoke(text);
        }

        public static void Reset()
        {
            ChatCommands.Clear();
            OnChatCommand = null;
        }
    }
}

namespace Simulant.Core.Environment
{
    internal static class GameMarkers
    {
        internal static void Mark(Simulant.Game.FFCS.Client.Game.UI.MarkerType marker, uint id) { }
        internal static readonly List<Vector3?[]> WaymarkCommands = new List<Vector3?[]>();
        internal static Action<Vector3?[]> OnWaymarkCommand;

        public static void PlaceWaymarks(WaymarkLayout layout)
            => Record(Enumerable.Range(0, 8).Select(layout.Position).ToArray());

        public static void ClearWaymarks() => Record(null);

        private static void Record(Vector3?[] markers)
        {
            WaymarkCommands.Add(markers);
            OnWaymarkCommand?.Invoke(markers);
        }

        public static void Reset()
        {
            WaymarkCommands.Clear();
            OnWaymarkCommand = null;
            Simulant.Core.GameChat.Reset();
        }
    }
}

namespace Simulant.Game
{
    internal static class NativeTestExtensions
    {
        internal static bool IsNull(this FFCS.Client.Game.Object.GameObjectManager value)
            => throw new InvalidOperationException("测试必须使用注入的对象表观察数据。");
        internal static bool IsNull(this FFCS.Client.Game.Object.GameObject value)
            => throw new InvalidOperationException("测试不得访问原生游戏内存。");
    }
}

namespace Simulant.Game.FFCS.Client.Game.Object
{
    [global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Explicit, Size = 8)]
    public struct GameObjectId
    {
        [global::System.Runtime.InteropServices.FieldOffset(0)]
        public ulong Id;
        [global::System.Runtime.InteropServices.FieldOffset(0)]
        public uint ObjectId;
        public static implicit operator GameObjectId(ulong id) => new GameObjectId
        {
            Id = id
        };
        public static implicit operator ulong(GameObjectId id) => id.Id;
    }

    public enum ObjectKind : byte
    {
        None = 0,
        Pc = 1,
        BattleNpc = 2,
        EventObj = 7
    }

    public struct GameObject
    {
        public IntPtr Ptr;
        public uint EntityId;
        public ObjectKind ObjectKind;
    }

    public struct GameObjectManager
    {
        public static GameObjectManager Instance()
            => throw new InvalidOperationException("测试必须使用注入的对象表观察数据。");
        public ObjectArrays Objects => default(ObjectArrays);
    }

    public struct ObjectArrays
    {
        public GameObjectPtrs IndexSorted => default(GameObjectPtrs);
    }

    public struct GameObjectPtrs
    {
        public const int Count = 819;
        public GameObject this[int slot] => throw new InvalidOperationException("测试不得访问原生游戏内存。");
    }
}

namespace Simulant.Game.FFCS.Client.Network
{
    internal static class PacketDispatcher
    {
        internal static IntPtr HandleActorControl(uint actor, Simulant.Game.ActorControlCategory category,
            uint a1, uint a2, uint a3, uint a4, uint a5, uint a6, uint a7, uint a8, uint target, uint unknown)
            => IntPtr.Zero;
    }
}

namespace Simulant.Game.FFCS.Client.UI
{
    internal static class UIGlobals
    {
        internal static void PlaySoundEffect(uint effectId)
        {
        }
    }
}

namespace Simulant.ACT
{
    internal static class XivNetworkLog
    {
        internal static void WriteLine(int id, string payload, DateTimeOffset? timestamp)
            => throw new InvalidOperationException("测试不能写入实际 ACT 日志。");
    }
}

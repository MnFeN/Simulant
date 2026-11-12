using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

// 仅替换原生适配器、CSV 数据行和外部 PictoACT 回调；测试
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

        internal EntityBase(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
        {
            _registry = registry;
            Identity = identity;
            Id = observation.EntityId;
            Address = observation.Address;
        }

        public void SetReadyToDraw() { }
        public void EnableDraw() { }
        public void Redraw() { }
    }

    public sealed class Character : EntityBase
    {
        private readonly StatusTestCharacter _native = new StatusTestCharacter();
        internal StatusTestCharacter Native
        {
            get
            {
                if (!IsAlive) throw new InvalidOperationException("状态控制器访问了已失效的实体。");
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
        internal Action<Vector3, float, byte> OnActorMove;
        internal readonly List<uint> CastStarts = new List<uint>();
        internal readonly List<uint> FinalizedCasts = new List<uint>();
        internal readonly List<ushort> PlayedTimelines = new List<ushort>();
        internal readonly List<ushort> PlayedBaseTimelines = new List<ushort>();
        internal ushort BaseTimelineId { get; private set; }
        private long _baseTimelineVersion;
        internal int BaseTimelineResetCount;

        internal Character(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation) { }

        public Vector2 Pos
        {
            get { PositionReads++; return _position; }
            set
            {
                if (!IsAlive) throw new InvalidOperationException("移动控制器写入了已失效的实体。");
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
                if (!IsAlive) throw new InvalidOperationException("移动控制器旋转了已失效的实体。");
                _heading = value;
            }
        }

        public Vector3 Pos3D
        {
            get { PositionReads++; return new Vector3(_position.X, _position.Y, _height); }
            set { Pos = new Vector2(value.X, value.Y); _height = value.Z; }
        }

        public Simulant.Game.FFCS.Client.Game.Character.Job Job { get; set; }
        internal sealed class NetworkMove
        {
            internal Vector3 Position;
            internal float Heading;
            internal byte SpeedParam;
            internal int Thread;
        }

        public void ActorMove(Vector3 position, float heading, byte speedParam = 20)
        {
            if (!IsAlive) throw new InvalidOperationException("移动控制器向已失效的实体发送了移动包。");
            NetworkMoves.Add(new NetworkMove
            {
                Position = position, Heading = heading, SpeedParam = speedParam,
                Thread = System.Threading.Thread.CurrentThread.ManagedThreadId
            });
            OnActorMove?.Invoke(position, heading, speedParam);
        }
        public float HitboxRadius { get; set; }
        private uint _castingActionId;
        internal Action BeforeCastingActionIdRead;
        public uint CastingActionId
        {
            get { BeforeCastingActionIdRead?.Invoke(); return _castingActionId; }
            set { _castingActionId = value; }
        }

        public void SetCastInfo(uint actionId, float castTime)
        {
            if (!IsAlive) throw new InvalidOperationException("读条控制器写入了已失效的实体。");
            CastingActionId = actionId;
            CastStarts.Add(actionId);
        }

        public bool FinalizeCast(uint expectedActionId)
        {
            if (!IsAlive) throw new InvalidOperationException("读条控制器结束了已失效实体的读条。");
            if (CastingActionId == 0 || CastingActionId != expectedActionId) return false;
            FinalizedCasts.Add(CastingActionId);
            CastingActionId = 0;
            return true;
        }

        public void PlayBaseTimeline(ushort timelineId, bool interrupt = true)
        {
            if (!IsAlive) throw new InvalidOperationException("移动控制器播放了已失效实体的动画。");
            if (timelineId == 0) return;
            BaseTimelineId = timelineId;
            _baseTimelineVersion++;
            PlayedBaseTimelines.Add(timelineId);
        }

        public void ResetBaseTimeline()
        {
            if (!IsAlive) throw new InvalidOperationException("移动控制器重置了已失效实体的动画。");
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
            if (!IsAlive) throw new InvalidOperationException("移动控制器清理了已失效实体的动画。");
            if (BaseTimelineId != timelineId || _baseTimelineVersion != version) return false;
            ResetBaseTimeline();
            return true;
        }

        public void PlayBlendTimeline(ushort timelineId) { PlayedTimelines.Add(timelineId); }
    }

    public sealed class EventObject : EntityBase
    {
        internal EventObject(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation) { }
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

    public sealed class StatusManager
    {
        public static IntPtr SetStatusFuncPtr = new IntPtr(1), RemoveStatusFuncPtr = new IntPtr(2);
        private readonly Status[] _values = new Status[60];
        internal int SetCalls, RemoveCalls;
        internal Action<int> AfterSet, BeforeRemove, AfterRemove;
        public StatusManager() { for (var i = 0; i < _values.Length; i++) _values[i] = new Status(); }
        public Status GetStatus(int index) => _values[index];
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
        internal uint EnmityTarget;
        internal IReadOnlyList<int> EnmityValues;
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
            if (!IsOwnedBy(owner)) return;
            RestoreAttempts++;
            BeforeRestore?.Invoke();
            _owner = null;
        }
        internal void RemoveMember(object owner, uint id)
        {
            if (IsOwnedBy(owner)) Restore(owner);
        }
        internal void SetEnmity(object owner, uint targetId,
            IReadOnlyList<Simulant.Simulation.Runtime.SimActor> members, IReadOnlyList<int> values)
        {
            if (!IsOwnedBy(owner)) throw new InvalidOperationException("测试小队尚未被本会话接管。");
            EnmityTarget = targetId;
            EnmityValues = values;
            EnmitySetCount++;
        }
        internal void RefreshEnmity(object owner)
        {
            if (!IsOwnedBy(owner)) throw new InvalidOperationException("小队恢复后仍调用了仇恨刷新。");
            EnmityRefreshCount++;
            OnRefreshEnmity?.Invoke();
        }
    }
}

namespace Simulant.Game.ExtractedCsv
{
    public abstract class TypedCsvRow { }

    public sealed class CsvManager
    {
        public static CsvManager Instance { get; } = new CsvManager();
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();
        public Dictionary<int, T> Get<T>()
        {
            if (!_tables.TryGetValue(typeof(T), out var table))
                _tables.Add(typeof(T), table = new Dictionary<int, T>());
            return (Dictionary<int, T>)table;
        }
    }
}

namespace Simulant.Game.ExtractedCsv.Rows
{
    public sealed class BNpcBase : Simulant.Game.ExtractedCsv.TypedCsvRow
    {
        public ushort ModelCharaId { get; set; }
        public byte Battalion { get; set; }
    }

    public sealed class BNpcName : Simulant.Game.ExtractedCsv.TypedCsvRow
    {
        public string Name { get; set; }
    }

    public sealed class Action
    {
        public int Index { get; set; }
        public float CastTime { get; set; }
        public ushort OmenId { get; set; }
        public short AnimationEndId { get; set; }
        public byte ShapeType { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public Omen Omen { get; set; }
    }

    public sealed class Omen { public string Name { get; set; } }
}

namespace Simulant.ACT
{
    public static class TriggernometryInterop
    {
        internal static readonly List<string> Commands = new List<string>();
        internal static Action<string> OnCommand;
        public static void InvokeNamedCallback(string name, string command)
        {
            if (name != "PictoACT") throw new InvalidOperationException("非预期的回调：" + name);
            Commands.Add(command);
            OnCommand?.Invoke(command);
        }

        internal static void Reset() { Commands.Clear(); OnCommand = null; }
    }
}

namespace Simulant.Game.FFCS.Client.Game.Character
{
    public enum Job : byte
    {
        Adv = 0, GLA = 1, PLD = 19, MNK = 20, WAR = 21, DRG = 22, BRD = 23,
        WHM = 24, BLM = 25, SMN = 27, SCH = 28, PCT = 42
    }
    public enum CharacterModes : byte { None = 0, Normal = 1, Dead = 2, AnimLock = 8 }
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
        [global::System.Runtime.InteropServices.FieldOffset(0)] public ulong Id;
        [global::System.Runtime.InteropServices.FieldOffset(0)] public uint ObjectId;
        public static implicit operator GameObjectId(ulong id) => new GameObjectId { Id = id };
        public static implicit operator ulong(GameObjectId id) => id.Id;
    }

    public enum ObjectKind : byte { None = 0, Pc = 1, BattleNpc = 2, EventObj = 7 }
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
    public struct ObjectArrays { public GameObjectPtrs IndexSorted => default(GameObjectPtrs); }
    public struct GameObjectPtrs
    {
        public const int Count = 819;
        public GameObject this[int slot] => throw new InvalidOperationException("测试不得访问原生游戏内存。");
    }
}

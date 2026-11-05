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
        private Vector2 _position;
        private float _height;
        private float _heading;
        internal readonly List<Vector2> PositionWrites = new List<Vector2>();
        internal readonly List<int> WriteThreads = new List<int>();
        internal Action<Vector2> OnPositionWritten;
        internal readonly List<uint> CastStarts = new List<uint>();
        internal readonly List<uint> FinalizedCasts = new List<uint>();
        internal readonly List<ushort> PlayedTimelines = new List<ushort>();
        internal int BaseTimelineResetCount;

        internal Character(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation) { }

        public Vector2 Pos
        {
            get => _position;
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
            get => new Vector3(_position.X, _position.Y, _height);
            set { Pos = new Vector2(value.X, value.Y); _height = value.Z; }
        }

        public Simulant.Game.FFCS.Client.Game.Character.Job Job { get; set; }
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

        public void ResetBaseTimeline() { BaseTimelineResetCount++; }
        public void PlayBlendTimeline(ushort timelineId) { PlayedTimelines.Add(timelineId); }
    }

    public sealed class EventObject : EntityBase
    {
        internal EventObject(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation) { }
    }
}

namespace Simulant.Game.ExtractedCsv
{
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
    public enum Job : byte { Adv = 0 }
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

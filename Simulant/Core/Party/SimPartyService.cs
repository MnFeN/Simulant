using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Control;
using Simulant.Game.FFCS.Client.Game.Group;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Game.FFCS.Client.Network;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace Simulant.Core.Party
{
    /// <summary>借用客户端主小队；原生收包负责入队和界面，停止时恢复进入前的名单。</summary>
    internal sealed class SimPartyService
    {
        private readonly PluginHost _host;
        private readonly object _gate = new object();
        private object _owner;
        private GreyMagicExternalProcessMemory _memory;
        private IntPtr _process, _group, _uiState;
        private UpdatePartyPacket _original, _shown;
        private SimActor[] _shownActors;
        private byte[][] _memberValues;
        private MemberDetails[] _memberDetails;
        private byte[] _hate;
        private int _worldVersion, _snapshotVersion;
        private Entity.Character _enmityTarget, _combatPlayer;
        private UpdateHatePacket _enmityPacket;
        private IntPtr _targetSystem;
        private bool _enmityDirty, _restoreCombat;

        private sealed class MemberDetails
        {
            internal Vector3 Position;
            internal byte Flags, Shield;
            internal byte[] Name;
        }

        internal SimPartyService(PluginHost host) { _host = host ?? throw new ArgumentNullException(nameof(host)); }
        internal bool IsOwnedBy(object owner) { lock (_gate) return owner != null && ReferenceEquals(owner, _owner); }

        internal void Show(object owner, IReadOnlyList<SimActor> members)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            lock (_gate)
            {
                var worldVersion = Volatile.Read(ref _worldVersion);
                if (!_host.PluginReady || !_host.FirewallEnabled || _host.Disposed || _host.IsInitializing)
                    throw new InvalidOperationException("请在初始化完成并启用防火墙后显示模拟小队。");
                if (_owner != null && !ReferenceEquals(_owner, owner))
                    throw new InvalidOperationException("已有另一场模拟占用原生小队列表，请先停止它。");
                if (members == null || members.Count < 2 || members.Count > 8)
                    throw new ArgumentException("模拟小队必须包含自己及队员，共 2-8 人。", nameof(members));
                var self = _host.EntityProvider.GetMyself();
                if (self == null || !self.IsAlive) throw new InvalidOperationException("无法取得当前玩家。");
                var ids = new HashSet<uint>();
                foreach (var actor in members)
                {
                    if (actor == null || !actor.IsActive || !actor.Entity.IsAlive || actor.Entity.ObjectKind != ObjectKind.Pc || !ids.Add(actor.Id))
                        throw new ArgumentException("小队成员必须是有效且不重复的玩家实体。", nameof(members));
                    if (actor.Id != self.Id && ((actor.Id & 0xFFFF0000) != 0x10FF0000 ||
                        actor.Entity.Native.ObjectIndex < 2 || actor.Entity.Native.ObjectIndex >= 200 || actor.Entity.Native.ObjectIndex % 2 != 0))
                        throw new ArgumentException("其他队员必须通过网络管线生成。", nameof(members));
                }
                if (!ids.Contains(self.Id)) throw new ArgumentException("模拟小队必须包含当前玩家。", nameof(members));
                var ordered = members.OrderByDescending(a => a.Id == self.Id).ToArray();
                var packet = new UpdatePartyPacket { MemberCount = (byte)ordered.Length };
                for (var i = 0; i < ordered.Length; i++) packet.SetMember(i, FromActor(ordered[i]));

                if (_owner == null) Capture(worldVersion);
                EnsureContext();
                packet.PartyId = _original.PartyId != 0 ? _original.PartyId : 0xFFFFFFFFL;
                packet.PartyId_2 = _original.PartyId_2 != 0 ? _original.PartyId_2 : packet.PartyId;
                // 原生调用可能已经改过名单再抛异常，必须先登记恢复责任。
                _owner = owner;
                _shown = packet;
                _shownActors = ordered;
                PacketDispatcher.HandleUpdatePartyPacket(packet);
                EnsureContext();
                VerifyRoster(packet);
            }
        }

        private unsafe UpdatePartyPacketMember FromActor(SimActor actor)
        {
            var native = actor.Entity.Native;
            var id = actor.Id;
            var member = new UpdatePartyPacketMember
            {
                EntityId = id, AccountId = native.AccountId, ContentId = native.ContentId,
                PetEntityId = 0xE0000000, CompanionEntityId = 0xE0000000,
                CurrentHP = actor.Entity.HP, MaxHP = actor.Entity.MaxHP,
                CurrentMP = checked((ushort)actor.Entity.MP), MaxMP = checked((ushort)actor.Entity.MaxMp),
                HomeWorld = native.HomeWorld,
                TerritoryType = checked((ushort)(XivPluginInterop.plugin?.DataRepository.GetCurrentTerritoryID() ?? 0)),
                Valid = 1, ClassJob = (byte)actor.Entity.Job, Level = actor.Entity.Level,
                Sex = native.DrawData.Ptr.Read<byte>(0x221)
            };
            if (member.AccountId == 0) member.AccountId = 0xFF00000000000000UL | id;
            if (member.ContentId == 0) member.ContentId = 0xFF00000000000000UL | id;
            CopyName(native.GetName().ReadBytes(64), member.Name);
            CopyStatuses(native.StatusManager.Ptr.ReadBytes(UpdatePartyPacketMember.StatusCount * 0x10, 8), ref member);
            return member;
        }

        private unsafe void Capture(int worldVersion)
        {
            _snapshotVersion = worldVersion;
            _memory = NamazuInterop.Plugin?.Memory ?? throw new InvalidOperationException("无法访问游戏内存。");
            _process = _memory.ProcessHandle;
            _group = GroupManager.InstancePtr;
            _uiState = UIState.InstancePtr;
            EnsureContext();
            var group = GroupManager.Instance().MainGroup;
            byte count = group.MemberCount;
            uint leader = group.PartyLeaderIndex;
            if (count > 8 || (count != 0 && leader >= count))
                throw new InvalidOperationException("原生小队人数或队长索引异常，请核对客户端版本与结构布局。");
            if (group.AllianceFlags != 0)
                throw new InvalidOperationException("当前处于团队中，请先离开团队再使用模拟小队。");
            _original = new UpdatePartyPacket { MemberCount = count, LeaderIndex = count == 0 ? (byte)0 : (byte)leader,
                PartyId = count == 0 ? 0 : (long)group.PartyId, PartyId_2 = count == 0 ? 0 : (long)group.PartyId_2 };
            _memberValues = new byte[count][];
            _memberDetails = new MemberDetails[count];
            for (var i = 0; i < count; i++)
            {
                var original = group.GetPartyMember(i);
                var member = new UpdatePartyPacketMember
                {
                    EntityId = original.EntityId, AccountId = original.AccountId, ContentId = original.ContentId,
                    PetEntityId = original.PetEntityId, CompanionEntityId = original.CompanionEntityId,
                    CurrentHP = original.CurrentHP, MaxHP = original.MaxHP, CurrentMP = original.CurrentMP, MaxMP = original.MaxMP,
                    HomeWorld = original.HomeWorld, TerritoryType = original.TerritoryType,
                    Valid = 1, ClassJob = original.ClassJob, Sex = original.Sex,
                    Level = original.Level, SyncedLevel = ((byte)original.Flags & 8) != 0 ? (byte)original.Level : (byte)0
                };
                _memberDetails[i] = new MemberDetails
                {
                    Position = original.Position, Flags = original.Flags, Shield = original.DamageShield,
                    Name = original.ReadNameBytes()
                };
                CopyName(_memberDetails[i].Name, member.Name);
                // 排除 StatusManager.Owner 指针，保存全部 60 条状态及计数、标志等尾部值。
                _memberValues[i] = ReadExact(original.Ptr + 8, 0x3D8);
                CopyStatuses(_memberValues[i], ref member);
                _original.SetMember(i, member);
            }
            _hate = ReadExact(UIState.Instance().Hate.Ptr, Hate.Size);
            EnsureContext();
        }

        internal void RemoveMember(object owner, uint id)
        {
            lock (_gate)
            {
                if (!IsOwnedBy(owner)) return;
                var index = -1;
                for (var i = 0; i < _shown.MemberCount; i++)
                    if (_shown.GetMember(i).EntityId == id) { index = i; break; }
                if (index < 0) return;
                if (index == 0 || _shown.MemberCount <= 2) { Restore(owner); return; }
                EnsureContext();
                var packet = _shown;
                var remaining = _shownActors.Where(actor => actor.Id != id).ToArray();
                packet.MemberCount = (byte)remaining.Length;
                // 整包移除成员时重新采样角色，不能把初次入队的血量和状态重放给留下的队员。
                for (var i = 0; i < UpdatePartyPacket.Capacity; i++)
                    packet.SetMember(i, i < remaining.Length ? FromActor(remaining[i]) : default);
                PacketDispatcher.HandleUpdatePartyPacket(packet);
                EnsureContext();
                VerifyRoster(packet);
                _shown = packet;
                _shownActors = remaining;
                var enmity = new UpdateHatePacket();
                for (var i = 0; i < _enmityPacket.MemberCount; i++)
                {
                    var entry = _enmityPacket.GetMember(i);
                    if (entry.EntityId != id) enmity.SetMember(enmity.MemberCount++, entry);
                }
                _enmityPacket = enmity;
                _enmityDirty = true;
                RefreshEnmity(owner);
            }
        }

        /// <summary>提供当前目标的小队仇恨百分比；原生列表自行显示顺序和仇恨条。</summary>
        internal void SetEnmity(object owner, uint targetId, IReadOnlyList<SimActor> members, IReadOnlyList<int> enmity)
        {
            lock (_gate)
            {
                if (!IsOwnedBy(owner)) throw new InvalidOperationException("本会话尚未显示模拟小队。");
                if ((targetId & 0xFFFF0000) != 0x40FF0000 || !_host.EntityProvider.GetCharacters().Any(e => e.Id == targetId && e.IsAlive))
                    throw new ArgumentException("仇恨目标必须是有效的模拟战斗实体。", nameof(targetId));
                if (members == null || enmity == null || members.Count != enmity.Count || members.Count > 8)
                    throw new ArgumentException("仇恨值必须与小队成员一一对应。");
                var ids = new HashSet<uint>();
                var entries = new List<UpdateHatePacketMember>();
                for (var i = 0; i < members.Count; i++)
                {
                    var actor = members[i];
                    if (actor == null || !actor.IsActive || !actor.Entity.IsAlive || !ids.Add(actor.Id) ||
                        !Enumerable.Range(0, _shown.MemberCount).Any(j => _shown.GetMember(j).EntityId == actor.Id) ||
                        enmity[i] < 0 || enmity[i] > 100)
                        throw new ArgumentException("仇恨成员必须在本小队中且不重复，数值范围为 0-100。");
                    entries.Add(new UpdateHatePacketMember { EntityId = actor.Id, Enmity = (byte)enmity[i] });
                }
                EnsureContext();
                if (TargetSystem.InstancePtr == IntPtr.Zero || PacketDispatcher.HandleUpdateHatePacketFuncPtr == IntPtr.Zero ||
                    PacketDispatcher.HandleActorControlFuncPtr == IntPtr.Zero)
                    throw new InvalidOperationException("目标系统或仇恨、战斗状态更新函数尚未定位，请重新初始化。");
                var target = _host.EntityProvider.GetCharacters().First(e => e.Id == targetId && e.IsAlive);
                if (target.ObjectKind != ObjectKind.BattleNpc)
                    throw new ArgumentException("仇恨目标必须是模拟战斗 NPC。", nameof(targetId));
                var packet = new UpdateHatePacket { MemberCount = (byte)entries.Count };
                var sorted = entries.OrderByDescending(e => e.Enmity).ToArray();
                for (var i = 0; i < sorted.Length; i++) packet.SetMember(i, sorted[i]);
                EnterCombat();
                _enmityTarget = target;
                _targetSystem = TargetSystem.InstancePtr;
                _enmityPacket = packet;
                _enmityDirty = true;
                RefreshEnmity(owner);
            }
        }

        private void EnterCombat()
        {
            if (_combatPlayer != null) return;
            var self = _host.EntityProvider.GetMyself();
            if (self == null || !self.IsAlive) throw new InvalidOperationException("无法取得当前玩家以进入模拟战斗。");
            var wasInCombat = self.Native.InCombat.Get();
            // Sapphire 的 SetBattle 使用 a1=1 入战；先登记责任，兼容调用部分成功后抛错。
            _combatPlayer = self;
            _restoreCombat = !wasInCombat;
            if (_restoreCombat)
            {
                EnsureContext();
                self.ActorControl(ActorControlCategory.SetBattle, 1);
            }
        }

        /// <summary>目标切换可能清空客户端仇恨；由会话低频检查，仅在选中对应目标且数据变化时发送。</summary>
        internal void RefreshEnmity(object owner)
        {
            lock (_gate)
            {
                if (!IsOwnedBy(owner) || _enmityTarget == null) return;
                EnsureContext();
                if (!_enmityTarget.IsAlive) { _enmityTarget = null; return; }
                if (TargetSystem.InstancePtr != _targetSystem)
                    throw new InvalidOperationException("目标系统已变化，停止更新旧场景的仇恨。");
                var selected = GetSelectedTarget();
                if (selected != _enmityTarget.Address) { _enmityDirty = true; return; }
                if (!_enmityDirty && MatchesEnmity()) return;
                EnsureContext();
                PacketDispatcher.HandleUpdateHatePacket(_enmityTarget.Id, _enmityPacket);
                EnsureContext();
                if (!MatchesEnmity())
                {
                    _enmityDirty = true;
                    // 用户可在收包期间切换目标；这时客户端清空 Hate 不代表函数或布局错误。
                    if (GetSelectedTarget() != _enmityTarget.Address) return;
                    throw new InvalidOperationException("客户端仇恨数据与更新包不符，请核对当前版本的函数和布局。");
                }
                _enmityDirty = false;
            }
        }

        private static IntPtr GetSelectedTarget()
        {
            var targets = TargetSystem.Instance();
            IntPtr target = targets.Target;
            return target != IntPtr.Zero ? target : targets.SoftTarget.Get();
        }

        private bool MatchesEnmity()
        {
            var data = ReadExact(_uiState + 8, Hate.Size);
            if (BitConverter.ToUInt32(data, 0x104) != _enmityTarget.Id ||
                BitConverter.ToInt32(data, 0x100) != _enmityPacket.MemberCount) return false;
            for (var i = 0; i < _enmityPacket.MemberCount; i++)
            {
                var entry = _enmityPacket.GetMember(i);
                if (BitConverter.ToUInt32(data, i * 8) != entry.EntityId ||
                    BitConverter.ToInt32(data, i * 8 + 4) != entry.Enmity) return false;
            }
            return true;
        }

        internal void Restore(object owner)
        {
            lock (_gate)
            {
                if (!IsOwnedBy(owner)) return;
                EnsureContext();
                PacketDispatcher.HandleUpdatePartyPacket(_original);
                EnsureContext();
                VerifyRoster(_original);
                var group = GroupManager.Instance().MainGroup;
                for (var i = 0; i < _original.MemberCount; i++)
                {
                    var member = group.GetPartyMember(i);
                    WriteExact(member.Ptr + 8, _memberValues[i]);
                    var details = _memberDetails[i];
                    WriteExact(member.NamePtr, details.Name);
                    WriteExact(member.Flags.Ptr, new[] { details.Flags });
                    WriteExact(member.DamageShield.Ptr, new[] { details.Shield });
                    WriteExact(member.Position.Ptr, BitConverter.GetBytes(details.Position.X)
                        .Concat(BitConverter.GetBytes(details.Position.Y)).Concat(BitConverter.GetBytes(details.Position.Z)).ToArray());
                }
                WriteExact(_uiState + 8, _hate);
                if (_restoreCombat)
                {
                    EnsureContext();
                    if (!_combatPlayer.IsAlive)
                        throw new InvalidOperationException("原玩家身份已经变化，不能恢复其战斗状态。");
                    _combatPlayer.ActorControl(ActorControlCategory.SetBattle, 0);
                }
                // 只有全部恢复成功后才能放弃责任；失败时可再次停止以重试。
                Forget();
            }
        }

        internal void RestoreAll() { lock (_gate) { if (_owner != null) Restore(_owner); } }
        internal void InvalidateWorld()
        {
            // 换图通常不会改变全局地址；先让正在执行的旧操作失效，再等待它释放锁。
            Interlocked.Increment(ref _worldVersion);
            lock (_gate) Forget();
        }

        private void Forget()
        {
            _owner = null; _memory = null; _memberValues = null; _memberDetails = null; _hate = null;
            _original = default; _shown = default;
            _shownActors = null;
            _enmityTarget = null; _combatPlayer = null; _restoreCombat = false;
            _enmityPacket = default; _enmityDirty = false; _targetSystem = IntPtr.Zero;
        }

        private void EnsureContext()
        {
            var current = NamazuInterop.Plugin?.Memory;
            if (_snapshotVersion != Volatile.Read(ref _worldVersion) ||
                _memory == null || current == null || !ReferenceEquals(_memory.RawMemory, current.RawMemory) ||
                !_memory.IsProcessOpen || _process == IntPtr.Zero || current.ProcessHandle != _process || _memory.ProcessHandle != _process ||
                _group == IntPtr.Zero || GroupManager.InstancePtr != _group || _uiState == IntPtr.Zero || UIState.InstancePtr != _uiState)
                throw new InvalidOperationException("模拟小队所属的进程或原生结构已变化，不能写回旧快照。");
        }

        private void VerifyRoster(UpdatePartyPacket packet)
        {
            var group = GroupManager.Instance().MainGroup;
            if (group.MemberCount != packet.MemberCount)
                throw new InvalidOperationException("客户端未接受小队人数，请核对小队更新函数和结构布局。");
            for (var i = 0; i < packet.MemberCount; i++)
                if (group.GetPartyMember(i).EntityId != packet.GetMember(i).EntityId)
                    throw new InvalidOperationException("客户端小队成员与请求不符，已保留恢复责任。");
        }

        private byte[] ReadExact(IntPtr address, int count)
        {
            var data = _memory.ReadBytes(address, count);
            if (data == null || data.Length != count) throw new InvalidOperationException("读取原生小队快照不完整。");
            return data;
        }

        private void WriteExact(IntPtr address, byte[] data)
        {
            EnsureContext();
            if (_memory.WriteBytes(address, data) != data.Length)
                throw new InvalidOperationException("写入原生小队数据不完整，请重试停止模拟。");
        }

        private static unsafe void CopyName(byte[] source, byte* destination)
        {
            var count = Array.IndexOf(source, (byte)0);
            if (count < 0) count = source.Length;
            var chars = Encoding.UTF8.GetChars(source, 0, count);
            var bytes = new byte[UpdatePartyPacketMember.NameLength - 1];
            Encoding.UTF8.GetEncoder().Convert(chars, 0, chars.Length, bytes, 0, bytes.Length, true,
                out _, out var written, out _);
            for (var i = 0; i < written; i++) destination[i] = bytes[i];
        }

        private static void CopyStatuses(byte[] values, ref UpdatePartyPacketMember member)
        {
            if (values == null || values.Length < UpdatePartyPacketMember.StatusCount * 0x10)
                throw new InvalidOperationException("读取小队状态数据不完整。");
            for (var i = 0; i < UpdatePartyPacketMember.StatusCount; i++)
            {
                var offset = i * 0x10;
                member.SetStatus(i, new UpdatePartyPacketStatus
                {
                    StatusId = BitConverter.ToUInt16(values, offset), Param = BitConverter.ToUInt16(values, offset + 2),
                    RemainingTime = BitConverter.ToSingle(values, offset + 4), SourceEntityId = BitConverter.ToUInt32(values, offset + 8)
                });
            }
        }
    }
}

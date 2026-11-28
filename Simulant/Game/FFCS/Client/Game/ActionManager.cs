using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Simulant.Game.FFCS.Client.Game.Object;

namespace Simulant.Game.FFCS.Client.Game
{
    // 原生 Client::Game::ActionManager，无基类。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/ActionManager.cs
    public struct ActionManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * F3 0F 10 13")]
        public static IntPtr InstancePtr { get; set; }
        public static ActionManager Instance
            => InstancePtr.As<ActionManager>();

        public MemoryField<float> AnimationLock => Ptr.Field<float>(0x08);

        internal ComboDetail Combo => Ptr.As<ComboDetail>(0x60);

        internal MemoryField<uint> AreaTargetingActionId => Ptr.Field<uint>(0x90);

        // FFCS 的 ida/data.yml：CalculateAreaTargetingTargetState
        // 即 Triggernometry UseActionModule 中的 MouseToWorldPtr
        [SigPattern("4C 8B DC 49 89 5B ?? 49 89 6B ?? 49 89 73 ?? 57 48 81 EC ?? ?? ?? ?? 33 C0")]
        internal static IntPtr CalculateAreaTargetingTargetStateFuncPtr { get; set; }

        internal Vector3? GetGroundTargetCandidate()
        {
            var memory = GameInterop.Connection.Memory;
            var result = memory.AllocateMemory(0x20);
            if (result == IntPtr.Zero)
                throw new InvalidOperationException("无法分配地面选点缓冲区。");

            try
            {
                CalculateAreaTargetingTargetStateFuncPtr.Call(Ptr, uint.MaxValue, (uint)ActionType.FieldMarker, result);
                // +0x00 表示有候选点，+0x01 表示允许放置。瞬移只需要前者和 +0x10 的原生坐标。
                if (result.Read<byte>() == 0)
                    return null;
                return result.Read<Vector3>(0x10);
            }
            finally
            {
                memory.FreeMemory(result);
            }
        }

        internal MemoryArray<RecastDetail> _cooldowns => Ptr.ArrayField<RecastDetail>(0x188, 80);

        public MemoryField<float> DistanceToTargetHitbox => Ptr.Field<float>(0x7EC); // distance to target minus both self & target hitbox radius, clamped to 0

        /// <summary>
        /// Initiate action execution.
        /// </summary>
        /// <remarks>
        /// If called shortly before action is available (due to cooldown or animation lock), action is queued.
        /// If action is area-targeted, starts area targeting mode rather than executing it immediately.
        /// </remarks>
        /// <param name="actionType">Type of action to execute.</param>
        /// <param name="actionId">Id of action to execute.</param>
        /// <param name="targetId">Intended target for the action.</param>
        /// <param name="extraParam">For items - inventory slot to use from (bag id in high byte, slot id in low byte), or 0xFFFF if unspecified (e.g. used from hotbar).</param>
        /// <param name="mode">Special action execution mode.</param>
        /// <param name="comboRouteId"></param>
        /// <param name="outOptAreaTargeted">If non-null, will be set to true if area-targeting mode was started instead of executing an action.</param>
        /// <returns></returns>
        [SigPattern("E8 * * * * B0 01 EB B6")]
        public static IntPtr UseActionFuncPtr { get; set; }

        public bool UseAction(
            ActionType actionType,
            uint actionId,
            uint targetId = 0xE0000000,
            uint extraParam = 0,
            UseActionMode mode = UseActionMode.None,
            uint comboRouteId = 0,
            IntPtr outOptAreaTargeted = default) // bool*
        => UseActionFuncPtr.Call<bool>(Ptr, actionType, actionId, targetId, extraParam, (int)mode, comboRouteId, outOptAreaTargeted);

        /// <summary>
        /// Actually execute the action right now, if possible. This skips queueing, area targeting mode, etc.
        /// </summary>
        /// <remarks>
        /// The function name is a bit misleading - this function is called internally for all actions, not necessarily location-targeted ones.
        /// This function verifies that action can actually be cast (checks LoS, various states that could prevent successful cast, etc).
        /// This expects input action to be already adjusted - i.e. don't pass General actions here, it won't work properly. See code for UseAction for details.
        /// </remarks>
        /// <param name="actionType">Type of action to execute. Should be adjusted.</param>
        /// <param name="actionId">Id of action to execute. Should be adjusted.</param>
        /// <param name="targetId">Intended target for the action. Note that real target can be modified (e.g. replaced with player for self-targeted actions, etc) by ResolveTarget.</param>
        /// <param name="location">Target position, important for area-targeted spells. Be careful if passing null - game doesn't really expect that and might dereference it in some code paths!</param>
        /// <param name="extraParam">See UseAction.</param>
        /// <param name="a7">unknown</param>
        /// <returns></returns>
        [SigPattern("E8 * * * * 48 8B BC 24 ? ? ? ? 44 0F B6 F8 B0")]
        public static IntPtr UseActionLocationFuncPtr { get; set; }

        public bool UseActionLocation(
            ActionType actionType,
            uint actionId,
            uint targetId = 0xE0000000,
            IntPtr vec3Ptr = default, // Vector3*
        uint extraParam = 0,
            byte a7 = 0)
            => UseActionLocationFuncPtr.Call<bool>(Ptr, (byte)actionType, actionId, targetId, vec3Ptr, extraParam, a7);

        /*
        /// <summary>
        /// Start a cooldown cycle for the specified action. Upon calling, the game will begin to track state in the
        /// relevant <see cref="RecastDetail"/>, which can be retrieved separately. Consult that struct's documentation for
        /// more information.
        /// </summary>
        /// <remarks>
        /// This method should not be called by developers and is instead provided for hooking and API completeness.
        /// </remarks>
        /// <param name="actionType">The type of the action (generally, Spell) to trigger a cooldown for.</param>
        /// <param name="actionId">The ID of the action to trigger a cooldown for.</param>
        [SigPattern("48 89 6C 24 ? 56 57 41 54 48 83 EC 30 44 8B E2")]
        public static IntPtr StartCooldownFuncPtr { get; set; }
        public void StartCooldown(ActionType actionType, uint actionId)
            => StartCooldownFuncPtr.Call(Ptr, actionType, actionId);
        */
        /// <summary>
        /// Calculate target position for area-targeted spell corresponding to current cursor position.
        /// </summary>
        /// <param name="outPosition">If successful, contains coordinates of the point on the ground.</param>
        /// <returns>Whether intersection with ground was found.</returns>
        [SigPattern("E8 * * * * E9 ? ? ? ? 44 8B 84 24 80 00 00 00 33 C0")]
        public static IntPtr GetGroundPositionForCursorFuncPtr { get; set; }

        public bool GetGroundPositionForCursor(IntPtr outPosition)
            => GetGroundPositionForCursorFuncPtr.Call<bool>(Ptr, outPosition);

        /// <summary>
        /// Called every frame, responsible for ticking down timers (cooldowns, animation lock, etc) and executing queued action as soon as possible.
        /// </summary>
        [SigPattern("48 8B C4 48 89 58 ? 56 48 81 EC ? ? ? ? 48 8B 35")]
        public static IntPtr UpdateFuncPtr { get; set; }

        public void Update()
            => UpdateFuncPtr.Call(Ptr);

        [SigPattern("E8 * * * * 41 83 FC ? 0F 84 ? ? ? ? 41 81 FC")]
        public static IntPtr OpenCastBarFuncPtr { get; set; }

        public void OpenCastBar(Character.Character character, ActionType actionType, uint actionId, uint spellId, uint extraParam, float castTimeElapsed, float castTimeTotal)
            => OpenCastBarFuncPtr.Call(Ptr, character.Ptr, actionType, actionId, spellId, extraParam, castTimeElapsed, castTimeTotal);

        [SigPattern("E8 * * * * 40 0F B6 CE E9")]
        internal static IntPtr GetActionCostFuncPtr { get; set; }

        /// <summary>查询客户端计算的技能消耗，返回值的单位由技能的消耗类型决定。</summary>
        /// <remarks>
        /// 国服 7.58（2026.11.03.0000.0000）：从 ActionManager.CheckActionResources 的魔力比较分支进入本函数，
        /// 可核对类型 4 使用第四参 a4 = 1 取得最低魔力。a4 = 0 时，本函数读取当前魔力计算实际消耗。
        /// </remarks>
        internal static int GetActionCost(ActionType actionType, uint actionId, byte a3, byte a4, byte a5, byte a6)
            => GetActionCostFuncPtr.Call<int>(actionType, actionId, a3, a4, a5, a6);

        /// <summary>
        /// Calculate cast time for an action, in milliseconds, adjusted by player stats (haste, sks/sps, etc.) and optionally various class mechanics.
        /// </summary>
        /// <param name="actionType">The type of action to check.</param>
        /// <param name="actionId">The ID of the action to check.</param>
        /// <param name="applyProcs">If true, applies various class mechanics (procs, swiftcast, etc).</param>
        /// <param name="outOptProc">If non-null and applyProcs is true, will be set to applied proc.</param>
        /// <returns></returns>
        [SigPattern("E8 * * * * 8B D0 48 8B CF E8 ?? ?? ?? ?? 0F B7 55")]
        internal static IntPtr GetAdjustedCastTimeFuncPtr { get; set; }

        // 默认应用光速、即刻咏唱等职业修正。返回零表示瞬发，正值为修正后的毫秒数。
        internal static int GetAdjustedCastTime(ActionType actionType, uint actionId, bool applyProcs = true, IntPtr outOptProc = default)
            => GetAdjustedCastTimeFuncPtr.Call<int>(actionType, actionId, applyProcs, outOptProc);

        [SigPattern("E8 * * * * 8B D0 48 8B CD 8B F0")]
        internal static IntPtr GetRecastGroupFuncPtr { get; set; }

        internal int GetRecastGroup(int type, uint actionId)
            => GetRecastGroupFuncPtr.Call<int>(Ptr, type, actionId);

        /// <summary>
        /// Method used by game server to update cooldowns on the client.
        /// Only operates on *primary* recast group for the specified action.
        /// </summary>
        [SigPattern("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 8B F2 0F 29 74 24 ?? 0F B6 D2")]
        internal static IntPtr SetCooldownFuncPtr { get; set; }

        internal void SetCooldown(ActionType actionType, uint actionId, float newElapsed, float newTotal)
            => SetCooldownFuncPtr.Call(Ptr, actionType, actionId, newElapsed, newTotal);

        [SigPattern("E9 * * * * 41 B0 ?? 8B D3")]
        internal static IntPtr GetMaxChargesFuncPtr { get; set; }

        internal static ushort GetMaxCharges(uint actionId, uint level) // 0 for current level
            => GetMaxChargesFuncPtr.Call<ushort>(actionId, level);

        internal enum CastTimeProc : byte
        {
            None = 0,
            Firestarter = 1, // THM/BLM
            //Thundercloud = 2, // THM/BLM, gone in 7.0
            Swiftcast = 3,
            Lightspeed = 4, // AST
            Dualcast = 5, // RDM
            Abridged = 6, // AST
            Triplecast = 7, // BLM
            DemiBahamut = 8, // SMN
            Requiescat = 9, // PLD
            DemiPhoenix = 11, // SMN
            EnhancedHarpe = 12, // RPR
            SoulsowOutOfCombat = 13, // RPR
            Acceleration = 14, // RDM
            DivineMight = 15, // PLD
            MotifOutOfCombat = 18, // PCT
            RainbowBright = 19, // PCT
        }

        public enum UseActionMode
        {
            None = 0, // usual action execution, e.g. a hotbar button press
            Queue = 1, // previously queued action is now ready and is being executed (=> will ignore queue)
            Macro = 2, // action execution originating from a macro (=> won't be queued)
            Combo = 3, // action execution is from a single-button combo
        }
    }

    /// <summary>
    /// A struct representing information about recast timers/cooldowns for a specific RecastGroup. A recast group may be
    /// shared between one (or more) actions, depending on the group in question.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct RecastDetail
    {
        internal const int Size = 0x14;

        /// <summary>
        /// A boolean representing if this recast group is currently "active." When this is a non-zero value (true), this
        /// recast group is actively in cooldown.
        /// </summary>
        [FieldOffset(0x0), MarshalAs(UnmanagedType.I1)] public bool IsActive;

        /// <summary>The last Action ID that triggered an update for this recast group.</summary>
        [FieldOffset(0x4)] public uint ActionId;

        /// <summary>
        /// The current "elapsed" time of this action's recharge. For most actions, this value will be set to zero when the
        /// action is used. For actions with multiple charges, this value will give "credit" for unspent actions.
        /// </summary>
        /// <remarks>
        /// For multi-charge actions, it helps to think of this field as representing the current value of a resource gauge.
        /// This value represents the "current level" of the resource gauge, with each second adding 1 unit to the gauge up
        /// until the maximum as defined in the <see cref="Total"/> field.
        /// <para />
        /// When a normal action is cast, this gauge is "depleted" to zero. When a multi-charge action is cast, however,
        /// the appropriate value (defined by the action, but generally the recharge time) is subtracted from this value.
        /// </remarks>
        [FieldOffset(0x8)] public float Elapsed;

        /// <summary>
        /// The total number of seconds this recast group takes to go from "fully exhausted" to "fully charged." For most
        /// actions, this will simply be the adjusted recast time from <see cref="ActionManager.GetAdjustedRecastTime"/>
        /// (which displays in the tooltip UI as the "recast time"). Multi-charge actions such as Ninja's Mudra will show
        /// the total charge time (the Adjusted Recast Time multiplied by the number of charges this action has at max
        /// level).
        /// </summary>
        /// <remarks>
        /// The total value shown here depends on the last action used. For example, if a specific action is
        /// bound to the GCD but is faster/slower than the normal GCD, this value will be set accordingly.
        /// <para />
        /// Continuing the resource gauge analogy from <see cref="Elapsed"/>, this field would represent the "cap" of the
        /// resource gauge. For normal actions, the resource gauge must be completely filled before the action can be used
        /// again. Multi-charge actions will instead allow the gauge to charge to the maximum number of actions allowed.
        /// <para />
        /// It is recommended to use <see cref="ActionManager.GetRecastTime"/> over this field, as it handles an edge case
        /// in charge management.
        /// </remarks>
        [FieldOffset(0xC)] public float Total;
    }

    public struct ComboDetail : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<float> Timer => Ptr.Field<float>(0x00);
        public MemoryField<uint> Action => Ptr.Field<uint>(0x04);
    }

    public enum ActionType : uint
    {
        None,
        Action,
        Item,
        EventItem,
        EventAction,
        GeneralAction,
        BuddyAction,
        MainCommand,
        Companion,
        CraftAction,
        Unk10, // Fishing per Sapphire? Something to do with items.
        PetAction,
        Unk12, // Not in UseAction. Sapphire says CompanyAction, but not actually triggered.
        Mount,
        PvPAction,
        FieldMarker,
        ChocoboRaceAbility,
        ChocoboRaceItem,
        Unk18, // Not in UseAction (?)
        BgcArmyAction,
        Ornament,
    }
}

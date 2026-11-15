using System;
using Simulant.Game.FFCS.Client.Game.Object;

namespace Simulant.Game.FFCS.Client.Game
{
    // Client::Game::ActionManager
    public struct ActionManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * F3 0F 10 13")]
        public static IntPtr InstancePtr { get; set; }
        public static ActionManager Instance
            => InstancePtr.As<ActionManager>();

        public MemoryField<float> AnimationLock => Ptr.Field<float>(0x08);

        // 迁移自 FFXIVClientStructs ActionManager；复用客户端处理冷却更新的方法。
        [SigPattern("E8 * * * * 8B D0 48 8B CD 8B F0")]
        public static IntPtr GetRecastGroupFuncPtr { get; set; }
        public int GetRecastGroup(uint actionId) => GetRecastGroupFuncPtr.Call<int>(Ptr, 1, actionId);

        [SigPattern("40 53 48 83 EC 20 48 63 DA 85 D2")]
        public static IntPtr GetRecastGroupDetailFuncPtr { get; set; }
        public IntPtr GetRecastGroupDetail(int group) => GetRecastGroupDetailFuncPtr.Call<IntPtr>(Ptr, group);

        [SigPattern("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 8B F2 0F 29 74 24 ?? 0F B6 D2")]
        public static IntPtr SetCooldownFuncPtr { get; set; }
        public void SetCooldown(uint actionId, float elapsed, float total)
            => SetCooldownFuncPtr.Call(Ptr, 1, actionId, elapsed, total);
        // [FieldOffset(0x184), FixedSizeArray] internal FixedSizeArray80<RecastDetail> _cooldowns;
        public MemoryField<float> DistanceToTargetHitbox => Ptr.Field<float>(0x7E8); // distance to target minus both self & target hitbox radius, clamped to 0

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

        public enum UseActionMode
        {
            None = 0, // usual action execution, e.g. a hotbar button press
            Queue = 1, // previously queued action is now ready and is being executed (=> will ignore queue)
            Macro = 2, // action execution originating from a macro (=> won't be queued)
            Combo = 3, // action execution is from a single-button combo
        }
    }

    /*
    /// <summary>
    /// A struct representing information about recast timers/cooldowns for a specific RecastGroup. A recast group may be
    /// shared between one (or more) actions, depending on the group in question.
    /// </summary>
    public struct RecastDetail : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        /// <summary>
        /// A boolean representing if this recast group is currently "active." When this is a non-zero value (true), this
        /// recast group is actively in cooldown.
        /// </summary>
        public MemoryField<bool> IsActive => Ptr.Field<bool>(0x0);

        /// <summary>
        /// The last Action ID that triggered an update for this recast group.
        /// </summary>
        public MemoryField<uint> ActionId => Ptr.Field<uint>(0x4);

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
        public MemoryField<float> Elapsed => Ptr.Field<float>(0x8);

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
        public MemoryField<float> Total => Ptr.Field<float>(0xC);
    }
    */
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

using Simulant.Game.FFCS.Client.Game.Control;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Graphics.Scene;
using System;
using System.Dynamic;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    public struct Character : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<GameObject>
        private GameObject GameObject => Ptr.As<GameObject>();
        #region GameObject
        public MemoryField<Vector3> DefaultPosition => GameObject.DefaultPosition;
        public MemoryField<float> DefaultRotation => GameObject.DefaultRotation;
        public MemoryField<byte> EventState => GameObject.EventState;
        public MemoryField<uint> EntityId => GameObject.EntityId;
        public MemoryField<uint> LayoutId => GameObject.LayoutId;
        public MemoryField<uint> GimmickId => GameObject.GimmickId;
        public MemoryField<uint> BaseId => GameObject.BaseId;
        public MemoryField<uint> OwnerId => GameObject.OwnerId;
        public MemoryField<ushort> ObjectIndex => GameObject.ObjectIndex;
        public MemoryField<ObjectKind> ObjectKind => GameObject.ObjectKind;
        public MemoryField<byte> YalmDistanceFromPlayerX => GameObject.YalmDistanceFromPlayerX;
        public MemoryField<byte> TargetStatus => GameObject.TargetStatus;
        public MemoryField<byte> YalmDistanceFromPlayerZ => GameObject.YalmDistanceFromPlayerZ;
        public MemoryField<ObjectTargetableFlags> TargetableStatus => GameObject.TargetableStatus;
        public MemoryField<Vector3> Position => GameObject.Position;
        public MemoryField<float> Rotation => GameObject.Rotation;
        public MemoryField<float> Scale => GameObject.Scale;
        public MemoryField<float> Height => GameObject.Height;
        public MemoryField<float> VfxScale => GameObject.VfxScale;
        public MemoryField<float> HitboxRadius => GameObject.HitboxRadius;
        public MemoryField<Vector3> DrawOffset => GameObject.DrawOffset;
        public DrawObject DrawObject => GameObject.DrawObject;
        public MemoryField<VisibilityFlags> RenderFlags => GameObject.RenderFlags;
        public bool GetIsTargetable() => GameObject.GetIsTargetable();
        public IntPtr GetName() => GameObject.GetName();
        public float GetRadius(bool adjustByTransformation = true) => GameObject.GetRadius(adjustByTransformation);
        public void EnableDraw() => GameObject.EnableDraw();
        public void DisableDraw() => GameObject.DisableDraw();
        public void SetReadyToDraw() => GameObject.SetReadyToDraw();
        public void PositionModified() => GameObject.PositionModified();
        public void RotationModified() => GameObject.RotationModified();
        public bool IsNotMounted() => GameObject.IsNotMounted();
        #endregion

        // Inherits<CharacterData>
        private CharacterData CharacterData => Ptr.As<CharacterData>(0x1A0);
        #region CharacterData
        public MemoryField<float> ModelScale => CharacterData.ModelScale;
        public MemoryField<uint> Health => CharacterData.Health;
        public MemoryField<uint> MaxHealth => CharacterData.MaxHealth;
        public MemoryField<uint> Mana => CharacterData.Mana;
        public MemoryField<uint> MaxMana => CharacterData.MaxMana;
        public MemoryField<ushort> GatheringPoints => CharacterData.GatheringPoints;
        public MemoryField<ushort> MaxGatheringPoints => CharacterData.MaxGatheringPoints;
        public MemoryField<ushort> CraftingPoints => CharacterData.CraftingPoints;
        public MemoryField<ushort> MaxCraftingPoints => CharacterData.MaxCraftingPoints;
        public MemoryField<short> TransformationId => CharacterData.TransformationId;
        public MemoryField<ushort> TitleId => CharacterData.TitleId;
        public MemoryField<ushort> StatusLoopVfxId => CharacterData.StatusLoopVfxId;
        public MemoryField<Job> ClassJob => CharacterData.ClassJob;
        public MemoryField<byte> Level => CharacterData.Level;
        public MemoryField<byte> Icon => CharacterData.Icon;
        public MemoryField<byte> SEPack => CharacterData.SEPack;
        public MemoryField<byte> ShieldValue => CharacterData.ShieldValue;
        public MemoryField<byte> Map => CharacterData.Map;
        public MemoryField<byte> OnlineStatus => CharacterData.OnlineStatus;
        public MemoryField<byte> Battalion => CharacterData.Battalion;
        public MemoryField<byte> Flags => CharacterData.Flags;
        public MemoryBitField IsHostile => CharacterData.IsHostile;
        public MemoryBitField InCombat => CharacterData.InCombat;
        #endregion

        public EmoteController EmoteController => Ptr.As<EmoteController>(0x630);
        public DrawDataContainer DrawData => Ptr.As<DrawDataContainer>(0x6F8);
        public TimelineContainer Timeline => Ptr.As<TimelineContainer>(0xA30);
        public VfxContainer Vfx => Ptr.As<VfxContainer>(0x1988);
        public CharacterSetupContainer CharacterSetup => Ptr.As<CharacterSetupContainer>(0x1B10);
        public ModelContainer ModelContainer => Ptr.As<ModelContainer>(0x1B28);
        public MemoryField<float> Alpha => Ptr.Field<float>(0x22E8);
        public MemoryField<ulong> AccountId => Ptr.Field<ulong>(0x2350);
        public MemoryField<ulong> ContentId => Ptr.Field<ulong>(0x2358);
        public MemoryField<ushort> CurrentWorld => Ptr.Field<ushort>(0x2360);
        public MemoryField<ushort> HomeWorld => Ptr.Field<ushort>(0x2362);
        public MemoryField<CharacterModes> Mode => Ptr.Field<CharacterModes>(0x2364);
        public MemoryField<byte> ModeParam => Ptr.Field<byte>(0x2365);

        // The fields below are from BattleChara, just put here for simplicity
        public StatusManager StatusManager => Ptr.As<StatusManager>(0x23B0);
        public CastInfo CastInfo => Ptr.As<CastInfo>(0x2790);
        // public ActionEffectHandler ActionEffectHandler => Ptr.As<ActionEffectHandler>(0x2900);

        [SigPattern("E8 * * * * 45 84 FF 75 40")]
        public static IntPtr SetModeFuncPtr { get; set; }
        public void SetMode(CharacterModes mode, byte modeParam) 
            => SetModeFuncPtr.Call(Ptr, (byte)mode, modeParam);

        [SigPattern("E8 * * * * 38 43 ? 0F 85")]
        public static IntPtr HasStatusFuncPtr { get; set; }
        public bool HasStatus(uint statusId) 
            => HasStatusFuncPtr.Call<bool>(Ptr, statusId);

        /// <summary>Check if this character is in a jumping/falling animation.</summary>
        [SigPattern("E8 * * * * 84 C0 75 46 48 8B 4B 08")]
        public static IntPtr IsJumpingFuncPtr { get; set; }
        public bool IsJumping() 
            => IsJumpingFuncPtr.Call<bool>(Ptr);

        // from Kodakku
        [SigPattern("E8 * * * * 48 8D 0D ? ? ? ? E8 ? ? ? ? FF C6")]
        public static IntPtr KnockbackFuncPtr { get; set; }
        public IntPtr Knockback(float angle, float distance, float duration, byte a5 = 0, int a6 = 0)
        {
            return KnockbackFuncPtr.Call<IntPtr>(Ptr, angle, distance, duration, a5, a6);
        }

        // Clear cast animation / cast state / related VFX slots.
        // sub_1409097A0(actor, actionType, actionId, withExtraStopEffect)
        [SigPattern("48 89 6C 24 18 48 89 74 24 20 41 54 41 56 41 57 48 83 EC 30 48 8B 01 45 0F B6 E1 45 8B F8 44 8B F2 48 8B F1 48 8B E9 FF 50 10 83 F8 02")]
        public static IntPtr ClearCastAnimationAndStateFuncPtr { get; set; }
        public IntPtr ClearCastAnimationAndState(byte actionType, uint actionId, byte withExtraStopEffect = 0)
        {
            return ClearCastAnimationAndStateFuncPtr.Call<IntPtr>(Ptr, (uint)actionType, actionId, withExtraStopEffect);
        }

    }

    // LogMessages for errors starting at 7700
    public enum CharacterModes : byte
    {
        None = 0, // Mode is never used
        Normal = 1, // Param always 0
        Dead = 2,
        EmoteLoop = 3, // Param is an EmoteMode entry
        Mounted = 4, // Param always 0
        Crafting = 5, // Param always 0
        Gathering = 6,
        MateriaAttach = 7,
        AnimLock = 8, // Param always 0
        Carrying = 9, // Param is a Carry entry
        RidingPillion = 10, // Param is the pillion seat number
        InPositionLoop = 11, // Param is an EmoteMode entry
        RaceChocobo = 12,
        TripleTriad = 13,
        Lovm = 14, // Lord of Verminion
        // CustomMatch = 15, // PvP, untested
        Performance = 16, // Param is Perform row id (the instrument)
    }

}

using System;
using Simulant.Game.FFCS.Client.Game.Control;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    public struct TimelineContainer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<ContainerInterface>
        private ContainerInterface ContainerInterface => Ptr.As<ContainerInterface>();
        #region ContainerInterface
        public Character OwnerObject => ContainerInterface.OwnerObject;
        #endregion

        public ActionTimelineSequencer TimelineSequencer => Ptr.As<ActionTimelineSequencer>(0x10);
        public MemoryField<byte> ModelState => Ptr.Field<byte>(0x2C0);
        // FFCS：Flags3 的第 6 位表示武器已经拔出；只读状态，拔刀仍走 ActorControl。
        public MemoryBitField IsWeaponDrawn => Ptr.BitField(0x34E, 6);

        public MemoryField<float> OverallSpeed => Ptr.Field<float>(0x2C8); // The overall speed which is applied to all slots as well as things like particles attached to the owner
        public MemoryField<ushort> BaseOverride => Ptr.Field<ushort>(0x2E6); // Forces base animation when character is in a Normal or AnimLock state
        public MemoryField<ushort> LipsOverride => Ptr.Field<ushort>(0x2E8); // Forces the character lips to play timeline

        // [MemberFunction("0F B7 C2 4C 8B C9 3D 72 02 00 00")]
        // public partial void SetLipsOverrideTimeline(ushort actionTimelineId);

        // [MemberFunction("40 53 48 83 EC 30 48 8B D9 0F 29 74 24 ?? 48 8B 49 08 E8 ?? ?? ?? ?? 0F 28 F0 0F 57 C9 0F 2F F1 0F 86 ?? ?? ?? ?? 48 8B 4B 08 48 8B 01 FF 50 10 83 F8 08 75 60 48 8B 4B 08 48 89 7C 24 ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 74 45 66 83 B8 ?? ?? ?? ?? ?? 74 3B 48 8D 88 ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 2B 0F B7 8F ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 1A 0F B6 40 50 66 0F 6E C0 0F 5B C0 F3 0F 59 C6 0F 28 F0 F3 0F 5E 35 ?? ?? ?? ?? 48 8B 7C 24 ?? F6 83")]
        // public partial bool CalculateAndApplyOverallSpeed(); // Calculates the current overall speed and applies it, returns true if the speed changed

        // [MemberFunction("E8 ?? ?? ?? ?? 48 8D 8F ?? ?? ?? ?? B2 12")]
        // public partial void PlayActionTimeline(ushort introId, ushort loopId = 0, void* a4 = null);
    }
}

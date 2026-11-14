using System;

namespace Simulant.Game.FFCS.Client.Game.InstanceContent
{
    // Client::Game::InstanceContent::PublicContentDirector
    //   Client::Game::InstanceContent::ContentDirector
    //     Client::Game::Event::Director
    //       Client::Game::Event::LuaEventHandler
    //         Client::Game::Event::EventHandler
    public struct PublicContentDirector : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<ContentDirector>
        private ContentDirector ContentDirector => Ptr.As<ContentDirector>();
        #region ContentDirector
        public MemoryField<byte> ContentTypeRowId => ContentDirector.ContentTypeRowId;
        public ContentDirector.MapEffectList MapEffects => ContentDirector.MapEffects;
        public MemoryField<float> ContentTimeLeft => ContentDirector.ContentTimeLeft;

        public uint GetCurrentLevel() => ContentDirector.GetCurrentLevel();
        public uint GetMaxLevel() => ContentDirector.GetMaxLevel();
        public uint GetContentTimeMax() => ContentDirector.GetContentTimeMax();

        #endregion
        private const int ContentDirectorSize = 0xD30;
        public MemoryField<ushort> ContentFinderCondition => Ptr.Field<ushort>(ContentDirectorSize + 0x22);
        public MemoryField<PublicContentDirectorType> Type => Ptr.Field<PublicContentDirectorType>(ContentDirectorSize + 0x30);
        public ContentDirector.MapEffectList ManagedSharedGroups => Ptr.As<ContentDirector.MapEffectList>(ContentDirectorSize + 0x34);
    }

    public enum PublicContentDirectorType : byte
    {
        BondingCeremony = 1,
        TripleTriad = 2,
        Eureka = 3,
        CalamityRetold = 4, // seems to be only for the rising event in 2018
        LeapOfFaith = 5,
        Diadem = 6,
        Bozja = 7,
        Delubrum = 8,
        IslandSanctuary = 9,
        FallGuys = 10,
        OccultCrescent = 11,
    }
}

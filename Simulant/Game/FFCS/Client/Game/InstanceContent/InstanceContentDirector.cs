using System;

namespace Simulant.Game.FFCS.Client.Game.InstanceContent
{
    // Client::Game::InstanceContent::InstanceContentDirector
    //   Client::Game::InstanceContent::ContentDirector
    //     Client::Game::Event::Director
    //       Client::Game::Event::LuaEventHandler
    //         Client::Game::Event::EventHandler
    public struct InstanceContentDirector : IMemoryObject
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
        public MemoryField<InstanceContentType> InstanceContentType => Ptr.Field<InstanceContentType>(0xD30 + 0x9E);
    }

    public enum InstanceContentType : byte
    {
        Raid = 1,
        Dungeon = 2,
        GuildOrder = 3, // Guildhests
        Trial = 4,
        CrystallineConflict = 5,
        Frontlines = 6,
        QuestBattle = 7,
        BeginnerTraining = 8,
        DeepDungeon = 9,
        TreasureHuntDungeon = 10,
        SeasonalDungeon = 11,
        RivalWing = 12,
        MaskedCarnivale = 13,
        Mahjong = 14,
        GoldSaucer = 15, // only used for Air Force One in Gold Saucer
        OceanFishing = 16,
        UnrealTrial = 17,
        TripleTriad = 18,
        VariantDungeon = 19,
        CriterionDungeon = 20,
        AdvancedVariantDungeon = 21,
        Unk22 = 22, // related to XBM
        Unk23 = 23,
    }
}

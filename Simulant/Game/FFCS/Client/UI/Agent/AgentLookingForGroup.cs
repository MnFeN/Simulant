using System;

namespace Simulant.Game.FFCS.Client.UI.Agent
{
    // Client::UI::Agent::AgentLookingForGroup
    public struct AgentLookingForGroup : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        /// <summary>
        /// 上一次查看的招募详情。
        /// </summary>
        public Detailed LastViewedListing
            => Ptr.As<Detailed>(0x2CA0);

        /// <summary>
        /// 当前打开的招募详情的 Content ID。<br />
        /// 仅在招募详情打开期间为非 0。
        /// </summary>
        public ulong ListingContentId
            => Ptr.Read<ulong>(0x3620);

        /// <summary>
        /// 招募详情数据。
        /// </summary>
        public struct Detailed : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            public ulong ListingId
                => Ptr.Read<ulong>(0x00);

            public ulong LeaderAccountId
                => Ptr.Read<ulong>(0x08);

            public ulong LeaderContentId
                => Ptr.Read<ulong>(0x10);

            public DutyCategory Category
                => Ptr.Read<DutyCategory>(0x24);

            public ushort DutyId
                => Ptr.Read<ushort>(0x28);

            public ushort World
                => Ptr.Read<ushort>(0x36);

            public Objective Objective
                => Ptr.Read<Objective>(0x38);

            public byte BeginnerFriendly
                => Ptr.Read<byte>(0x39);

            public CompletionStatus CompletionStatus
                => Ptr.Read<CompletionStatus>(0x3A);

            public DutyFinderSetting DutyFinderSettingFlags
                => Ptr.Read<DutyFinderSetting>(0x3B);

            public LootRule LootRule
                => Ptr.Read<LootRule>(0x3C);

            public uint LastPatchHotfixTimestamp
                => Ptr.Read<uint>(0x40);

            public uint TimeLeft
                => Ptr.Read<uint>(0x48);

            public ushort AvgItemLv
                => Ptr.Read<ushort>(0x50);

            public ushort HomeWorld
                => Ptr.Read<ushort>(0x52);

            public ushort CurrentWorld
                => Ptr.Read<ushort>(0x54);

            public Language LeaderClientLanguage
                => Ptr.Read<Language>(0x56);

            public Language LanguageFlags
                => Ptr.Read<Language>(0x57);

            public byte TotalSlots
                => Ptr.Read<byte>(0x58);

            public byte SlotsFilled
                => Ptr.Read<byte>(0x59);

            public JoinCondition JoinConditionFlags
                => Ptr.Read<JoinCondition>(0x5B);

            public bool IsAlliance
                => Ptr.Read<byte>(0x5D) != 0;

            public byte NumberOfParties
                => Ptr.Read<byte>(0x5E);

            /// <summary>
            /// 取得指定招募槽位玩家的 Content ID。
            /// </summary>
            public ulong GetMemberContentId(int index)
            {
                if (index < 0 || index >= 48)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return Ptr.Read<ulong>(0x60 + index * 8);
            }

            /// <summary>
            /// 取得指定招募槽位的 SlotFlags。
            /// </summary>
            public ulong GetSlotFlags(int index)
            {
                if (index < 0 || index >= 48)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return Ptr.Read<ulong>(0x1E0 + index * 8);
            }

            /// <summary>
            /// 取得指定招募槽位的职业 ID。
            /// </summary>
            public byte GetJob(int index)
            {
                if (index < 0 || index >= 48)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return Ptr.Read<byte>(0x360 + index);
            }
        }

        [Flags]
        public enum DutyCategory : uint
        {
            None = 0,
            Roulette = 1 << 1,
            Dungeons = 1 << 2,
            GuildQuests = 1 << 3,
            Trials = 1 << 4,
            Raids = 1 << 5,
            HighEndDuty = 1 << 6,
            PvP = 1 << 7,
            GoldSaucer = 1 << 8,
            FATEs = 1 << 9,
            TreasureHunts = 1 << 10,
            TheHunt = 1 << 11,
            GatheringForays = 1 << 12,
            DeepDungeons = 1 << 13,
            FieldOperations = 1 << 14,
            VCDungeonFinder = 1 << 15
        }

        [Flags]
        public enum Objective : byte
        {
            None = 1 << 0,
            DutyCompletion = 1 << 1,
            Practice = 1 << 2,
            Loot = 1 << 3
        }

        [Flags]
        public enum CompletionStatus : byte
        {
            None = 1 << 0,
            DutyComplete = 1 << 1,
            DutyIncomplete = 1 << 2,
            DutyCompleteWeeklyUnclaimed = 1 << 3
        }

        [Flags]
        public enum DutyFinderSetting : byte
        {
            None = 0,
            UnrestrictedParty = 1,
            MinimumIL = 2,
            SilenceEcho = 4
        }

        public enum LootRule : byte
        {
            Normal = 0,
            GreedOnly = 1,
            Lootmaster = 2
        }

        [Flags]
        public enum Language : byte
        {
            Japanese = 1,
            English = 2,
            German = 4,
            French = 8
        }

        [Flags]
        public enum JoinCondition : byte
        {
            None = 0,
            DataCenter = 1 << 0,
            Private = 1 << 1,
            AllianceRaid = 1 << 2,
            World = 1 << 3,
            OnePlayerPerJob = 1 << 5
        }
    }
}
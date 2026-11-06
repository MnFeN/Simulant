using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Simulant.Replay
{
    /// <summary>区分明确选择版本后的匹配、候选与冲突，避免把长度相同当成同一种包。</summary>
    public enum ReplayPacketConfidence
    {
        Unknown,
        LengthCandidate,
        OpcodeCandidate,
        SelectedProfileCandidate,
        SelectedProfileMatch,
        LengthConflict,
        RecordMarker
    }

    public sealed class ReplayPacketClassification
    {
        internal ReplayPacketClassification(string name, ReplayPacketConfidence confidence,
            IEnumerable<string> candidates, string evidence, string profile, string sourceUrl)
        {
            Name = name ?? string.Empty;
            Confidence = confidence;
            Candidates = new ReadOnlyCollection<string>(candidates.Distinct().ToArray());
            Evidence = evidence;
            Profile = profile ?? string.Empty;
            SourceUrl = sourceUrl;
        }

        /// <summary>仅明确选择版本且长度一致，或回放专用记录通过长度检查时填写。</summary>
        public string Name { get; }
        public ReplayPacketConfidence Confidence { get; }
        public IReadOnlyList<string> Candidates { get; }
        public string Evidence { get; }
        public string Profile { get; }
        public string SourceUrl { get; }
    }

    /// <summary>离线包目录；版本来源固定，不联网、不访问客户端，也不自动猜测内部构建号。</summary>
    public static class ReplayPacketCatalog
    {
        public const string China757Profile = "CN7.57";
        public const string OpcodeSourceUrl =
            "https://github.com/karashiiro/FFXIVOpcodes/blob/d85eaebec24b08948550e09c005fe5860d17c3f2/FFXIVOpcodes/Ipcs_cn.cs";
        public const string SupplementalOpcodeSourceUrl =
            "https://github.com/zhyupe/ffxiv-opcode-worker/blob/06df8d0ddf52e230c73826006cfbc50e6c2891a6/json/7.57.json";
        public const string BuildSourceUrl =
            "https://github.com/zhyupe/ffxiv-opcode-worker/blob/06df8d0ddf52e230c73826006cfbc50e6c2891a6/reports/7.57.json";
        public const string LayoutSourceUrl =
            "https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Network/ServerIPC.cs";
        public const string RecordLayoutSourceUrl =
            "https://github.com/gamous/FFXIVNetworkOpcodes/blob/cd1a8b5704b92c60971fbd0189616691e8753f89/output/Global_2023.03.24/opcodes_record.json";
        public const string RecordMarkerSourceUrl =
            "https://github.com/gamous/FFXIVNetworkOpcodes/blob/cd1a8b5704b92c60971fbd0189616691e8753f89/script/replay_updater_online.py";
        public const string SpawnLayoutSourceUrl =
            "https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Network/SpawnPackets.cs";
        public const string CountdownLayoutSourceUrl =
            "https://github.com/OverlayPlugin/OverlayPlugin/blob/5b16a642351e588fc729f3ea9cf345cb0862f9d4/OverlayPlugin.Core/NetworkProcessors/LineCountdown.cs";

        // 只维护有资料支撑的正文长度。回放长度包含正文末尾对齐，不能直接用精简的本地结构大小。
        // 旧版回放表用于已知稳定包的对齐；生成包使用当前 FFCS，名单使用项目已核对的原生收包布局。
        private static readonly Dictionary<string, int[]> Lengths = new Dictionary<string, int[]>
        {
            { "ActorMove", new[] { 0x10 } },
            { "ActorSetPos", new[] { 0x18 } },
            { "ActorCast", new[] { 0x20 } },
            { "ActorControl", new[] { 0x18 } },
            { "ActorControlSelf", new[] { 0x20 } },
            { "ActorControlTarget", new[] { 0x20 } },
            { "ActionEffect1", new[] { 0x78 } },
            { "ActionEffect8", new[] { 0x278 } },
            { "ActionEffect16", new[] { 0x4B8 } },
            { "ActionEffect24", new[] { 0x6F8 } },
            { "ActionEffect32", new[] { 0x938 } },
            { "StatusEffectList", new[] { 0x180 } },
            { "StatusEffectListDouble", new[] { 0x2E8 } },
            { "StatusEffectListPlayer", new[] { 0x168 } },
            { "StatusEffectListPlayerDouble", new[] { 0x2D0 } },
            { "EffectResult", new[] { 0x60 } },
            { "EffectResult4", new[] { 0x168 } },
            { "EffectResult8", new[] { 0x2C8 } },
            { "EffectResult16", new[] { 0x588 } },
            { "EffectResultBasic", new[] { 0x18 } },
            { "EffectResultBasic4", new[] { 0x48 } },
            { "EffectResultBasic8", new[] { 0x88 } },
            { "EffectResultBasic16", new[] { 0x108 } },
            { "EffectResultBasic32", new[] { 0x208 } },
            { "EffectResultBasic64", new[] { 0x408 } },
            { "UpdateHpMpTp", new[] { 0x08 } },
            { "UpdateHate", new[] { 0x48 } },
            { "UpdateHater", new[] { 0x108 } },
            { "MapEffect", new[] { 0x10 } },
            { "DirectorVars", new[] { 0x18 } },
            { "ContentDirectorSync", new[] { 0x88 } },
            { "PlayerSpawn", new[] { 0x298 } },
            { "NpcSpawn", new[] { 0x290 } },
            { "ObjectSpawn", new[] { 0x40 } },
            { "DespawnCharacter", new[] { 0x08 } },
            { "ObjectDespawn", new[] { 0x08 } },
            { "UpdateParty", new[] { 0xE58 } },
            { "PlaceFieldMarker", new[] { 0x10 } },
            { "PlaceFieldMarkerPreset", new[] { 0x68 } },
            { "NpcYell", new[] { 0x20 } },
            // 7.3 起倒计时包头部增加 16 字节；取消包也有同样变化。
            { "Countdown", new[] { 0x40 } },
            { "CountdownCancel", new[] { 0x38 } },
            { "WeatherChange", new[] { 0x08 } },
            { "ActorGauge", new[] { 0x10 } },
            { "FirstAttack", new[] { 0x10 } },
            { "PrepareZoning", new[] { 0x10 } },
            { "EventStart", new[] { 0x18 } },
            { "EventFinish", new[] { 0x10 } },
            { "EventPlay", new[] { 0x28 } },
            { "EventPlay4", new[] { 0x30 } },
            { "EventPlay8", new[] { 0x40 } },
            { "EventPlay16", new[] { 0x60 } },
            { "EventPlay32", new[] { 0xA0 } },
            { "EventPlay64", new[] { 0x120 } },
            { "EventPlay128", new[] { 0x220 } },
            { "EventPlay255", new[] { 0x418 } },
            { "BattleTalk2", new[] { 0x28 } },
            { "BattleTalk4", new[] { 0x30 } },
            { "BattleTalk8", new[] { 0x40 } },
            { "BalloonTalk2", new[] { 0x30 } },
            { "BalloonTalk4", new[] { 0x38 } },
            { "BalloonTalk8", new[] { 0x48 } },
            { "SystemLogMessage", new[] { 0x18 } },
            { "SystemLogMessage2", new[] { 0x20 } },
            { "SystemLogMessage4", new[] { 0x30 } },
            { "SystemLogMessage8", new[] { 0x50 } },
            { "SystemLogMessage16", new[] { 0x90 } },
        };

        private static readonly Dictionary<int, string[]> NamesByLength = Lengths
            .SelectMany(pair => pair.Value.Select(length => new { length, pair.Key }))
            .GroupBy(item => item.length)
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.Key).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        // CN 7.57 的服务端区域消息；统一少量同义名称，保留 opcode 来源。UpdateParty 来自补充来源。
        private static readonly Dictionary<ushort, string> China757Opcodes = new Dictionary<ushort, string>
        {
            { 0x03E0, "ActorSetPos" },
            { 0x01E6, "ActorControlSelf" },
            { 0x0276, "ActionEffect32" },
            { 0x0198, "ActorControl" },
            { 0x0178, "AirshipTimers" },
            { 0x02EC, "ActorGauge" },
            { 0x013F, "ActionEffect8" },
            { 0x0125, "AirshipExplorationResult" },
            { 0x0301, "ActorCast" },
            { 0x00C8, "ActorControlTarget" },
            { 0x0389, "AirshipStatus" },
            { 0x0217, "ActorMove" },
            { 0x009E, "ActionEffect24" },
            { 0x03A5, "AirshipStatusList" },
            { 0x0390, "ActionEffect16" },
            { 0x00A2, "BattleTalk2" },
            { 0x025E, "BlackList" },
            { 0x0194, "BattleTalk8" },
            { 0x0267, "BalloonTalk4" },
            { 0x02F2, "BossSpawn" },
            { 0x023A, "BalloonTalk2" },
            { 0x02C9, "BalloonTalk8" },
            { 0x0381, "BattleTalk4" },
            { 0x038E, "CraftingLog" },
            { 0x02ED, "CFCancel" },
            { 0x02EE, "CFRegistered" },
            { 0x01A4, "Countdown" },
            { 0x00CE, "CFPreferredRole" },
            { 0x012C, "CFDutyInfo" },
            { 0x02DB, "ContainerInfo" },
            { 0x031E, "CurrencyCrystalInfo" },
            { 0x0173, "CountdownCancel" },
            { 0x028A, "CFNotify" },
            { 0x020A, "CEDirector" },
            { 0x01DC, "DespawnCharacter" },
            { 0x0366, "DesynthResult" },
            { 0x01DB, "EventPlay16" },
            { 0x0277, "EffectResultBasic4" },
            { 0x02B4, "MapEffect4" },
            { 0x0286, "EventStart" },
            { 0x019C, "Examine" },
            { 0x0189, "EffectResultBasic64" },
            { 0x01A6, "MapEffect" },
            { 0x01CD, "ExamineSearchInfo" },
            { 0x01A8, "EventFinish" },
            { 0x028C, "EventPlay128" },
            { 0x01CB, "EventPlay64" },
            { 0x01C5, "EventPlay32" },
            { 0x02E3, "EffectResult4" },
            { 0x0323, "MapEffect8" },
            { 0x00BD, "ActionEffect1" },
            { 0x0162, "EffectResult16" },
            { 0x009B, "EventPlay8" },
            { 0x0154, "EffectResultBasic8" },
            { 0x00CD, "EffectResultBasic16" },
            { 0x0137, "MapEffect12" },
            { 0x0256, "EventPlay" },
            { 0x02F6, "EventPlay255" },
            { 0x0098, "ExamineFreeCompanyInfo" },
            { 0x03B8, "EffectResult8" },
            { 0x0324, "EventPlay4" },
            { 0x0108, "EffectResultBasic32" },
            { 0x0225, "EffectResultBasic" },
            { 0x00F2, "EffectResult" },
            { 0x0368, "FreeCompanyDialog" },
            { 0x0176, "FirstAttack" },
            { 0x03B9, "FreeCompanyInfo" },
            { 0x03CB, "FishCaught" },
            { 0x035B, "FashionReport" },
            { 0x018E, "GCAffiliation" },
            { 0x03D9, "GatheringLog" },
            { 0x0153, "HousingWardInfo" },
            { 0x029F, "IslandWorkshopFavors" },
            { 0x02F5, "IslandWorkshopDemandResearch" },
            { 0x031C, "IslandWorkshopGranaryResult" },
            { 0x032D, "InventoryTransaction" },
            { 0x0291, "InventoryActionAck" },
            { 0x02CC, "InitZone" },
            { 0x0245, "Init" },
            { 0x0183, "ItemMarketBoardInfo" },
            { 0x006F, "InitSearchInfo" },
            { 0x012F, "ItemMarketBoardSummary" },
            { 0x00D1, "ItemInfo" },
            { 0x0199, "IslandWorkshopSupplyDemand" },
            { 0x0088, "InventoryTransactionFinish" },
            { 0x01D3, "LogMessage" },
            { 0x030D, "Logout" },
            { 0x0091, "MapUpdate" },
            { 0x01B6, "MarketBoardSale" },
            { 0x0166, "MapUpdate4" },
            { 0x02DC, "MapUpdate128" },
            { 0x0161, "MapUpdate8" },
            { 0x010D, "MarketBoardItemListingCount" },
            { 0x03C0, "MarketBoardSearchResult" },
            { 0x011E, "MarketBoardItemListingHistory" },
            { 0x03D5, "ModelEquip" },
            { 0x0294, "MapUpdate32" },
            { 0x01FE, "MiniCactpotInit" },
            { 0x0234, "MarketBoardItemListing" },
            { 0x03D7, "MapUpdate16" },
            { 0x0262, "MarketBoardPurchase" },
            { 0x026D, "MapUpdate64" },
            { 0x0065, "NpcYell" },
            { 0x0114, "NpcSpawn" },
            { 0x0193, "ObjectSpawn" },
            { 0x02D2, "ObjectDespawn" },
            { 0x015B, "PlayerRemovedFromBlacklist" },
            { 0x0302, "PlayerSetup" },
            { 0x02C8, "PlayerRetainerInfo" },
            { 0x0192, "PartyFinderList" },
            { 0x01BD, "PlayerTitleList" },
            { 0x00B5, "PlayerClassInfo" },
            { 0x0281, "PlaceFieldMarker" },
            { 0x027A, "PlayerBlueMageActions" },
            { 0x03AF, "PlayerAddedToBlacklist" },
            { 0x0265, "PlayerSpawn" },
            { 0x007A, "Playtime" },
            { 0x01F8, "PlaceFieldMarkerPreset" },
            { 0x0251, "PlayerStats" },
            { 0x03DC, "PartyMessage" },
            { 0x0326, "PlayerStateFlags" },
            { 0x0130, "PrepareZoning" },
            { 0x00F9, "RetainerState" },
            { 0x0340, "RetainerSaleHistory" },
            { 0x0112, "RSVData" },
            { 0x0399, "RSFData" },
            { 0x0078, "RetainerSummary" },
            { 0x0201, "ResultDialog" },
            { 0x01D2, "RetainerInformation" },
            { 0x037B, "SocialRequestResponse" },
            { 0x0097, "StatusEffectListPlayer" },
            { 0x038C, "SocialList" },
            { 0x01F2, "SubmarineExplorationResult" },
            { 0x02F3, "SystemLogMessage4" },
            { 0x0290, "ServerNoticeShort" },
            { 0x0367, "SubmarineStatusList" },
            { 0x00E0, "SocialMessage2" },
            { 0x0360, "SystemLogMessage2" },
            { 0x039B, "StatusEffectListDouble" },
            { 0x025A, "StatusEffectListEureka" },
            { 0x00EB, "SystemLogMessage" },
            { 0x0328, "StatusEffectListPlayerDouble" },
            { 0x0231, "SystemLogMessage8" },
            { 0x016B, "SocialMessage" },
            { 0x02CE, "StatusEffectList" },
            { 0x02DA, "SubmarineProgressionStatus" },
            { 0x013D, "SetOnlineStatus" },
            { 0x022B, "StatusEffectListOccult" },
            { 0x0139, "ServerNotice" },
            { 0x021B, "StatusEffectListBozja" },
            { 0x0122, "SystemLogMessage16" },
            { 0x039C, "SubmarineTimers" },
            { 0x03BA, "UpdateDutyRecastTimes" },
            { 0x01F4, "UpdateAllianceNormal" },
            { 0x03B7, "UpdateHpMpTp" },
            { 0x026E, "UpdateClassInfoBozja" },
            { 0x0250, "UpdateClassInfo" },
            { 0x02FB, "UpdateClassInfoEureka" },
            { 0x0275, "UpdateAllianceSmallMemberPositions" },
            { 0x01E1, "UpdateAllianceSmall" },
            { 0x00AB, "UpdateHater" },
            { 0x0365, "UpdateSearchInfo" },
            { 0x0354, "UpdateDutyRecastTimes5" },
            { 0x0299, "UpdateClassInfoOccult" },
            { 0x02A1, "UpdateHate" },
            { 0x02A3, "UpdateInventorySlot" },
            { 0x0311, "UpdateAllianceNormalMemberPositions" },
            { 0x008B, "UpdateRecastTimes" },
            { 0x0134, "UpdatePartyMemberPositions" },
            { 0x0254, "WeatherChange" },
            { 0x016A, "UpdateParty" },
        };

        public static ReplayPacketClassification Classify(uint gameVersion, ushort opcode,
            int payloadLength, string profile = null)
        {
            if (payloadLength < 0 || payloadLength > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(payloadLength), "回放包正文长度必须在 0-65535 之间。");
            if (!string.IsNullOrEmpty(profile) && profile != China757Profile)
                throw new ArgumentException("未支持的回放包版本配置：" + profile, nameof(profile));

            string[] lengthCandidates;
            if (!NamesByLength.TryGetValue(payloadLength, out lengthCandidates))
                lengthCandidates = new string[0];

            // F003 是回放专用的标识偏移记录，不是某一版本的网络 opcode。
            if (opcode == 0xF003)
                return new ReplayPacketClassification(payloadLength == 4 ? "IDScramble" : null,
                    payloadLength == 4 ? ReplayPacketConfidence.RecordMarker : ReplayPacketConfidence.LengthConflict,
                    new[] { "IDScramble" },
                    payloadLength == 4 ? "回放专用记录 0xF003，正文长度为 4。" : "回放专用记录 0xF003 的正文长度应为 4。",
                    null, RecordMarkerSourceUrl);

            if (opcode == 0xF001 || opcode == 0xF002)
                return new ReplayPacketClassification(null, ReplayPacketConfidence.OpcodeCandidate,
                    new[] { opcode == 0xF001 ? "RSVData" : "RSFData" },
                    "回放专用记录；正文布局尚未核验，仅保留类型候选。",
                    null, RecordMarkerSourceUrl);

            string mappedName;
            China757Opcodes.TryGetValue(opcode, out mappedName);
            int[] expectedLengths = null;
            if (mappedName != null)
                Lengths.TryGetValue(mappedName, out expectedLengths);
            bool lengthMatches = expectedLengths != null && expectedLengths.Contains(payloadLength);
            string buildEvidence = "回放内部构建号 0x" + gameVersion.ToString("X8", CultureInfo.InvariantCulture)
                + " 尚未建立与版本配置的自动对应关系。";

            if (!string.IsNullOrEmpty(profile) && mappedName != null)
            {
                string evidence = "明确指定 CN 7.57；" + buildEvidence;
                string source = mappedName == "UpdateParty" ? SupplementalOpcodeSourceUrl : OpcodeSourceUrl;
                if (lengthMatches)
                    return new ReplayPacketClassification(mappedName, ReplayPacketConfidence.SelectedProfileMatch,
                        new[] { mappedName }, evidence + " opcode 与已知正文长度一致。",
                        profile, source);
                if (expectedLengths != null)
                    return new ReplayPacketClassification(null, ReplayPacketConfidence.LengthConflict,
                        new[] { mappedName }.Concat(lengthCandidates),
                        evidence + " opcode 对应 " + mappedName + "，但正文长度应为 "
                        + string.Join("/", expectedLengths.Select(value => value.ToString(CultureInfo.InvariantCulture)))
                        + "，实为 " + payloadLength.ToString(CultureInfo.InvariantCulture) + "。",
                        profile, source);
                return new ReplayPacketClassification(null, ReplayPacketConfidence.SelectedProfileCandidate,
                    new[] { mappedName }.Concat(lengthCandidates),
                    evidence + " opcode 存在于来源表，正文长度尚未核验。",
                    profile, source);
            }

            if (string.IsNullOrEmpty(profile) && mappedName != null && (lengthMatches || expectedLengths == null))
                return new ReplayPacketClassification(null, ReplayPacketConfidence.OpcodeCandidate,
                    new[] { mappedName }.Concat(lengthCandidates),
                    "未指定版本；CN 7.57 表中有相同 opcode"
                    + (lengthMatches ? "，正文长度也一致；" : "，正文长度尚未核验；")
                    + buildEvidence + "候选不能作为已确认事件。",
                    null, mappedName == "UpdateParty" ? SupplementalOpcodeSourceUrl : OpcodeSourceUrl);

            return new ReplayPacketClassification(null,
                lengthCandidates.Length == 0 ? ReplayPacketConfidence.Unknown : ReplayPacketConfidence.LengthCandidate,
                lengthCandidates,
                lengthCandidates.Length == 0 ? "目录中无对应候选，保留原始 opcode 与正文。" :
                    "仅正文长度相同；目录并不穷尽所有消息，单个候选也不能确认为包类型。",
                profile, LayoutSourceUrl + "；" + RecordLayoutSourceUrl);
        }
    }
}


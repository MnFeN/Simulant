namespace Simulant.Game
{
    /// <summary>ActorControl 系列的子类别，供原生分发函数使用。</summary>
    /// <remarks>
    /// 核对 BossMod、Sapphire、FFXIV-Packet-Dissector 与 Machina；来源与差异见 docs/Entities.md。
    /// </remarks>
    public enum ActorControlCategory : ushort
    {
        /// <summary>收起或拔出武器；a1：0 收起、1 拔出。</summary>
        ToggleWeapon = 0x0000,
        /// <summary>自动攻击开关；a1：0 关闭、1 开启。</summary>
        AutoAttack = 0x0001,
        /// <summary>设置角色状态；具体参数按客户端版本核对。</summary>
        SetStatus = 0x0002,
        /// <summary>开始施法控制。</summary>
        CastStart = 0x0003,
        /// <summary>战斗状态开关；a1：0 脱战、1 入战。Sapphire 名称为 SetBattle。</summary>
        ToggleAggro = 0x0004,
        /// <summary>变更职业；a1 为 ClassJob 行号。</summary>
        ClassJobChange = 0x0005,
        /// <summary>死亡通知；Sapphire 名称为 DefeatMsg。</summary>
        Death = 0x0006,
        /// <summary>获得经验的提示。</summary>
        GainExpMsg = 0x0007,
        /// <summary>职业信息更新；来自 Sapphire 的定义。</summary>
        ClassJobUpdate = 0x0009,
        /// <summary>升级表现。</summary>
        LevelUpEffect = 0x000A,
        /// <summary>经验连锁提示。</summary>
        ExpChainMsg = 0x000C,
        /// <summary>血量相关状态；参数未确认，不能代替完整血量同步包。</summary>
        HpSetStat = 0x000D,
        /// <summary>死亡动画。</summary>
        DeathAnimation = 0x000E,
        /// <summary>中断施法；其他来源也称 CastInterrupt 或 CancelAbility。</summary>
        CancelCast = 0x000F,
        /// <summary>冷却组进度；a1：组号，a2：已经过时间，a3：总时间，剩余量为 a3 − a2。</summary>
        RecastDetails = 0x0010,
        /// <summary>技能开始的冷却通知；a1 的含义未完全确认，a2：技能 ID，a3：冷却毫秒数除以 10。通常来自 ActorControlSelf。</summary>
        Cooldown = 0x0011,
        /// <summary>状态获得提示和受击特效；此类别本身不更新角色状态列表。</summary>
        GainEffect = 0x0014,
        /// <summary>状态消失提示。</summary>
        LoseEffect = 0x0015,
        /// <summary>状态变化提示。</summary>
        UpdateEffect = 0x0016,
        /// <summary>旧版持续治疗／伤害飘字类别。另保留 7.10 国际服定义的 HoT、DoT，见来源说明。</summary>
        HotDot = 0x0017,
        /// <summary>休息经验更新。</summary>
        UpdateRestedExp = 0x0018,
        /// <summary>用途未确认；保留 Sapphire 的原名称。</summary>
        Unk2 = 0x0019,
        /// <summary>逃离相关控制；参数未确认。</summary>
        Flee = 0x001B,
        /// <summary>疑似可见性控制；参数含生成后的延迟、可见状态及编号，含义待核对。</summary>
        UnkVisControl = 0x001E,
        /// <summary>疑似动画控制；保留 Sapphire 的原名称。</summary>
        Unk3 = 0x0020,
        /// <summary>头顶机制点名；a1：标记 ID。其他参数按具体客户端调用核对。</summary>
        TargetIcon = 0x0022,
        /// <summary>连线控制。</summary>
        Tether = 0x0023,
        /// <summary>生成表现。</summary>
        SpawnEffect = 0x0025,
        /// <summary>隐身或可见性控制。</summary>
        ToggleInvisible = 0x0026,
        /// <summary>死亡后的淡出表现。</summary>
        DeadFadeOut = 0x0027,
        /// <summary>技能解锁标志；Sapphire 名称为 SetRewardFlag。</summary>
        ToggleActionUnlock = 0x0029,
        /// <summary>更新界面经验。</summary>
        UpdateUiExp = 0x002B,
        /// <summary>受伤提示；Sapphire 名称为 SetFallDamage。</summary>
        DmgTakenMsg = 0x002D,
        /// <summary>取消连线。</summary>
        TetherCancel = 0x002F,
        /// <summary>切换目标；目标实体 ID 通过 HandleActorControl 的 targetId 传递，不是 a1。</summary>
        SetTarget = 0x0032,
        /// <summary>可选中开关；a1：0 不可选中、1 可选中。旧来源也称 ToggleNameHidden。</summary>
        Targetable = 0x0036,
        /// <summary>模型动画状态；a1：动画组索引（0 或 1），a2：组内动画索引（0 至 7）。</summary>
        SetAnimationState = 0x003E,
        /// <summary>模型状态；a1：ModelState 行号。</summary>
        SetModelState = 0x003F,
        /// <summary>开始极限技的控制。</summary>
        LimitBreakStart = 0x0047,
        /// <summary>小队极限技开始通知。</summary>
        LimitBreakPartyStart = 0x0048,
        /// <summary>气泡文字。</summary>
        BubbleText = 0x0049,
        /// <summary>伤害表现。</summary>
        DamageEffect = 0x0050,
        /// <summary>复活动画。</summary>
        RaiseAnimation = 0x0051,
        /// <summary>宝箱屏幕提示。</summary>
        TreasureScreenMsg = 0x0057,
        /// <summary>设置所属实体。</summary>
        SetOwnerId = 0x0059,
        /// <summary>装备修理提示。</summary>
        ItemRepairMsg = 0x005C,
        /// <summary>名称控制；不能据此假定 a1 能传字符串。</summary>
        SetName = 0x0062,
        /// <summary>青魔法习得表现；a1：技能 ID，a2：0 仅文字、1 文字与动画。不会实际解锁技能，参数待实机核对。</summary>
        BluActionLearn = 0x0063,
        /// <summary>初始化副本或事件控制器。</summary>
        DirectorInit = 0x0064,
        /// <summary>清理副本或事件控制器。</summary>
        DirectorClear = 0x0065,
        /// <summary>理符开始表现。</summary>
        LeveStartAnim = 0x0066,
        /// <summary>理符开始错误。</summary>
        LeveStartError = 0x0067,
        /// <summary>设置 GameObject.EventState；也作为 SetEventState 使用。</summary>
        DirectorEObjMod = 0x006A,
        /// <summary>副本或事件控制器更新。</summary>
        DirectorUpdate = 0x006D,
        /// <summary>设置临危受命状态。</summary>
        SetFateState = 0x0074,
        /// <summary>获得物品提示；Sapphire 名称为 ObtainFateItem。</summary>
        ItemObtainMsg = 0x0075,
        /// <summary>临危受命请求失败提示。</summary>
        FateReqFailMsg = 0x0076,
        /// <summary>副本任务屏幕提示。</summary>
        DutyQuestScreenMsg = 0x007B,
        /// <summary>临危受命位置。</summary>
        FatePosition = 0x007D,
        /// <summary>设置内容通关标记。</summary>
        SetContentClearFlag = 0x0082,
        /// <summary>设置内容开放标记。</summary>
        SetContentOpenFlag = 0x0083,
        /// <summary>获得物品的图标提示。</summary>
        ItemObtainIcon = 0x0084,
        /// <summary>临危受命物品失败提示。</summary>
        FateItemFailMsg = 0x0085,
        /// <summary>临危受命失败提示；Sapphire 名称为 ItemFailMsg。</summary>
        FateFailMsg = 0x0086,
        /// <summary>技能习得提示之一。</summary>
        ActionLearnMsg1 = 0x0087,
        /// <summary>事件位置相关控制；Sapphire 名称为 SetRollbackFlag，语义差异待核对。</summary>
        FreeEventPos = 0x008A,
        /// <summary>同步临危受命。</summary>
        FateSync = 0x008B,
        /// <summary>移动类型；Sapphire 记录为 0 普通、1 玩家对战。</summary>
        MoveType = 0x008E,
        /// <summary>每日任务种子；a1：种子值。</summary>
        DailyQuestSeed = 0x0090,
        /// <summary>设置临危受命进度。</summary>
        SetFateProgress = 0x009B,
        /// <summary>切换背景音乐。</summary>
        SetBGM = 0x00A1,
        /// <summary>风脉解锁提示。</summary>
        UnlockAetherCurrentMsg = 0x00A4,
        /// <summary>移除名称显示。</summary>
        RemoveName = 0x00A8,
        /// <summary>画面淡出。</summary>
        ScreenFadeOut = 0x00AA,
        /// <summary>目标特效。</summary>
        TargetVFX = 0x00B8,
        /// <summary>入场表现；Sapphire 名称为 Appear。</summary>
        ZoneIn = 0x00C8,
        /// <summary>使用默认位置入场。</summary>
        ZoneInDefaultPos = 0x00C9,
        /// <summary>传送开始。</summary>
        TeleportStart = 0x00CB,
        /// <summary>受邀传送控制。</summary>
        OnInvitationTelepo = 0x00CC,
        /// <summary>传送完成。</summary>
        TeleportDone = 0x00CD,
        /// <summary>传送后的画面淡出；Sapphire 名称为 TownTranslate。</summary>
        TeleportDoneFadeOut = 0x00CE,
        /// <summary>区域移动屏幕提示；Sapphire 名称为 WarpStart。</summary>
        DespawnZoneScreenMsg = 0x00CF,
        /// <summary>副本实例选择窗口。</summary>
        InstanceSelectDlg = 0x00D2,
        /// <summary>实体消失表现。</summary>
        ActorDespawnEffect = 0x00D4,
        /// <summary>机关跳跃，通常来自 ActorControlSelf。a1：打包的平面坐标，a2：高度编码；坐标解码为编码值 × 0.0305180438 − 1000。a3 低 8 位为 GimmickJump 行号，0x10000（第 16 位）为移动标志；a4 为移动序号。来自用户提供的客户端观察。</summary>
        GimmickJump = 0x00DC,
        /// <summary>可变参数击退，通常来自 ActorControlSelf。a1：压缩角度，a2：距离（米）× 10000，a3：速度相关参数（比例未确认）。普通技能击退还可能由 ActionEffect 表达。</summary>
        KnockBack = 0x00E1,
        /// <summary>强制移动。</summary>
        ForcedMovement = 0x00E2,
        // 用户附注：0xEC 疑似设置移动相关标志的低两位，0x107 疑似写入全局布尔标志；语义未确认，暂不命名。
        /// <summary>搭档解锁。</summary>
        CompanionUnlock = 0x00FD,
        /// <summary>获得陆行鸟装甲。</summary>
        ObtainBarding = 0x00FE,
        /// <summary>装备陆行鸟装甲。</summary>
        EquipBarding = 0x00FF,
        /// <summary>搭档提示之一。</summary>
        CompanionMsg1 = 0x0102,
        /// <summary>搭档提示之二。</summary>
        CompanionMsg2 = 0x0103,
        /// <summary>显示宠物热键栏。</summary>
        ShowPetHotbar = 0x0104,
        /// <summary>技能习得提示。</summary>
        ActionLearnMsg = 0x0109,
        /// <summary>实体淡出。</summary>
        ActorFadeOut = 0x010A,
        /// <summary>实体淡入。</summary>
        ActorFadeIn = 0x010B,
        /// <summary>撤退提示。</summary>
        WithdrawMsg = 0x010C,
        /// <summary>搭档指令。</summary>
        OrderCompanion = 0x010D,
        /// <summary>搭档开关。</summary>
        ToggleCompanion = 0x010E,
        /// <summary>搭档技能习得。</summary>
        LearnCompanion = 0x010F,
        /// <summary>淡出相关控制；保留来源名称，详细用途待确认。</summary>
        ActorFateOut1 = 0x0110,
        /// <summary>表情动作。</summary>
        Emote = 0x0122,
        /// <summary>中断表情动作。</summary>
        EmoteInterrupt = 0x0123,
        /// <summary>中断持续表情模式。</summary>
        EmoteModeInterrupt = 0x0124,
        /// <summary>非立即中断持续表情模式。</summary>
        EmoteModeInterruptNonImmediate = 0x0125,
        /// <summary>切换姿势。</summary>
        SetPose = 0x0127,
        /// <summary>来源名称存在差异：Sapphire 称 CraftingUnk。其记录 a1=18/19 为简易制作结果（19 为高品质），a2 为物品 ID，a3 为数量；不能直接假定所有版本都用于钓鱼。</summary>
        FishingLightChange = 0x012C,
        /// <summary>采集感知提示。</summary>
        GatheringSenseMsg = 0x0130,
        /// <summary>小队提示。</summary>
        PartyMsg = 0x0131,
        /// <summary>采集感知提示之一。</summary>
        GatheringSenseMsg1 = 0x0132,
        /// <summary>采集感知提示之二。</summary>
        GatheringSenseMsg2 = 0x0138,
        /// <summary>钓鱼提示。</summary>
        FishingMsg = 0x0140,
        /// <summary>钓鱼累计数量。</summary>
        FishingTotalFishCaught = 0x0142,
        /// <summary>鱼饵提示。</summary>
        FishingBaitMsg = 0x0145,
        /// <summary>钓鱼距离提示。</summary>
        FishingReachMsg = 0x0147,
        /// <summary>钓鱼失败提示。</summary>
        FishingFailMsg = 0x0148,
        /// <summary>每周周期更新时间。</summary>
        WeeklyIntervalUpdateTime = 0x0150,
        /// <summary>魔晶石精制提示。</summary>
        MateriaConvertMsg = 0x015E,
        /// <summary>魔晶石镶嵌成功。</summary>
        MeldSuccessMsg = 0x015F,
        /// <summary>魔晶石镶嵌失败。</summary>
        MeldFailMsg = 0x0160,
        /// <summary>魔晶石镶嵌模式开关。</summary>
        MeldModeToggle = 0x0161,
        /// <summary>以太恢复提示。</summary>
        AetherRestoreMsg = 0x0163,
        /// <summary>染色提示。</summary>
        DyeMsg = 0x0168,
        /// <summary>部队队徽开关提示。</summary>
        ToggleCrestMsg = 0x016A,
        /// <summary>批量部队队徽开关提示。</summary>
        ToggleBulkCrestMsg = 0x016B,
        /// <summary>魔晶石取出提示。</summary>
        MateriaRemoveMsg = 0x016C,
        /// <summary>投影提示。</summary>
        GlamourCastMsg = 0x016D,
        /// <summary>解除投影提示。</summary>
        GlamourRemoveMsg = 0x016E,
        /// <summary>古武注入提示。</summary>
        RelicInfuseMsg = 0x0179,
        /// <summary>玩家货币更新。</summary>
        PlayerCurrency = 0x017A,
        /// <summary>精选结果；Sapphire 记录 a1：原物品 ID + 500000，a2/a3：第一项结果与数量，a4/a5：可选第二项结果与数量。高品质编码等细节须按版本核对。</summary>
        AetherReductionDlg = 0x017D,
        /// <summary>播放动作时间轴；与 Unk6 的关系尚未确认。</summary>
        PlayActionTimeline = 0x0197,
        /// <summary>共享时间轴状态；a1：状态。Sapphire 称 SetSharedGroupTimelineState；也用于 EObj.SharedTimelineState。</summary>
        EObjSetState = 0x0199,
        /// <summary>用途未确认；来源推测与动作时间轴有关。</summary>
        Unk6 = 0x019C,
        /// <summary>播放共享组时间轴；Sapphire 名称为 PlaySharedGroupTimeline。</summary>
        EObjAnimation = 0x019D,
        /// <summary>设置 EObj 共享组状态；来自用户提供的结构观察，参数未完全确认。</summary>
        EObjSetSharedGroupState = 0x01A0,
        /// <summary>设置搭档所属实体。</summary>
        SetCompanionOwnerId = 0x01A1,
        /// <summary>按另一编号查找 EObj 后播放动画；用户记录使用 a3 查找对象，具体编号语义待核对。</summary>
        EObjAnimationByOtherId = 0x01A2,
        /// <summary>设置称号。</summary>
        SetTitle = 0x01F4,
        /// <summary>设置目标标记。</summary>
        SetTargetSign = 0x01F6,
        /// <summary>设置仇恨字母标记。</summary>
        SetHateLetter = 0x01F7,
        /// <summary>设置状态图标。</summary>
        SetStatusIcon = 0x01F8,
        /// <summary>极限技量谱；参数涉及最高等级、当前量、增长表现与类型（0 普通、1 玩家对战）。</summary>
        LimitBreakGauge = 0x01F9,
        /// <summary>设置返回点。</summary>
        SetHomepoint = 0x01FB,
        /// <summary>设置收藏传送点。</summary>
        SetFavorite = 0x01FC,
        /// <summary>学习传送点。</summary>
        LearnTeleport = 0x01FD,
        /// <summary>开始地图更新；a1 是事件类型位掩码：1 任务、2 理符分配、4 行会令引导、8 九宫幻卡、16 自定义对话、32 前置事件。</summary>
        BeginMapUpdate = 0x01FF,
        /// <summary>推荐任务界面；Sapphire 名称为 FinishMapUpdate，具体语义按版本核对。</summary>
        OpenRecommendationGuide = 0x0200,
        /// <summary>兵装库错误提示。</summary>
        ArmoryErrorMsg = 0x0201,
        /// <summary>成就进度。</summary>
        AchievementProgress = 0x0202,
        /// <summary>成就完成提示。</summary>
        AchievementPopup = 0x0203,
        /// <summary>设置过场动画标志。</summary>
        SetCutsceneFlag = 0x0204,
        /// <summary>日志消息。</summary>
        LogMsg = 0x0205,
        /// <summary>成就获得消息。</summary>
        AchievementMsg = 0x0206,
        /// <summary>设置过场动画标志组。</summary>
        SetCutsceneFlags = 0x0207,
        /// <summary>设置物品等级信息。</summary>
        SetItemLevel = 0x0209,
        /// <summary>挑战任务完成提示。</summary>
        ChallengeEntryCompleteMsg = 0x020B,
        /// <summary>挑战任务解锁提示。</summary>
        ChallengeEntryUnlockMsg = 0x020C,
        /// <summary>分解或精选结果；a1：结果类型，a3：物品，a4：数量，a5：经验 × 100。结果类型与物品附加编码存在版本差异，调用前核对。</summary>
        DesynthOrReductionResult = 0x020F,
        /// <summary>金币相关提示；详细用途待确认。</summary>
        GilTrailMsg = 0x0211,
        /// <summary>讨伐笔记等级解锁。</summary>
        HuntingLogRankUnlock = 0x021D,
        /// <summary>讨伐笔记条目更新。</summary>
        HuntingLogEntryUpdate = 0x021E,
        /// <summary>讨伐笔记分组完成。</summary>
        HuntingLogSectionFinish = 0x021F,
        /// <summary>讨伐笔记等级完成。</summary>
        HuntingLogRankFinish = 0x0220,
        /// <summary>套装最大数量；保留用户与 BossMod 的 0x230，Sapphire 的不同编号单独列出。</summary>
        SetMaxGearSets = 0x0230,
        /// <summary>角色装备参数界面；Sapphire 名称为 SetConfigFlags。</summary>
        SetCharaGearParamUI = 0x0260,
        /// <summary>线框渲染开关。</summary>
        ToggleWireframeRendering = 0x0261,
        /// <summary>技能被拒绝；来源为 ActorControlSelf 的定义。</summary>
        ActionRejected = 0x02BC,
        /// <summary>查看装备失败。</summary>
        ExamineError = 0x02BF,
        /// <summary>Sapphire 的套装最大数量编号 0x320；不能直接当作国服 SetMaxGearSets 的替代值。</summary>
        SetMaxGearSetsSapphire = 0x0320,
        /// <summary>装备套装提示。</summary>
        GearSetEquipMsg = 0x0321,
        /// <summary>设置鱼饵；a1：鱼饵 ID。</summary>
        SetBait = 0x0325,
        /// <summary>设置节庆；a1：Festival 表行号。</summary>
        SetFestival = 0x0386,
        /// <summary>坐骑解锁状态；a1：坐骑 ID，a2：1 解锁、0 锁定。</summary>
        SetMountBitmask = 0x0387,
        /// <summary>周期时间设置。</summary>
        SetCycleTimes = 0x038B,
        /// <summary>管弦乐曲解锁开关。</summary>
        ToggleOrchestrionUnlock = 0x0396,
        /// <summary>事件战斗对话。</summary>
        EventBattleDialog = 0x039C,
        /// <summary>服务器请求回调响应。</summary>
        ServerRequestCallbackResponse = 0x039D,
        /// <summary>坐骑速度；来源记录一星、二星分别发送 12、15，须在上坐骑完成前生效。</summary>
        SetMountSpeed = 0x039F,
        /// <summary>下坐骑。</summary>
        Dismount = 0x03A1,
        /// <summary>战斗回放开始确认。</summary>
        BeginReplayAck = 0x03A2,
        /// <summary>战斗回放结束确认。</summary>
        EndReplayAck = 0x03A3,
        /// <summary>显示房屋建造预设界面。</summary>
        ShowBuildPresetUI = 0x03E9,
        /// <summary>显示房屋外装界面；a1：地块编号。</summary>
        ShowEstateExternalAppearanceUI = 0x03EA,
        /// <summary>显示房屋内装界面。</summary>
        ShowEstateInternalAppearanceUI = 0x03EB,
        /// <summary>建造预设响应。</summary>
        BuildPresetResponse = 0x03ED,
        /// <summary>移除室外家具；a1 打包地块编号与槽位。</summary>
        RemoveExteriorHousingItem = 0x03EF,
        /// <summary>移除室内家具；a1：对象表索引。</summary>
        RemoveInteriorHousingItem = 0x03F1,
        /// <summary>显示家具界面。</summary>
        ShowHousingItemUI = 0x03F7,
        /// <summary>家具移动确认；a1 打包容器、地块信息，a2 包含家具槽位。</summary>
        HousingItemMoveConfirm = 0x03F9,
        /// <summary>打开房屋设置。</summary>
        OpenEstateSettingsUI = 0x03FF,
        /// <summary>隐藏个人房间入口。</summary>
        HideAdditionalChambersDoor = 0x0400,
        /// <summary>房屋仓库信息；a1 含室外搬迁状态、放置数、库存数与上限，a2/a3 含室内状态与数量。</summary>
        HousingStoreroomStatus = 0x0419,
        /// <summary>九宫幻卡卡片信息。</summary>
        TripleTriadCard = 0x04B4,
        /// <summary>九宫幻卡相关未知控制。</summary>
        TripleTriadUnknown = 0x04B5,
        /// <summary>设置玩家对战状态；Sapphire 记录决斗场景的 a3 固定为 6。</summary>
        SetPvPState = 0x05E0,
        /// <summary>结束决斗会话。</summary>
        EndDuelSession = 0x05E1,
        /// <summary>开始决斗倒计时。</summary>
        StartDuelCountdown = 0x05E2,
        /// <summary>开始决斗。</summary>
        StartDuel = 0x05E3,
        /// <summary>决斗结果界面。</summary>
        DuelResultScreen = 0x05E4,
        /// <summary>设置任务技能组；Sapphire 名称为 SetDutyActionId。</summary>
        SetDutyActionSet = 0x05E8,
        /// <summary>任务技能详细信息；Sapphire 名称为 SetDutyActionHud。</summary>
        SetDutyActionDetails = 0x05E9,
        /// <summary>任务技能是否存在；Sapphire 把此编号命名为 SetDutyActionActive，勿与下一项混用。</summary>
        SetDutyActionPresent = 0x05EA,
        /// <summary>任务技能可用状态；Sapphire 把此编号命名为 SetDutyActionRemaining。</summary>
        SetDutyActionActive = 0x05EB,
        /// <summary>任务技能层数。</summary>
        SetDutyActionCharges = 0x05EC,
        /// <summary>冷却进度增量；a1：冷却组，a2：以 100 毫秒量化的增量。</summary>
        IncrementRecast = 0x0600,
        /// <summary>持续治疗；Machina 国际服定义注明从 7.10 起使用 0x604。保留旧类别，国服参数需实机核对。</summary>
        HoT = 0x0604,
        /// <summary>持续伤害；Machina 国际服定义注明从 7.10 起使用 0x605。保留旧类别，国服参数需实机核对。</summary>
        DoT = 0x0605,
        /// <summary>驯兽师捕获相关；a1：目标 ID，a2：捕获类别，a3：累计捕获数。来自用户提供的定义，未在本项目验证。</summary>
        BeastMasterLearn = 0x060B,
        /// <summary>优雷卡进度。</summary>
        EurekaStep = 0x073A,
        /// <summary>临危受命角色信息。</summary>
        FateNpc = 0x092F,
        /// <summary>临危受命初始化。</summary>
        FateInit = 0x0931,
        /// <summary>临危受命接收物品。</summary>
        FateReceiveItem = 0x0932,
        /// <summary>分配临危受命 ID；a1：分配给主对象的临危受命编号。</summary>
        FateAssignID = 0x0934,
        /// <summary>临危受命开始。</summary>
        FateStart = 0x0935,
        /// <summary>临危受命结束。</summary>
        FateEnd = 0x0936,
        /// <summary>临危受命进度。</summary>
        FateProgress = 0x093E,

        // 常用操作的明确名称，与上面的类别保持同一编号。
        /// <summary>设置战斗状态；a1 为 0 或 1。</summary>
        SetBattle = ToggleAggro,
        /// <summary>设置 GameObject.EventState。</summary>
        SetEventState = DirectorEObjMod,
        /// <summary>设置 EObj 的共享时间轴状态。</summary>
        SetSharedGroupTimelineState = EObjSetState,
        /// <summary>播放 EObj 的共享组时间轴。</summary>
        PlaySharedGroupTimeline = EObjAnimation,
        /// <summary>击退类别的通用拼写，兼容用户原列表中的 KnockBack。</summary>
        Knockback = KnockBack,
    }
}


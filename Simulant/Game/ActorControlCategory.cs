namespace Simulant.Game
{
    /// <summary>ActorControl 系列的控制子类别，供原生分发函数使用。</summary>
    /// <remarks>
    /// 按国服 7.58（2026.11.03）客户端研究核对；数值是 category，不是数据包 opcode。<br />
    /// a1–a4 中未列出的参数在该版本不使用；“待确认”不表示未使用。<br />
    /// a5–a8 及独立 targetId 必须通过完整的 HandleActorControl 入口传入；四参实体入口不能代替。<br />
    /// 坐标采用客户端 X、Y、Z（Y 为高度）；压缩坐标解码为值 × 2000 / 65535 − 1000 米，
    /// 压缩朝向解码为值 × 2π / 65535 − π 弧度。<br />
    /// 类别的本机／远端限制及前置状态仍需满足；参数说明不表示该调用会同步服务端状态。
    /// </remarks>
    public enum ActorControlCategory : ushort
    {
        /// <summary>
        /// 收刀／拔刀。<br />
        /// a1：<br />
        /// 0: 收刀<br />
        /// 1: 拔刀。<br />
        /// a2：<br />
        /// 0: 常规切换<br />
        /// 1: 即时切换。<br />
        /// a3：<br />
        /// 0: 本机使用常规切换<br />
        /// 1: 本机采用 a2 选项。<br />
        /// 远端角色采用 a2，不使用 a3；即时切换还会中断相关表情。
        /// </summary>
        ToggleWeapon = 0x0000,

        /// <summary>
        /// 本机自动攻击开关。<br />
        /// a1：<br />
        /// 0: 关闭<br />
        /// 1: 开启。
        /// </summary>
        AutoAttack = 0x0001,

        /// <summary>
        /// 设置角色行为模式。<br />
        /// a1：角色行为模式。<br />
        /// a2：模式附加参数，含义随模式变化。<br />
        /// 不是状态效果列表。<br />
        /// a1 为 2 时进入死亡行为模式；不代替死亡演出或 HP 同步。
        /// </summary>
        SetStatus = 0x0002,

        /// <summary>
        /// 事件道具施法。<br />
        /// a1：<br />
        /// 0: 远端执行表现<br />
        /// 1: 事件道具施法准备。<br />
        /// a2：EventItem 编号。<br />
        /// 使用独立 targetId；执行表现仅针对非本机角色。
        /// </summary>
        CastStart = 0x0003,

        /// <summary>
        /// 战斗状态。<br />
        /// a1：<br />
        /// 0: 脱离战斗<br />
        /// 1: 进入战斗。<br />
        /// 更新战斗状态及相关仇恨、界面。
        /// </summary>
        ToggleAggro = 0x0004,

        /// <summary>
        /// 职业切换特效。<br />
        /// a1–a4 不使用。<br />
        /// 播放职业切换特效；不在此设置当前职业。
        /// </summary>
        ClassJobChange = 0x0005,

        /// <summary>
        /// 死亡／击败消息。<br />
        /// a1：优先使用的击败相关角色 ID。<br />
        /// a2：a1 角色不存在时使用的备用角色 ID。<br />
        /// 死亡／击败消息，不等同于完整死亡动作。
        /// </summary>
        Death = 0x0006,

        /// <summary>
        /// 获得职业经验消息。<br />
        /// a1：职业编号。<br />
        /// a2：获得经验。<br />
        /// a3：经验附加参数（具体含义待确认）。<br />
        /// a2 为 0 时不使用 a3。
        /// </summary>
        GainExpMsg = 0x0007,

        /// <summary>
        /// 解锁职业。<br />
        /// a1：职业编号。<br />
        /// 初始化该职业等级为 1，并刷新相应职业数据。
        /// </summary>
        UnlockClassJob = 0x0008,

        /// <summary>
        /// 更新职业等级。<br />
        /// a1：职业编号。<br />
        /// a2：新等级。<br />
        /// 路由角色当前职业匹配 a1 时才更新其等级。
        /// </summary>
        ClassJobUpdate = 0x0009,

        /// <summary>
        /// 升级表现。<br />
        /// a1：职业编号。<br />
        /// a2：新等级。<br />
        /// a3：旧等级。<br />
        /// a4：可选当前等级；0 不额外设置。<br />
        /// 逐级播放升级提示；a4 用于本机当前职业匹配的情况。
        /// </summary>
        LevelUpEffect = 0x000A,

        /// <summary>
        /// 呼叫增援提示。<br />
        /// a1：呼叫角色 ID。<br />
        /// a2：加入敌人数。<br />
        /// 角色以 a1 指定，不用路由对象代替。
        /// </summary>
        CallForHelp = 0x000B,

        /// <summary>
        /// 经验连锁计数与倒计时。<br />
        /// a1：经验连锁数。<br />
        /// a2：剩余时间，单位 0.01 秒。<br />
        /// 连锁数 0 不显示获得连锁消息。
        /// </summary>
        ExpChainMsg = 0x000C,

        /// <summary>
        /// 更新 HP、MP、GP 或 CP。<br />
        /// a1：<br />
        /// 1: 当前 HP<br />
        /// 105: 当前 HP<br />
        /// 3: 当前 MP<br />
        /// 12: 当前 MP<br />
        /// 51: 当前 MP<br />
        /// 7: 当前 GP<br />
        /// 8: 当前 CP。<br />
        /// a2：新的绝对值。<br />
        /// 更新当前值，不更新最大值。
        /// </summary>
        HpSetStat = 0x000D,

        /// <summary>
        /// 死亡表现启动。<br />
        /// a1：<br />
        /// 0: 自动选择死亡动作<br />
        /// 0xFFFF: 特殊死亡收尾<br />
        /// 其他值: ActionTimeline 编号。<br />
        /// a2：死亡动作起始位置，单位 30Hz 时间轴帧；0 从头开始。<br />
        /// a2 仅在部分自动选择动作的路径生效；不是等待时长。
        /// </summary>
        DeathAnimation = 0x000E,

        /// <summary>
        /// 取消施法。<br />
        /// a1：可选失败提示 LogMessage 编号；0 不显示。<br />
        /// a2：动作类型 ActionType。<br />
        /// a3：该类型下的动作编号。<br />
        /// a4：<br />
        /// 0: 取消选项关闭<br />
        /// 1: 取消选项开启。<br />
        /// a4 的表现差异待确认；失败提示还受动作数据条件限制。
        /// </summary>
        CancelCast = 0x000F,

        /// <summary>
        /// 设置冷却计时。<br />
        /// a1：冷却组索引。<br />
        /// a2：已过时间，单位 0.01 秒。<br />
        /// a3：总时长，单位 0.01 秒。<br />
        /// 同时设置冷却计时的活动状态。
        /// </summary>
        RecastDetails = 0x0010,

        /// <summary>
        /// 更新冷却总时长。<br />
        /// a1：冷却组索引。<br />
        /// a2：动作编号 ActionId。<br />
        /// a3：总时长，单位 0.01 秒。<br />
        /// 冷却组记录的动作为空或匹配 a2 才更新；不重设已过时间。
        /// </summary>
        Cooldown = 0x0011,

        /// <summary>
        /// 清除条件技能触发窗口。<br />
        /// a1：<br />
        /// 0: 保留槽<br />
        /// 1: 攻击被回避／未命中后的窗口<br />
        /// 2: 格挡后的窗口<br />
        /// 3: 招架后的窗口<br />
        /// 4: 回避后的窗口。<br />
        /// 清除指定条件技能的触发窗口。
        /// </summary>
        ClearProcTimer = 0x0012,

        /// <summary>
        /// 状态获得提示。<br />
        /// a1：状态编号 StatusId。<br />
        /// a2：状态附加参数。<br />
        /// 获得提示与视觉通知；不创建真实状态槽。
        /// </summary>
        GainEffect = 0x0014,

        /// <summary>
        /// 状态移除／消失提示。<br />
        /// a1：状态编号 StatusId。<br />
        /// a2：状态附加参数。<br />
        /// a3：来源角色 ID；0xE0000000 不限来源。<br />
        /// a4：<br />
        /// 0: 查找并移除状态<br />
        /// 1: 仅通知。<br />
        /// 移除路径需要状态编号和来源匹配。
        /// </summary>
        LoseEffect = 0x0015,

        /// <summary>
        /// 更新状态附加参数。<br />
        /// a1：状态槽索引。<br />
        /// a2：预期状态编号 StatusId。<br />
        /// a3：新状态附加参数。<br />
        /// 已有状态槽的编号匹配 a2 才更新。
        /// </summary>
        UpdateEffect = 0x0016,

        /// <summary>
        /// 持续伤害／治疗。<br />
        /// a1：状态编号 StatusId。<br />
        /// a2：<br />
        /// 3: 持续伤害<br />
        /// 4: 持续治疗。<br />
        /// a3：伤害／治疗值。<br />
        /// a4：来源角色 ID。<br />
        /// 兼容类别仍有消费者，不是完全废弃。
        /// </summary>
        HotDot = 0x0017,

        /// <summary>
        /// 休息经验。<br />
        /// a1：休息经验。<br />
        /// 同步休息经验，并显示相应教程。
        /// </summary>
        UpdateRestedExp = 0x0018,

        /// <summary>
        /// 用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 当前消费者未匹配，不能据此认定废弃。
        /// </summary>
        Unknown0019 = 0x0019,

        /// <summary>
        /// 副手武器开关。<br />
        /// a1：<br />
        /// 0: 收起<br />
        /// 1: 拔出。<br />
        /// 副手武器状态。
        /// </summary>
        ToggleOffhandWeapon = 0x001A,

        /// <summary>
        /// 设置移动速度倍率。<br />
        /// a1：<br />
        /// 0: 恢复正常速度计算<br />
        /// 1: 固定 1 倍速<br />
        /// 2: 固定 2 倍速<br />
        /// 其他正整数: 对应倍速。<br />
        /// 旧名：Flee。客户端共用的移动速度倍率；不指定逃跑方向或目的地。
        /// </summary>
        SetMovementSpeedMultiplier = 0x001B,

        /// <summary>
        /// 设置 NPC 部件状态。<br />
        /// a1：部件索引 0–5。<br />
        /// a2：<br />
        /// 0: 部件状态 0<br />
        /// 1: 部件状态 1<br />
        /// 2: 部件状态 2。<br />
        /// a3：可选破坏提示中的另一角色 ID。<br />
        /// a4：<br />
        /// 0: 不显示破坏提示<br />
        /// 1: 显示破坏提示。<br />
        /// 状态表现由 BNpcParts 配置决定；可作用于待生成角色记录。
        /// </summary>
        SetNpcPartState = 0x001E,

        /// <summary>
        /// 更新组合模型表现。<br />
        /// a1：ModelState 编号；bit31 表示先清理原有动作。<br />
        /// a2：ModelScale 编号。<br />
        /// a3：<br />
        /// bits0–3: 四项属性值<br />
        /// bits4–7: 对应四项的修改开关。<br />
        /// a4：<br />
        /// 低半字节: 第一组动画索引<br />
        /// 高半字节: 第二组动画索引。<br />
        /// 组合更新模型状态、缩放、属性和动画；索引查表后选择动作。
        /// </summary>
        SetModelAppearance = 0x001F,

        /// <summary>
        /// 濒危状态开关。<br />
        /// a1：<br />
        /// 0: 解除濒危<br />
        /// 1: 进入濒危。<br />
        /// 影响跪地姿态、任务敌人判定及指定任务物品资格；不等于角色死亡。
        /// </summary>
        SetDying = 0x0020,

        /// <summary>
        /// 队伍职能加成。<br />
        /// a1：<br />
        /// bit0: 防护<br />
        /// bit1: 近战<br />
        /// bit2: 远程物理<br />
        /// bit3: 远程魔法<br />
        /// bit4: 治疗。<br />
        /// 队伍职业构成加成掩码。
        /// </summary>
        SetPartyRoleBonus = 0x0021,

        /// <summary>
        /// 从接收者向另一对象播放头顶标记 VFX。<br />
        /// a1：标记特效编号；0 不播放。<br />
        /// a2：另一对象 ID。<br />
        /// 需要有效的另一对象。
        /// </summary>
        TargetIcon = 0x0022,

        /// <summary>
        /// 创建/更新指定槽的连线。<br />
        /// a1：连线槽索引。<br />
        /// a2：Channeling 编号；0 取消该槽连线。<br />
        /// a3：连线对端角色 ID。<br />
        /// a4：连线宽度，数值÷100。<br />
        /// a3 是参数内角色 ID，不是独立 targetId。
        /// </summary>
        Tether = 0x0023,

        /// <summary>
        /// 出入场演出。<br />
        /// a1：<br />
        /// 0: 不渐显<br />
        /// 1: 以 1 秒完整行程速率渐显。<br />
        /// a2：可选 ActionTimeline 编号；0 不播放。<br />
        /// 不自动将当前透明度重置为 0。
        /// </summary>
        PlayEntranceTimeline = 0x0024,

        /// <summary>
        /// 角色出现/生成表现。<br />
        /// a1：<br />
        /// 0: 跳过出现特效<br />
        /// 1: 播放出现特效。<br />
        /// 特效由角色资源和敌我关系决定。
        /// </summary>
        SpawnEffect = 0x0025,

        /// <summary>
        /// 角色隐身标志。<br />
        /// a1：<br />
        /// 0: 清除隐身标志<br />
        /// 1: 设置隐身标志。<br />
        /// 角色专用隐身控制。
        /// </summary>
        ToggleInvisible = 0x0026,

        /// <summary>
        /// 请求死亡后淡出流程。<br />
        /// a1–a4 不使用。
        /// </summary>
        DeadFadeOut = 0x0027,

        /// <summary>
        /// 设置 UIState 解锁位/奖励标志。<br />
        /// a1：解锁／奖励位索引。<br />
        /// a2：<br />
        /// 0: 清除<br />
        /// 1: 设置。
        /// </summary>
        ToggleActionUnlock = 0x0029,

        /// <summary>
        /// 更新本机 UI 经验与休息经验。<br />
        /// a1：传 UI 经验更新函数的职业/类型值。<br />
        /// a2：当前经验绝对值。<br />
        /// a3：休息经验绝对值。
        /// </summary>
        UpdateUiExp = 0x002B,

        /// <summary>
        /// 慈悲杀恢复体力提示。<br />
        /// a1：36: 慈悲杀。<br />
        /// a2：恢复 HP 的显示值。<br />
        /// 此类别只通知恢复量，不直接修改 HP。
        /// </summary>
        MercyStrokeHealMessage = 0x002C,

        /// <summary>
        /// 落伤/受伤提示。<br />
        /// a1：受伤显示值。<br />
        /// 本机可显示落伤提示。
        /// </summary>
        DmgTakenMsg = 0x002D,

        /// <summary>
        /// 取消排队动作。<br />
        /// a1：排队动作对应的冷却组。<br />
        /// 只取消匹配的排队动作。
        /// </summary>
        CancelQueuedAction = 0x002E,

        /// <summary>
        /// 取消指定连线槽，可按连线 ID 匹配。<br />
        /// a1：连线槽索引。<br />
        /// a2：<br />
        /// 0: 无条件取消<br />
        /// 其他值: 当前连线编号匹配才取消。
        /// </summary>
        TetherCancel = 0x002F,

        /// <summary>
        /// 设置 ModelScale 表控制的模型缩放状态。<br />
        /// a1：ModelScale 编号；0 恢复基础比例。<br />
        /// 比例来自资源表，不直接传倍数。
        /// </summary>
        SetModelScale = 0x0030,

        /// <summary>
        /// 模型属性开关。<br />
        /// a1：<br />
        /// bits0–3: 四项属性目标值<br />
        /// bits4–7: 对应四项的修改开关。<br />
        /// 仅修改开关为 1 的属性被更新。
        /// </summary>
        SetModelAttributes = 0x0031,

        /// <summary>
        /// 切换角色硬目标及目标连线相关状态。<br />
        /// a1–a4 不使用。<br />
        /// 目标来自独立 64 位 targetId。
        /// </summary>
        SetTarget = 0x0032,

        /// <summary>
        /// 播放固定特效 118。<br />
        /// a1–a4 不使用。
        /// </summary>
        PlayFixedVfx118 = 0x0034,

        /// <summary>
        /// 播放固定特效 119。<br />
        /// a1–a4 不使用。
        /// </summary>
        PlayFixedVfx119 = 0x0035,

        /// <summary>
        /// 设置可选中状态并更新相关显示。<br />
        /// a1：<br />
        /// 0: 不可选中<br />
        /// 1: 可选中。
        /// </summary>
        Targetable = 0x0036,

        /// <summary>
        /// 装备队徽显示。<br />
        /// a1：装备部位队徽显示掩码。
        /// </summary>
        SetEquipmentCrestFlags = 0x0037,

        /// <summary>
        /// 联盟负面状态提示。<br />
        /// a1：联盟成员角色 ID。<br />
        /// a2：<br />
        /// 0: 清除提示<br />
        /// 1: 显示负面状态提示。<br />
        /// 不添加或移除具体状态。
        /// </summary>
        SetAllianceDebuffIndicator = 0x0038,

        /// <summary>
        /// 设置软目标。<br />
        /// a1–a4 不使用。<br />
        /// 独立 64 位 targetId。
        /// </summary>
        SetSoftTarget = 0x003A,

        /// <summary>
        /// 设置覆盖动作时间轴。<br />
        /// a1：进入动作 ActionTimeline 编号；0 不设置进入动作。<br />
        /// a2：覆盖／退出动作 ActionTimeline 编号；0 清除。<br />
        /// 两项均为 0 时清除现有覆盖动作。
        /// </summary>
        SetOverrideTimeline = 0x003B,

        /// <summary>
        /// 重置角色动作。<br />
        /// a1–a4 不使用。
        /// </summary>
        ResetActionTimeline = 0x003C,

        /// <summary>
        /// 特殊移动许可限制。<br />
        /// a1：<br />
        /// 0: 清除限制<br />
        /// 1: 设置限制。<br />
        /// a2：<br />
        /// 0: 限制选项 A<br />
        /// 1: 限制选项 B。<br />
        /// 已确认至少影响跳跃；两种附加选项的完整区别待确认。
        /// </summary>
        SetMovementRestriction = 0x003D,

        /// <summary>
        /// 设置一组模型动画状态并播放相应时间轴。<br />
        /// a1：<br />
        /// 0: 第一组<br />
        /// 1: 第二组。<br />
        /// a2：组内动画索引。
        /// </summary>
        SetAnimationState = 0x003E,

        /// <summary>
        /// 模型状态切换与对应时间轴维护。<br />
        /// a1：ModelState 编号。<br />
        /// 不是装备武器的模型编号。
        /// </summary>
        SetModelState = 0x003F,

        /// <summary>
        /// 元素等级或知见等级提升。<br />
        /// a1：提升后的元素等级／知见等级。<br />
        /// a2：附带等级值；当前提示不使用。
        /// </summary>
        SpecialContentLevelUp = 0x0041,

        /// <summary>
        /// 元素等级或知见等级下降。<br />
        /// a1：下降后的元素等级／知见等级。
        /// </summary>
        SpecialContentLevelDown = 0x0042,

        /// <summary>
        /// 辅助职业升级。<br />
        /// a1：MKDSupportJob 行号（辅助职业）。<br />
        /// a2：新等级；达到等级上限时用于追加提示。
        /// </summary>
        SupportJobLevelUp = 0x0043,

        /// <summary>
        /// 播放语音台词。<br />
        /// a1：VoiceLine 资源编号，入口只接受 8201000..8299000（含端点）。
        /// </summary>
        PlayVoiceLine = 0x0046,

        /// <summary>
        /// 通知极限技准备阶段的 HUD 状态及可选准备特效。<br />
        /// a1：超越技 ActionId；10001 使用单格计量槽表现。<br />
        /// a2：<br />
        /// 0: 不播放特效<br />
        /// 1: 播放特效。
        /// </summary>
        LimitBreakStart = 0x0047,

        /// <summary>
        /// 通知极限技执行阶段，并保留量表演出指定时长。<br />
        /// a1：超越技 ActionId；10001 使用单格计量槽表现。<br />
        /// a2：<br />
        /// 0: 不播放职业超越技特效<br />
        /// 1: 播放职业超越技特效。<br />
        /// a3：除 10，单位逻辑秒；AddonLimitBreak 量表执行演出保留时间。
        /// </summary>
        LimitBreakPartyStart = 0x0048,

        /// <summary>
        /// 角色气泡/喊话显示。<br />
        /// a1：Balloon 表行号。
        /// </summary>
        BubbleText = 0x0049,

        /// <summary>
        /// 结束极限技 HUD 演出。<br />
        /// a1：HUD 中记录的动作 ActionId。
        /// </summary>
        EndLimitBreakHud = 0x004A,

        /// <summary>
        /// 伤害飘字/VFX 并同步当前 HP。<br />
        /// a1：显示伤害量。<br />
        /// a2：新的当前 HP 绝对值；同步角色、小队及相关 UI。<br />
        /// a3：<br />
        /// bits0–15: 可选 VFX 编号<br />
        /// bits16–31: 可选 LogMessage 编号；0 使用默认 504。<br />
        /// 同时同步当前 HP，不只是受击特效。
        /// </summary>
        DamageEffect = 0x0050,

        /// <summary>
        /// 复活表现。<br />
        /// a1–a4 不使用。
        /// </summary>
        RaiseAnimation = 0x0051,

        /// <summary>
        /// 角色行为控制。<br />
        /// a1：基础行为编号。<br />
        /// a2：bit0: 抑制相应位置恢复。<br />
        /// 行为编号大于 30000 时使用 Behavior 表；小编号的最终动作路径未完全具名。
        /// </summary>
        SetBehavior = 0x0052,

        /// <summary>
        /// 屏幕横幅与提示音。<br />
        /// a1：ScreenImage 编号；0 不显示。
        /// </summary>
        TreasureScreenMsg = 0x0057,

        /// <summary>
        /// 更新指定角色 HP。<br />
        /// a1：被更新角色 EntityId。<br />
        /// a2：新的绝对 CurrentHP。
        /// </summary>
        SetCharacterHp = 0x0058,

        /// <summary>
        /// 设置 OwnerId。<br />
        /// a1：所有者 EntityId。
        /// </summary>
        SetOwnerId = 0x0059,

        /// <summary>
        /// 通知缺少修理暗物质，并列出指定物品容器所需材料数量。<br />
        /// a1：InventoryType 容器编号；9999 特殊选择背包四页 Inventory1..4（0/1/2/3）。
        /// </summary>
        ItemRepairMsg = 0x005C,

        /// <summary>
        /// 设置敌对关系。<br />
        /// a1：<br />
        /// 2: 敌对<br />
        /// 3: 敌对<br />
        /// 5: 敌对<br />
        /// 其他值: 非敌对。
        /// </summary>
        SetHostility = 0x0061,

        /// <summary>
        /// 设置 NPC 名称行号并请求名称重建。<br />
        /// a1：BNpcName 编号。<br />
        /// 更新角色名称。
        /// </summary>
        SetName = 0x0062,

        /// <summary>
        /// 青魔法习得提示与可选演出。<br />
        /// a1：传 RepresentationContainer 的技能编号。<br />
        /// a2：<br />
        /// 0: 只走文字通知<br />
        /// 1: 启用额外表现。
        /// </summary>
        BluActionLearn = 0x0063,

        /// <summary>
        /// 通知 EventFramework 初始化/刷新控制器。<br />
        /// a1：Director 编号。<br />
        /// 普通公共处理分支不使用 a1；回放外层用它定位内容控制器。
        /// </summary>
        DirectorInit = 0x0064,

        /// <summary>
        /// 清理内容控制器。<br />
        /// a1：事件 EventId。
        /// </summary>
        DirectorClear = 0x0065,

        /// <summary>
        /// 播放两份 LeveVfx 资源指定的理符表现。<br />
        /// a1：第 1 份 LeveVfx 资源行号，由行中路径选择表现资源。<br />
        /// a2：第 2 份 LeveVfx 资源行号；并非时长、模式或 a1 附加标志。
        /// </summary>
        LeveStartAnim = 0x0066,

        /// <summary>
        /// 按原因码显示理符开始失败消息或触发 UI 命令。<br />
        /// a1：Leve 编号。<br />
        /// a2：<br />
        /// 1: 开始地点错误<br />
        /// 2: 已有理符任务<br />
        /// 3: 队员不在任务地点<br />
        /// 4: 队员正在其他任务<br />
        /// 6: 界面操作（具体业务待确认）<br />
        /// 7: 无法继续提高难度<br />
        /// 8: 采集任务不能组队开始<br />
        /// 9: 未装备对应主工具<br />
        /// 10: 未装备对应副工具<br />
        /// 11: 军队理符不能组队<br />
        /// 12: 队员正在制作<br />
        /// 14: 队员正在使用坐骑运输<br />
        /// 15: 队员在过场动画。<br />
        /// 原因 1–4 用于相应任务名提示；原因 1 还提示任务地点。
        /// </summary>
        LeveStartError = 0x0067,

        /// <summary>
        /// 设置角色关联事件。<br />
        /// a1：完整 EventId。
        /// </summary>
        SetEventId = 0x0069,

        /// <summary>
        /// 设置 GameObject.EventState。<br />
        /// a1：事件状态值。
        /// </summary>
        DirectorEObjMod = 0x006A,

        /// <summary>
        /// 将八参数派发给指定 Director 的更新处理。<br />
        /// a1：Director/EventId。<br />
        /// a2：更新/事件类型值。<br />
        /// a3：Director 专用参数 1；匹配副本 Director 的 a2=40000001/80000003 时用于副本剩余秒数，回放跳转按经过整秒恢复；公共 80000004 用于剩余秒数校正，其它事件另论。<br />
        /// a4：Director 专属数据 2。<br />
        /// a5–a8 继续完整转发，需与 Director 类型一起解释。
        /// </summary>
        DirectorUpdate = 0x006D,

        /// <summary>
        /// 控制器事件提示。<br />
        /// a1：Director/EventId。
        /// </summary>
        ShowDirectorTutorial = 0x006E,

        /// <summary>
        /// 任务进入或事件操作失败。<br />
        /// a1：LogMessage 行号；仅白名单 955,1526,1528,1529,1541..1544,1597,1620,3718..3722,3725,7734,7751,7784,9404 被显示。<br />
        /// a2：<br />
        /// 参数 1: 多数带内容名模板为 InstanceContentTextData<br />
        /// 1541 为 Leve 行；1528 虽构造但当前模板不引用；无参消息不读。<br />
        /// a3：<br />
        /// 参数 2: 多数为 InstanceContent 行（0 选择泛称任务战场）<br />
        /// 1541 为 InstanceContentTextData；1528 构造但当前模板不引用。<br />
        /// a4：1526 最低等级；1528/1541 的 InstanceContent 行；其他消息不用。<br />
        /// a5 仅 1526 使用，表示所需人数；a6..a8 未读。
        /// </summary>
        ShowContentError = 0x006F,

        /// <summary>
        /// 控制器类型提示。<br />
        /// a1：Director/EventId。
        /// </summary>
        ShowDirectorMessage = 0x0070,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 旧名 SetFateState；当前语义未确认，已确认的 FATE 生命周期控制使用 SetFateState（0x0931）。
        /// </summary>
        LegacyFateState = 0x0074,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        ItemObtainMsg = 0x0075,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FateReqFailMsg = 0x0076,

        /// <summary>
        /// 指定接收者的屏幕提示。<br />
        /// a1：ScreenImage 资源行号。<br />
        /// a2：接收角色 EntityId；0xE0000000 不限制接收角色。
        /// </summary>
        DutyQuestScreenMsg = 0x007B,

        /// <summary>
        /// 副本道具选择与数量。<br />
        /// a1：<br />
        /// 0: 选择事件道具<br />
        /// 1: 设置数量<br />
        /// 2: 显示消息。<br />
        /// a2：EventItem 编号。<br />
        /// a3：a1=1 时为数量。
        /// </summary>
        UpdateDirectorEventItem = 0x007C,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FatePosition = 0x007D,

        /// <summary>
        /// 设置副本内容通关标志并刷新相关事件/UI。<br />
        /// a1：InstanceContent 编号。<br />
        /// a2：<br />
        /// 0: 清除通关状态<br />
        /// 1: 设置通关状态。
        /// </summary>
        SetContentClearFlag = 0x0082,

        /// <summary>
        /// 设置副本内容开放标志并刷新相关事件/UI。<br />
        /// a1：InstanceContent 编号。<br />
        /// a2：<br />
        /// 0: 清除开放状态<br />
        /// 1: 设置开放状态。
        /// </summary>
        SetContentOpenFlag = 0x0083,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1：仅 Sapphire sender：Item 行号；货币分支为 CrystalType+1。<br />
        /// a2：仅 Sapphire sender：获得数量，普通物品分支 originalQuantity。<br />
        /// a3：待确认。<br />
        /// a4：待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        ItemObtainIcon = 0x0084,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FateItemFailMsg = 0x0085,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FateFailMsg = 0x0086,

        /// <summary>
        /// 显示技能习得通知。<br />
        /// a1：习得的 ActionId。<br />
        /// 固定使用普通 Action 类型。
        /// </summary>
        ActionLearnMsg1 = 0x0087,

        /// <summary>
        /// 取消当前事件场景对包头角色的位置和朝向恢复。<br />
        /// a1：事件 EventId；须匹配当前事件。
        /// </summary>
        FreeEventPos = 0x008A,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FateSync = 0x008B,

        /// <summary>
        /// 增强移动同步。<br />
        /// a1：<br />
        /// 0: 关闭增强模式<br />
        /// 1: 开启增强模式。
        /// </summary>
        MoveType = 0x008E,

        /// <summary>
        /// 动作请求提交控制。<br />
        /// a1：<br />
        /// 0: 不强制提交动作请求<br />
        /// 1: 强制提交动作请求。
        /// </summary>
        SetActionRequestSubmission = 0x008F,

        /// <summary>
        /// 同步每日任务随机种子并刷新事件状态。<br />
        /// a1：每日任务随机种子。<br />
        /// a2：<br />
        /// 0: 常规更新<br />
        /// 1: 附加事件刷新。
        /// </summary>
        DailyQuestSeed = 0x0090,

        /// <summary>
        /// 对象间特效。<br />
        /// a1：来源对象 ID 的低 32 位。<br />
        /// a2：目标对象 ID 的低 32 位。<br />
        /// a3：<br />
        /// bits0–7: 来源类型<br />
        /// bits8–15: 目标类型。<br />
        /// a4：特效编号。<br />
        /// 对象类型 3 表示在对应 ID 中另设置 bit32。
        /// </summary>
        PlayObjectVfx = 0x0094,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 旧名 SetFateProgress；当前语义未确认，已确认的 FATE 进度控制使用 SetFateProgress（0x093C）。
        /// </summary>
        LegacyFateProgress = 0x009B,

        /// <summary>
        /// 事件关联状态。<br />
        /// a1：StatusId。<br />
        /// a2：关联的事件处理器 EventHandler 编号。<br />
        /// 与 a2 关联，用于特定事件对象的状态资格判断。
        /// </summary>
        SetEventStatusReference = 0x009F,

        /// <summary>
        /// 设置/恢复区域 BGM。<br />
        /// a1：<br />
        /// 0: 恢复／清理区域 BGM<br />
        /// 其他值: 区域 BGM 切换参数，完整编号及特殊值含义待确认。
        /// </summary>
        SetBGM = 0x00A1,

        /// <summary>
        /// 风脉解锁位与完成表现/状态更新。<br />
        /// a1：AetherCurrent 事件编号。<br />
        /// a2：<br />
        /// 0: 本次未完成区域风脉<br />
        /// 1: 本次完成区域风脉。<br />
        /// a3：<br />
        /// bits24–31: AetherCurrentCompFlgSet 行号（从 1 开始）<br />
        /// bits8–23: ScreenImage 编号。<br />
        /// a4：<br />
        /// 0: 禁止飞行<br />
        /// 1: 允许飞行。<br />
        /// a5: 0 不显示教程、1 在区域完成时显示教程<br />
        /// a6: 0 解锁风脉、1 撤销<br />
        /// a4 仅在 a6=1，或 a6=0 且 a2=1 时应用。
        /// </summary>
        UnlockAetherCurrentMsg = 0x00A4,

        /// <summary>
        /// 当前区域飞行许可。<br />
        /// a1：<br />
        /// 0: 禁止飞行<br />
        /// 1: 允许飞行。
        /// </summary>
        SetFlightPermission = 0x00A5,

        /// <summary>
        /// 对象可见性。<br />
        /// a1：<br />
        /// 0: 显示<br />
        /// 1: 隐藏。<br />
        /// 旧名：RemoveName。
        /// </summary>
        SetObjectVisibility = 0x00A8,

        /// <summary>
        /// 对象可见性频道。<br />
        /// a1：可见性频道编号。<br />
        /// 频道编号，不是开关或位图。
        /// </summary>
        SetVisibilityChannel = 0x00A9,

        /// <summary>
        /// 事件屏幕过渡。<br />
        /// a1–a4 不使用。
        /// </summary>
        ScreenFadeOut = 0x00AA,

        /// <summary>
        /// 生成可用技能列表。<br />
        /// a1：打包三个职业：bits16..23 主职业、bits8..15 基础职业、bits0..7 附加职业。<br />
        /// a2：主职业等级。<br />
        /// a4：附加职业等级。
        /// </summary>
        SetAvailableActions = 0x00AB,

        /// <summary>
        /// 清除可用技能列表。<br />
        /// a1–a4 不使用。
        /// </summary>
        ClearAvailableActions = 0x00AD,

        /// <summary>
        /// 刷新指定事件。<br />
        /// a1：EventId。
        /// </summary>
        RefreshEventHandler = 0x00AF,

        /// <summary>
        /// 骑乘剩余时间。<br />
        /// a1：倒计时，单位 0.01 秒；0 无倒计时。
        /// </summary>
        SetMountTimeRemaining = 0x00B5,

        /// <summary>
        /// 位置特效。<br />
        /// a1：VFX 编号。<br />
        /// a2：压缩 X。<br />
        /// a3：压缩 Y（高度）。<br />
        /// a4：压缩 Z。<br />
        /// a5: 强制回收时间，单位毫秒；0 不启用此计时<br />
        /// a6: 可选关联角色 EntityId；0xE0000000 不关联<br />
        /// 计时 0 不保证特效永久存在。
        /// </summary>
        PositionVFX = 0x00B7,

        /// <summary>
        /// 对象间特效。<br />
        /// a1：VFX 编号。<br />
        /// a2：来源对象 EntityId。<br />
        /// a3：目标对象 EntityId。<br />
        /// a4：强制回收时间，单位毫秒；0 不启用此计时。<br />
        /// 来源和目标都需要存在；计时 0 不保证特效永久存在。
        /// </summary>
        TargetVFX = 0x00B8,

        /// <summary>
        /// 角色入场/复活相关显现表现。<br />
        /// a1：<br />
        /// 0: 立即出现<br />
        /// 1: 以 1 秒完整行程速率渐显。<br />
        /// a2：<br />
        /// 0: 不播放复活动作<br />
        /// 1: 播放复活动作。<br />
        /// a3：可选 ActionTimeline 编号。<br />
        /// a4：可选 VFX 编号。
        /// </summary>
        ZoneIn = 0x00C8,

        /// <summary>
        /// 按压缩坐标瞬移角色并保留当前朝向。<br />
        /// a1：压缩原生 X。<br />
        /// a2：压缩原生 Y 高度。<br />
        /// a3：压缩原生 Z。
        /// </summary>
        ZoneInDefaultPos = 0x00C9,

        /// <summary>
        /// 传送请求结果/开始施法。<br />
        /// a1：<br />
        /// 0: 执行传送<br />
        /// 2: 失败（具体原因待确认）<br />
        /// 4: 失败（具体原因待确认）<br />
        /// 5: 失败（具体原因待确认）<br />
        /// 6: 失败（具体原因待确认）。<br />
        /// a2：动作附加参数（具体业务待确认）。<br />
        /// a3：<br />
        /// 0: 不附加队友条件提示<br />
        /// 1: 附加队友条件提示。
        /// </summary>
        TeleportStart = 0x00CB,

        /// <summary>
        /// 接收队友传送邀请。<br />
        /// a1：<br />
        /// 0: 不提示拒绝原因<br />
        /// 1: 提示拒绝原因。<br />
        /// a2：Aetheryte 编号。<br />
        /// a3：邀请者在本机 Group 的 0-based 小队成员索引，用于名字/跨服世界名查询。
        /// </summary>
        OnInvitationTelepo = 0x00CC,

        /// <summary>
        /// 结束传送请求的 UI 等待状态。<br />
        /// a1–a4 不使用。
        /// </summary>
        TeleportDone = 0x00CD,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        TeleportDoneFadeOut = 0x00CE,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        DespawnZoneScreenMsg = 0x00CF,

        /// <summary>
        /// 显示分线选择窗口及人数信息。<br />
        /// a1：TerritoryType 行号。<br />
        /// a2：分线数量。<br />
        /// a3：第一条分线的玩家人数。<br />
        /// a4：第二条分线的玩家人数。<br />
        /// a5: 第三条分线人数<br />
        /// a6: 第四条分线人数。
        /// </summary>
        InstanceSelectDlg = 0x00D2,

        /// <summary>
        /// 结束临时位置控制。<br />
        /// a1–a4 不使用。
        /// </summary>
        ReleaseTemporaryPositionControl = 0x00D3,

        /// <summary>
        /// 远端实体退场表现和移交清理。<br />
        /// a1：<br />
        /// 0: 按本机身份选择退场<br />
        /// 2: 强制淡出<br />
        /// 其他值: 附加退场模式（具体差异待确认）。<br />
        /// a2：可选 VFX。<br />
        /// a3：<br />
        /// 0: 收回子对象<br />
        /// 1: 保留子对象流程。<br />
        /// a4：<br />
        /// 0: 附加清理选项关闭<br />
        /// 1: 附加清理选项开启。<br />
        /// a4 的具体表现差异待确认。
        /// </summary>
        ActorDespawnEffect = 0x00D4,

        /// <summary>
        /// 脱离指定事件范围。<br />
        /// a1：EventRange 布局实例 ID（LayoutInstanceType=49/0x31）。
        /// </summary>
        RecoverFromEventRange = 0x00D5,

        /// <summary>
        /// 按 GimmickJump 配置启动机关跳跃。<br />
        /// a1：<br />
        /// bits16–31: 压缩 X<br />
        /// bits0–15: 压缩 Y（高度）。<br />
        /// a2：压缩 Z。<br />
        /// a3：GimmickJump 编号；bit16 为移动选项。<br />
        /// a4：移动确认序号。
        /// </summary>
        GimmickJump = 0x00DC,

        /// <summary>
        /// 启动延后机关跳跃。<br />
        /// a1–a4 不使用。
        /// </summary>
        StartPendingGimmickJump = 0x00DD,

        /// <summary>
        /// 远端动作与位移。<br />
        /// a1：<br />
        /// bits16–31: 压缩 X<br />
        /// bits0–15: 压缩 Y（高度）。<br />
        /// a2：<br />
        /// bits16–31: 压缩 Z<br />
        /// bits0–15: 压缩朝向。<br />
        /// a3：ActionTimelineMove 编号。<br />
        /// a4：ActionTimeline 编号。<br />
        /// 仅对非本机角色执行；移动规则来自 ActionTimelineMove，编号 1 使用包内终点。
        /// </summary>
        PlayRemoteTimelineMove = 0x00DF,

        /// <summary>
        /// 水平移动目标。<br />
        /// a1：压缩 X。<br />
        /// a2：压缩 Z。
        /// </summary>
        SetHorizontalMoveTarget = 0x00E0,

        /// <summary>
        /// 按绝对方向启动强制位移/击退。<br />
        /// a1：压缩绝对朝向。<br />
        /// a2：距离，数值÷10000 米。<br />
        /// a3：持续时间，数值÷10000 秒。<br />
        /// 沿绝对方向移动，高度不变；同时控制平移与旋转时长。
        /// </summary>
        KnockBack = 0x00E1,

        /// <summary>
        /// 向绝对位置强制移动，含额外移动参数。<br />
        /// a1：压缩 X。<br />
        /// a2：压缩 Y（高度）。<br />
        /// a3：压缩 Z。<br />
        /// a4：压缩朝向。<br />
        /// a5: 持续时间，数值÷10000 秒<br />
        /// a6: 移动模式（0、1、2，完整表现差异待确认）<br />
        /// a7、a8: —。
        /// </summary>
        ForcedMovement = 0x00E2,

        /// <summary>
        /// 清理时间轴移动。<br />
        /// a1–a4 不使用。
        /// </summary>
        ClearTimelineMove = 0x00E3,

        /// <summary>
        /// 结束时间轴移动。<br />
        /// a1–a4 不使用。
        /// </summary>
        FinishTimelineMove = 0x00E4,

        /// <summary>
        /// 解除远端特殊移动。<br />
        /// a1–a4 不使用。
        /// </summary>
        ReleaseRemoteSpecialMove = 0x00E5,

        /// <summary>
        /// 本机移动参数。<br />
        /// a1：ActionTimelineMove 表行号。<br />
        /// a2：ActionTimeline 表行号。<br />
        /// a3：压缩朝向。
        /// </summary>
        SetLocalMovementParameters = 0x00E7,

        /// <summary>
        /// 带终点的受控动作。<br />
        /// a1：ActionTimelineMove 表行号。<br />
        /// a2：ActionTimeline 表行号。<br />
        /// a3：压缩 X。<br />
        /// a4：压缩 Y 高度。<br />
        /// a5 压缩 Z。
        /// </summary>
        PlayControlledMoveTimeline = 0x00E8,

        /// <summary>
        /// 取消受控运动。<br />
        /// a1–a4 不使用。
        /// </summary>
        CancelControlledMovement = 0x00EB,

        /// <summary>
        /// 移动上报附加标记。<br />
        /// a1：移动报告选项掩码；部分位的具体含义待确认。<br />
        /// bit5、bit6 影响移动报告路径。
        /// </summary>
        SetMovementReportFlags = 0x00EC,

        /// <summary>
        /// 碰撞位移请求应答。<br />
        /// a1：<br />
        /// 0: 重发请求<br />
        /// 1: 清除请求。
        /// </summary>
        CollisionMoveResponse = 0x00ED,

        /// <summary>
        /// 重试潜水请求。<br />
        /// a1–a4 不使用。
        /// </summary>
        RetryDive = 0x00EE,

        /// <summary>
        /// 等待服务端位置收尾。<br />
        /// a1–a4 不使用。
        /// </summary>
        FinishTimelineMoveAtServerPosition = 0x00EF,

        /// <summary>
        /// 转向指定朝向。<br />
        /// a1：压缩朝向，换算 u16*2π/65535−π弧度。
        /// </summary>
        TurnToDirection = 0x00F0,

        /// <summary>
        /// 带 ActionTimeline 的强制传送。<br />
        /// a1：<br />
        /// bits16–31: 压缩 X<br />
        /// bits0–15: 压缩 Y（高度）。<br />
        /// a2：<br />
        /// bits16–31: 压缩 Z<br />
        /// bits0–15: 压缩朝向。<br />
        /// a3：<br />
        /// bits0–15: ActionTimeline 编号<br />
        /// bit16: 移动状态选项<br />
        /// bit17: 跳过一条直接播放动作的路径。<br />
        /// a4：移动确认序号。<br />
        /// 两项状态选项的完整玩法意义待确认。
        /// </summary>
        WarpWithAnimation = 0x00F1,

        /// <summary>
        /// 启动目标点受控移动。<br />
        /// a1：<br />
        /// bits16–31: 压缩 X<br />
        /// bits0–15: 压缩 Y（高度）。<br />
        /// a2：bits16–31: 压缩 Z。<br />
        /// a3：移动确认序号。<br />
        /// 以指定终点进入受控移动状态；可被优先级更高的移动阻止。
        /// </summary>
        StartPackedTargetMove = 0x00F2,

        /// <summary>
        /// 相对角色定位与双方动作。<br />
        /// a1：参考角色 EntityId。<br />
        /// a2：<br />
        /// bits16–23: 相对 X<br />
        /// bits8–15: 相对 Y<br />
        /// bits0–7: 相对 Z<br />
        /// 各项÷10 米。<br />
        /// a3：<br />
        /// bits0–15: 相对角度，数值−180 度<br />
        /// bits16–31: 0 跑步、1 步行。<br />
        /// a4：到位后双方播放的 ActionTimeline 编号。<br />
        /// 终点按收包时参考角色的位置、朝向计算；不会持续跟随。步行速度 2 米/秒，跑步 5 米/秒。
        /// </summary>
        MoveAndPlayPairedTimeline = 0x00F3,

        /// <summary>
        /// 设置附加水平速度。<br />
        /// a1：X 方向速度，IEEE754 单精度浮点数位模式。<br />
        /// a2：Y 方向分量，IEEE754 单精度浮点数位模式。<br />
        /// a3：Z 方向速度，IEEE754 单精度浮点数位模式。<br />
        /// 当前移动仅应用 X、Z；Y 仍参与全零关闭判定。
        /// </summary>
        SetAdditionalMovementVelocity = 0x00F4,

        /// <summary>
        /// 停止布局路线移动。<br />
        /// a1–a4 不使用。
        /// </summary>
        StopClientPath = 0x00F5,

        /// <summary>
        /// 潜水与上浮控制。<br />
        /// a1：<br />
        /// 0: 请求潜水<br />
        /// 1: 请求上浮。
        /// </summary>
        DiveControl = 0x00F6,

        /// <summary>
        /// 陆行鸟搭档资料。<br />
        /// a1：搭档数据编号。<br />
        /// a2：<br />
        /// 0: 不更新剩余时间<br />
        /// 1: 更新剩余时间。<br />
        /// a3：剩余时间，单位 0.01 秒。<br />
        /// a4：当前经验。<br />
        /// 旧名：CompanionUnlock。a5: 星级<br />
        /// a6: 技能点。
        /// </summary>
        UpdateBuddyData = 0x00FD,

        /// <summary>
        /// 解锁陆行鸟装甲并输出获得消息。<br />
        /// a1：BuddyEquip 行号（0–111）。
        /// </summary>
        ObtainBarding = 0x00FE,

        /// <summary>
        /// 设置陆行鸟装甲槽。<br />
        /// a1：<br />
        /// 0: 头部<br />
        /// 1: 身体<br />
        /// 2: 脚部。<br />
        /// a2：BuddyEquip 行号（0–111）。
        /// </summary>
        EquipBarding = 0x00FF,

        /// <summary>
        /// 坐骑鸟甲模型。<br />
        /// a1：<br />
        /// 0: 头部<br />
        /// 1: 身体<br />
        /// 2: 脚部。<br />
        /// a2：装备模型基础 ID。
        /// </summary>
        SetMountBardingModel = 0x0100,

        /// <summary>
        /// 召唤物与搭档对象。<br />
        /// a1：拥有者/队伍成员 EntityId。<br />
        /// a2：a5=2 时为槽索引，最终槽号=a2+2。<br />
        /// a3：召唤物或搭档 EntityId。<br />
        /// a4：<br />
        /// 0: 不刷新九槽资料<br />
        /// 1: 刷新九槽资料。<br />
        /// a5 为映射类型 0、1、2，完整业务名待确认；回放使用回放小队资料。
        /// </summary>
        SetCompanionEntity = 0x0101,

        /// <summary>
        /// 伙伴离开提示与可选离场动作。<br />
        /// a1：<br />
        /// 0: 不播放离场动作<br />
        /// 1: 播放离场动作。
        /// </summary>
        CompanionMsg1 = 0x0102,

        /// <summary>
        /// 搭档离开消息。<br />
        /// a1–a4 不使用。
        /// </summary>
        CompanionMsg2 = 0x0103,

        /// <summary>
        /// 同步宠物类型、命令、姿态和 UI 数据。<br />
        /// a1：Pet 数据 ID；变化时通知事件/UI。<br />
        /// a2：宠物指令。<br />
        /// a3：宠物姿态。<br />
        /// a4：宠物附加状态（具体意义待确认）。<br />
        /// a5: 附加选项 0/1（具体意义待确认）<br />
        /// 回放路径不更新实际宠物信息，仍更新回放小队资料。
        /// </summary>
        ShowPetHotbar = 0x0104,

        /// <summary>
        /// 搭档骑乘状态。<br />
        /// a1：<br />
        /// 0: 未骑乘<br />
        /// 1: 骑乘中。
        /// </summary>
        SetBuddyMounted = 0x0107,

        /// <summary>
        /// 搭档技能习得提示。<br />
        /// a1：<br />
        /// 0: 防护<br />
        /// 1: 攻击<br />
        /// 2: 治疗。<br />
        /// a2：BuddySkill 行号。
        /// </summary>
        ActionLearnMsg = 0x0109,

        /// <summary>
        /// 角色半秒淡出，并启用特殊透明度控制。<br />
        /// a1–a4 不使用。
        /// </summary>
        ActorFadeOut = 0x010A,

        /// <summary>
        /// 从透明开始一秒渐显。<br />
        /// a1–a4 不使用。
        /// </summary>
        ActorFadeIn = 0x010B,

        /// <summary>
        /// 伙伴撤退/离开日志。<br />
        /// a1：BNpcName 资源行号，作为离开日志中伙伴/对象的名称。
        /// </summary>
        WithdrawMsg = 0x010C,

        /// <summary>
        /// 伙伴命令日志。<br />
        /// a1：LogMessage642 的对象/伙伴引用参数。<br />
        /// a2：LogMessage642 的命令引用参数。
        /// </summary>
        OrderCompanion = 0x010D,

        /// <summary>
        /// 设置跟随宠物/迷你宠物。<br />
        /// a1：Companion 编号；0 移除。
        /// </summary>
        ToggleCompanion = 0x010E,

        /// <summary>
        /// 设置跟随宠物解锁位并可输出习得消息。<br />
        /// a1：Companion 行号/解锁位索引。<br />
        /// a2：<br />
        /// 0: 移除解锁<br />
        /// 1: 解锁；首次解锁时提示。
        /// </summary>
        LearnCompanion = 0x010F,

        /// <summary>
        /// 角色快速淡出。<br />
        /// a1–a4 不使用。<br />
        /// 旧名 ActorFateOut1，其中 Fate 为 Fade 的拼写错误。
        /// </summary>
        ActorQuickFadeOut = 0x0110,

        /// <summary>
        /// 副本助手列表。<br />
        /// a1：DutyHelper 槽索引 0–6。<br />
        /// a2：<br />
        /// 0: 禁用<br />
        /// 1: 启用。<br />
        /// a3：DawnMember 编号。
        /// </summary>
        SetDutyHelpers = 0x0114,

        /// <summary>
        /// 冒险者分队战术指令。<br />
        /// a1：<br />
        /// BgcArmyAction 行号<br />
        /// 1: 战斗开始<br />
        /// 2: 撤退<br />
        /// 3: 撤退解除<br />
        /// 4: 极限技。
        /// </summary>
        SquadronTacticalOrder = 0x0115,

        /// <summary>
        /// 小队组成横幅。<br />
        /// a1：<br />
        /// 8: 满编小队横幅<br />
        /// 4–7: 轻锐小队横幅<br />
        /// 0–3: 不显示。
        /// </summary>
        ShowPartyComposition = 0x0116,

        /// <summary>
        /// 角色跟随模型。<br />
        /// a1：FollowMount 表行号/控制 ID，0 触发清理路径。
        /// </summary>
        SetFollowMountModel = 0x0117,

        /// <summary>
        /// 表情动作播放及事件通知。<br />
        /// a1：Emote 行号，仅表中指定允许标志非零时处理。<br />
        /// a3：<br />
        /// bit0: 不显示日志<br />
        /// bit1: 含义待确认<br />
        /// bit3: 不执行初始坐下动作。<br />
        /// 使用独立 targetId 指定表情目标，并参与事件通知。
        /// </summary>
        Emote = 0x0122,

        /// <summary>
        /// 中断表情。<br />
        /// a1–a4 不使用。
        /// </summary>
        EmoteInterrupt = 0x0123,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        EmoteModeInterrupt = 0x0124,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        EmoteModeInterruptNonImmediate = 0x0125,

        /// <summary>
        /// 切换姿势。<br />
        /// a1：<br />
        /// 0: 闲置<br />
        /// 1: 拔刀<br />
        /// 2: 坐椅<br />
        /// 3: 坐地<br />
        /// 4: 睡觉<br />
        /// 5: 伞<br />
        /// 6: 时尚配饰。<br />
        /// a2：对应类别内的姿势索引。
        /// </summary>
        SetPose = 0x0127,

        /// <summary>
        /// 生活技能动作与状态。<br />
        /// a1：生活技能子命令 0–40。<br />
        /// a2：随子命令变化。<br />
        /// a3：随子命令变化。<br />
        /// a4：随子命令变化。<br />
        /// 部分语义待确认。旧名：FishingLightChange。包含采集、制作、钓鱼与刺鱼；不是仅控制钓鱼灯光。
        /// </summary>
        LifeSkillControl = 0x012C,

        /// <summary>
        /// 刷新采集感知。<br />
        /// a1：<br />
        /// bits0–15: 第 1 个采集事件短编号<br />
        /// bits16–31: 第 2 个采集事件短编号。<br />
        /// a2：<br />
        /// bits0–15: 第 3 个采集事件短编号<br />
        /// bits16–31: 第 4 个采集事件短编号。<br />
        /// a3：<br />
        /// bits0–15: 第 5 个采集事件短编号<br />
        /// bits16–31: 第 6 个采集事件短编号。<br />
        /// a4：<br />
        /// bits0–15: 第 7 个采集事件短编号<br />
        /// bits16–31: 第 8 个采集事件短编号。<br />
        /// 最多更新八个采集点。
        /// </summary>
        GatheringSenseMsg = 0x0130,

        /// <summary>
        /// 单参数日志。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：可选数值参数，非零才加入参数列表。
        /// </summary>
        PartyMsg = 0x0131,

        /// <summary>
        /// 采集点感知消失提示。<br />
        /// a1：GatheringPointName 资源行号，Log3502 直接取该名称。<br />
        /// a2：<br />
        /// 0: 清槽 2<br />
        /// 1: 清 MiniMapGatheringMarker 槽 1。
        /// </summary>
        GatheringSenseMsg1 = 0x0132,

        /// <summary>
        /// 采集次数与许可。<br />
        /// a1：采集事件短编号。<br />
        /// a2：总次数与剩余次数的新值。<br />
        /// 同一个 a2 同时设置总次数和剩余次数。
        /// </summary>
        GatheringSenseMsg2 = 0x0138,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FishingMsg = 0x0140,

        /// <summary>
        /// 同步普通钓鱼成功次数累计值。<br />
        /// a1：累计钓鱼成功次数。<br />
        /// 绝对值，不是增量或已发现鱼种数。
        /// </summary>
        FishingTotalFishCaught = 0x0142,

        /// <summary>
        /// 设置当前鱼饵并提示。<br />
        /// a1：鱼饵 ItemId。
        /// </summary>
        FishingBaitMsg = 0x0145,

        /// <summary>
        /// 结束当前交互。<br />
        /// a1–a4 不使用。
        /// </summary>
        EndInteraction = 0x0146,

        /// <summary>
        /// 大鱼出现提示。<br />
        /// a1：FishingSpot 资源行号；Log3530 取该行 BigFishOnReach 列 1，Log5582 取 BigFishOnRefresh 列 3。<br />
        /// a2：<br />
        /// 0: 首次钓获<br />
        /// 1: 再次钓获。
        /// </summary>
        FishingReachMsg = 0x0147,

        /// <summary>
        /// 显示指定钓场的大鱼出现条件结束提示。<br />
        /// a1：FishingSpot 资源行号；Log3531 转读该行 BigFishOnEnd 列 2。
        /// </summary>
        FishingFailMsg = 0x0148,

        /// <summary>
        /// 同步下次藏宝图额度与下次挑战笔记重置的绝对时间戳。<br />
        /// a1：时间戳的低 32 位。<br />
        /// a2：时间戳的高 32 位。<br />
        /// a3：NextChallengeLogResetTimestamp 的 32 位 Unix 秒，存 64 位。<br />
        /// a1、a2 合成 64 位时间戳。
        /// </summary>
        WeeklyIntervalUpdateTime = 0x0150,

        /// <summary>
        /// 魔晶石镶嵌结果。<br />
        /// a1：目标装备 ItemId。<br />
        /// a2：魔晶石 Item 引用；LogMessage1201 使用 Item(lnum2)。<br />
        /// a3：<br />
        /// 0: 失败，选择 1202<br />
        /// 1: 成功，选择 LogMessage1201。<br />
        /// a4：结果消息第三数值，失败模板用于损失魔晶石数量。<br />
        /// 旧名：MateriaConvertMsg。
        /// </summary>
        MateriaMeldResult = 0x015E,

        /// <summary>
        /// 执行镶嵌动作，并可播放分档的 cft_matng 特效。<br />
        /// a1：<br />
        /// 0: 不允许特效<br />
        /// 1: 允许特效。<br />
        /// a2：控制 VFX 档位：1–15: 947、16–30: 948、&gt;30: 949；确切代表数值待核对。<br />
        /// 部分语义待确认。
        /// </summary>
        MeldSuccessMsg = 0x015F,

        /// <summary>
        /// 镶嵌委托窗口。<br />
        /// a1：委托窗口对方角色 EntityId；必须能找到 BattleChara 才处理。<br />
        /// a2：<br />
        /// 0: 受普通孔数限制<br />
        /// 1: 允许超出普通魔晶石孔数继续镶嵌（对方禁忌镶嵌许可）。<br />
        /// a3：<br />
        /// bits24–31: 刻木<br />
        /// bits16–23: 锻铁<br />
        /// bits8–15: 铸甲<br />
        /// bits0–7: 雕金。<br />
        /// a4：<br />
        /// bits24–31: 制革<br />
        /// bits16–23: 裁衣<br />
        /// bits8–15: 炼金<br />
        /// bits0–7: 烹调。<br />
        /// 旧名：MeldFailMsg。a3、a4 分别携带八个制作职业等级；存在任一职业达到装备镶嵌等级即可通过等级检查，禁忌镶嵌许可另由 a2 决定。
        /// </summary>
        MateriaMeldRequestUi = 0x0160,

        /// <summary>
        /// 关闭镶嵌委托。<br />
        /// a1–a4 不使用。<br />
        /// 旧名：MeldModeToggle。
        /// </summary>
        CloseMateriaMeldRequest = 0x0161,

        /// <summary>
        /// 无法镶嵌提示。<br />
        /// a1：相关对象 EntityId；找到时按是否自己选择 LogMessage1220 或 1226。<br />
        /// 旧名：AetherRestoreMsg。
        /// </summary>
        MateriaMeldUnavailable = 0x0163,

        /// <summary>
        /// 染色结束的 UI 刷新。<br />
        /// a1：<br />
        /// 0: 跳过<br />
        /// 1: 刷新 UIModule 的物品上下文相关模块。
        /// </summary>
        DyeMsg = 0x0168,

        /// <summary>
        /// 单件装备队徽装饰/取下提示。<br />
        /// a1：<br />
        /// 0: 取下，选择 1898<br />
        /// 1: 为装饰队徽，选择 LogMessage1896。<br />
        /// a2：装备 Item 引用，作为消息数值参数。
        /// </summary>
        ToggleCrestMsg = 0x016A,

        /// <summary>
        /// 批量装备队徽装饰/取下提示。<br />
        /// a1：装备件数，作为消息数值参数。<br />
        /// a2：<br />
        /// 0: 取下，选择 1899<br />
        /// 1: 装饰队徽，选择 1897。
        /// </summary>
        ToggleBulkCrestMsg = 0x016B,

        /// <summary>
        /// 武具投影成功提示。<br />
        /// a1：原型装备 Item 引用。<br />
        /// a2：目标装备 Item 引用。<br />
        /// a3：<br />
        /// 0: 不刷新物品关联界面<br />
        /// 1: 刷新物品关联界面。<br />
        /// 旧名：MateriaRemoveMsg。
        /// </summary>
        GlamourCastMessage = 0x016C,

        /// <summary>
        /// 解除武具投影提示。<br />
        /// a1：解除投影的装备 Item 引用。<br />
        /// a2：<br />
        /// 0: 不刷新物品关联界面<br />
        /// 1: 刷新物品关联界面。<br />
        /// 旧名：GlamourCastMsg。
        /// </summary>
        GlamourRemoveMessage = 0x016D,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1：仅 Sapphire sender：被解除投影的装备 Item 行号。<br />
        /// a2：仅 Sapphire sender：invalidateGearSet 值。<br />
        /// a3：待确认。<br />
        /// a4：待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        GlamourRemoveMsg = 0x016E,

        /// <summary>
        /// 古武嵌写/属性提升两条结果消息。<br />
        /// a1：LogMessage4260 的嵌写材料/物品引用。<br />
        /// a2：LogMessage4260 的目标装备引用。<br />
        /// a3：LogMessage4261 的属性引用。<br />
        /// a4：LogMessage4261 的新属性数值。
        /// </summary>
        RelicInfuseMsg = 0x0179,

        /// <summary>
        /// 同步特殊货币的物品和持有数量。<br />
        /// a1：specialId，按有符号解释。<br />
        /// a2：Item 行号。<br />
        /// a3：允许的最大持有数量。<br />
        /// a4：当前持有数量。<br />
        /// a5:<br />
        /// 0: 有数量上限<br />
        /// 1: 无数量上限<br />
        /// a4 是绝对持有量。
        /// </summary>
        PlayerCurrency = 0x017A,

        /// <summary>
        /// 重置采集点交互。<br />
        /// a1：GatheringPoint 事件短编号。<br />
        /// 旧名：AetherReductionDlg。
        /// </summary>
        ResetGatheringPointInteraction = 0x017D,

        /// <summary>
        /// 多人坐骑乘客。<br />
        /// a1：槽号 0..6，超范围忽略。<br />
        /// a2：乘客 EntityId。
        /// </summary>
        SetMountPassenger = 0x0190,

        /// <summary>
        /// 清除转移中的乘客记录。<br />
        /// a1：槽号 0..6。<br />
        /// a2：预期乘客 EntityId。
        /// </summary>
        ClearMountPassengerSnapshot = 0x0191,

        /// <summary>
        /// 播放单个 ActionTimeline。<br />
        /// a1：ActionTimeline 编号。<br />
        /// 与 PlayCharacterTimeline（0x019C）共用客户端处理流程，两个实际类别编号均保留。
        /// </summary>
        PlayActionTimeline = 0x0197,

        /// <summary>
        /// 设置事件对象共享时间轴状态。<br />
        /// a1：共享动作状态。<br />
        /// a3：<br />
        /// 0: 按包头 EntityId 查找<br />
        /// 1: 按 a4 LayoutId 查找。<br />
        /// a4：a3=1 时使用的 LayoutId。
        /// </summary>
        EObjSetState = 0x0199,

        /// <summary>
        /// 事件对象时间轴。<br />
        /// a1：SharedGroup 本地时间轴选择索引 0..15，经 TimelineIndices 映射；不是 ActionTimeline 资源 ID。<br />
        /// a2：动作快进时间，单位毫秒。<br />
        /// a3：<br />
        /// 0: 不抑制<br />
        /// 1: 满足事件条件时抑制动作。
        /// </summary>
        PlayEventObjectTimeline = 0x019A,

        /// <summary>
        /// 停止角色及附属对象动作。<br />
        /// a1–a4 不使用。
        /// </summary>
        StopActorTimeline = 0x019B,

        /// <summary>
        /// 播放角色时间轴。<br />
        /// a1：ActionTimeline 行号。<br />
        /// 与 PlayActionTimeline（0x0197）共用客户端处理流程，两个实际类别编号均保留。
        /// </summary>
        PlayCharacterTimeline = 0x019C,

        /// <summary>
        /// 对包头指定 EventObject 播放动画。<br />
        /// a1：动画 ID。<br />
        /// a2：动画 state 参数。
        /// </summary>
        EObjAnimation = 0x019D,

        /// <summary>
        /// 角色显示与渐显。<br />
        /// a1：<br />
        /// 0: 隐藏<br />
        /// 1: 显示。<br />
        /// a2：显示时的渐变时长，单位 0.1 秒。
        /// </summary>
        SetRenderVisibility = 0x019E,

        /// <summary>
        /// 角色特殊隐藏。<br />
        /// a1–a4 不使用。
        /// </summary>
        SuppressActorRendering = 0x019F,

        /// <summary>
        /// 事件对象通用参数。<br />
        /// a1：事件对象附加参数；用途随对象种类变化。<br />
        /// 旧名：EObjSetSharedGroupState。
        /// </summary>
        SetEventObjectArgument = 0x01A0,

        /// <summary>
        /// 设置角色 OwnerId 并调整伙伴对象子类型。<br />
        /// a1：<br />
        /// OwnerId<br />
        /// 0xE0000000 表示无效对象。
        /// </summary>
        SetCompanionOwnerId = 0x01A1,

        /// <summary>
        /// 按 a3 指定的另一个 EObj 播放共享组动画。<br />
        /// a1：动画索引。<br />
        /// a2：动画状态。<br />
        /// a3：另一个事件对象的 EntityId，替代包头实体。
        /// </summary>
        EObjAnimationByOtherId = 0x01A2,

        /// <summary>
        /// 指定事件对象时间轴。<br />
        /// a1：SharedGroup 本地时间轴选择索引 0..15，经 TimelineIndices 映射；不是 ActionTimeline 资源 ID。<br />
        /// a2：动作快进时间，单位毫秒。<br />
        /// a3：<br />
        /// 0: 不抑制<br />
        /// 1: 满足事件条件时抑制动作。<br />
        /// a4：事件对象 EntityId，替代包头实体。
        /// </summary>
        PlayOtherEventObjectTimeline = 0x01A3,

        /// <summary>
        /// 停止事件对象时间轴。<br />
        /// a1：StopTimeline 的 index。
        /// </summary>
        StopEventObjectTimeline = 0x01A4,

        /// <summary>
        /// 停止指定事件对象时间轴。<br />
        /// a1：StopTimeline 的 index。<br />
        /// a3：事件对象 EntityId，替代包头实体。
        /// </summary>
        StopOtherEventObjectTimeline = 0x01A5,

        /// <summary>
        /// 设置角色称号。<br />
        /// a1：Title 行号。
        /// </summary>
        SetTitle = 0x01F4,

        /// <summary>
        /// 设置目标标记并可显示标记操作者日志。<br />
        /// a1：槽索引。<br />
        /// a2：操作者角色 ID。
        /// </summary>
        SetTargetSign = 0x01F6,

        /// <summary>
        /// 设置仇恨字母标记的实体映射。<br />
        /// a1：字母槽索引。<br />
        /// a2：该槽 EntityId。
        /// </summary>
        SetHateLetter = 0x01F7,

        /// <summary>
        /// 设置角色在线状态图标。<br />
        /// a1：OnlineStatus 行号。
        /// </summary>
        SetStatusIcon = 0x01F8,

        /// <summary>
        /// 更新极限技量表及 HUD 附加状态。<br />
        /// a1：BarCount。<br />
        /// a2：CurrentUnits。<br />
        /// a3：BarUnits。<br />
        /// a4：HUD 附加值（用途待确认）。<br />
        /// a5:<br />
        /// 0: 非 PvP<br />
        /// 1: PvP。
        /// </summary>
        LimitBreakGauge = 0x01F9,

        /// <summary>
        /// 设置返回点水晶。<br />
        /// a1：返回点 Aetheryte 编号。
        /// </summary>
        SetHomepoint = 0x01FB,

        /// <summary>
        /// 设置偏好传送水晶槽。<br />
        /// a1：偏好槽索引 0–3。<br />
        /// a2：Aetheryte 行号。
        /// </summary>
        SetFavorite = 0x01FC,

        /// <summary>
        /// 设置水晶解锁位图。<br />
        /// a1：Aetheryte 行号/位索引。<br />
        /// a2：<br />
        /// 0: 清位<br />
        /// 1: 设置位。
        /// </summary>
        LearnTeleport = 0x01FD,

        /// <summary>
        /// 开始地图事件更新。<br />
        /// a1：<br />
        /// bit0: 任务<br />
        /// bit1: 理符<br />
        /// bit2: 行会令<br />
        /// bit3: 九宫幻卡<br />
        /// bit4: 自定义对话<br />
        /// bit5: 事件上下文。<br />
        /// 地图标记组的刷新掩码。
        /// </summary>
        BeginMapUpdate = 0x01FF,

        /// <summary>
        /// 提交地图事件更新。<br />
        /// a1–a4 不使用。<br />
        /// 旧名：OpenRecommendationGuide。
        /// </summary>
        CommitMapEventUpdate = 0x0200,

        /// <summary>
        /// 收藏柜存取结果。<br />
        /// a1：<br />
        /// 0: 成功<br />
        /// 1–6: 错误码（逐值意义待确认）。<br />
        /// a2：收藏柜条目索引。<br />
        /// a3：<br />
        /// 0: 清除<br />
        /// 1: 登记。<br />
        /// 旧名：ArmoryErrorMsg。
        /// </summary>
        ArmoireResult = 0x0201,

        /// <summary>
        /// 接收指定成就当前进度。<br />
        /// a1：Achievement 行号。<br />
        /// a2：当前进度。<br />
        /// a3：最大进度。
        /// </summary>
        AchievementProgress = 0x0202,

        /// <summary>
        /// 标记成就完成并提示。<br />
        /// a1：Achievement 行号。
        /// </summary>
        AchievementPopup = 0x0203,

        /// <summary>
        /// 设置过场动画观看记录位图。<br />
        /// a1：Cutscene 索引。<br />
        /// a2：<br />
        /// 0: 清位<br />
        /// 1: 设置位。
        /// </summary>
        SetCutsceneFlag = 0x0204,

        /// <summary>
        /// 以角色为对象显示参数化 LogMessage。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：第 1 个模板参数。<br />
        /// a3：第 2 个模板参数。<br />
        /// a4：第 3 个模板参数。<br />
        /// a5/a6 为第 4/第 5 个模板参数；要求包头对应角色存在。
        /// </summary>
        LogMsg = 0x0205,

        /// <summary>
        /// 显示附近角色成就获得消息。<br />
        /// a1：Achievement 行号。
        /// </summary>
        AchievementMsg = 0x0206,

        /// <summary>
        /// 设置队伍成员过场/状态标志。<br />
        /// a1：被更新队伍成员的 EntityId；不是包头 EntityId。<br />
        /// a2：bit1: 过场动画状态。
        /// </summary>
        SetCutsceneFlags = 0x0207,

        /// <summary>
        /// 更新本机装等及界面通知。<br />
        /// a1：装等值。
        /// </summary>
        SetItemLevel = 0x0209,

        /// <summary>
        /// 挑战笔记条目完成及提示。<br />
        /// a1：ContentsNote 条目编号。<br />
        /// a2：第一类奖励数量；仅该奖励存在时使用。<br />
        /// a3：第二类奖励数量；仅该奖励存在时使用。
        /// </summary>
        ChallengeEntryCompleteMsg = 0x020B,

        /// <summary>
        /// 提示挑战笔记开放新内容或追加新项目，并刷新解锁列表。<br />
        /// a1：<br />
        /// 0: =Log3782“追加了新的项目”<br />
        /// 1: =Log3781“开放了新的内容”。
        /// </summary>
        ChallengeEntryUnlockMsg = 0x020C,

        /// <summary>
        /// 源与目标的四参数日志。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：目标 EntityId。<br />
        /// a3：第 1 个模板参数。<br />
        /// a4：第 2 个模板参数。<br />
        /// 旧名：DesynthOrReductionResult。a5/a6 为第 3/第 4 个模板参数。
        /// </summary>
        ShowSourceTargetMessage = 0x020F,

        /// <summary>
        /// 本机与目标日志。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：目标 EntityId；不存在时尝试小队成员资料及备用名。<br />
        /// a3：文本数值参数 1。<br />
        /// a4：文本数值参数 2。<br />
        /// a5/a6 为文本数值参数 3/4。
        /// </summary>
        ShowLocalSourceTargetMessage = 0x0210,

        /// <summary>
        /// 提示免费试玩持有金币达到上限，丢弃超额金币。<br />
        /// a1–a4 不使用。
        /// </summary>
        GilTrailMsg = 0x0211,

        /// <summary>
        /// 角色飘字提示。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：传 AddToScreenLogWithLogMessageId 的 value3，其显示业务取决于模板。
        /// </summary>
        ShowActorScreenLog = 0x0214,

        /// <summary>
        /// 五参数通用日志。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：文本数值参数 1。<br />
        /// a3：文本数值参数 2。<br />
        /// a4：文本数值参数 3。<br />
        /// a5/a6 分别为文本数值参数 4/5。
        /// </summary>
        ShowFiveParameterMessage = 0x0215,

        /// <summary>
        /// 清除焦点目标。<br />
        /// a1：当前 FocusTarget 的 EntityId 匹配值。
        /// </summary>
        ClearFocusTarget = 0x0219,

        /// <summary>
        /// 显示讨伐笔记解锁提示。<br />
        /// a1：ClassJob 行号，LogMessage1011 的 ClassJob(lnum1)。<br />
        /// a2：显示等级，LogMessage1011 的 lnum2。
        /// </summary>
        HuntingLogRankUnlock = 0x021D,

        /// <summary>
        /// 讨伐笔记目标击杀计数更新提示。<br />
        /// a1：MonsterNote 行号。<br />
        /// a2：该行目标索引。<br />
        /// a3：新的击杀计数。
        /// </summary>
        HuntingLogEntryUpdate = 0x021E,

        /// <summary>
        /// 显示讨伐笔记条目完成提示。<br />
        /// a1：MonsterNote 资源行号：职业条目为 ClassJobId*10000+条目序号，军队为 GrandCompanyId*1000000+序号。<br />
        /// a2：完成横幅的图标编号来源：职业路径 icon=62100+a2；军队路径 a2 非 0 时 icon=60870+a2，为 0 时不显示图标。
        /// </summary>
        HuntingLogSectionFinish = 0x021F,

        /// <summary>
        /// 显示讨伐笔记等级完成提示。<br />
        /// a1：<br />
        /// 0: 剑术师<br />
        /// 1: 格斗家<br />
        /// 2: 斧术师<br />
        /// 3: 枪术师<br />
        /// 4: 弓箭手<br />
        /// 5: 幻术师<br />
        /// 6: 咒术师<br />
        /// 7: 秘术师<br />
        /// 8: 黑涡团<br />
        /// 9: 双蛇党<br />
        /// 10: 恒辉队<br />
        /// 11: 双剑师。<br />
        /// a2：完成等级的索引（从 0 开始）。<br />
        /// 提示显示等级为 a2+1。
        /// </summary>
        HuntingLogRankFinish = 0x0220,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 保留历史名称，不能据此认定当前客户端仍用于设置装备套装上限。
        /// </summary>
        SetMaxGearSets = 0x0230,

        /// <summary>
        /// 源与目标的三参数日志。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：source BattleChara EntityId。<br />
        /// a3：target BattleChara EntityId。<br />
        /// a4：文本数值参数 1。<br />
        /// a5/a6 分别为文本数值参数 2/3。
        /// </summary>
        ShowSourceTargetThreeParameterMessage = 0x0257,

        /// <summary>
        /// 指定角色显示与动作。<br />
        /// a1：目标 EntityId。<br />
        /// a2：<br />
        /// 1: 淡出<br />
        /// 2: 特效并淡出<br />
        /// 3: 播放动作<br />
        /// 4: 重置<br />
        /// 5: 播放对象动作。<br />
        /// a3：随 a2 选择的特效／ActionTimeline 编号。<br />
        /// a4：子命令 1/2 渐隐时长，除 100 为秒。
        /// </summary>
        ActorVisualControl = 0x025F,

        /// <summary>
        /// 角色显示与个人设置。<br />
        /// a1：角色装备显示及个人设置位图。<br />
        /// 部分语义待确认。逐位完整定义待确认。
        /// </summary>
        SetCharaGearParamUI = 0x0260,

        /// <summary>
        /// 切换场景线框渲染。<br />
        /// a1–a4 不使用。
        /// </summary>
        ToggleWireframeRendering = 0x0261,

        /// <summary>
        /// 两个实体间特效。<br />
        /// a1：VFX 表行号。<br />
        /// a2：source EntityId，0xE0000000 允许空。<br />
        /// a3：target EntityId，0xE0000000 允许空。
        /// </summary>
        PlayVfxBetweenActors = 0x0262,

        /// <summary>
        /// 角色渐隐或隐藏。<br />
        /// a1：淡出时间；0 立即完成，其余以 0.1 秒为单位。
        /// </summary>
        HideActor = 0x0263,

        /// <summary>
        /// 重置角色动作与武器状态。<br />
        /// a1–a4 不使用。
        /// </summary>
        ResetActorPresentation = 0x0264,

        /// <summary>
        /// 动作被拒绝，取消匹配动作并校正冷却。<br />
        /// a1：BattleLog 错误/LogMessage ID。<br />
        /// a2：ActionType。<br />
        /// a3：动作 ID。<br />
        /// a4：冷却已流逝时间 newElapsed，除 100 转秒。<br />
        /// a5: 冷却总时长，单位 0.01 秒<br />
        /// a6: 动作来源序列号 CastInfo.SourceSequence，非 0 且匹配当前值时取消施法表现<br />
        /// a4、a5 同时为 0 时采用另一冷却重置路径。
        /// </summary>
        ActionRejected = 0x02BC,

        /// <summary>
        /// 无对象通用战斗日志。<br />
        /// a1：LogMessage 行号。
        /// </summary>
        ShowBattleLog = 0x02BD,

        /// <summary>
        /// 位移或传送请求失败。<br />
        /// a1：移动请求模式/操作码。<br />
        /// a2：<br />
        /// 0: 播放失败动作<br />
        /// 1: 跳过失败动作。<br />
        /// a3：<br />
        /// 2: 目的地区域暂停进入<br />
        /// 3: 静默收尾<br />
        /// 4: 无法移动到下一区域<br />
        /// 5: 直接返回<br />
        /// 6: 显示 a4 消息<br />
        /// 7: 主线任务条件不满足<br />
        /// 8: 显示 a4 消息<br />
        /// 其他值: 无法离开区域。<br />
        /// a4：原因 6、8 时使用的 LogMessage 编号。<br />
        /// 部分语义待确认。
        /// </summary>
        WarpRequestFailed = 0x02BE,

        /// <summary>
        /// 通知查看装备请求失败，结束等待并更新失败界面。<br />
        /// a1–a4 不使用。
        /// </summary>
        ExamineError = 0x02BF,

        /// <summary>
        /// 中层屏幕过渡。<br />
        /// a1–a4 不使用。
        /// </summary>
        FadeMiddle = 0x02C0,

        /// <summary>
        /// 三参数通用日志。<br />
        /// a1：LogMessage 行号。<br />
        /// a2：文本数值参数 1。<br />
        /// a3：文本数值参数 2。<br />
        /// a4：文本数值参数 3。
        /// </summary>
        ShowThreeParameterMessage = 0x02C1,

        /// <summary>
        /// 物品或技能使用失败。<br />
        /// a1：ActionType；2 选 LogMessage539，其他选 613。<br />
        /// a2：Action ID/该类型对应资源 ID。
        /// </summary>
        ActionUseFailed = 0x02C2,

        /// <summary>
        /// 指定角色单数值日志。<br />
        /// a1：source BattleChara EntityId。<br />
        /// a2：LogMessage 行号。<br />
        /// a3：文本数值参数 1。
        /// </summary>
        ShowActorNumberMessage = 0x02C4,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 名称来自 Sapphire 历史定义；当前客户端参数语义未确认。
        /// </summary>
        SetMaxGearSetsSapphire = 0x0320,

        /// <summary>
        /// 完成套装装备响应，按本地逐槽结果提示并更新套装状态。<br />
        /// a1：<br />
        /// 套装索引，普通索引为零基<br />
        /// 100 为恢复原装备<br />
        /// 254 为最强装备特殊结果。
        /// </summary>
        GearSetEquipMsg = 0x0321,

        /// <summary>
        /// 套装肖像预览应答。<br />
        /// a1–a4 不使用。<br />
        /// 旧名：SetBait。
        /// </summary>
        GearsetPortraitResponse = 0x0325,

        /// <summary>
        /// 复活倒计时与战章变化。<br />
        /// a1：角色 EntityId；等于本机 sourceId 时更新本机 Revive，观战路径按此 ID 匹配角色记录。<br />
        /// a2：剩余时间，单位 0.1 秒。<br />
        /// a3：复活阈值时间，单位 0.1 秒。<br />
        /// a4：更新前持有的战章数。<br />
        /// a5: Feast 掉落后的战章持有量<br />
        /// 观战路径只使用 a1–a3。
        /// </summary>
        SetRespawnState = 0x0358,

        /// <summary>
        /// 炮台弹药可用值。<br />
        /// a1：弹药槽索引 0–3。<br />
        /// a2：<br />
        /// 0: 无弹药<br />
        /// 1: 有弹药。<br />
        /// 部分语义待确认。具体数量单位待确认。
        /// </summary>
        SetBallistaAmmunition = 0x0385,

        /// <summary>
        /// 排队更新八项场景节庆活动。<br />
        /// a1：第 1 项 Festival：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a2：第 2 项 Festival：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a3：第 3 项 Festival：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a4：第 4 项 Festival：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a5–a8 携带同布局的第五至第八项；排队等待当前拍照／过场等事件结束后应用。
        /// </summary>
        SetFestival = 0x0386,

        /// <summary>
        /// 设置坐骑解锁位并提示新获得坐骑。<br />
        /// a1：坐骑解锁位索引（Mount.Order），要求&lt;360。<br />
        /// a2：<br />
        /// 0: 撤销<br />
        /// 1: 解锁。<br />
        /// a3：Mount 行号，用来检查是否已解锁及显示 LogMessage610。
        /// </summary>
        SetMountBitmask = 0x0387,

        /// <summary>
        /// 炮台或设施不可用提示。<br />
        /// a1：错误类型 1..6 分别日志 647..652。
        /// </summary>
        BallistaUnavailableMessage = 0x0389,

        /// <summary>
        /// 更新五项周期计数数组中的一项。<br />
        /// a1：1..5，转零基数组索引。<br />
        /// a2：新值。<br />
        /// 部分语义待确认。
        /// </summary>
        SetCycleTimes = 0x038B,

        /// <summary>
        /// 玩家功能与资格旗标。<br />
        /// a1：旗标组索引 0–2。<br />
        /// a2：所选组的八项功能／资格旗标；部分位含义待确认。<br />
        /// 部分语义待确认。全局位编号=8×a1+ 组内 bit 号；未命名位保留待确认。
        /// </summary>
        SetPlayerFlags = 0x038C,

        /// <summary>
        /// 固定界面提示音 20。<br />
        /// a1–a4 不使用。
        /// </summary>
        PlayUiSound20 = 0x0391,

        /// <summary>
        /// 下坐骑应答。<br />
        /// a1：下坐骑请求序号；与当前待应答序号匹配才清除请求。<br />
        /// a2：<br />
        /// 0: 不显示失败<br />
        /// 1: 无骑乘权限<br />
        /// 2: 无法在跳跃／飞行中执行<br />
        /// 其他值: 通用失败提示。
        /// </summary>
        DismountResponse = 0x0393,

        /// <summary>
        /// 登记待处理事件。<br />
        /// a1：完整 EventHandlerId，0 忽略。
        /// </summary>
        RegisterPendingEventHandler = 0x0394,

        /// <summary>
        /// 新手教学对话进度。<br />
        /// a1：<br />
        /// bit0: 首次欢迎对话完成<br />
        /// bit1: 首次训练后的指导对话完成。<br />
        /// 旧名：ToggleOrchestrionUnlock。其他位意义待确认。
        /// </summary>
        SetHallOfNoviceProgress = 0x0396,

        /// <summary>
        /// 解锁管弦乐谱。<br />
        /// a1：OrchestrionRoll 编号。<br />
        /// a2：<br />
        /// 0: 清除解锁<br />
        /// 1: 解锁并显示提示。<br />
        /// a3：提示使用的 ItemId。<br />
        /// 解锁的乐谱编号与提示的物品编号分开传递。<br />
        /// 旧定义把此操作误放在 0x0396，已更正到实际编号。
        /// </summary>
        ToggleOrchestrionUnlock = 0x0397,

        /// <summary>
        /// 节庆活动阶段。<br />
        /// a1：第 1 项活动：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a2：第 2 项活动：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a3：第 3 项活动：bits0–15 为编号，bits16–31 为阶段。<br />
        /// a4：第 4 项活动：bits0–15 为编号，bits16–31 为阶段。<br />
        /// 旧名：EventBattleDialog。a5–a8 为相同布局的第五至第八项活动。
        /// </summary>
        SetFestivalProgress = 0x039C,

        /// <summary>
        /// 分发简单服务器请求的回调响应。<br />
        /// a1：callbackIndex。<br />
        /// a2：commandId。<br />
        /// a3：响应载荷第一项。<br />
        /// a4：响应载荷第二项。<br />
        /// a5: 响应载荷第三项<br />
        /// a6: 响应载荷第四项<br />
        /// 载荷语义由 commandId 及注册的请求决定。
        /// </summary>
        ServerRequestCallbackResponse = 0x039D,

        /// <summary>
        /// PvP 快捷聊天。<br />
        /// a1：发言者 EntityId。<br />
        /// a2：QuickChat 行号。<br />
        /// a3：快捷发言编号为 28 时使用的 ClassJob 编号。<br />
        /// a4：目标 EntityId。<br />
        /// 旧名：SetMountSpeed。
        /// </summary>
        PvpQuickChat = 0x039F,

        /// <summary>
        /// 坐骑地面基础速度。<br />
        /// a1：地面基础速度覆盖值；0 使用默认表值。<br />
        /// 仅在坐骑使用非默认 MoveControl 配置时覆盖 GroundSpeed；不是额外倍率。<br />
        /// 旧定义误放在 0x039F；此处设置地面基础速度，不是速度倍率。
        /// </summary>
        SetMountSpeed = 0x03A0,

        /// <summary>
        /// 技能追加效果飘字。<br />
        /// a1：1: 接受本次提示。<br />
        /// a2：<br />
        /// 0: 感叹号样式<br />
        /// 1: 普通样式。<br />
        /// a3：a2=0 时为日志中的 ActionId。<br />
        /// a4：a2=0 时为日志中的 StatusId。<br />
        /// 旧名：Dismount。a5: 飘字主数值；a2=0 时也作为日志中的百分比数值。
        /// </summary>
        ShowActionEffectNumber = 0x03A1,

        /// <summary>
        /// 下坐骑与位置校正。<br />
        /// a1：压缩 X。<br />
        /// a2：压缩 Y 高度。<br />
        /// a3：压缩 Z。<br />
        /// a4：<br />
        /// bit3: 抑制坐骑离开动作<br />
        /// bit4: 抑制骑手落差探测／校正<br />
        /// bit5: 同时抑制上述两项及下坐骑声音。<br />
        /// 旧名：BeginReplayAck。<br />
        /// 旧定义误放在 0x03A1；此操作下坐骑并校正位置。
        /// </summary>
        Dismount = 0x03A2,

        /// <summary>
        /// 副本回放开始应答。<br />
        /// a1：<br />
        /// 0: 不允许开始<br />
        /// 1: 允许开始。<br />
        /// 旧名：EndReplayAck。<br />
        /// 旧定义误放在 0x03A2。
        /// </summary>
        BeginReplayAck = 0x03A3,

        /// <summary>
        /// 副本回放结束应答。<br />
        /// a1：<br />
        /// 0: 本地结束<br />
        /// 1: 重新初始化区域。<br />
        /// 旧定义误放在 0x03A3。
        /// </summary>
        EndReplayAck = 0x03A4,

        /// <summary>
        /// 回放等级资料。<br />
        /// a1：等级。
        /// </summary>
        SetReplayLevel = 0x03A6,

        /// <summary>
        /// 装备时尚配饰。<br />
        /// a1：Ornament 编号；0 清除。<br />
        /// a2：<br />
        /// 0: 常规装配<br />
        /// 1: 角色复制装配<br />
        /// 2: 请求保留姿态<br />
        /// 3: 初始资料装配<br />
        /// 4: 解除抑制后恢复，并请求保留姿态。<br />
        /// 模式 0、1 参与收刀；模式 2、4 能否保留姿态取决于当前动作和表情类别。
        /// </summary>
        SetFashionAccessory = 0x03AC,

        /// <summary>
        /// 自动撑伞配饰。<br />
        /// a1：AutoUmbrellaOrnamentId。
        /// </summary>
        SetUmbrellaAccessory = 0x03AF,

        /// <summary>
        /// 眼镜与面部配饰。<br />
        /// a1：眼镜槽 index（通常 0）。<br />
        /// a2：Glasses 编号。<br />
        /// a3：<br />
        /// 0: 不刷新<br />
        /// 1: 刷新。<br />
        /// a4：面部配饰相关选项（具体意义待确认）。
        /// </summary>
        SetFacewear = 0x03B0,

        /// <summary>
        /// 建房申请应答。<br />
        /// a1：地块／公寓分区编号。<br />
        /// a2：<br />
        /// 0: 请求成功<br />
        /// 1: 请求失败。<br />
        /// 资料缓存不匹配时可能重新请求地块资料。
        /// </summary>
        ShowBuildPresetUI = 0x03E9,

        /// <summary>
        /// 外部装潢申请应答。<br />
        /// a1：地块／公寓分区编号。<br />
        /// a2：<br />
        /// 0: 请求成功<br />
        /// 1: 请求失败。<br />
        /// 资料缓存不匹配时可能重新请求地块资料。
        /// </summary>
        ShowEstateExternalAppearanceUI = 0x03EA,

        /// <summary>
        /// 房屋内装请求响应/界面。<br />
        /// a1：<br />
        /// 0: 成功<br />
        /// 1: 失败。
        /// </summary>
        ShowEstateInternalAppearanceUI = 0x03EB,

        /// <summary>
        /// 清除对应房屋预设请求选择标识。<br />
        /// a1：当前待应答的房屋预设选择标识；匹配时清除等待状态。
        /// </summary>
        BuildPresetResponse = 0x03ED,

        /// <summary>
        /// 移除庭具应答。<br />
        /// a1：住宅单元与室外槽的组合索引。<br />
        /// a2：<br />
        /// 0: 成功<br />
        /// 1: 失败。
        /// </summary>
        RemoveExteriorHousingItem = 0x03EF,

        /// <summary>
        /// 移除家具应答。<br />
        /// a1：场景物件槽索引。<br />
        /// a2：<br />
        /// 0: 成功<br />
        /// 1: 失败。
        /// </summary>
        RemoveInteriorHousingItem = 0x03F1,

        /// <summary>
        /// 房屋物件编辑请求响应/打开界面。<br />
        /// a1：<br />
        /// 0: 编辑成功<br />
        /// 1: 编辑失败。
        /// </summary>
        ShowHousingItemUI = 0x03F7,

        /// <summary>
        /// 确认家具移动。<br />
        /// a1：<br />
        /// bits0–15: 已布置家具 InventoryType<br />
        /// bits16–23: 室外 HouseUnit。<br />
        /// a2：家具槽索引。<br />
        /// 确认本地家具当前位置及朝向；包内不直接传 XYZ。
        /// </summary>
        HousingItemMoveConfirm = 0x03F9,

        /// <summary>
        /// 住房设置菜单控制。<br />
        /// a1：<br />
        /// 0: 抑制菜单重新打开<br />
        /// 1: 允许菜单重新打开。
        /// </summary>
        OpenEstateSettingsUI = 0x03FF,

        /// <summary>
        /// 个人房间入口可选中状态。<br />
        /// a1：<br />
        /// 0: 常规门交互处理<br />
        /// 1: 附加门交互处理。<br />
        /// 旧名：HideAdditionalChambersDoor。具体提示依住宅归属、共享权限及室内类型变化。
        /// </summary>
        SetChambersDoorTargetability = 0x0400,

        /// <summary>
        /// 刷新家具/庭具仓库状况，以及搬迁和内部整装空间条件。<br />
        /// a1：<br />
        /// bits0–7: 庭具容量<br />
        /// bits8–15: 庭具仓库数量<br />
        /// bits16–23: 已布置庭具数量<br />
        /// bits24–31: 庭具搬迁空间条件 0/1。<br />
        /// a2：<br />
        /// bits0–15: 已布置家具数量<br />
        /// bits16–23: 家具搬迁空间条件 0/1<br />
        /// bits24–31: 内部整装空间条件 0/1。<br />
        /// a3：<br />
        /// bits0–15: 已布置家具容量<br />
        /// bits16–31: 家具仓库数量。<br />
        /// a4：家具仓库容量。<br />
        /// 空间条件：<br />
        /// 0: 不通过<br />
        /// 1: 通过<br />
        /// 庭具已布置与仓库共用 a1 中的容量值。
        /// </summary>
        HousingStoreroomStatus = 0x0419,

        /// <summary>
        /// 设置幻卡卡片获得位图和未查看标志。<br />
        /// a1：TripleTriadCardResident 行号。<br />
        /// a2：<br />
        /// 0: 撤销<br />
        /// 1: 获得。
        /// </summary>
        TripleTriadCard = 0x04B4,

        /// <summary>
        /// 幻卡 NPC 胜利记录。<br />
        /// a1：TripleTriadResident 行号；当前入口接受 2293762–2293905。<br />
        /// a2：<br />
        /// 0: 未击败 NPC<br />
        /// 1: 已击败 NPC。<br />
        /// 旧名：TripleTriadUnknown。
        /// </summary>
        SetTripleTriadNpcDefeated = 0x04B5,

        /// <summary>
        /// 状态触发提示与特效。<br />
        /// a1：Status 行号。<br />
        /// a2：状态 param，传 BattleLog。
        /// </summary>
        ShowStatusProc = 0x05DF,

        /// <summary>
        /// 设置角色 PvP 状态。<br />
        /// a1：PvP 状态值。
        /// </summary>
        SetPvPState = 0x05E0,

        /// <summary>
        /// 结束决斗会话并清理标志。<br />
        /// a1–a4 不使用。
        /// </summary>
        EndDuelSession = 0x05E1,

        /// <summary>
        /// 开始决斗倒数提示。<br />
        /// a1–a4 不使用。
        /// </summary>
        StartDuelCountdown = 0x05E2,

        /// <summary>
        /// 记录决斗对手。<br />
        /// a1：决斗对手 EntityId。
        /// </summary>
        StartDuel = 0x05E3,

        /// <summary>
        /// 决斗结果演出与会话清理。<br />
        /// a1：<br />
        /// 0: 结果演出 432<br />
        /// 1: 结果演出 433<br />
        /// 其他值: 仅清理。<br />
        /// 两种结果演出的胜负对应关系待确认。
        /// </summary>
        DuelResultScreen = 0x05E4,

        /// <summary>
        /// 角色间特效。<br />
        /// a1：VFX ID。<br />
        /// a2：源角色 EntityId。<br />
        /// a3：目标角色 EntityId。
        /// </summary>
        PlayVfxBetweenCharacters = 0x05E5,

        /// <summary>
        /// 带来源序号的技能动作。<br />
        /// a1：Action 行号，同时作为动作原始 ID。<br />
        /// a2：目标 64 位标识高 32 位。<br />
        /// a3：目标 64 位标识低 32 位。<br />
        /// a4：动作来源序列号，与 CastInfo.ResponseSourceSequence 匹配。<br />
        /// 与全局效果序列号 GlobalSequence 含义不同。
        /// </summary>
        PlaySequencedAction = 0x05E6,

        /// <summary>
        /// 清除状态附加参数。<br />
        /// a1：Status ID；仅 858 或 865 处理。
        /// </summary>
        ClearStatusParameter = 0x05E7,

        /// <summary>
        /// 设置 DutyAction 组。<br />
        /// a1：组号；与当前组相同则不重复初始化，组 24 有特殊标志。
        /// </summary>
        SetDutyActionSet = 0x05E8,

        /// <summary>
        /// 设置两个职责技能及 HUD 附加状态。<br />
        /// a1：第一项 DutyAction 的 ActionId。<br />
        /// a2：第一项 HUD 附加状态（意义待确认）。<br />
        /// a3：第二项 DutyAction 的 ActionId。<br />
        /// a4：第二项 HUD 附加状态（意义待确认）。<br />
        /// a5、a6 分别为另外两项 HUD 附加状态（意义待确认）。
        /// </summary>
        SetDutyActionDetails = 0x05E9,

        /// <summary>
        /// 设置五个 DutyAction 条目与各条目的存在标志。<br />
        /// a1：第 1 项 DutyAction 的 ActionId。<br />
        /// a2：第 2 项 DutyAction 的 ActionId。<br />
        /// a3：第 3 项 DutyAction 的 ActionId。<br />
        /// a4：第 4 项 DutyAction 的 ActionId。<br />
        /// a5: 第五项 DutyAction 的 ActionId<br />
        /// 0 表示对应项不可用；组 24 使用 PhantomAction 槽。
        /// </summary>
        SetDutyActionPresent = 0x05EA,

        /// <summary>
        /// DutyAction 总体激活开关。<br />
        /// a1：<br />
        /// 0: 非活动状态<br />
        /// 1: 活动状态。
        /// </summary>
        SetDutyActionActive = 0x05EB,

        /// <summary>
        /// 职责技能可用状态。<br />
        /// a1：<br />
        /// 0: 第一组不可用<br />
        /// 1: 第一组可用。<br />
        /// a2：<br />
        /// 0: 第二组不可用<br />
        /// 1: 第二组可用。<br />
        /// 旧名：SetDutyActionCharges。
        /// </summary>
        SetDutyActionAvailability = 0x05EC,

        /// <summary>
        /// 职责技能当前充能数。<br />
        /// a1：第一组当前充能数。<br />
        /// a2：第二组当前充能数。<br />
        /// 旧定义把此操作误放在 0x05EC，已更正到实际编号。
        /// </summary>
        SetDutyActionCharges = 0x05ED,

        /// <summary>
        /// 诗人乐谱演出提示。<br />
        /// a1–a4 不使用。
        /// </summary>
        BardRepertoireEffect = 0x05EE,

        /// <summary>
        /// 冻结动作源的效果派发。<br />
        /// a1：ActionEffect.Header.GlobalSequence；0 不匹配有效缓存。<br />
        /// a2：ActionEffect 中的目标索引，从 0 开始。<br />
        /// 仅处理本机或回放观察角色的缓存；动作来源角色处于动作冻结状态时，按全局序号和目标索引派发缓存效果，不重新执行技能。
        /// </summary>
        DispatchFrozenActionEffects = 0x05EF,

        /// <summary>
        /// 青魔法技能槽。<br />
        /// a1：技能槽 0..23。<br />
        /// a2：Action 行号，0 可清空。
        /// </summary>
        SetBlueMageActionSlot = 0x05F0,

        /// <summary>
        /// 带技能的死亡动作。<br />
        /// a1：触发死亡表现的 Action 行号；0 走普通死亡动画。<br />
        /// a2：压缩朝向，转-π..π供技能表现路径。<br />
        /// a3：死亡 ActionTimeline；0 自动选择，FFFF 及 4057 有特殊处理。
        /// </summary>
        DeathWithAction = 0x05F1,

        /// <summary>
        /// 打断动作并保留已打断施法条。<br />
        /// a1：ActionId。<br />
        /// a2：保留已打断施法条的秒数。<br />
        /// a3：动作数据允许时为日志来源 EntityId。
        /// </summary>
        InterruptActionCast = 0x05F2,

        /// <summary>
        /// 角色渐隐后隐藏。<br />
        /// a1–a4 不使用。
        /// </summary>
        FadeOutAndHide = 0x05F3,

        /// <summary>
        /// 设置角色半透明。<br />
        /// a1–a4 不使用。
        /// </summary>
        SetActorTransparency = 0x05F4,

        /// <summary>
        /// 副本助手 NPC。<br />
        /// a1：副本助手槽索引 0–6。<br />
        /// a2：ENpcId。
        /// </summary>
        SetDutyHelperNpc = 0x05F5,

        /// <summary>
        /// 支援 NPC 极限技动作。<br />
        /// a1–a4 不使用。
        /// </summary>
        PlaySupportNpcLimitBreak = 0x05F7,

        /// <summary>
        /// 舞者结束技量谱特效。<br />
        /// a1–a4 不使用。
        /// </summary>
        DancerFinisherEffect = 0x05F8,

        /// <summary>
        /// 重置并播放角色动作。<br />
        /// a1：ActionTimeline 编号；按有符号整数解释，大于 0 时播放，其他值仅重置。
        /// </summary>
        ResetAndPlayTimeline = 0x05F9,

        /// <summary>
        /// 护盾百分比。<br />
        /// a1：ShieldValue。
        /// </summary>
        SetShieldPercent = 0x05FA,

        /// <summary>
        /// 按 StanceChange 表启动形态变换状态机。<br />
        /// a1：StanceChange 行号。
        /// </summary>
        StanceChange = 0x05FB,

        /// <summary>
        /// 目标 HUD 补充状态。<br />
        /// a1：Status 编号；0 为空槽。<br />
        /// a2：剩余秒数，IEEE754 单精度浮点数位模式。<br />
        /// a3：状态显示所属目标 EntityId。<br />
        /// 按目标 EntityId 匹配；a3 不是状态来源 EntityId。
        /// </summary>
        SetTargetHudStatus = 0x05FC,

        /// <summary>
        /// 移除目标 HUD 补充状态。<br />
        /// a1：Status 表 RowId。<br />
        /// a2：目标 EntityId。<br />
        /// 按状态编号和目标 EntityId 精确匹配，无通配。
        /// </summary>
        RemoveTargetHudStatus = 0x05FD,

        /// <summary>
        /// 联盟成员职业与 HP。<br />
        /// a1：ClassJob。<br />
        /// a2：绝对 CurrentHP。<br />
        /// a3：绝对 MaxHP。
        /// </summary>
        SetAllianceMemberVitals = 0x05FE,

        /// <summary>
        /// 队伍成员 HP。<br />
        /// a1：当前 HP。
        /// </summary>
        SetPartyMemberHp = 0x05FF,

        /// <summary>
        /// 武僧脉轮消耗动画。<br />
        /// a1–a4 不使用。<br />
        /// 旧名：IncrementRecast。
        /// </summary>
        MonkChakraConsumptionEffect = 0x0600,

        /// <summary>
        /// 增加冷却已过时间。<br />
        /// a1：冷却组索引。<br />
        /// a2：无符号整数/100 秒的正向增量（0.01 秒单位）。<br />
        /// 旧定义把此操作误放在 0x0600，已更正到实际编号。
        /// </summary>
        IncrementRecast = 0x0601,

        /// <summary>
        /// 本机移动后转向。<br />
        /// a1：IEEE-754 浮点数位模式 X。<br />
        /// a2：IEEE-754 浮点数位模式 Y 高度。<br />
        /// a3：IEEE-754 浮点数位模式 Z。<br />
        /// a4：IEEE-754 浮点数位模式，目标朝向（弧度）。<br />
        /// a5 为 IEEE-754 浮点数位模式的水平移动速度（世界坐标单位/逻辑秒）；a6..a8 未读取。
        /// </summary>
        MoveAndTurnLocalActor = 0x0602,

        /// <summary>
        /// 结束本机移动并校正位置。<br />
        /// a1：IEEE-754 浮点数位模式 X。<br />
        /// a2：IEEE-754 浮点数位模式 Y 高度。<br />
        /// a3：IEEE-754 浮点数位模式 Z。
        /// </summary>
        FinishLocalMove = 0x0603,

        /// <summary>
        /// 持续治疗提示/效果处理。<br />
        /// a1：StatusId。<br />
        /// a2：治疗量。<br />
        /// a3：来源 EntityId。<br />
        /// a4：damageType/效果类型，传 ProcessHotDot；治疗模式部分路径不使用。
        /// </summary>
        HoT = 0x0604,

        /// <summary>
        /// 持续伤害提示/效果处理。<br />
        /// a1：StatusId。<br />
        /// a2：伤害量。<br />
        /// a3：来源 EntityId。<br />
        /// a4：damageType，非负时转换为飘字类型。
        /// </summary>
        DoT = 0x0605,

        /// <summary>
        /// 场景区域特效触发。<br />
        /// a1：区域特效触发编号。<br />
        /// 与当前编号相同则无操作。
        /// </summary>
        SetAreaObjectVfxTrigger = 0x0606,

        /// <summary>
        /// 斗兽战斗反馈。<br />
        /// a1：<br />
        /// 0: 精准闪躲<br />
        /// 1: 精准反击<br />
        /// 2: 升腾火焰<br />
        /// 3: 攻防事件。<br />
        /// 反馈种类，不是强弱等级。
        /// </summary>
        ShowBeastmasterBattleFeedback = 0x0607,

        /// <summary>
        /// 兽笛选定魔兽。<br />
        /// a1：一号兽笛的 XBMPet 编号。<br />
        /// a2：二号兽笛的 XBMPet 编号。<br />
        /// a3：三号兽笛的 XBMPet 编号。<br />
        /// 0 表示对应兽笛未选定魔兽。
        /// </summary>
        SetBeastWhistles = 0x0609,

        /// <summary>
        /// 角色持有特效。<br />
        /// a1：VFX ID。
        /// </summary>
        PlayPersistentActorVfx = 0x060A,

        /// <summary>
        /// 驯兽师捕获演出与图鉴解锁。<br />
        /// a1：捕获目标 EntityId，RepresentationContainer 中按此查 BattleChara。<br />
        /// a2：累计结识魔兽种类数量；作为 LogMessage11400 的数值参数。<br />
        /// a3：宠物图鉴解锁索引。
        /// </summary>
        BeastMasterLearn = 0x060B,

        /// <summary>
        /// 清除对象目标引用。<br />
        /// a1：GameObjectId，0xE0000000 跳过。
        /// </summary>
        ClearTargetReferences = 0x060C,

        /// <summary>
        /// 副本背景音乐。<br />
        /// a1：BGM 编号。
        /// </summary>
        SetContentBgm = 0x0646,

        /// <summary>
        /// 恢复 NPC 默认外观。<br />
        /// a1：ENpcBase／ENpcResident 共享的 NPC 基础编号。<br />
        /// 与场景中的 EntityId 不同。
        /// </summary>
        ResetNpcAppearanceOverride = 0x06A4,

        /// <summary>
        /// 刷新 NPC 外观覆盖。<br />
        /// a1–a4 不使用。
        /// </summary>
        RefreshNpcAppearanceOverrides = 0x06A5,

        /// <summary>
        /// 同步优雷卡剧情进度、等级资格值及 32 位剧情旗标。<br />
        /// a1：EurekaStoryProgress 剧情进度编号。<br />
        /// a2：剧情资格使用的元素等级。<br />
        /// a3：剧情／场景资格旗标。<br />
        /// 部分语义待确认。剧情先后按资源定义的链顺序比较；旗标 bit20 还参与场景组显示门槛，各位完整意义待确认。
        /// </summary>
        EurekaStep = 0x073A,

        /// <summary>
        /// 副本战斗对白。<br />
        /// a1：<br />
        /// 0: 显示姓名<br />
        /// 1: 隐藏姓名。<br />
        /// a2：完整 32 位 ContentDirector 文本索引，经 Director 事件专用处理取 Utf8String；不是通用 NpcYell 或 LogMessage 行号。<br />
        /// a3：显示时长，单位毫秒。
        /// </summary>
        ShowContentBattleTalk = 0x0834,

        /// <summary>
        /// 显示新月岛同款的获取宝物弹窗。<br />
        /// a1 为显示方式。1 隐藏数量，2–4 读取普通物品数量。<br />
        /// a2 为 Item 或 EventItem 编号。<br />
        /// a3 为普通物品数量。
        /// </summary>
        ShowTreasureItem = 0x0835,

        /// <summary>
        /// FATE 对象交互资格。<br />
        /// a1：对象 EntityId。<br />
        /// a2：<br />
        /// bit0: 允许非战斗 NPC 的 FATE 交互<br />
        /// bit1: 职业匹配且超过 FATE 等级上限时拒绝交互。<br />
        /// 其他位意义待确认。
        /// </summary>
        FateNpc = 0x092F,

        /// <summary>
        /// 移除 FATE 开始 NPC。<br />
        /// a1：FATE 开始 NPC 的对象标识。
        /// </summary>
        RemoveFateStartNpc = 0x0930,

        /// <summary>
        /// FATE 生命周期状态。<br />
        /// a1：FATE 编号。<br />
        /// a2：<br />
        /// 1: 含义待确认<br />
        /// 2: 含义待确认<br />
        /// 3: 准备<br />
        /// 4: 进行中<br />
        /// 5: 结束过程<br />
        /// 6: 含义待确认<br />
        /// 7: 成功<br />
        /// 8: 失败<br />
        /// 9: 含义待确认<br />
        /// 10: 含义待确认。<br />
        /// 部分语义待确认。旧名：FateInit。同步状态及提示，不负责创建 FATE 上下文。
        /// </summary>
        SetFateState = 0x0931,

        /// <summary>
        /// 显示获得物品消息。<br />
        /// a1：物品/模板第 1 参数。<br />
        /// a2：数量/模板第 2 参数。<br />
        /// a3：LogMessage 行号；0 自动选择 2010 单件或 2011 多件，0xFFFFFFFF 抑制日志。<br />
        /// a4：模板第 3 参数。<br />
        /// a5 为模板第 4 参数；a1..a5 均来自 Self 路径才能完整表达。
        /// </summary>
        FateReceiveItem = 0x0932,

        /// <summary>
        /// 角色所属 FATE。<br />
        /// a1：FateId。
        /// </summary>
        SetActorFateId = 0x0933,

        /// <summary>
        /// 创建并登记 FATE。<br />
        /// a1：FateId。<br />
        /// a2：<br />
        /// 0: 无额外奖励<br />
        /// 1: 有额外奖励。
        /// </summary>
        FateAssignID = 0x0934,

        /// <summary>
        /// 移除 FATE。<br />
        /// a1：FateId。<br />
        /// 旧名：FateStart。
        /// </summary>
        RemoveFate = 0x0935,

        /// <summary>
        /// 当前用途待确认。<br />
        /// a1–a4 的用途待确认。<br />
        /// 部分语义待确认。
        /// </summary>
        FateEnd = 0x0936,

        /// <summary>
        /// FATE 发起目标与备用位置。<br />
        /// a1：FateId。<br />
        /// a2：MotivationNpc EntityId，E0000000 可清。<br />
        /// a3：BNpcName.RowId：实体不存在/不可用时，作为目标地图标记的备用名称，与包内坐标配合。<br />
        /// a4：/1000 得到 X 米。<br />
        /// a5: Y（高度），有符号整数÷1000 米<br />
        /// a6: Z，有符号整数÷1000 米<br />
        /// 目标 EntityId 未改变时整项忽略，备用名和坐标也不更新。
        /// </summary>
        SetFateInitiator = 0x0937,

        /// <summary>
        /// FATE 交付目标 NPC。<br />
        /// a1：FateId。<br />
        /// a2：ObjectiveNpc EntityId，E0000000 清目标。
        /// </summary>
        SetFateDeliveryNpc = 0x0938,

        /// <summary>
        /// 显示物品获得的系统日志和飘字。<br />
        /// a1 为物品编号。<br />
        /// a2 为获得数量，单件和多件分别使用 LogMessage 2071 和 2072。
        /// </summary>
        ShowItemGain = 0x0939,

        /// <summary>
        /// 当前同步 FATE。<br />
        /// a1：SyncedFateId。
        /// </summary>
        SetSyncedFate = 0x093A,

        /// <summary>
        /// FATE 进度与目标数。<br />
        /// a1：FateId。<br />
        /// a2：FateContext.Progress。<br />
        /// a3：<br />
        /// 0: 不更新计数<br />
        /// 1: 同步目标类型 12 的计数。
        /// </summary>
        SetFateProgress = 0x093C,

        /// <summary>
        /// FATE 屏幕横幅。<br />
        /// a1：FateId。<br />
        /// a2：ScreenImage 编号。
        /// </summary>
        ShowFateScreenImage = 0x093D,

        /// <summary>
        /// 物品获得或背包已满提示。<br />
        /// a1：<br />
        /// 0: 物品栏已满<br />
        /// 1: 获得物品。<br />
        /// a2：<br />
        /// 0: 不额外提示<br />
        /// 1: 成功时额外提示获得徽章物品。<br />
        /// a3：完整 32 位 Item 标识，成功用于获得日志/徽章提示，失败用于 Log4658 物品名称。<br />
        /// a4：获得数量；仅成功时使用。<br />
        /// 旧名：FateProgress。额外徽章提示不显示数量。
        /// </summary>
        ItemGainResult = 0x093E,

        /// <summary>
        /// FATE 范围内日志。<br />
        /// a1：FateId。<br />
        /// a2：LogMessage 行号。<br />
        /// a3：文本参数 1。<br />
        /// a4：文本参数 2。<br />
        /// a5/a6 为文本参数 3/4；a7/a8 未读。
        /// </summary>
        ShowFateMessage = 0x093F,

        /// <summary>
        /// FATE 地图标记位置。<br />
        /// a1：FateId。<br />
        /// a2：/1000 得到 X 米。<br />
        /// a3：/1000 得到 Y 米。<br />
        /// a4：/1000 得到 Z 米。
        /// </summary>
        SetFateMapMarker = 0x0940,

        /// <summary>
        /// 清空 FATE 关联对象。<br />
        /// a1：FateId。
        /// </summary>
        ClearFateObjectTracking = 0x0941,

        /// <summary>
        /// 刷新指定 FATE 对象。<br />
        /// a1：非零 FateId。
        /// </summary>
        RefreshFateObjects = 0x0942,

        /// <summary>
        /// FATE 参加状态。<br />
        /// a1：FateId，0 解除。<br />
        /// a2：参与状态附加选项（具体业务待确认）。
        /// </summary>
        SetFateParticipation = 0x0943,

        /// <summary>
        /// 动态事件结果。<br />
        /// a1：动态事件槽索引 0–15。<br />
        /// a2：<br />
        /// 0: 显示失败结果横幅<br />
        /// 1: 显示完成结果横幅。<br />
        /// a3：<br />
        /// 成功结果时：<br />
        /// 0: 贡献不足<br />
        /// 1: 贡献足够。
        /// </summary>
        DynamicEventResult = 0x0A8C,

        /// <summary>
        /// 动态事件单挑邀请。<br />
        /// a1：动态事件槽索引 0–15。
        /// </summary>
        OpenDynamicEventDuelInvitation = 0x0A8D,

        /// <summary>
        /// 关闭动态事件单挑邀请。<br />
        /// a1：动态事件槽索引 0–15。
        /// </summary>
        CloseDynamicEventDuelInvitation = 0x0A8E,

        /// <summary>
        /// 单挑抽选与勇气值。<br />
        /// a1：动态事件槽索引 0–15。<br />
        /// a2：<br />
        /// 0: 显示 Log9670 未获选但勇气值+1<br />
        /// 1: 显示 Log9671 获选参加并重算勇气值。
        /// </summary>
        DynamicEventDuelLotteryResult = 0x0A8F,

        /// <summary>
        /// 动态事件截止时间。<br />
        /// a1：动态事件槽索引 0–15。<br />
        /// a2：服务器截止时间戳，单位秒。<br />
        /// a3：战斗时限，单位秒；0 使用默认值。
        /// </summary>
        SetDynamicEventDeadline = 0x0A90,

        /// <summary>
        /// 动态事件完成进度。<br />
        /// a1：动态事件槽索引 0–15。<br />
        /// a2：完成进度百分比。
        /// </summary>
        SetDynamicEventProgress = 0x0A91,

        /// <summary>
        /// 事件敌人出现脚本。<br />
        /// a1：EventHandlerId。<br />
        /// a2：OnPopEnemy 参数 1。<br />
        /// a3：OnPopEnemy 参数 2。<br />
        /// a4：OnPopEnemy 参数 3。<br />
        /// a5、a6 为脚本参数 4、5。
        /// </summary>
        EventPopEnemy = 0x0AF0,

        /// <summary>
        /// 事件场景选项与音画恢复。<br />
        /// a1–a4 不使用。
        /// </summary>
        FinishEventSceneOptions = 0x0AF2,

        /// <summary>
        /// 推进事件与集体动作任务。<br />
        /// a1–a4 不使用。
        /// </summary>
        UpdateEventSceneTasks = 0x0AF3,

        /// <summary>
        /// 暂停并复位事件音乐。<br />
        /// a1–a4 不使用。
        /// </summary>
        ResetEventBgm = 0x0AF4,

        /// <summary>
        /// 事件处理器附加控制。<br />
        /// a1：EventHandlerId。<br />
        /// a2：虚函数参数 1。<br />
        /// a3：虚函数参数 2。<br />
        /// a4：虚函数参数 3。<br />
        /// a5 为处理器专用参数 4；参数的业务语义取决于具体 EventHandler，尚未完全确认。
        /// </summary>
        EventHandlerControl = 0x0AF5,

        /// <summary>
        /// 准备过场动画。<br />
        /// a1：Cutscene 行号，32 位。
        /// </summary>
        PrepareCutscene = 0x0D48,

        /// <summary>
        /// 取消准备中的过场动画。<br />
        /// a1：Cutscene 编号；0 表示全部。
        /// </summary>
        CancelPreparedCutscene = 0x0D49,

        /// <summary>
        /// 解除角色特殊隐藏。<br />
        /// a1–a4 不使用。
        /// </summary>
        ReleaseActorRenderSuppression = 0x0D4A,

        /// <summary>
        /// 执行反应对象交互。<br />
        /// a1：事件或交互载荷首项，随处理器类型变化。<br />
        /// a2：ReactionEventObject 编号。<br />
        /// a3：交互载荷第二项，随处理器类型变化。<br />
        /// 部分语义待确认。执行已注册且激活的交互，并在成功后回传请求；不创建对象。机甲场景可达的具体处理器类型尚未全部证实。
        /// </summary>
        ExecuteReactionInteraction = 0x0E00,

        /// <summary>
        /// 大规模内容位置特效。<br />
        /// a1：VFX 行号。<br />
        /// a2：压缩 X，u16*2000/65535-1000 米。<br />
        /// a3：压缩 Y，同公式。<br />
        /// a4：压缩 Z，同公式。<br />
        /// a5: 强制回收时长，单位毫秒<br />
        /// a6: 可选绑定角色 EntityId；0xE0000000 不绑定。
        /// </summary>
        MassiveContentPositionVfx = 0x0E10,

        /// <summary>
        /// 大规模内容对象间特效。<br />
        /// a1：VFX 行号。<br />
        /// a2：源对象 EntityId，E0000000 不处理。<br />
        /// a3：目标对象 EntityId，E0000000 不处理。<br />
        /// a4：*0.001 转逻辑秒强制回收时长。
        /// </summary>
        MassiveContentTargetVfx = 0x0E11,

        /// <summary>战斗状态开关；ToggleAggro 的别名，参数相同。</summary>
        SetBattle = ToggleAggro,

        /// <summary>设置事件状态；DirectorEObjMod 的别名。a1 为事件状态值。</summary>
        SetEventState = DirectorEObjMod,

        /// <summary>设置共享组时间轴状态；EObjSetState 的别名，参数及对象选择规则相同。</summary>
        SetSharedGroupTimelineState = EObjSetState,

        /// <summary>播放共享组动画；EObjAnimation 的别名，a1 为动画编号，a2 为状态。</summary>
        PlaySharedGroupTimeline = EObjAnimation,

        /// <summary>按绝对朝向击退；KnockBack 的别名，a2 为距离、a3 为时长，均除以 10000。</summary>
        Knockback = KnockBack,
    }
}

<a name="本地副本描述文件"></a>

# 自定义副本 JSON 作者手册

一份 UTF-8 JSON 描述一个副本阶段或一段练习。作者可以直接分享这份文件，玩家在 ACT 的「阶段 / 模拟」旁点击「导入本地 JSON」。仅看场地时点击「加载区域」，带时间轴的练习加载后再点「开始模拟」。同一 `id` 表示更新已有练习，使用新 `id` 可以让两个版本并存。

| 想做什么 | 查哪里 |
| --- | --- |
| 写第一份可导入的文件 | [从一次读条开始](#quickstart) |
| 设置场地、敌人、八人小队、标点或打法选项 | [文件声明](#declarations) |
| 查某个 `op` 的字段和最低版本 | [全部时间轴操作](#operations) |
| 等技能结算，按状态或人数分支 | [事件](#signals) · [条件](#conditions) · [范围形状](#shapes) |
| 按仇恨或距离选人，让玩家截线 | [目标选择与截线](#targets) |
| 判断一个内置机制能否只用 JSON 复现 | [光暗未来绝境战能力对照](#coverage) |
| 找能直接试的机制，或定位导入报错 | [示例与复验](#examples) · [校验与排错](#validation) |

<a name="quickstart"></a>
<a name="作者示例"></a>

## 从一次读条开始

把下面的完整文件保存为 `o8s-after-trine.json` 并导入。进入 O8S 地图后开始模拟，本体就绪 3 秒后读异三角。留着默认选项，你会在技能实际结算后看到提示。将「结算播报」改为「安静」再开一轮，同一个技能仍会播放，之后直接收尾。

```json
{
  "schemaVersion": 3,
  "id": "guide.o8s.after-trine",
  "name": "O8S · 异三角结算监听",
  "territoryId": 755,
  "environment": {"weatherId": 79, "bgmId": 533},
  "actors": [
    {"id": "boss", "baseId": 8528, "nameId": 7131, "level": 80, "position": [0, 0, 0], "heading": 3.14159}
  ],
  "options": [
    {"id": "mode", "label": "结算播报", "values": ["显示提示", "安静"], "default": "显示提示"}
  ],
  "timeline": [
    {
      "at": 0, "op": "trigger",
      "event": {"type": "castResolved", "actor": "boss", "actionId": 10509},
      "timeout": 20,
      "then": [
        {
          "at": 0, "op": "branch",
          "if": {"type": "optionEquals", "option": "mode", "value": "显示提示"},
          "then": [{"at": 0, "op": "hint", "text": "异三角已经结算。"}]
        },
        {"at": 1, "op": "finish"}
      ],
      "else": [
        {"at": 0, "op": "hint", "text": "等待异三角结算超时。"},
        {"at": 1, "op": "finish"}
      ]
    },
    {"at": 3, "op": "cast", "actor": "boss", "actionId": 10509}
  ]
}
```

只做场地预览时，提供 `schemaVersion`、`id`、`name` 和 `territoryId` 就已足够。控制天气和音乐时再写 `environment`。可以从这两份短文件开始改。[O8S v1 开场读条](examples/o8s-trine-opening.json) · [绝神兵风场预览](examples/uwu-garuda-arena.json)

<a name="declarations"></a>
<a name="字段与运行语义"></a>

## 文件声明

版本号取整数 `1`、`2` 或 `3`。v1 能描述场地、NPC 和定时读条。v2 加入小队与固定时间轴机制。v3 加入事件、分支、选人和仇恨。下表的版本是字段首次可用的版本，高版本沿用前面的字段。写新机制可直接从 v3 开始，已有文件按自己声明的版本解析。

| 顶层字段 | 最低版本 | 类型与要求 |
| --- | --- | --- |
| `schemaVersion` | v1 | 必填。整数版本号。 |
| `id` | v1 | 必填。1–96 个小写英文字母、数字、点或连字符，如 `example.uwu.garuda-opening`。改显示名称或文件名时可以保留这个 ID。 |
| `name` | v1 | 必填。非空字符串，显示在 ACT 预设列表。 |
| `territoryId` | v1 | 必填。1–65535 的整数，对应本机 `TerritoryType` 表行号。 |
| `author` | v1 | 可选。作者名，省略时显示「未署名」。 |
| `description` | v1 | 可选。显示在预设界面的说明。 |
| `environment` | v1 | 可选对象。显式指定时必须同时给 `weatherId`（0–255，`Weather` 行）和 `bgmId`（0–65535，`BGM` 行）。可选 `mapEffectFlags` 数组，下标从 0 开始，每项为 0–65535 的整数或 `null`，后者表示跳过该槽。省略整个对象时采用区域默认环境。 |
| `actors` | v1 | NPC 声明的非空数组，练习必须与 `timeline` 一起提供。场地预览省略这两项。 |
| `timeline` | v1 | 非空的操作数组。所有声明的 NPC 和所需队员准备就绪后，从第 0 秒开始调度。 |
| `party` | v2 | 练习可选对象。必须同时写 `MT`、`ST`、`H1`、`H2`、`D1`、`D2`、`D3`、`D4`，每项为该职能起点 `[X,Y]`。任何使用队员职能的字段均依赖此声明。 |
| `roleSwap` | v2 | 依赖 `party`。必填 `label` 和 `roles`，后者恰好包含两个不同职能。如 `{"label":"交换近战","roles":["D1","D2"]}`。勾选后交换两人的起点、点名、移动和判定分工。 |
| `waymarks` | v2 | 练习可选对象。键从 `A`–`D`、`1`–`4` 中选取，每个值为 `[X,Y,高度]`，如 `{"1":[100,107,0]}`。会话开始时放置，停止时清理。 |
| `options` | v3 | 练习可选的下拉选项数组。每项声明一种玩家可选的打法，字段见下文。 |

### NPC 与仇恨

| `actors[]` 字段 | 最低版本 | 类型与要求 |
| --- | --- | --- |
| `id` | v1 | 必填。文件内唯一的非空字符串，如 `"boss"`。后续 `actor` 字段引用它。 |
| `baseId` | v1 | 必填。`BNpcBase` 行号，1–2147483647。 |
| `nameId` | v1 | 必填。`BNpcName` 行号，v1 为 1–2147483647，v2 起允许 0。仍需本机游戏表存在该行。 |
| `level` | v1 | 必填。NPC 等级，整数 1–255。 |
| `position` | v1 | 必填。开场坐标 `[X,Y,高度]`，前两项为水平坐标。 |
| `heading` | v1 | 必填。朝向以弧度计，0 朝 +Y，π/2 朝 +X。 |
| `hidden` | v2 | 可选布尔值，默认 `false`。为 `true` 时以隐藏、不可选中状态开场，之后可用 `visible` 控制登场。 |
| `enmity` | v3 | 依赖 `party`。职能到初始仇恨权重的映射，每项为 0–100 的整数，省略的职能计 0。如 `{"MT":100,"ST":65}`。空对象 `{}` 让首仇等待实际接怪。 |
| `maxHp` | v3 | 依赖 `enmity`。整数 1–4294967295。显式声明后，已支持的玩家攻击和 `partyHit` 会扣减此 NPC 的 HP。省略时 NPC 的默认最大 HP 为 10000，脚本仇恨命中仅累积仇恨。 |

<a name="v2-小队与机制时间轴"></a>

### 小队与模拟选项

声明 `party` 会提供「自身职能」和「生成七名模拟队友」。玩家控制所选职能的移动。其余已生成队员执行 `move` 示范。只有文件声明 `roleSwap` 才显示分工互换。提示或判定文本中的 `{D1}` 等占位符会显示本轮实际负责的职能，普通文本里的 `D1` 原样显示。包含 `hint` 的文件还会提供「显示机制提示」。关闭后仍能看到判定和成绩。

| `options[]` 字段 | 类型与要求 |
| --- | --- |
| `id` | 必填，同一文件内唯一的选项名，供 `optionEquals.option` 引用。 |
| `label` | 必填，玩家在界面上看到的名称。 |
| `values` | 必填，非空字符串数组，各项非空且互不重复。`"*"` 是保留值。 |
| `default` | 必填，取一个候选值。允许随机的选项也可用 `"*"` 作为默认值。 |
| `random` | 可选布尔值，默认 `false`。启用后下拉框多出「随机」。玩家选随机时，每轮开始抽取一个候选值，同一轮的所有节点读取同一结果。 |

<a name="operations"></a>

## 全部时间轴操作

每个节点都要有 `op`（下表的操作名）和 `at`（非负秒数，可以有小数）。表中的必填字段是在这两项之外还要填写的字段。`roles` 是非空且不重复的职能数组，`targetRole` 是一个职能字符串。每个 `then` 或 `else` 都是与主时间轴相同的非空节点数组。

主轴的 `at` 从开场起算。每条子轴有自己的零点，即选择分支、监听命中或超时、本次队员遍历开始的时刻。等待事件期间外层时间轴继续走。同一数组中同一时刻的节点按书写顺序执行，新排入的零秒子节点排在已经入队的同刻节点之后。要让结果严格依赖前一步，可把后续节点放入它的子轴。

### 读条、角色与场地

| `op` | 最低版本 | 必填字段 | 可选字段 | 执行效果 |
| --- | --- | --- | --- | --- |
| `cast` | v1 | `actor`, `actionId` | v2 起 `targetRole` 或 `groundAt`。v3 起也可用 `target` | 播放 `Action` 技能，时长取自游戏表。三种目标写法最多选一种。`targetRole` 点职能，`target` 点一个命名目标，`groundAt` 只接受 `"self"` 或职能名，在起读时锁定地面位置。 |
| `visible` | v2 | `actor`, `value` | `targetable` | 两个开关均为布尔值。`value` 控制显示，显示时默认可选中。写 `targetable: false` 可让分身出场并保持不可选中。 |
| `place` | v2 | `actor`, `position`, `heading` | — | 立即把 NPC 放到水平坐标 `[X,Y]`，保留高度并设置朝向。这里的 `position` 是两项，NPC 开场声明用三项。 |
| `move` | v2 | `roles`, `position` | — | 让这些职能的模拟队友以每秒 6 个场地单位走向 `[X,Y]`，并更新示范点。玩家自行移动。 |
| `icon` | v2 | `targetRole`, `iconId` | — | 对该职能显示 `Lockon` 表里的点名图标。 |
| `tether` | v2 | `actor`, `targetRole`, `tetherId`, `duration` | — | 从 NPC 向指定职能连线，持续正数秒后撤线。`tetherId` 引用 `Channeling` 表。 |
| `object` | v2 | `actor`, `objectId`, `duration` | — | 在来源 NPC 的当前位置生成 `EObj` 场地物件，经正数秒回收，同时最多 40 个。可用 `objectActive` 查询存续状态。 |
| `status` | v2 | `actor`, `statusId`, `duration` | `targetRole`, `stacks`。v3 增加 `target`, `addStacks` | `actor` 是来源。目标在 `targetRole` 和 `target` 中选一，省略时作用于来源自身。`duration` 为正数时施加，为 0 时移除。`stacks` 为 0–65535 的整数，省略时传 0。`addStacks` 为 1–65535，仅用于正持续时间，与 `stacks` 互斥，在当前模拟层数上累加至多 65535。 |
| `health` | v3 | `actor`, `percent` | — | 把 NPC 当前 HP 设置到最大 HP 的指定百分比，`percent` 可取 0–100，HP 取最近整数，中点取偶数。 |
| `mapEffect` | v3 | `slot`, `flags` | — | 在从 0 开始的地图槽 `slot` 播放 `flags`（1–65535）。`environment.mapEffectFlags[slot]` 必须有非空初值。在同一场地停止练习时还原。 |
| `knockback` | v3 | `roles`, `knockbackId`，以及 `position` 或 `heading` 二选一 | — | `position: [X,Y]` 表示从中心径向击退，`heading` 表示沿给定方向击退。距离和速度来自 `Knockback` 表，两者都必须有效。 |

同一施法者在读条结束前再使用 `cast` 会替换旧施法。要在八人脚下同时放圈，可像迦楼罗飞翎雨示例那样给每个落点分配一个辅助 NPC。单练时，`groundAt` 指向缺席职能会采用该职能最近的示范点。`targetRole` 指向缺席队员时普通读条仍可播放，点名、连线和该队员的状态则跳过。

### 提示、判定与流程

| `op` | 最低版本 | 必填字段 | 可选字段 | 执行效果 |
| --- | --- | --- | --- | --- |
| `hint` | v2 | `text` | — | 开启机制提示时，向 ACT 和游戏回声窗口显示单行文本。 |
| `check` | v2 | `roles`, `position`, `radius`, `text` | v3 起 `id` | 只有玩家所选职能在列表中时才计分。检查玩家当前位置与 `[X,Y]` 的距离是否小于等于正半径 `radius`。提供 `id` 会记录结果并发出 `checkResolved`。 |
| `finish` | v2 | — | — | 汇报到此刻的成绩。v2 作者应把它放在最后。v3 还会终止剩余的脚本节点和监听。点击「停止」回收会话实体。 |
| `branch` | v3 | `if`, `then` | `else` | 当场求值一次，成立播放 `then`，否则播放可选的 `else`。 |
| `trigger` | v3 | `timeout`, `then`，以及 `event` 或 `if` 二选一 | `else` | 一次性等待。`event` 从注册后开始匹配。`if` 在注册时立即检查，随后约每 0.1 秒和到期时检查。命中走 `then`，经过正数 `timeout` 秒仍未命中走 `else`。 |
| `judge` | v3 | `id`, `if`, `text` | — | 判断任意条件，报告通过或失败并计分。以 `id` 保存结果，同时发出 `checkResolved`。关闭机制提示也会计分和报告。 |
| `forParty` | v3 | `then` | `roles`, `interval`, `duration` | 为每名在场且存活的候选队员各执行一次子轴，默认全队。重复检查时同时填写 `interval`（至少 0.1 秒）和正数 `duration`。第一次立即执行，后续每次重新检查在场人员。子轴用 `target: "$member"` 引用本次队员。 |

### 选人、动态连线与队友攻击

| `op` | 最低版本 | 必填字段 | 可选字段 | 执行效果 |
| --- | --- | --- | --- | --- |
| `selectTarget` | v3 | `actor`, `key`, `by` | `roles`, `rank` | 当场选一名队员，把职能保存在 `key`。之后队员移动或换仇仍引用这个人。 |
| `trackTether` | v3 | `actor`, `key`, `by`, `tetherId` | `roles`, `rank`, `interceptRadius`, `initialRole` | 建立动态连线并持续更新持线者。正数 `interceptRadius` 开启途中截线，只有此模式可指定优先起始职能 `initialRole`。 |
| `resolveTether` | v3 | `key` | — | 对该动态连线最后采样一次，撤线并保留最终持线者。后续施法、消层和判定可共用同一个结果。 |
| `partyHit` | v3 | `actor`, `targetRole`, `damage` | — | 此处 `targetRole` 是出手的模拟队员，`actor` 是声明了 `enmity` 的受击 NPC，`damage` 为正整数。命中累积仇恨。敌人还声明了 `maxHp` 时会扣血。若该职能由玩家扮演，须由玩家自己出手。 |

<a name="signals"></a>

## 可监听的事件

在 v3 的 `trigger.event` 中填写 `type` 和下表字段。每个监听器只响应注册后第一次匹配的事件。多个已注册的监听器可以响应同一次事件。技能事件来自当前练习 `actors` 声明的 NPC，关闭 ACT 战斗日志写入也可触发。

| `type` | 必填字段 | 可选字段 | 匹配时机 |
| --- | --- | --- | --- |
| `castStarted` | `actor`, `actionId` | — | 指定 NPC 实际开始有时长的读条。 |
| `castResolved` | `actor`, `actionId` | — | 指定 NPC 的技能实际结算，瞬发技能也适用。若旧读条被新施法替换，旧技能不会发出结算事件。 |
| `checkResolved` | `check` | `passed` | `check` 引用命名的 `check` 或 `judge`。省略 `passed` 可匹配任一结果，填写布尔值可只监听通过或失败。 |

例如，先注册 `{"type":"castResolved","actor":"boss","actionId":10509}`，再安排首领读条。要等队员进塔或失去状态，使用 `trigger.if` 和下面的条件。注册时已成立的条件会立即触发。

<a name="conditions"></a>

## 条件

v3 的 `branch.if`、`trigger.if` 和 `judge.if` 共用以下条件对象，每项都要写 `type`。判定名在整个文件（包括子轴）内唯一，可以在声明前引用。每轮开始时清空上轮判定结果。

| `type` | 必填字段 | 可选字段 | 条件何时成立 |
| --- | --- | --- | --- |
| `all` | `conditions` | — | 非空条件数组中每一项都成立。 |
| `any` | `conditions` | — | 非空条件数组中至少一项成立。 |
| `not` | `condition` | — | 单个子条件为假。三种逻辑条件都可以嵌套。 |
| `optionEquals` | `option`, `value` | — | 本轮该选项等于指定候选值。`option` 引用 `options[].id`，`value` 要写实际候选值。 |
| `checkResult` | `check`, `passed` | — | 最近一次同名判定结果与布尔值 `passed` 相同。尚未执行时，查询成功和失败都返回假。 |
| `hpAtMost` | `actor`, `percent` | — | 在场 NPC 当前 HP 百分比小于等于 `percent`（0–100）。填 0 可等刺羽被击破。 |
| `hasStatus` | `statusId`，以及 `actor`、`role`、`target` 三选一 | `minStacks` | NPC、指定职能或命名目标身上有此状态。给出 `minStacks`（1–65535）时检查模拟器管理的状态层数是否达到阈值。目标缺席时为假。 |
| `targetIs` | `target`, `role` | — | 命名目标当前对应该职能，且该职能在场并存活。例如按实际持线者是否为 ST 单独打分。 |
| `objectActive` | `actor`, `objectId` | — | 由此 NPC 通过 `object` 放置的同类物件中，至少一个仍在场。 |
| `areaCount` | `shape`, `min`, `max`，以及 `position`、`actor`、`centerRole` 三选一 | `heading`，以及 `roles` 或 `target` | 需要 `party`。在范围内的人数介于 `min` 和 `max` 之间，含两端，满足 `0 ≤ min ≤ max ≤ 8`。默认检查全队，可按职能列表或一个命名目标筛选。 |

`areaCount` 统计存活且在场的队员，以各人的水平判定点为准，边界也算命中。`position` 是固定中心 `[X,Y]`，`actor` 和 `centerRole` 分别以 NPC 或队员为中心。省略 `heading` 时，固定中心朝向 0，跟随角色的范围采用其当前朝向。中心不可用时条件为假。中心有效而受检目标缺席时人数为 0，能满足 `min: 0` 的判定。

<a name="shapes"></a>

## 范围形状

`areaCount.shape` 由 `type` 和下表的尺寸字段组成。距离使用场地坐标单位。例如 `{"type":"cone","radius":15,"degrees":90}` 表示向前覆盖 90 度的扇形。只有 `degrees` 使用角度，`heading` 使用弧度。

| `type` | 必填字段 | 几何含义 |
| --- | --- | --- |
| `circle` | `radius` | 以中心为圆心，半径大于 0。 |
| `donut` | `inner`, `outer` | 内外圆周均计入，要求 `0 ≤ inner < outer`。 |
| `cone` | `radius`, `degrees` | 正半径。完整张角大于 0 且不超过 360 度，两侧各占一半。 |
| `rectangle` | `length`, `width` | 从中心朝前延伸 `length`，左右各占半宽，两项均大于 0。 |
| `cross` | `length`, `width` | 围绕中心沿前后左右各伸出一个臂。`length` 是单臂长度，`width` 是臂的完整宽度，两项均大于 0。 |

<a name="targets"></a>

## 目标选择与截线

`selectTarget` 和 `trackTether` 共用 `by`。它们都需要 `party`，只从在场且存活的队员中选择。可用 `roles` 限定候选人。支持排名的规则使用 `rank`（1–8，默认 1），候选不足时得到空目标。

| `by` | 接受 `rank` | 选人规则 |
| --- | --- | --- |
| `nearest` | 是 | 按队员判定点到来源 NPC 的水平距离，由近到远。同距按 MT、ST、H1、H2、D1、D2、D3、D4 的脚本职能顺序。 |
| `farthest` | 是 | 按相同距离由远到近，同距沿用相同职能顺序。 |
| `enmity` | 是 | 按此 NPC 当前的正仇恨值从高到低选人。NPC 须声明 `enmity`。已支持的攻击、挑衅和退避会改变仇恨排序。 |
| `firstEnmity` | 否 | 锁定此 NPC 的首个接怪者。`enmity` 中有正初值时取初始一仇，全为 0 时等待首次有效命中或挑衅。之后换仇仍保留首个接怪者。 |

**命名与作用域。** `key` 是非空单行纯文本。`selectTarget` 和 `trackTether` 的名字在整个文件内唯一。`cast.target`、`status.target`、`hasStatus.target`、`areaCount.target` 和 `targetIs.target` 可引用这些名字。读取未选中的名字得到空目标，指向它的施法和状态操作会跳过。保留名 `"$member"` 表示本次 `forParty` 遍历的队员，在该次子轴及其嵌套分支或监听内有效。在遍历中执行 `judge` 时，每人各计一次分，`checkResult` 保存这个判定名最近一次执行的结果。两根线需要分别记分时，应给两个 `judge` 各取一个名字。

**动态连线。** 仅按排名选人时，`trackTether` 大约每 0.1 秒重选。加上正数 `interceptRadius` 后，首次优先连给候选中的 `initialRole`，缺席或省略时按 `by` 选初始持线者。其他候选人走进来源与持线者之间、距离连线不超过容差时即可截走线。多人同时截线取沿线最靠近来源者。持线者走开会带着线，死亡或缺席会按 `by` 重选。一个来源只有一个连线槽，同时演示双线需要两个来源 NPC。

下面用三个时间轴节点演示迦楼罗西线。沿用练习文件里的 `party` 和西侧姐妹声明，保留同名连线的一份定义即可。`chirada` 为已声明的西侧姐妹。第 125 秒先用 `resolveTether` 最后采样并撤线，再用同一个 `key` 把中高压打给实际持线者。东西两线分别锁定，玩家改变站位可以改变其中一条线的结算。[完整迦楼罗 JSON](examples/uwu-garuda-opening.json)

```json
[
  {"at": 120, "op": "trackTether", "actor": "chirada", "key": "西线", "by": "nearest", "initialRole": "H1", "interceptRadius": 1.5, "tetherId": 4},
  {"at": 125, "op": "resolveTether", "key": "西线"},
  {"at": 125, "op": "cast", "actor": "chirada", "actionId": 11081, "target": "西线"}
]
```

<a name="examples"></a>

## 可导入示例与复验

| 文件 | 在插件里验证什么 |
| --- | --- |
| [O8S 异三角开场（v1）](examples/o8s-trine-opening.json) | 加载区域 755，开始后本体就绪第 3 秒播放 `10509`。查资源时游戏内的十六进制 `0x290D` 对应 JSON 的十进制 `10509`。[O8S 预设](../Simulant/Content/S4b/Presets/异三角.cs) |
| [绝神兵 P1 场地预览](examples/uwu-garuda-arena.json) | 只加载区域 777，观察天气 28 的迦楼罗风场。 |
| [绝神兵 P1 迦楼罗练习（v3）](examples/uwu-garuda-opening.json) | 用模拟 ST 正常接刺羽，再用玩家 MT 提前挑衅，对比气旋实际命中者。双线阶段由原分工接线，再由无层队员截走西线，对比持线者、消层和觉醒判定。[职能、时间点和复验路线](uwu-garuda.md) |
| [光暗未来绝境战 · 复乐园南塔（v3）](examples/fru-south-tower.json) | 复乐园实际结算后开南侧 1 标塔。首人进圈后两秒检查人数，开塔八秒仍无人则走空塔路线。 |

### v2 固定时间轴示例

下面是一份可直接导入的个人站位练习。选 D1，开始后走到 1 标，第 5 秒在标点半径 2 以内即通过。开启队友时 D2 会示范走位，选 MT 可以在旁观察两名近战集合。这个例子同时给出了小队起点、标点、移动和个人判定的写法。

```json
{
  "schemaVersion": 2,
  "id": "guide.o8s.stack",
  "name": "O8S · 近战集合站位",
  "territoryId": 755,
  "actors": [{"id":"boss","baseId":8528,"nameId":7131,"level":80,"position":[0,0,0],"heading":0}],
  "party": {
    "MT":[0,3],"ST":[3,3],"H1":[-6,-6],"H2":[6,-6],
    "D1":[-4,-2],"D2":[4,-2],"D3":[-8,0],"D4":[8,0]
  },
  "waymarks": {"1":[0,-6,0]},
  "timeline": [
    {"at":0,"op":"move","roles":["D1","D2"],"position":[0,-6]},
    {"at":5,"op":"check","roles":["D1","D2"],"position":[0,-6],"radius":2,"text":"双近战在 1 标集合"},
    {"at":6,"op":"finish"}
  ]
}
```

<a name="south-tower"></a>

### 复乐园南塔的四种走法

导入[复乐园南塔教学](examples/fru-south-tower.json)，加载区域并开始模拟，按下面的路线观察同一座塔的不同结算。

| 模拟设置与操作 | 应看到的结果 |
| --- | --- |
| 选 H1，开启七名模拟队友，示范组选「双治疗」，跟 H2 进 1 标并留下。 | 正好两人，通过判定并播放技能 40320。 |
| 同上，但 H1 留在塔外。 | H2 进塔启动倒数，两秒后人数错误，播放 40321。 |
| 关闭七名模拟队友，自己留在塔外。 | 八秒后提示空塔并播放 40321。 |
| 选 MT，开启队友，示范组改为「双近战」。 | D1、D2 进塔，治疗留在原位。选择「随机」会在每轮开始时决定示范组。 |

<a name="coverage"></a>

### 与内置「光暗未来绝境战」的能力对照

作者用手册里的接口组合新机制，只需分享 JSON。仓库中的 `GarudaChecks.cs` 属于开发者运行的示例回归测试，会检查抢刺羽、提前击破和截西线等路线。插件导入和运行练习时直接读取 JSON。[迦楼罗回归测试](../tools/EncounterChecks/GarudaChecks.cs)

下表列出哪些部分可以直接写 JSON，以及这些内置机制里仍需扩展通用接口的部分。复乐园教学将连续三塔简化为南侧一座，半径为 3，方便分别验证成功、人数错误和空塔。[内置塔位与分工](../Simulant/Content/U7a/Presets/P52复乐园.Plan.cs)

| 内置练习用到的能力 | JSON 作者现在可以写什么 | 仍由内置代码完成的部分 |
| --- | --- | --- |
| 复乐园的塔、连线和范围判定 | 用 `mapEffect` 开关塔、`areaCount` 检查人数。用 `trackTether` 按远近换线，`resolveTether` 锁定实际持线者。 | 保存一次结算的完整命中名单并交给下一次伤害。[复乐园](../Simulant/Content/U7a/Presets/P52复乐园.cs) |
| 钻石星尘的击退与镜中奇遇的首领走位 | `knockback` 指定 `roles` 和游戏表 `knockbackId`，再给中心 `position` 作径向击退，或给 `heading` 定向击退。可用 `place` 移动首领、`move` 引导模拟队友。 | 首领按实时一仇持续追踪和转向。[钻石星尘](../Simulant/Content/U7a/Presets/P21钻石星尘.Round.cs) · [镜中奇遇](../Simulant/Content/U7a/Presets/P22镜中奇遇.Round.cs) |
| 光之失控的分工、时间结晶的状态 | 声明打法选项、按轮随机选一种分工，按状态或职能检查站位。用 `status` 施加指定层数或移除状态。 | 程序化抽取多组互斥搭档、读取某一来源的状态层数，以及保存和回放整队快照。[光之失控](../Simulant/Content/U7a/Presets/P23光之失控.Plan.cs) · [时间结晶](../Simulant/Content/U7a/Presets/P42时间结晶.Judgement.cs) |
| P5 的血量推进与收尾 | `health` 指定 `actor` 和 `percent` 直接设置 NPC 当前血量。可等血线、按选项播放不同结尾，再用 `finish` 结束。 | 连续血量曲线、极限技槽和坦克 LB 检查、专用过场演出。[P5 完整时间轴](../Simulant/Content/U7a/Presets/P5完整.cs) |

<a name="validation"></a>
<a name="失败时作者能看到什么"></a>

## 校验与排错

文件应为最多 1 MiB 的 UTF-8 JSON，可带 UTF-8 BOM。字段名、操作名、职能和引用的名字都区分大小写。未知或重复字段会导致导入失败。版本、等级、槽位和资源行号都写十进制整数，坐标、朝向和秒数可用有限小数。`hint`、`check`、`judge` 的 `text` 以及选人 `key` 使用单行纯文本，请避开控制字符和尖括号。

| 操作引用的资源或数值 | 接受范围 |
| --- | --- |
| `actionId`（`Action`）、`iconId`（`Lockon`）、`objectId`（`EObj`） | 1–4294967295，同时要求本机对应游戏表存在该行。 |
| `statusId`（`Status`）、`knockbackId`（`Knockback`） | 1–65535。击退行还需要非零的距离和速度。 |
| `tetherId`（`Channeling`） | `tether` 接受 1–65535，`trackTether` 接受 1–4294967295。两者都查同一张表。 |
| `mapEffect.slot`、`mapEffect.flags`、`partyHit.damage` | 槽位 0–65535，且要有初值。运行中的效果值 1–65535。队友攻击伤害 1–4294967295。 |

编辑器可按版本关联以下 JSON Schema，三个文件放在同一目录以便解析相对引用。在编辑器设置里做关联即可，描述文件顶层只接受[文件声明](#declarations)所列字段。[v1 Schema](encounter-v1.schema.json) · [v2 Schema](encounter-v2.schema.json) · [v3 Schema](encounter-v3.schema.json)

导入时，插件也会检查跨节点引用，并用本机游戏数据逐项核对资源，包括尚未执行的分支和监听条件。若提示游戏数据未就绪，等插件完成初始化后重新导入。若错误路径是 `timeline[0].then[1].actor`，先定位第一个节点的 `then` 数组，再检查第二个子节点的 `actor` 是否与 `actors[].id` 完全相同。已有同 ID 的练习只有在新文件校验通过且确认替换后才会更新。[导入实现](../Simulant/Content/Local/LocalEncounterLibrary.cs) · [字段与资源校验](../Simulant/Content/Local/LocalEncounter.cs)

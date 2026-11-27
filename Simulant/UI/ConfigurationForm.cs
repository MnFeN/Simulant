using Simulant.Core;
using Simulant.Game.Data;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed class ConfigurationForm : FormEx
    {
        private readonly IContainer _components = new Container();
        private readonly PluginHost _host;
        private readonly Configuration _draft;
        private readonly List<SimOptionBase> _partyOptions;
        private readonly List<SimOptionBase> _miscOptions;

        internal ConfigurationForm(PluginHost host)
        {
            _host = host;
            _draft = host.Configuration.Copy();
            Text = "仿生石配置";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            var tabs = new TabControlEx
            {
                Dock = DockStyle.Fill
            };
            var partyTab = new TabPageEx
            {
                Text = "模拟队员",
                AutoScroll = true,
                Padding = new Padding(14)
            };
            var table = new TableLayoutPanel();
            var toolTip = new ToolTip(_components)
            {
                AutoPopDelay = 60000,
                ToolTipTitle = "说明",
                ToolTipIcon = ToolTipIcon.Info
            };
            _partyOptions = CreatePartyOptions(_draft.SimulatedParty);
            partyTab.Controls.Add(table);
            new OptionEditor(table, toolTip).Build(_partyOptions);
            tabs.TabPages.Add(partyTab);

            var miscTab = new TabPageEx
            {
                Text = "杂项",
                AutoScroll = true,
                Padding = new Padding(14)
            };
            var miscTable = new TableLayoutPanel();
            var developerMode = new CheckBoxOption(nameof(Configuration.DeveloperMode), "开发者模式", _draft.DeveloperMode,
                "允许在任意地图启用防火墙，并在未启用防火墙时使用调试功能。");
            _miscOptions = new List<SimOptionBase>
            {
                new FilePathOption(nameof(Configuration.FfxivPath), "FFXIV 安装路径", _draft.FfxivPath,
                    "FFXIV 客户端|ffxiv_dx11.exe",
                    "用于在游戏启动前预扫 EXE 和读取游戏资源。连接时以 FFXIV 解析插件绑定的客户端为准。首次成功连接后会自动保存尚未设置的路径。"),
                new ComboBoxOption<GameDataLanguage>(nameof(Configuration.GameDataLanguage), "游戏数据语言", _draft.GameDataLanguage,
                    new Map<GameDataLanguage>
                    {
                        [GameDataLanguage.Auto] = "自动",
                        [GameDataLanguage.CHS]  = "简体中文（国服）",
                        [GameDataLanguage.EN]   = "英语（国际服）",
                        [GameDataLanguage.CHT]  = "繁体中文（繁中服）",
                        [GameDataLanguage.KR]   = "韩语（韩服）",
                        [GameDataLanguage.JP]   = "日语（国际服）",
                        [GameDataLanguage.DE]   = "德语（国际服）",
                        [GameDataLanguage.FR]   = "法语（国际服）"
                    }, "保存后重新加载插件生效。"),
                new CheckBoxOption(nameof(Configuration.AutoInstallUpdates), "自动安装更新", _draft.AutoInstallUpdates,
                    "开启插件时异步检查更新；发现新版本后自动下载安装，重启 ACT 后生效。"),
                new CheckBoxOption(nameof(Configuration.QuickControlsEnabled), "模拟期间启用快捷控制悬浮窗", _draft.QuickControlsEnabled,
                    "选中模拟预设并进入副本后，显示置顶的模拟控制与冲刺、防击退按钮。单击图标执行操作，按住拖动移动窗口，拖动边缘调整大小；退出副本后自动关闭。"),
                new ComboBoxOption<Keys>(nameof(Configuration.GroundTeleportKey), "鼠标传送快捷键", _draft.GroundTeleportKey,
                    CreateGroundTeleportKeys(), "开启防火墙后，按键传送到鼠标指向的地面。"),
                new DecimalOption(nameof(Configuration.SimLatency), "模拟判定延迟（秒）", _draft.SimLatency, 1, 0, 2,
                    "判定采用自身此前的位置和防击退状态，模拟服务器提前判定；不影响假队员、演出和报错时间。设为 0 关闭，下次模拟生效。\n此延迟与网络延迟不同，建议不低于 0.30 s 以反映真实副本情况。"),
                new DecimalOption(nameof(Configuration.SlidecastWindow), "本机滑步窗口（秒）", _draft.SlidecastWindow,
                    max: 1, decimalPlaces: 2, hint: "防火墙开启时，读条剩余时间在此范围内的走动可以保留读条。设为 0 时，读条期间的走动都会打断。"),
                new CheckBoxOption(nameof(Configuration.GenerateActLogs), "生成 ACT 日志（实验性）", _draft.GenerateActLogs,
                    "开启后可生成对应的 ACT 及网络日志，供 Triggernometry 等插件使用。"),
                developerMode
            };
            miscTab.Controls.Add(miscTable);
            new OptionEditor(miscTable, toolTip).Build(_miscOptions);
            var developerCheckBox = (CheckBox)developerMode.Control;
            developerCheckBox.CheckedChanged += (_, __) =>
            {
                if (developerCheckBox.Checked && MessageBox.Show(this,
                    "该选项开启后不会限制非预期的危险操作，可能会将异常数据发送至服务器。",
                    "开启开发者模式", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    developerCheckBox.Checked = false;
            };
            tabs.TabPages.Add(miscTab);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var cancel = new ButtonEx
            {
                Text = "取消",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };
            var save = new ButtonEx
            {
                Text = "保存",
                Tone = ButtonTone.Primary,
                AutoSize = true
            };
            save.Click += Save;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            Controls.Add(tabs);
            Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;

            Load += (_, __) =>
            {
                // 按实际字体测量路径和两个选项页，切换标签时保持窗口大小。
                foreach (var path in _miscOptions.OfType<FilePathOption>())
                    path.FitPathWidth();
                foreach (var combo in miscTable.Controls.OfType<ComboBox>())
                    combo.Width = combo.Items.Cast<object>().Max(item => TextRenderer.MeasureText(combo.GetItemText(item), combo.Font).Width)
                        + SystemInformation.VerticalScrollBarWidth + combo.Font.Height;
                var pages = tabs.TabPages.Cast<TabPage>()
                    .Select(page => page.Controls[0].GetPreferredSize(Size.Empty) + page.Padding.Size).ToArray();
                var tabFrame = tabs.Size - tabs.DisplayRectangle.Size;
                var footer = buttons.GetPreferredSize(Size.Empty);
                var desired = SizeFromClientSize(new Size(
                    Math.Max(pages.Max(page => page.Width) + tabFrame.Width, footer.Width),
                    pages.Max(page => page.Height) + tabFrame.Height + footer.Height));
                var area = Screen.FromControl(Owner ?? this).WorkingArea;
                Size = new Size(Math.Min(desired.Width, area.Width), Math.Min(desired.Height, area.Height));
                CenterToParent();
            };
        }

        private static List<SimOptionBase> CreatePartyOptions(SimActorBehaviorOptions value) => new List<SimOptionBase>
        {
            new LabelOption("用于所有预设的模拟队员，不接管自己的角色。\r\n保存后从下一次模拟开始生效。"),
            new CheckBoxOption(nameof(value.AttackTarget), "保持攻击 Boss", value.AttackTarget, "仅作为演出效果"),
            new DecimalOption(nameof(value.IdleRadius), "空闲走动半径范围（米）", value.IdleRadius, 3, 0, 1,
                "没有机制站位约束时，原地小范围随机走动的半径，仅作为演出效果"),
            new DecimalOption(nameof(value.RouteAngleDegrees), "绕路角度范围（°）", value.RouteAngleDegrees, 90, 0, 0,
                "时间允许时，走位过程中随机加入的小幅转折，仅作为演出效果"),
            new CheckBoxOption(nameof(value.DelayArrival), "延迟到达安全位置", value.DelayArrival,
                "防止模拟实体过早到达正确位置，干扰判断机制"),
        };

        private static Map<Keys> CreateGroundTeleportKeys()
        {
            var keys = new Map<Keys>
            {
                [Keys.None] = "禁用",
                [Keys.OemSemicolon] = "分号（;）",
                [Keys.Oemtilde] = "`",
                [Keys.OemMinus] = "-",
                [Keys.Oemplus] = "=",
                [Keys.OemOpenBrackets] = "[",
                [Keys.OemCloseBrackets] = "]",
                [Keys.OemPipe] = "\\",
                [Keys.OemQuotes] = "'",
                [Keys.Oemcomma] = ",",
                [Keys.OemPeriod] = ".",
                [Keys.OemQuestion] = "/"
            };
            var converter = new KeysConverter();
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key >= Keys.D0 && key <= Keys.D9 || key >= Keys.A && key <= Keys.Z ||
                    key >= Keys.NumPad0 && key <= Keys.F12)
                    keys[key] = converter.ConvertToString(key);
            }
            return keys;
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                foreach (var option in _partyOptions)
                    option.ApplyTo(_draft.SimulatedParty);

                foreach (var option in _miscOptions)
                    option.ApplyTo(_draft);

                _host.SaveConfiguration(_draft);
                DialogResult = DialogResult.OK;
            }
            catch (Exception error)
            {
                _host.LogError("保存全局配置失败：" + error);
                MessageBox.Show(this, "配置保存失败：" + error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _components.Dispose();

            base.Dispose(disposing);
        }
    }
}

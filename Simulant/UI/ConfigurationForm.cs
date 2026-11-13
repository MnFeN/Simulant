using Simulant.Core;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed class ConfigurationForm : Form
    {
        private readonly IContainer _components = new Container();
        private readonly PluginHost _host;
        private readonly Configuration _draft;
        private readonly List<SimOptionBase> _partyOptions;

        internal ConfigurationForm(PluginHost host, Font font)
        {
            _host = host;
            _draft = host.Configuration.Copy();
            Font = font;
            Text = "仿生石配置";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 370);
            MinimumSize = new Size(540, 400);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var partyTab = new TabPage("模拟队员") { AutoScroll = true, Padding = new Padding(5) };
            var table = new TableLayoutPanel();
            var toolTip = new ToolTip(_components) { AutoPopDelay = 60000, ToolTipTitle = "说明", ToolTipIcon = ToolTipIcon.Info };
            _partyOptions = CreatePartyOptions(_draft.SimulatedParty);
            partyTab.Controls.Add(table);
            new OptionEditor(table, toolTip).Build(_partyOptions);
            tabs.TabPages.Add(partyTab);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "保存", AutoSize = true };
            save.Click += Save;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            Controls.Add(tabs);
            Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;
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

        private void Save(object sender, EventArgs e)
        {
            try
            {
                foreach (var option in _partyOptions) option.ApplyTo(_draft.SimulatedParty);
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
            if (disposing) _components.Dispose();
            base.Dispose(disposing);
        }
    }
}

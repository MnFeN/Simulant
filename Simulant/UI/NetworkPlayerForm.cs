using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game.Character;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    /// <summary>手动生成玩家时只选择常用属性，其余外观由当前玩家提供。</summary>
    internal sealed class NetworkPlayerForm : Form
    {
        private static readonly string[] JobNames =
        {
            "冒险者", "剑术师", "格斗家", "斧术师", "枪术师", "弓箭手", "幻术师", "咒术师",
            "刻木匠", "锻铁匠", "铸甲匠", "雕金匠", "制革匠", "裁衣匠", "炼金术士", "烹调师",
            "采矿工", "园艺工", "捕鱼人", "骑士", "武僧", "战士", "龙骑士", "吟游诗人",
            "白魔法师", "黑魔法师", "秘术师", "召唤师", "学者", "双剑师", "忍者", "机工士",
            "暗黑骑士", "占星术士", "武士", "赤魔法师", "青魔法师", "绝枪战士", "舞者", "钐镰客",
            "贤者", "蝰蛇剑士", "绘灵法师"
        };

        private readonly ComboBox _job = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly NumericUpDown _level = new NumericUpDown { Minimum = 1, Maximum = 255, Width = 90 };
        private readonly TextBox _name = new TextBox { Dock = DockStyle.Fill };
        private readonly CheckBox _jobEquipment = new CheckBox { Text = "优先使用已核实的职业套装", Checked = true, AutoSize = true };
        private readonly Label _equipmentHint = new Label { AutoSize = true, MaximumSize = new Size(420, 0) };

        internal NetworkPlayerForm(Job initialJob, byte initialLevel, NetworkPlayerSpawnSpec previous)
        {
            Text = "生成网络玩家";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var layout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            var introduction = new Label
            {
                Text = "角色将在你面前生成并保留，直到手动清理或退出模拟。\n外貌和饰品复制自身，装备可选择职业套装。",
                AutoSize = true, Margin = new Padding(0, 0, 0, 12)
            };
            layout.Controls.Add(introduction, 0, 0);
            layout.SetColumnSpan(introduction, 2);
            AddField(layout, 1, "职业", _job);
            AddField(layout, 2, "等级", _level);
            AddField(layout, 3, "名字", _name);
            var nameHint = new Label { Text = "名字留空时使用“模拟 + 职业名”。", AutoSize = true };
            layout.Controls.Add(nameHint, 1, 4);
            layout.Controls.Add(_jobEquipment, 0, 5);
            layout.SetColumnSpan(_jobEquipment, 2);
            layout.Controls.Add(_equipmentHint, 0, 6);
            layout.SetColumnSpan(_equipmentHint, 2);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill,
                Margin = new Padding(0, 14, 0, 0)
            };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var create = new Button { Text = "生成", AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(create);
            layout.Controls.Add(buttons, 0, 7);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            AcceptButton = create;
            CancelButton = cancel;

            for (var i = 1; i < JobNames.Length; i++) _job.Items.Add($"{JobNames[i]}（{(Job)i}）");
            var selectedJob = (int)(previous?.Job ?? initialJob);
            _job.SelectedIndex = selectedJob > 0 && selectedJob < JobNames.Length ? selectedJob - 1 : (int)Job.PLD - 1;
            _level.Value = Math.Max(1, (int)(previous?.Level ?? initialLevel));
            _name.Text = previous?.Name ?? string.Empty;
            _jobEquipment.Checked = previous?.UseJobEquipment ?? true;
            _job.SelectedIndexChanged += (_, __) => UpdateEquipmentHint();
            _jobEquipment.CheckedChanged += (_, __) => UpdateEquipmentHint();
            UpdateEquipmentHint();
        }

        internal NetworkPlayerSpawnSpec GetSpec()
        {
            var job = (Job)(_job.SelectedIndex + 1);
            return new NetworkPlayerSpawnSpec
            {
                Job = job, Level = (byte)_level.Value,
                Name = string.IsNullOrWhiteSpace(_name.Text) ? "模拟" + JobNames[(int)job] : _name.Text.Trim(),
                UseJobEquipment = _jobEquipment.Checked
            };
        }

        private void UpdateEquipmentHint()
        {
            var job = (Job)(_job.SelectedIndex + 1);
            _equipmentHint.Text = _jobEquipment.Checked && JobEquipment.HasSet(job)
                ? "使用该职业的经典武器和五件护甲，保留自身饰品与眼镜。"
                : "复制自身全部装备；武器外观可能与所选职业不同。";
        }

        private static void AddField(TableLayoutPanel layout, int row, string title, Control input)
        {
            layout.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) }, 0, row);
            input.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(input, 1, row);
        }
    }
}

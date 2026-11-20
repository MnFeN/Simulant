using Simulant.Core;
using Simulant.Core.Zone;
using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed class GreenScreenForm : FormEx
    {
        private readonly PluginHost _host;
        private readonly Timer _update = new Timer { Interval = 80 };
        private readonly TabControlEx _tabs = new TabControlEx { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
        private readonly LabelEx _status = new LabelEx { AutoSize = true, Tone = LabelTone.Muted, Margin = new Padding(0, 8, 0, 0) };
        private readonly ButtonEx _reset = MakeButton("重置默认值");
        private GreenScreenStage _stage;
        private GreenScreenAppearance _appearance = GreenScreenAppearance.Default;
        private event Action LoadValues;
        private bool _loading;
        private bool _pending;

        internal GreenScreenForm(PluginHost host)
        {
            _host = host;
            Text = "绿幕控制";
            Icon = PluginWindowIcon;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 475);
            MinimumSize = new Size(370, 315);
            Padding = new Padding(14);
            TopMost = true;

            var pin = new CheckBoxEx
            {
                Text = "窗口置顶",
                Checked = true,
                Style = CheckBoxStyle.Switch,
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };
            pin.CheckedChanged += (_, __) => TopMost = pin.Checked;
            _reset.Click += (_, __) =>
            {
                SetValues(GreenScreenAppearance.Default);
                _pending = true;
            };

            var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 10) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(new LabelEx { Text = "区域 1 · 绿幕", AutoSize = true, Tone = LabelTone.Heading, Anchor = AnchorStyles.Left }, 0, 0);
            header.Controls.Add(pin, 1, 0);
            header.Controls.Add(_reset, 2, 0);

            var floor = AddTab("地板");
            AddColor(floor, () => _appearance.FloorColor, rgb => _appearance.FloorColor = rgb);
            AddMaterialValue(floor, "自发光", 2, () => _appearance.Emission, v => _appearance.Emission = v);
            AddMaterialValue(floor, "漫反射", 1, () => _appearance.Diffuse, v => _appearance.Diffuse = v);
            AddMaterialValue(floor, "高光", 1, () => _appearance.Specular, v => _appearance.Specular = v);
            AddMaterialValue(floor, "泛光", 1, () => _appearance.Glare, v => _appearance.Glare = v);
            AddLightTab("场内灯", 32, () => _appearance.PointLight, v => _appearance.PointLight = v);
            AddLightTab("全局光照", 4, () => _appearance.WorldLight, v => _appearance.WorldLight = v);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_tabs, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);
            SetValues(_appearance);
            UpdateAvailability();
            _update.Tick += (_, __) => ApplyChanges();
            _update.Start();
            Disposed += (_, __) => _update.Dispose();
        }

        private TableLayoutPanel AddTab(string title)
        {
            var tab = new TabPageEx { Text = title, Padding = new Padding(14), AutoScroll = true };
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tab.Controls.Add(grid);
            _tabs.TabPages.Add(tab);
            return grid;
        }

        private void AddLightTab(string title, int max, Func<Vector4> get, Action<Vector4> set)
        {
            var grid = AddTab(title);
            AddColor(grid, () => new Vector3(get().X, get().Y, get().Z), rgb => set(new Vector4(rgb, get().W)));
            AddMaterialValue(grid, "强度", max, () => get().W, value =>
            {
                var light = get();
                light.W = value;
                set(light);
            });
        }

        private void AddColor(TableLayoutPanel grid, Func<Vector3> get, Action<Vector3> set)
        {
            var preview = new Panel { Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 22, Margin = new Padding(0, 3, 8, 3) };
            var choose = MakeButton("选色");
            var rgb = get();
            void UpdatePreview()
            {
                rgb = get();
                preview.BackColor = Color.FromArgb(ToByte(rgb.X), ToByte(rgb.Y), ToByte(rgb.Z));
            }

            choose.Click += (_, __) =>
            {
                using (var dialog = new ColorDialog { Color = preview.BackColor, FullOpen = true })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        set(new Vector3(dialog.Color.R, dialog.Color.G, dialog.Color.B) / 255f);
                        SetValues(_appearance);
                        _pending = true;
                    }
                }
            };

            AddRow(grid, new LabelEx { Text = "颜色", Tone = LabelTone.Heading, AutoSize = true, Anchor = AnchorStyles.Left }, preview, choose);
            void Change(Vector3 color)
            {
                set(color);
                UpdatePreview();
            }

            AddNumber(grid, "R", 255, 0, () => ToByte(get().X), v => Change(new Vector3((float)v / 255f, rgb.Y, rgb.Z)), Color.LightCoral);
            AddNumber(grid, "G", 255, 0, () => ToByte(get().Y), v => Change(new Vector3(rgb.X, (float)v / 255f, rgb.Z)), Color.LightGreen);
            AddNumber(grid, "B", 255, 0, () => ToByte(get().Z), v => Change(new Vector3(rgb.X, rgb.Y, (float)v / 255f)), Color.LightSkyBlue);
            LoadValues += UpdatePreview;
        }

        private static int ToByte(float value) => (int)Math.Round(value * 255f);

        private static ButtonEx MakeButton(string text) => new ButtonEx
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 2, 8, 2),
            Anchor = AnchorStyles.Right
        };

        private void AddMaterialValue(TableLayoutPanel grid, string title, int max, Func<float> get, Action<float> set)
            => AddNumber(grid, title, max, 2, () => (decimal)get(), v => set((float)v), WorkbenchTheme.Text);

        private void AddNumber(TableLayoutPanel grid, string title, int max, int decimals, Func<decimal> get, Action<decimal> set, Color labelColor)
        {
            var label = new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = labelColor, Margin = new Padding(0, 4, 10, 4) };
            var number = new NumericTextBoxEx
            {
                Maximum = max,
                DecimalPlaces = decimals,
                AccessibleName = title,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(4, 4, 0, 4)
            };
            var maxText = max.ToString("F" + decimals);
            void FitInput()
            {
                number.Width = TextRenderer.MeasureText(maxText, number.Font).Width + number.LogicalToDeviceUnits(12);
            }

            number.FontChanged += (_, __) => FitInput();
            number.DpiChangedAfterParent += (_, __) => FitInput();
            FitInput();
            var units = decimals == 0 ? 1 : 100;
            var slider = new TrackBar
            {
                AutoSize = false,
                Height = LogicalToDeviceUnits(28),
                Maximum = max * units,
                TickStyle = TickStyle.None,
                LargeChange = decimals == 0 ? 16 : 10,
                AccessibleName = title,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 2, 4, 2),
                BackColor = WorkbenchTheme.Surface
            };
            number.ValueChanged += (_, __) =>
            {
                slider.Value = (int)(Math.Round(number.Value, decimals) * units);
                if (!_loading)
                {
                    set(number.Value);
                    _pending = true;
                }
            };
            slider.ValueChanged += (_, __) => number.Value = (decimal)slider.Value / units;
            LoadValues += () => number.Value = Math.Round(get(), decimals);
            AddRow(grid, label, slider, number);
        }

        private static void AddRow(TableLayoutPanel grid, Control label, Control field, Control value)
        {
            var row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(label, 0, row);
            grid.Controls.Add(field, 1, row);
            grid.Controls.Add(value, 2, row);
        }

        private void SetValues(GreenScreenAppearance appearance)
        {
            _appearance = appearance;
            _loading = true;
            try
            {
                LoadValues?.Invoke();
            }
            finally
            {
                _loading = false;
            }
        }

        private void UpdateAvailability()
        {
            var stage = _host.ZoneService.ActiveGreenScreen;
            var enabled = stage != null;
            if (stage != _stage)
            {
                _stage = stage;
                _pending = false;
                if (enabled)
                {
                    SetValues(stage.Appearance);
                    _status.Text = "调节即时生效";
                }
            }

            _tabs.Enabled = enabled;
            _reset.Enabled = enabled;
            if (!enabled)
            {
                _status.Text = "进入区域 1 后可调节";
            }
        }

        private void ApplyChanges()
        {
            UpdateAvailability();
            if (!_pending)
            {
                return;
            }

            _pending = false;
            try
            {
                if (_stage?.TryApplyAppearance(_appearance) == true)
                {
                    _status.Text = "调节即时生效";
                }
            }
            catch (Exception error)
            {
                _status.Text = "调节失败，详情见日志";
                _host.LogError("调节绿幕失败：" + error.Message);
                SetValues(_stage.Appearance);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ApplyChanges();
            base.OnFormClosing(e);
        }
    }
}

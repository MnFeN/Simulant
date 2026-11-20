using Simulant.Core.Environment;
using Simulant.Utils.Maths;
using System;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed partial class EnvironmentEditorForm
    {
        private static readonly StudioDrawLayer[] StudioLayers =
        {
            StudioDrawLayer.BaseLower, StudioDrawLayer.Base, StudioDrawLayer.BaseUpper
        };

        private readonly CheckBoxEx _studioEnabled = new CheckBoxEx
        {
            Text = "启用摄影棚", Style = CheckBoxStyle.Switch, AutoSize = true, Anchor = AnchorStyles.Left, Enabled = false,
            Margin = new Padding(0, 0, 12, 0)
        };
        private readonly LabelEx _studioStatus = StudioLabel(string.Empty, LabelTone.Muted);
        private readonly ToolTip _studioHints = new ToolTip();
        private readonly Timer _studioSaveTimer = new Timer { Interval = 400 };
        private readonly NumericTextBoxEx[] _studioPosition = new NumericTextBoxEx[4];
        private readonly CheckBoxEx _studioXYMode = StudioMode();
        private readonly CheckBoxEx _studioZMode = StudioMode();
        private readonly CheckBoxEx _studioAngleMode = StudioMode();
        private readonly NumericTextBoxEx[] _studioSize = new NumericTextBoxEx[3];
        private readonly NumericTextBoxEx[] _studioColor = new NumericTextBoxEx[4];
        private readonly CheckBoxEx _studioDepthTest = StudioCheck("深度测试");
        private readonly CheckBoxEx _studioDepthWrite = StudioCheck("写入深度");
        private readonly ComboBoxEx _studioFloor = new ComboBoxEx { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = true };
        private readonly ComboBoxEx _studioLayer = new ComboBoxEx { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = true };
        private readonly LabelEx _studioHex = StudioLabel("#FFFFFFFF", LabelTone.Muted);
        private readonly Panel _studioSwatch = new Panel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(8, 3, 0, 3)
        };
        private NumericTextBoxEx _studioEnvironmentLight;
        private NumericTextBoxEx _studioDirectionalLight;
        private bool _syncingStudio;

        private TabPageEx BuildStudioTab()
        {
            var tab = new TabPageEx { Text = "摄影棚", Padding = new Padding(16) };
            var settings = _host.Studio.Settings;

            var toolbar = MakeStudioGrid(SizeType.Percent, SizeType.AutoSize);
            toolbar.Margin = new Padding(0, 0, 0, 9);
            var reset = MakeStudioButton("重置默认值");
            reset.Anchor = AnchorStyles.Right;
            reset.Margin = Padding.Empty;
            reset.Click += (_, __) =>
            {
                LoadStudioSettings(StudioSettings.Default);
                SaveStudioSettings();
                FlushStudioSettings();
            };
            _studioHints.SetToolTip(reset, "重置摄影棚参数并保存。");
            AddStudioRow(toolbar, _studioEnabled, reset);
            AddStudioRow(toolbar, _studioStatus);
            toolbar.SetColumnSpan(_studioStatus, 2);

            var position = BuildStudioPosition();
            var size = BuildStudioSize();
            var color = BuildStudioColor();
            var render = BuildStudioRender();
            LoadStudioSettings(settings);
            _studioSaveTimer.Tick += (_, __) => FlushStudioSettings();
            var cards = MakeStudioGrid(SizeType.Percent, SizeType.Percent);
            cards.Dock = DockStyle.Top;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            scroll.Controls.Add(cards);
            ScrollBarTheme.Attach(scroll);
            bool? wideLayout = null;
            void ArrangeCards()
            {
                var cardWidth = Math.Max(Math.Max(StudioCardWidth(position), StudioCardWidth(size)),
                    Math.Max(StudioCardWidth(color), StudioCardWidth(render)));
                var wide = scroll.Width >= 2 * cardWidth + SystemInformation.VerticalScrollBarWidth;
                if (wideLayout == wide)
                    return;

                wideLayout = wide;
                cards.SuspendLayout();
                try
                {
                    cards.Controls.Clear();
                    cards.RowStyles.Clear();
                    cards.ColumnCount = wide ? 2 : 1;
                    cards.ColumnStyles[0].Width = wide ? 50 : 100;
                    cards.RowCount = wide ? 2 : 4;
                    for (var index = 0; index < cards.RowCount; index++)
                        cards.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                    cards.Controls.Add(position, 0, 0);
                    cards.Controls.Add(size, 0, 1);
                    cards.Controls.Add(color, wide ? 1 : 0, wide ? 0 : 2);
                    cards.Controls.Add(render, wide ? 1 : 0, wide ? 1 : 3);
                }
                finally
                {
                    cards.ResumeLayout();
                }
            }

            scroll.SizeChanged += (_, __) => ArrangeCards();
            scroll.FontChanged += (_, __) => ArrangeCards();
            scroll.DpiChangedAfterParent += (_, __) => ArrangeCards();
            ArrangeCards();

            var body = MakeRows();
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(toolbar, 0, 0);
            body.Controls.Add(scroll, 0, 1);
            tab.Controls.Add(body);
            _studioEnabled.CheckedChanged += (_, __) => ToggleStudio();
            return tab;
        }

        private PanelEx BuildStudioPosition()
        {
            var grid = MakeStudioGrid(SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize);
            var axes = new[] { "X", "Y", "Z", "θ" };
            for (var axis = 0; axis < axes.Length; axis++)
            {
                var limit = axis == 3 ? 360 : 10000;
                var label = StudioLabel(axes[axis]);
                var input = _studioPosition[axis] = MakeStudioNumber(-limit, limit, axis == 3 ? 1 : 3, 0,
                    axis == 3 ? "角度（度）" : axes[axis] + "坐标");
                input.ValueChanged += (_, __) => SaveStudioSettings();
                AddStudioRow(grid, label, input);
                if (axis == 3)
                    SetStudioHint("角度单位为度，正方向与角色朝向一致。", label, input);
            }

            _studioXYMode.Dock = DockStyle.Fill;
            _studioXYMode.Margin = new Padding(2, 0, 2, 0);
            grid.Controls.Add(_studioXYMode, 2, 0);
            grid.SetRowSpan(_studioXYMode, 2);
            grid.Controls.Add(_studioZMode, 2, 2);
            grid.Controls.Add(_studioAngleMode, 2, 3);
            BindStudioMode(_studioXYMode, 0, 2, "XY 坐标");
            BindStudioMode(_studioZMode, 2, 1, "Z 坐标");
            BindStudioMode(_studioAngleMode, 3, 1, "朝向");
            return MakeStudioCard("位置 · 米 / 度", grid);
        }

        private PanelEx BuildStudioSize()
        {
            var grid = MakeStudioGrid(SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize);
            var dimensions = new[] { "长", "宽", "高" };
            for (var axis = 0; axis < dimensions.Length; axis++)
            {
                var number = _studioSize[axis] = MakeStudioNumber((decimal)StudioSettings.MinSize, (decimal)StudioSettings.MaxSize,
                    1, (decimal)StudioSettings.DefaultSize, dimensions[axis]);
                AddStudioRow(grid, StudioLabel(dimensions[axis]), MakeStudioSlider(number, logarithmic: true), number);
            }
            return MakeStudioCard("尺寸 · 米", grid);
        }

        private PanelEx BuildStudioColor()
        {
            var grid = MakeStudioGrid(SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize, SizeType.AutoSize);
            var channels = new[] { "R", "G", "B", "A" };
            var colors = new[] { WorkbenchTheme.PaleRed, WorkbenchTheme.Green, WorkbenchTheme.Blue, WorkbenchTheme.Text };
            for (var channel = 0; channel < channels.Length; channel++)
            {
                var label = StudioLabel(channels[channel]);
                label.ForeColor = colors[channel];
                var number = _studioColor[channel] = MakeStudioNumber(0, 255, 0, 255, channels[channel]);
                AddStudioRow(grid, label, MakeStudioSlider(number), number);
            }

            var choose = MakeStudioButton("选色");
            choose.Dock = DockStyle.Fill;
            choose.Margin = _studioSwatch.Margin;
            choose.Click += (_, __) => PickStudioColor();
            _studioSwatch.Paint += (_, e) =>
            {
                e.Graphics.Clear(WorkbenchTheme.Field);
                var bounds = _studioSwatch.ClientRectangle;
                bounds.Width /= 2;
                e.Graphics.FillRectangle(Brushes.White, bounds);
                using (var brush = new SolidBrush(ReadStudioColor()))
                    e.Graphics.FillRectangle(brush, _studioSwatch.ClientRectangle);
            };
            grid.Controls.Add(_studioSwatch, 3, 0);
            grid.SetRowSpan(_studioSwatch, 3);
            grid.Controls.Add(choose, 3, 3);

            var title = MakeStudioGrid(SizeType.Percent, SizeType.AutoSize);
            _studioHex.Anchor = AnchorStyles.Right;
            _studioHex.Margin = Padding.Empty;
            AddStudioRow(title, StudioLabel("颜色", LabelTone.Heading), _studioHex);
            return MakeStudioCard(title, grid);
        }

        private PanelEx BuildStudioRender()
        {
            _studioFloor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _studioFloor.Items.AddRange(new object[] { "模型", "贴花" });
            _studioLayer.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _studioLayer.Items.AddRange(new object[] { "背景层", "普通层", "上层" });

            var layer = MakeStudioGrid(SizeType.AutoSize, SizeType.Percent);
            var floorLabel = StudioLabel("地板类型");
            var layerLabel = StudioLabel("模型绘制层");
            AddStudioRow(layer, floorLabel, _studioFloor);
            AddStudioRow(layer, layerLabel, _studioLayer);
            SetStudioHint("模型是独立平面，可悬空。贴花投影到地图表面，固定使用普通层。", floorLabel, _studioFloor);
            SetStudioHint("选择墙、顶和模型地板的绘制层。", layerLabel, _studioLayer);
            var depth = MakeStudioGrid(SizeType.AutoSize, SizeType.AutoSize);
            depth.Anchor = AnchorStyles.Left;
            AddStudioRow(depth, _studioDepthTest, _studioDepthWrite);
            SetStudioHint("使用已有深度判断模型是否被地形或物体挡住。", _studioDepthTest);
            SetStudioHint("将模型深度写入缓冲区，可能挡住之后绘制的特效。", _studioDepthWrite);
            _studioFloor.SelectedIndexChanged += (_, __) => SaveStudioSettings();
            _studioLayer.SelectedIndexChanged += (_, __) => SaveStudioSettings();
            _studioDepthTest.CheckedChanged += (_, __) => SaveStudioSettings();
            _studioDepthWrite.CheckedChanged += (_, __) => SaveStudioSettings();
            var lighting = MakeStudioGrid(SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize);
            lighting.Margin = new Padding(0, 5, 0, 0);
            _studioEnvironmentLight = AddStudioLighting(lighting, "环境光", "模型受场景环境光影响的强度，0 为关闭。");
            _studioDirectionalLight = AddStudioLighting(lighting, "定向光", "模型受场景定向光影响的强度，0 为关闭。");
            return MakeStudioCard("渲染", layer, depth, lighting);
        }

        private NumericTextBoxEx AddStudioLighting(TableLayoutPanel grid, string name, string hint)
        {
            var label = StudioLabel(name);
            var input = MakeStudioNumber(0, 100, 0, 0, name);
            var slider = MakeStudioSlider(input);
            AddStudioRow(grid, label, slider, input);
            SetStudioHint(hint, label, slider, input);
            return input;
        }

        private void SetStudioHint(string hint, params Control[] controls)
        {
            foreach (var control in controls)
                _studioHints.SetToolTip(control, hint);
        }

        private static PanelEx MakeStudioCard(string heading, params Control[] controls)
            => MakeStudioCard(StudioLabel(heading, LabelTone.Heading), controls);

        private static PanelEx MakeStudioCard(Control title, params Control[] controls)
        {
            var card = new PanelEx
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Tone = PanelTone.Inset,
                Padding = new Padding(13, 10, 13, 11),
                Margin = new Padding(0, 0, 10, 10)
            };
            var rows = MakeStudioGrid(SizeType.Percent);
            rows.Dock = DockStyle.Top;
            title.Margin = new Padding(0, 0, 0, 6);
            AddStudioRow(rows, title);
            foreach (var control in controls)
                AddStudioRow(rows, control);
            card.Controls.Add(rows);
            return card;
        }

        private static int StudioCardWidth(PanelEx card)
            => card.Controls[0].GetPreferredSize(Size.Empty).Width + card.Padding.Horizontal + card.Margin.Horizontal;

        private static TableLayoutPanel MakeStudioGrid(params SizeType[] columns)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = columns.Length,
                Margin = Padding.Empty
            };
            foreach (var style in columns)
                grid.ColumnStyles.Add(new ColumnStyle(style, 100f / columns.Length));
            return grid;
        }

        private static void AddStudioRow(TableLayoutPanel grid, params Control[] controls)
        {
            var row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var column = 0; column < controls.Length; column++)
                grid.Controls.Add(controls[column], column, row);
        }

        private static LabelEx StudioLabel(string text, LabelTone tone = LabelTone.Body)
            => new LabelEx
            {
                Text = text,
                Tone = tone,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 3, 8, 3)
            };

        private static CheckBoxEx StudioCheck(string text)
            => new CheckBoxEx { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 10, 3) };

        private static CheckBoxEx StudioMode() => new CheckBoxEx
        {
            Text = "绝对",
            AutoSize = true,
            Style = CheckBoxStyle.ToggleButton,
            Anchor = AnchorStyles.Left,
            BackColor = WorkbenchTheme.Field,
            ForeColor = WorkbenchTheme.Text,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(2, 2, 2, 2)
        };

        private static ButtonEx MakeStudioButton(string text)
            => new ButtonEx
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 2, 8, 2),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 5, 8, 4)
            };

        private static NumericTextBoxEx MakeStudioNumber(decimal minimum, decimal maximum, int places, decimal value, string name)
        {
            var number = new NumericTextBoxEx
            {
                Minimum = minimum, Maximum = maximum, DecimalPlaces = places, Value = value,
                AccessibleName = name, Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(4, 4, 6, 4), HideSelection = false
            };
            var format = "F" + places;
            var min = minimum.ToString(format, CultureInfo.CurrentCulture);
            var max = maximum.ToString(format, CultureInfo.CurrentCulture);
            void FitInput()
            {
                var width = Math.Max(TextRenderer.MeasureText(min, number.Font).Width, TextRenderer.MeasureText(max, number.Font).Width);
                number.MinimumSize = new Size(width + number.LogicalToDeviceUnits(9), 0);
                number.Width = number.MinimumSize.Width;
            }
            number.FontChanged += (_, __) => FitInput();
            number.DpiChangedAfterParent += (_, __) => FitInput();
            FitInput();
            return number;
        }

        private TrackBar MakeStudioSlider(NumericTextBoxEx number, bool logarithmic = false)
        {
            const int stepsPerDecade = 1000;
            int ToSlider(decimal value)
                => logarithmic ? (int)Math.Round(Math.Log10((double)value) * stepsPerDecade) : (int)value;

            var slider = new TrackBar
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false,
                TickStyle = TickStyle.None,
                Margin = new Padding(0, 3, 3, 3),
                Size = new Size(LogicalToDeviceUnits(76), LogicalToDeviceUnits(25)),
                Minimum = ToSlider(number.Minimum),
                Maximum = ToSlider(number.Maximum),
                SmallChange = logarithmic ? 10 : 1,
                LargeChange = logarithmic ? 100 : 16,
                Value = ToSlider(number.Value),
                AccessibleName = number.AccessibleName,
                BackColor = WorkbenchTheme.Inset
            };
            bool syncingSlider = false;
            slider.ValueChanged += (_, __) =>
            {
                if (syncingSlider)
                    return;

                syncingSlider = true;
                try
                {
                    number.Value = logarithmic
                        ? Math.Round((decimal)Math.Pow(10, (double)slider.Value / stepsPerDecade), number.DecimalPlaces)
                        : slider.Value;
                }
                finally
                {
                    syncingSlider = false;
                }
            };
            number.ValueChanged += (_, __) =>
            {
                if (!syncingSlider)
                {
                    syncingSlider = true;
                    try
                    {
                        slider.Value = ToSlider(number.Value);
                    }
                    finally
                    {
                        syncingSlider = false;
                    }
                }
                SaveStudioSettings();
            };
            return slider;
        }

        internal void RefreshStudioState()
        {
            if (IsDisposed || Disposing)
                return;

            var enabled = _host.Studio.HasStudio;
            _syncingStudio = true;
            try
            {
                _studioEnabled.Checked = enabled;
                _studioEnabled.Enabled = enabled || _host.CanRunDebug;
                _studioStatus.Text = enabled ? "换图自动关闭" :
                    _host.CanRunDebug ? "未放置" : "等待游戏就绪";
            }
            finally
            {
                _syncingStudio = false;
            }
        }

        private void ToggleStudio()
        {
            if (_syncingStudio)
                return;

            try
            {
                _host.SetStudioEnabled(_studioEnabled.Checked);
                SetFeedback(_studioEnabled.Checked ? "已放置摄影棚。" : "已关闭摄影棚。");
            }
            catch (Exception error)
            {
                SetFeedback("切换摄影棚失败。", error);
            }
            finally
            {
                RefreshStudioState();
            }
        }

        private void BindStudioMode(CheckBoxEx button, int first, int count, string target)
        {
            button.AccessibleName = target + "模式";
            SetStudioHint($"左键切换绝对值和相对值。相对模式下右键可改为绝对值，并填入角色当前{target}。", button);
            button.CheckedChanged += (_, __) =>
            {
                button.Text = button.Checked ? "相对" : "绝对";
                button.BackColor = button.Checked ? WorkbenchTheme.Selection : WorkbenchTheme.Field;
                ChangeStudioMode(button, first, count, false);
            };
            button.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Right && button.Checked)
                    ChangeStudioMode(button, first, count, true);
            };
        }

        private void ChangeStudioMode(CheckBoxEx button, int first, int count, bool capture)
        {
            if (_syncingStudio)
                return;

            _syncingStudio = true;
            try
            {
                if (_host.TryGetStudioPlayerTransform(out var position, out var heading))
                {
                    var reference = new[] { position.X, position.Y, position.Z, (float)Angles.ToDegrees(heading) };
                    if (capture)
                        button.Checked = false;

                    for (var index = first; index < first + count; index++)
                    {
                        var current = (float)_studioPosition[index].Value;
                        var value = capture ? reference[index] : current + (button.Checked ? -reference[index] : reference[index]);
                        if (index == 3)
                            value = (float)Math.IEEERemainder(value, 360);

                        SetStudioValue(_studioPosition[index], value);
                    }
                }
                else if (capture)
                {
                    SetFeedback("找不到当前角色。", failed: true);
                    return;
                }
            }
            catch (Exception error)
            {
                SetFeedback("读取角色位置或朝向失败。", error);
            }
            finally
            {
                _syncingStudio = false;
            }
            SaveStudioSettings();
        }

        private void LoadStudioSettings(StudioSettings settings)
        {
            _syncingStudio = true;
            try
            {
                SetStudioValue(_studioPosition[0], settings.Position.X);
                SetStudioValue(_studioPosition[1], settings.Position.Y);
                SetStudioValue(_studioPosition[2], settings.Position.Z);
                SetStudioValue(_studioPosition[3], settings.Angle);
                _studioXYMode.Checked = settings.RelativeXY;
                _studioZMode.Checked = settings.RelativeZ;
                _studioAngleMode.Checked = settings.RelativeAngle;
                SetStudioValue(_studioSize[0], settings.Size.X);
                SetStudioValue(_studioSize[1], settings.Size.Y);
                SetStudioValue(_studioSize[2], settings.Size.Z);
                SetStudioValue(_studioColor[0], settings.Color.X * 255);
                SetStudioValue(_studioColor[1], settings.Color.Y * 255);
                SetStudioValue(_studioColor[2], settings.Color.Z * 255);
                SetStudioValue(_studioColor[3], settings.Color.W * 255);
                _studioFloor.SelectedIndex = (int)settings.FloorType;
                _studioLayer.SelectedIndex = Array.IndexOf(StudioLayers, settings.DrawLayer);
                _studioDepthTest.Checked = settings.DepthTest;
                _studioDepthWrite.Checked = settings.DepthWrite;
                SetStudioValue(_studioEnvironmentLight, settings.EnvironmentLight);
                SetStudioValue(_studioDirectionalLight, settings.DirectionalLight);
                RefreshStudioColor();
            }
            finally
            {
                _syncingStudio = false;
            }
        }

        private static void SetStudioValue(NumericTextBoxEx input, float value)
        {
            var number = Math.Min((float)input.Maximum, Math.Max((float)input.Minimum, float.IsNaN(value) ? 0f : value));
            input.Value = Math.Round(Math.Max(input.Minimum, Math.Min(input.Maximum, (decimal)number)), input.DecimalPlaces);
        }

        private void PickStudioColor()
        {
            using (var dialog = new ColorDialog { Color = ReadStudioColor(), FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                _syncingStudio = true;
                try
                {
                    _studioColor[0].Value = dialog.Color.R;
                    _studioColor[1].Value = dialog.Color.G;
                    _studioColor[2].Value = dialog.Color.B;
                }
                finally
                {
                    _syncingStudio = false;
                }

                SaveStudioSettings();
            }
        }

        private Color ReadStudioColor() => Color.FromArgb((int)_studioColor[3].Value, (int)_studioColor[0].Value,
            (int)_studioColor[1].Value, (int)_studioColor[2].Value);

        private void RefreshStudioColor()
        {
            var color = ReadStudioColor();
            _studioHex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
            _studioSwatch.Invalidate();
        }

        private void SaveStudioSettings()
        {
            if (_syncingStudio || _studioFloor.SelectedIndex < 0 || _studioLayer.SelectedIndex < 0)
                return;

            var color = ReadStudioColor();
            RefreshStudioColor();
            _host.ChangeStudioSettings(new StudioSettings
            {
                Position = ReadStudioVector(_studioPosition),
                RelativeXY = _studioXYMode.Checked,
                RelativeZ = _studioZMode.Checked,
                Angle = (float)_studioPosition[3].Value,
                RelativeAngle = _studioAngleMode.Checked,
                Size = ReadStudioVector(_studioSize),
                Color = new Vector4(color.R, color.G, color.B, color.A) / 255f,
                FloorType = (StudioFloorType)_studioFloor.SelectedIndex,
                DrawLayer = StudioLayers[_studioLayer.SelectedIndex],
                DepthTest = _studioDepthTest.Checked,
                DepthWrite = _studioDepthWrite.Checked,
                EnvironmentLight = (int)_studioEnvironmentLight.Value,
                DirectionalLight = (int)_studioDirectionalLight.Value
            });
            _studioSaveTimer.Stop();
            _studioSaveTimer.Start();
        }

        private void FlushStudioSettings()
        {
            if (!_studioSaveTimer.Enabled)
                return;

            _studioSaveTimer.Stop();
            try
            {
                _host.SaveStudioSettings();
            }
            catch (Exception error)
            {
                SetFeedback("保存摄影棚设置失败。", error);
            }
        }

        private static Vector3 ReadStudioVector(NumericTextBoxEx[] inputs)
            => new Vector3((float)inputs[0].Value, (float)inputs[1].Value, (float)inputs[2].Value);
    }
}

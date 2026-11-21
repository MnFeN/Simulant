using Lumina.Excel.Sheets;
using Simulant.Core.Entity;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class ActionListForm
    {
        private bool _loadingTimelinePreview;
        private Timer _timelineTimer;
        private SimEntityManager _timelineEntities;
        private SimActor _timelineActor;
        private ushort _previewTimelineId;
        private int _timelineDisplayMilliseconds;
        private long _drawRequestedAt;
        private string _timelineResult;

        private void ConfigureTimelineMenu(ActionResource row, bool enabled)
        {
            var menu = miPreviewTimeline;
            var previousItems = menu.DropDownItems.Cast<ToolStripItem>().ToArray();
            menu.DropDownItems.Clear();
            foreach (var item in previousItems)
                item.Dispose();

            menu.DefaultAction = null;
            menu.Available = row?.CanPreviewAnimation == true;
            menu.Enabled = enabled;
            if (!menu.Available || !enabled)
                return;

            if (row.CanPlayOnSelf)
            {
                System.Action play = () => PreviewSelfTimeline(row);
                menu.DefaultAction = play;
                menu.DropDownItems.Add("在自身播放时间轴", null, (_, __) => play());
            }
            else if (row.CanPlayOnNpc)
                AddTimelineCasterItems(row, menu);

            var paths = row.VfxPaths;
            if (paths.Length != 0)
            {
                if (menu.DefaultAction == null)
                    menu.DefaultAction = async () => await PreviewTimelineVfxAsync(paths);

                var effects = menu.DropDownItems;
                if (effects.Count != 0)
                {
                    var singleEffects = new ToolStripMenuItem("单项特效");
                    effects.Add(singleEffects);
                    effects = singleEffects.DropDownItems;
                }
                foreach (var path in paths)
                    effects.Add(path, null, async (_, __) => await PreviewTimelineVfxAsync(new[] { path }));
            }
            else if (row.CanPlayOnNpc && !row.HasModelTimeline && menu.DefaultAction == null)
                menu.DefaultAction = async () => await PreviewTimelineAsync(row, Array.Empty<uint>());
        }

        private void AddTimelineCasterItems(ActionResource row, SplitMenuItem menu)
        {
            if (!ActionNpcIndex.Casters.TryGetValue(row.Id, out var recorded))
                recorded = Array.Empty<uint>();

            menu.DropDownItems.Add("选择实体", null, async (_, __) => await PreviewTimelineAsync(row, recorded));
            var fixedSkeleton = ActionTimelineModel.GetFixedSkeleton(row.AnimationEnd) != null;
            if (!fixedSkeleton && recorded.Length == 0)
                return;

            var data = GameDataService.Instance;
            var names = data.GetSheet<BNpcName>();
            var candidates = fixedSkeleton
                ? ActionTimelineModel.Find(data, row.AnimationEnd, recorded, _lifetimeToken)
                    .Select(model => (model.BaseId, model.NameId))
                : recorded.Select(baseId => (BaseId: baseId, NameId: BNpcNameIndex.GetNameId(baseId)));
            var items = new List<ToolStripItem>();
            foreach (var caster in candidates)
            {
                var name = ActionTimelineModel.GetName(names, caster.NameId);
                var text = string.IsNullOrWhiteSpace(name) ? $"BNpcBase {caster.BaseId}" : $"{name} {caster.BaseId}";
                System.Action play = async () => await PreviewTimelineAsync(row, recorded, caster);
                if (menu.DefaultAction == null)
                    menu.DefaultAction = play;

                items.Add(new ToolStripMenuItem(text, null, (_, __) => play()));
            }
            menu.DropDownItems.AddRange(items.ToArray());
        }

        private async Task PreviewTimelineAsync(ActionResource row, uint[] recorded,
            (uint BaseId, uint NameId)? caster = null)
        {
            if (_loadingTimelinePreview)
                return;

            _loadingTimelinePreview = true;

            try
            {
                if (_host == null)
                    throw new InvalidOperationException("请先初始化插件并进入游戏。");

                _host.EnsureDebugAllowed();
                var player = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("没有找到当前玩家。");
                var data = GameDataService.Instance;
                lblResult.Text = "正在查找兼容模型……";
                ActionTimelineModel? model;
                if (caster.HasValue)
                {
                    model = await Task.Run(() => ActionTimelineModel.FindCompatible(data, row.AnimationEnd, caster.Value,
                        recorded.Contains(caster.Value.BaseId)), _lifetimeToken);
                    _lifetimeToken.ThrowIfCancellationRequested();
                    if (!model.HasValue)
                        throw new InvalidOperationException($"BNpcBase {caster.Value.BaseId} 缺少兼容的时间轴资源。");
                }
                else
                {
                    var models = await Task.Run(() => ActionTimelineModel.Find(data, row.AnimationEnd, recorded, _lifetimeToken), _lifetimeToken);
                    _lifetimeToken.ThrowIfCancellationRequested();
                    if (models.Length == 0)
                        throw new InvalidOperationException($"没有找到含 {row.AnimationEnd} 的战斗 NPC 模型。");

                    model = SelectTimelineModel(models);
                }

                if (!model.HasValue)
                {
                    lblResult.Text = "已取消预览。";
                    return;
                }

                var selected = model.Value;
                var duration = await Task.Run(() => selected.GetTimelineDuration(data, row.AnimationEnd), _lifetimeToken);
                _lifetimeToken.ThrowIfCancellationRequested();
                _host.EnsureDebugAllowed();

                if (!player.IsAlive)
                    throw new InvalidOperationException("查找期间已切换区域，请重新预览。");

                StartTimelinePreview(player, selected, unchecked((ushort)row.AnimationEndId), duration);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                ShowError("预览结束时间轴失败", error);
            }
            finally
            {
                _loadingTimelinePreview = false;
            }
        }

        private ActionTimelineModel? SelectTimelineModel(ActionTimelineModel[] models)
        {
            using (var dialog = new FormEx
            {
                Text = "选择实体",
                ClientSize = new Size(Font.Height * 34, Font.Height * 21),
                MinimumSize = new Size(Font.Height * 23, Font.Height * 14),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(Font.Height / 2),
                    ColumnCount = 1,
                    RowCount = 4
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var filter = new TextBoxEx { Dock = DockStyle.Fill };
                var choices = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = dialog.BackColor,
                    ForeColor = dialog.ForeColor,
                    IntegralHeight = false
                };
                var play = new ButtonEx { Text = "预览", AutoSize = true, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right };
                layout.Controls.Add(new Label { Text = "搜索实体名、BNpcBase、BNpcName 或骨架编号", AutoSize = true }, 0, 0);
                layout.Controls.Add(filter, 0, 1);
                layout.Controls.Add(choices, 0, 2);
                layout.Controls.Add(play, 0, 3);
                dialog.Controls.Add(layout);
                dialog.AcceptButton = play;

                void UpdateChoices()
                {
                    var query = filter.Text.Trim();
                    var hexadecimal = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                    var hasId = uint.TryParse(hexadecimal ? query.Substring(2) : query,
                        hexadecimal ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var id);
                    choices.DataSource = query.Length == 0 ? models : models.Where(candidate => hasId
                        ? candidate.BaseId == id || candidate.NameId != 0 && candidate.NameId == id
                        : candidate.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            candidate.Skeleton.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    choices.SelectedIndex = choices.Items.Count == 1 ? 0 : -1;
                    play.Enabled = choices.SelectedIndex >= 0;
                }

                filter.TextChanged += (_, __) => UpdateChoices();
                choices.SelectedIndexChanged += (_, __) => play.Enabled = choices.SelectedIndex >= 0;
                choices.DoubleClick += (_, __) =>
                {
                    if (play.Enabled)
                        dialog.DialogResult = DialogResult.OK;
                };
                dialog.Shown += (_, __) =>
                {
                    UpdateChoices();
                    filter.Focus();
                };
                return dialog.ShowDialog(this) == DialogResult.OK && choices.SelectedItem is ActionTimelineModel selected
                    ? selected : (ActionTimelineModel?)null;
            }
        }

        private void StartTimelinePreview(Character player, ActionTimelineModel model, ushort timelineId, TimeSpan? duration)
        {
            ClearTimelinePreview();
            _timelineEntities = _host.DebugEntities;
            var heading = player.Heading;

            try
            {
                _timelineActor = _timelineEntities.SpawnBNpc(new CharacterSpawnSpec
                {
                    BNpcBaseId = model.BaseId,
                    BNpcNameId = model.NameId,
                    Pos = player.Pos3D + new Vector3((float)Math.Sin(heading), (float)Math.Cos(heading), 0) * 4,
                    Heading = heading
                });
                var native = _timelineActor.Entity.Native;
                native.InCombat.Set(false);
                native.IsHostile.Set(false);
                _timelineActor.SetReadyToDraw();
                _timelineActor.EnableDraw();
                var targetable = native.TargetableStatus;
                targetable.Set(targetable.Get() & ~ObjectTargetableFlags.IsTargetable);
                WaitForTimelineModel(timelineId, model.ToString(), duration);
            }
            catch
            {
                TryClearTimelinePreview();
                throw;
            }
        }

        private void PreviewSelfTimeline(ActionResource row)
        {
            if (_lifetimeToken.IsCancellationRequested)
                return;

            try
            {
                if (_host == null)
                    throw new InvalidOperationException("请先初始化插件并进入游戏。");

                _host.EnsureDebugAllowed();
                var player = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("没有找到当前玩家。");
                ClearTimelinePreview();

                var timelineId = unchecked((ushort)row.AnimationEndId);
                player.PlayBlendTimeline(timelineId);
                lblResult.Text = $"已在自身播放时间轴 {timelineId}";
            }
            catch (Exception error)
            {
                ShowError("预览结束时间轴失败", error);
            }
        }

        private void WaitForTimelineModel(ushort timelineId, string model, TimeSpan? duration)
        {
            if (_timelineTimer == null)
            {
                _timelineTimer = new Timer(components);
                _timelineTimer.Tick += TimelinePreviewTick;
            }
            _drawRequestedAt = Stopwatch.GetTimestamp();
            _previewTimelineId = timelineId;
            _timelineDisplayMilliseconds = duration.HasValue ? (int)duration.Value.TotalMilliseconds + 3000 : 20000;
            _timelineResult = $"已播放时间轴 {timelineId} · {model}";
            lblResult.Text = "正在加载预览模型……";
            _timelineTimer.Interval = 100;
            _timelineTimer.Start();
        }

        private void TimelinePreviewTick(object sender, EventArgs e)
        {
            try
            {
                if (_previewTimelineId == 0 || !_host.CanRunDebug || !_timelineActor.IsActive)
                {
                    ClearTimelinePreview();
                    return;
                }

                if (_timelineActor.Entity.Native.DrawObject.Ptr == IntPtr.Zero)
                {
                    if (Stopwatch.GetTimestamp() - _drawRequestedAt >= 5 * Stopwatch.Frequency)
                        throw new TimeoutException("预览模型加载超时，可尝试其他兼容模型。");

                    return;
                }

                _timelineActor.Entity.PlayBlendTimeline(_previewTimelineId);
                _previewTimelineId = 0;
                lblResult.Text = _timelineResult;
                _timelineTimer.Interval = _timelineDisplayMilliseconds;
            }
            catch (Exception error)
            {
                TryClearTimelinePreview();
                ShowError("播放结束时间轴失败", error);
            }
        }

        private void ClearTimelinePreview()
        {
            _timelineTimer?.Stop();
            _timelineEntities?.Delete(_timelineActor);
            _timelineActor = null;
            _timelineEntities = null;
        }

        private void TryClearTimelinePreview()
        {
            try
            {
                ClearTimelinePreview();
            }
            catch (Exception error)
            {
                _host?.LogWarning("清理时间轴预览实体失败：" + error.Message);
            }
        }
    }
}

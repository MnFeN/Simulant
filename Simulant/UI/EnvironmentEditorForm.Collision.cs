using Simulant.Core;
using Simulant.Core.Environment;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed partial class EnvironmentEditorForm
    {
        private readonly DataGridViewEx _collisions = ConfigureGrid(new DataGridViewEx(), 32);
        private readonly LabelEx _collisionStatus = MakeLabel("点击扫描，双击结果行开关碰撞。", LabelTone.Muted);
        private readonly ButtonEx _collisionScan = new ButtonEx { Text = "扫描", Width = 80 };
        private readonly CollisionInspector _collisionInspector;
        private readonly CancellationTokenSource _collisionLifetime = new CancellationTokenSource();
        private readonly TabPageEx _collisionTab;

        private int? _collisionGeneration;

        private TabPageEx BuildCollisionTab()
        {
            _collisions.ScrollBars = ScrollBars.Both;
            _collisions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            _collisions.Columns.Add(MakeColumn("状态", 64));
            _collisions.Columns.Add(MakeColumn("距离（m）", 76));
            _collisions.Columns.Add(MakeColumn("碰撞类型", 94));
            _collisions.Columns.Add(MakeColumn("材质", 104));
            _collisions.Columns.Add(MakeColumn("Layout ID", 128));
            _collisions.Columns.Add(MakeColumn("布局类型", 101));
            _collisions.Columns.Add(MakeColumn("图层", 68));
            var path = MakeColumn("资源路径", 0);
            path.MinimumWidth = 180;
            _collisions.Columns.Add(path);
            _collisionScan.Click += (_, __) => ScanCollisions();
            _collisions.CellDoubleClick += (_, e) => ToggleCollision(e.RowIndex);

            var tab = new TabPageEx { Text = "碰撞体", Padding = new Padding(14) };
            var layout = MakeRows();
            var warning = MakeLabel("关闭碰撞体会导致穿墙，请谨慎使用。", LabelTone.Small);
            warning.ForeColor = WorkbenchTheme.Yellow;
            warning.AutoEllipsis = false;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(warning, 0, 0);
            layout.Controls.Add(MakeBar(_collisionStatus, _collisionScan), 0, 1);
            layout.Controls.Add(_collisions, 0, 2);
            tab.Controls.Add(layout);
            return tab;
        }

        private async void ScanCollisions()
        {
            if (!_host.Configuration.DeveloperMode)
            {
                return;
            }

            _collisions.Rows.Clear();
            _collisionScan.Enabled = false;
            _collisionStatus.Text = "正在扫描碰撞体…";
            _collisionGeneration = null;
            try
            {
                var environment = _host.EnvironmentService;
                if (environment == null || !environment.TryReadCurrent(out var shown))
                    throw new InvalidOperationException("请等待当前区域加载完成。");

                var generation = _host.WorldGeneration;
                var cancellation = _collisionLifetime.Token;
                var (hits, groundHit, points) = await Task.Run(() => _collisionInspector.Scan(shown, generation), cancellation);
                if (IsDisposed || Disposing || !_host.Configuration.DeveloperMode)
                    return;
                if (generation != _host.WorldGeneration ||
                    !environment.TryReadCurrent(out var current) || !shown.IsSameRegion(current))
                    throw new InvalidOperationException("区域已变化，请重新扫描。");

                foreach (var hit in hits)
                {
                    var id = hit.LayoutId == 0 ? "—" : $"0x{unchecked((uint)hit.LayoutId):X8}";
                    if (hit.LayoutId >> 32 != 0)
                        id += $"/{hit.LayoutId >> 32:X8}";
                    var index = _collisions.Rows.Add(null, hit.Distance.ToString("0.00"), hit.ColliderType, $"0x{hit.Material:X}", id,
                        hit.InstanceType ?? "—", hit.LayerId?.ToString() ?? "—", hit.Path ?? "—");
                    var row = _collisions.Rows[index];
                    row.Tag = hit;
                    UpdateCollisionRow(row, hit.Enabled);
                }
                _collisionGeneration = generation;
                var ground = groundHit ? "脚下地面已命中" : "脚下地面未命中";
                var enabledCount = Array.FindAll(hits, hit => hit.Enabled).Length;
                _collisionStatus.Text = enabledCount == 0 ? $"{ground}，水平 {CollisionInspector.Range:0} 米内无命中。" :
                    $"{ground}，水平命中 {enabledCount} 个碰撞体。双击行切换。";
                if (points.Length != 0)
                {
                    await Task.Run(() => MarkCollisionPoints(points, generation, cancellation), cancellation);
                }
            }
            catch (OperationCanceledException) when (_collisionLifetime.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && _host.Configuration.DeveloperMode)
                    _collisionStatus.Text = error.Message;
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    _collisionScan.Enabled = true;
            }
        }

        private void ToggleCollision(int index)
        {
            if (index < 0 || !_host.Configuration.DeveloperMode || _collisionGeneration != _host.WorldGeneration)
            {
                return;
            }

            var row = _collisions.Rows[index];
            if (!(row.Tag is CollisionHit hit))
            {
                return;
            }

            try
            {
                _collisionInspector.Toggle(hit);
                UpdateCollisionRow(row, hit.Enabled);
                _collisionStatus.Text = hit.Enabled ? "碰撞已开启。" : "碰撞已关闭。双击可恢复。";
            }
            catch (Exception error)
            {
                _collisionStatus.Text = error.Message;
            }
        }

        private static void UpdateCollisionRow(DataGridViewRow row, bool enabled)
        {
            var status = row.Cells[0];
            status.Value = enabled ? "开启" : "关闭";
            status.Style.ForeColor = enabled ? WorkbenchTheme.Green : WorkbenchTheme.PaleRed;
            status.Style.SelectionForeColor = status.Style.ForeColor;
        }

        private void MarkCollisionPoints(Vector3[] points, int generation, CancellationToken cancellation)
        {
            foreach (var point in points)
            {
                if (!_host.Configuration.DeveloperMode || _host.WorldGeneration != generation ||
                    _host.IsSwitchingTerritory || _host.Disposed || cancellation.IsCancellationRequested)
                {
                    return;
                }

                // 原生命中点为 Y 轴向上，ShowOmen 接收 ACT 坐标。
                VfxManager.ShowOmen("general_1bf", new Vector3(point.X, point.Z, point.Y), 0f, Vector3.One,
                    3d, _collisionInspector, cancellation: cancellation);
            }
        }

        private void DisposeCollisionTools()
        {
            if (_collisionLifetime.IsCancellationRequested)
            {
                return;
            }

            _collisionLifetime.Cancel();
            _collisionLifetime.Dispose();
            RestoreCollisionChanges();
            _collisionTab.Dispose();
        }

        private void RestoreCollisionChanges()
        {
            try
            {
                _collisionInspector.Restore();
            }
            catch (Exception error)
            {
                _host.LogWarning("恢复碰撞体失败：" + error.Message);
            }
        }

        private void RefreshCollisionResults()
        {
            var visible = _tabs.TabPages.Contains(_collisionTab);
            if (!_host.Configuration.DeveloperMode)
            {
                if (visible)
                {
                    _tabs.TabPages.Remove(_collisionTab);
                    _collisionGeneration = null;
                    _collisions.Rows.Clear();
                    _collisionStatus.Text = "点击扫描，双击结果行开关碰撞。";
                    RestoreCollisionChanges();
                }
                return;
            }

            if (!visible)
            {
                _tabs.TabPages.Add(_collisionTab);
            }

            if (!_collisionGeneration.HasValue || _collisionGeneration.Value == _host.WorldGeneration)
                return;

            _collisionGeneration = null;
            _collisions.Rows.Clear();
            _collisionStatus.Text = "区域已变化，请重新扫描。";
        }
    }
}

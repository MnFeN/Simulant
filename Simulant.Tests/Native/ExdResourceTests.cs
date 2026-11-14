using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Simulant.Game.Data;
using Simulant.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Simulant.Tests.Native
{
    internal static class ExdResourceTests
    {
        private static string _path;
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                _path = GameDataService.ResolveSqPackPath(args.Single());
                Control.CheckForIllegalCrossThreadCalls = true;

                foreach (var method in typeof(ExdResourceTests).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0))
                {
                    var watch = Stopwatch.StartNew();
                    method.Invoke(null, null);
                    Console.WriteLine($"通过：{method.Name}（{watch.Elapsed.TotalSeconds:F2} 秒）");
                }

                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        public static void DiscoversSheetsAndResolvesTimelineNamesWithoutAnActionMapping()
        {
            Assert(ExdResource.ListSheets(_path, Language.ChineseSimplified, CancellationToken.None).Contains("Action"), "应从客户端目录发现 Action 表。");
            var page = Read("Action");
            var id = Array.IndexOf(page.Columns, "RowId");
            var timeline = Array.IndexOf(page.Columns, "AnimationEnd");
            Assert(page.Rows.Length == ExdResource.PageSize && page.Total > page.Rows.Length, "大表应按页预览。");

            using (var data = new GameData(_path, new LuminaOptions
            {
                DefaultExcelLanguage = Language.ChineseSimplified
            }))
            {
                var row = page.Rows.First(cells => cells[timeline].Contains(" → "));
                var action = data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRow(uint.Parse(row[id]));
                Assert(row[timeline] == $"ActionTimeline#{action.AnimationEnd.RowId} → {action.AnimationEnd.Value.Key.ExtractText()}", "时间轴名称必须来自关联行的 Key。");
            }

            var next = Read("Action", page: 1);
            Assert(next.Rows[0][id] != page.Rows[0][id], "下一页必须使用不同的行。");
        }

        public static void SupportsSubrowsCollectionsRawColumnsAndSearching()
        {
            var subrows = Read("AkatsukiNote");
            Assert(subrows.Columns.Contains("SubrowId") && subrows.Rows.Length > 0, "子行表应保留 SubrowId。");
            var collection = Read("ActionComboRoute");
            Assert(collection.Rows.Any(row => row.Any(cell => cell.StartsWith("["))), "集合字段应展开其内容。");
            var array = Read("ActionTimeline");
            Assert(array.Rows.Length > 0, "时间轴表应可读取。");
            var query = array.Rows.First(row => row.Any(cell => cell.Contains("/"))).First(cell => cell.Contains("/"));
            var filtered = Read("ActionTimeline", query);
            Assert(filtered.Matches > 0 && filtered.Rows.All(row => row.Any(cell => cell.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)), "搜索应筛选实际字段内容。");
            var types = typeof(SheetAttribute).Assembly.GetTypes().Select(type => type.GetCustomAttribute<SheetAttribute>()?.Name).Where(name => name != null).ToArray();
            var nameWithoutSchema = ExdResource.ListSheets(_path, Language.ChineseSimplified, CancellationToken.None).First(name => !types.Contains(name));
            var raw = Read(nameWithoutSchema);
            Assert(raw.Notice.Contains("原始列") && raw.Columns.Any(column => column.StartsWith("列 ")), "无强类型定义时仍应显示原始列。");

            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();

                try
                {
                    ExdResource.Read(_path, Language.ChineseSimplified, "Action", "", 0, cancel.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                throw new InvalidOperationException("已取消的请求不应继续读取。");
            }
        }

        public static void FormsCenterAndSwitchSheetsWithoutBlockingOrApplyingStaleResults()
        {
            using (var data = new GameDataService(_path))
            using (var form = new ExdBrowserForm
            {
                Opacity = 0,
                ShowInTaskbar = false
            }

            )
            using (var timer = new System.Windows.Forms.Timer
            {
                Interval = 10
            }

            )
            {
                typeof(GameDataService).GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
                var pulses = 0;
                timer.Tick += (_, __) => pulses++;
                timer.Start();
                form.Show();
                Await(() => Field<Label>(form, "lblStatus").Text.StartsWith("Action："));
                Assert(pulses > 0, "读取期间必须能够处理窗口消息。");
                AssertCentered(form);
                var grid = Field<DataGridView>(form, "grid");
                Assert(grid.VirtualMode && grid.RowCount == ExdResource.PageSize, "窗口应采用分页虚拟表格。");
                Context();
                Field<ListBox>(form, "listSheets").SelectedItem = "Item";
                Field<ListBox>(form, "listSheets").SelectedItem = "ActionTimeline";
                Await(() => Field<Label>(form, "lblStatus").Text.StartsWith("ActionTimeline："));
                Assert(grid.Columns.Contains("Key"), "切换表后不能显示旧请求的结果。");

                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExdBrowserForm.png"));
                }

                Context();
                Field<ListBox>(form, "listSheets").SelectedItem = "Item";
                form.Close();
                Assert(Field<CancellationToken>(form, "_token").IsCancellationRequested, "关闭窗口应取消读取。");

                using (var owner = new Form
                {
                    Opacity = 0,
                    ShowInTaskbar = false
                }

                )
                using (var action = new ActionListForm
                {
                    Opacity = 0,
                    ShowInTaskbar = false
                }

                )
                {
                    var panel = new Panel();
                    owner.Controls.Add(panel);
                    owner.Show();
                    action.Show(panel);
                    Application.DoEvents();
                    AssertCentered(action);
                    action.Close();
                }
            }
        }

        private static ExdResource Read(string name, string query = "", int page = 0) => ExdResource.Read(_path, Language.ChineseSimplified, name, query, page, CancellationToken.None);
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Context() => SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        private static void AssertCentered(Form form)
        {
            var area = Screen.FromControl(form).WorkingArea;
            Assert(Math.Abs(form.Left + form.Width / 2 - area.Left - area.Width / 2) <= 1 &&
                Math.Abs(form.Top + form.Height / 2 - area.Top - area.Height / 2) <= 1, "窗口最终尺寸应在屏幕工作区居中。");
        }

        private static void Await(Func<bool> ready)
        {
            var watch = Stopwatch.StartNew();

            while (!ready())
            {
                if (watch.Elapsed.TotalSeconds > 60)
                    throw new TimeoutException("等待窗口读取超时。");

                Application.DoEvents();
                Thread.Sleep(1);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}

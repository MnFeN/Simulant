using Lumina.Data;
using Simulant.Game.Data;
using Simulant.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Simulant.Tests.Native
{
    /// <summary>只读资源检索、复制内容与非模态窗体验证，不调用游戏或触发器。</summary>
    internal static class ActionResourceTests
    {
        private static string _gamePath;

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Control.CheckForIllegalCrossThreadCalls = true;
                _gamePath = GameDataService.ResolveSqPackPath(args.Single());

                foreach (var method in typeof(ActionResourceTests).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0))
                {
                    method.Invoke(null, null);
                    Console.WriteLine("通过：" + method.Name);
                }

                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        public static void FiltersPreserveSelectionsAndTreatEveryRowAsOld()
        {
            var rows = Samples();
            Assert(new ActionResourceFilter
            {
                NewOnly = true
            }.Apply(rows, CancellationToken.None).Length == 0, "新增项必须全部为 false。");
            Assert(new ActionResourceFilter
            {
                Shapes = new bool[18]
            }.Apply(rows, CancellationToken.None).Length == 2, "形状全不选表示不过滤。");
            Assert(new ActionResourceFilter
            {
                Shapes = Enumerable.Repeat(true, 18).ToArray()
            }.Apply(rows, CancellationToken.None).Length == 2, "形状全选表示不过滤。");
            var shapes = new bool[18];
            shapes[13] = true;
            Assert(new ActionResourceFilter
            {
                Shapes = shapes
            }.Apply(rows, CancellationToken.None).Single().Id == 20, "形状筛选必须使用 CastType。");
            Assert(new ActionResourceFilter
            {
                HexId = "^14$",
                Name = "扇",
                Omen = "FAN",
                AnimationEnd = "GIMMICK"
            }
                .Apply(rows, CancellationToken.None).Single().Id == 20, "各项正则应组合并忽略大小写。");
            Throws<ArgumentException>(() => new ActionResourceFilter
            {
                Name = "["
            }.Apply(rows, CancellationToken.None));
            Throws<Exception>(() => new ActionResourceFilter
            {
                Expression = "id + ("
            }.Apply(rows, CancellationToken.None));

            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Throws<OperationCanceledException>(() => new ActionResourceFilter().Apply(rows, canceled.Token));
            }
        }

        public static void ExpressionsAndSortingUseNumericValues()
        {
            var original = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var filter = new ActionResourceFilter
                {
                    Expression = "ID == 20 && t == 2.5 && min(X,Y) > 0 && vfxCount == 2 && isPlayer == 0"
                };
                Assert(filter.Apply(Samples(), CancellationToken.None).Single().Id == 20, "表达式应复用数学语法且不受系统小数符号影响。");
                Assert(new ActionResourceFilter
                {
                    Expression = "between(ID,15,25) && in(shape,3,13) && t ≈ 3"
                }
                    .Apply(Samples(), CancellationToken.None).Single().Id == 20, "技能筛选应支持同步后的范围、集合与近似比较语法。");
                Assert(new ActionResourceFilter
                {
                    SortColumn = 3,
                    SortAscending = false
                }.Apply(Samples(), CancellationToken.None)[0].Id == 20, "咏唱列应按数值排序。");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        public static void AuxiliaryLineCommandsKeepTwoCoordinatesInCommaCultures()
        {
            var method = typeof(GameDataService).Assembly.GetType("Simulant.Content.U7b.Presets.P5地火Logic", true)
                .GetMethod("AuxiliaryLinePictoACTCommand", BindingFlags.Static | BindingFlags.NonPublic);
            var original = CultureInfo.CurrentCulture;

            try
            {
                foreach (var culture in new[]
                {
                    "fr-FR", "de-DE"
                })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    for (var index = 1; index <= 6; index++)
                    {
                        foreach (var left in new[]
                        {
                            true, false
                        })
                        {
                            var command = (string)method.Invoke(null, new object[]
                            {
                                index, left
                            });
                            var position = command.Split('\n').Single(line => line.StartsWith("Pos: ")).Substring(5);
                            var coordinates = Simulant.Utils.ArgHelper.ParseArgs<double, double>(position);
                            Assert(position.Count(c => c == ',') == 1,
                                "辅助线必须只有两个坐标，小数不能产生额外的逗号。");
                            Assert(Math.Abs(coordinates.Item1 - (index - 3.5) * 5 * Math.Sqrt(2)) < 0.00001
                                && Math.Abs(coordinates.Item2 + 17.5 * Math.Sqrt(2)) < 0.00001,
                                "所有方向的辅助线坐标必须保持原值。");
                        }
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        public static void TemplateEscapesXmlAndSupportsMissingOmen()
        {
            var row = Samples()[0];
            row.Name = "A & B < C \"测试\"";
            var xml = XElement.Parse(ActionResource.ExportActions(new[]
            {
                row
            }));
            var value = xml.Descendants("Action").Single().Attribute("DictVariableValue").Value;
            Assert(value.Contains(row.Name), "复制的 XML 必须保留特殊字符且可解析。");
            row.Name = "_rsv";
            row.Omen = "";
            Assert(row.OmenCommand().StartsWith("_=_rsv"), "短名称不能触发 Substring 越界。");
            row.Shape = 11;
            Assert(row.OmenCommand().EndsWith("cross=1"), "十字预兆必须保留第二条方向。");
        }

        public static void VfxExtractionDeduplicatesSortsAndIgnoresBinaryBytes()
        {
            var bytes = Encoding.GetEncoding(28591).GetBytes("\u00ff\0vfx/z/eff/a.avfx\0VFX/z/eff/A.AVFX\0vfx/a/eff/b.avfx\0");
            var paths = ActionResource.ExtractVfxPaths(bytes);
            Assert(paths.SequenceEqual(new[]
            {
                "vfx/a/eff/b.avfx", "vfx/z/eff/a.avfx"
            }), "特效路径应忽略大小写去重并稳定排序。");
        }

        public static void ClientDataLoadsAndContainsGimmickReferences()
        {
            var watch = Stopwatch.StartNew();
            var rows = ActionResource.Load(_gamePath, Language.ChineseSimplified, CancellationToken.None, Console.WriteLine);
            Assert(rows.Length > 10000 && rows.Any(row => row.VfxPaths.Length != 0), "当前客户端应读到完整技能及时间轴特效。");
            Assert(rows.All(row => !row.IsNew), "真实客户端的新增项也必须统一为 false。");
            Assert(rows.Where(row => row.VfxPaths.Length != 0).All(row => row.AnimationEnd.IndexOf("gimmick", StringComparison.OrdinalIgnoreCase) >= 0), "只映射 Gimmick 时间轴。");
            Console.WriteLine($"读取 {rows.Length} 个技能，{rows.Count(row => row.VfxPaths.Length != 0)} 个含特效，耗时 {watch.Elapsed.TotalSeconds:0.00} 秒。");
        }

        public static void FormLoadsWithoutBlockingMessagesAndResetClearsPartialShapes()
        {
            using (var data = new GameDataService(_gamePath))
            using (var form = new ActionListForm())
            {
                typeof(GameDataService).GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
                var pulses = 0;

                using (var timer = new System.Windows.Forms.Timer
                {
                    Interval = 10
                }

                )
                {
                    timer.Tick += (_, __) => pulses++;
                    timer.Start();
                    form.Opacity = 0;
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-32000, -32000);
                    form.Show();
                    Await(() => Field<Label>(form, "lblResult").Text.StartsWith("当前"));
                    Assert(pulses > 0, "后台读取期间窗口消息必须仍能处理。");
                    var grid = Field<DataGridView>(form, "gridActions");
                    Assert(grid.RowCount > 10000 && grid.VirtualMode, "表格应采用虚拟模式显示实际技能。");
                    AssertVisibleBounds(Field<Button>(form, "btnSearch"));
                    AssertVisibleBounds(Field<TextBox>(form, "txtExpression"));
                    AssertVisibleBounds(Field<Label>(form, "lblResult"));
                    Field<CheckBox>(form, "chkShape13").Checked = true;
                    Invoke(form, "btnReset_Click", form, EventArgs.Empty);
                    Await(() => Field<Label>(form, "lblResult").Text.StartsWith("当前"));
                    Assert(!Field<CheckBox>(form, "chkShape13").Checked, "部分勾选时重置也必须清空形状。");
                    Field<CheckBox>(form, "chkNewOnly").Checked = true;
                    Invoke(form, "btnSearch_Click", form, EventArgs.Empty);
                    Await(() => Field<Label>(form, "lblResult").Text.StartsWith("当前"));
                    Assert(grid.RowCount == 0, "新增项勾选后应该显示零条。");
                    Invoke(form, "btnReset_Click", form, EventArgs.Empty);
                    Await(() => Field<Label>(form, "lblResult").Text.StartsWith("当前"));

                    using (var bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ActionListForm.png"));
                    }

                    form.Size = form.MinimumSize;
                    Application.DoEvents();
                    AssertVisibleBounds(Field<Button>(form, "btnSearch"));
                    AssertVisibleBounds(Field<TextBox>(form, "txtExpression"));
                    AssertVisibleBounds(Field<Label>(form, "lblResult"));
                }
            }
        }

        public static void ClosingDuringLoadCancelsWithoutLeavingTheFormAlive()
        {
            using (var data = new GameDataService(_gamePath))
            {
                typeof(GameDataService).GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
                var form = new ActionListForm
                {
                    Opacity = 0,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000)
                };
                form.Show();
                form.Close();
                Assert(form.IsDisposed && Field<CancellationToken>(form, "_lifetimeToken").IsCancellationRequested, "关闭窗口必须取消后台工作并释放窗体。");
                Application.DoEvents();
            }
        }

        private static ActionResource[] Samples() => new[]
        {
            new ActionResource
            {
                Id = 10,
                Name = "圆形",
                Shape = 2,
                X = 3,
                Y = 3
            },
            new ActionResource
            {
                Id = 20,
                Name = "扇形",
                Shape = 13,
                X = 4,
                Y = 4,
                CastTime = 2.5f,
                Omen = "gl_fan090_1bf",
                AnimationEnd = "mon_sp/gimmick/test",
                VfxPaths = new[]
                {
                    "a",
                    "b"
                }
            }
        };
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Invoke(object owner, string name, params object[] args)
        {
            // 离线入口没有 Application.Run，手动触发事件时需恢复界面上下文。
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        }

        private static void Await(Func<bool> ready)
        {
            var watch = Stopwatch.StartNew();

            while (!ready())
            {
                if (watch.Elapsed.TotalSeconds > 60)
                    throw new TimeoutException("等待窗口操作超时。");

                Application.DoEvents();
                Thread.Sleep(1);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertVisibleBounds(Control control)
        {
            for (var parent = control.Parent; parent != null; parent = parent.Parent)
            {
                var bounds = new Rectangle(parent.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(parent.ClientRectangle.Contains(bounds),
                    $"{control.Name} 被 {parent.Name} 的布局裁切：控件 {bounds}，容器 {parent.ClientRectangle}。");
            }
        }

        private static void Throws<T>(System.Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("预期错误未抛出：" + typeof(T).Name);
        }
    }
}

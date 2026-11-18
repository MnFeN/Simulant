using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Simulant.Tests
{
    internal static class PresetConfigurationTests
    {
        private enum Assignment
        {
            Left,
            Right,
            [RandomEnum(Left, Right)]
            Random
        }

        public static void AllOptionTypesRoundTripWithoutResolvingRandomOrUsingRegionalDecimals()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            var path = Path.Combine(Path.GetDirectoryName(typeof(PresetConfigurationTests).Assembly.Location),
                "simulant-options-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                using (var table = new TableLayoutPanel())
                {
                    var editor = new OptionEditor(table, null);
                    var options = Options();
                    editor.Build(options);
                    Assert(options.All(option => option.SerializeValue() == null), "各类默认值和说明不应写入配置。");
                    var values = new[]
                    {
                        "Random", "True", "7", "1.5", "", "C:\\游戏\\ffxiv_dx11.exe", null
                    };
                    for (var i = 0; i < values.Length - 1; i++)
                        options[i].RestoreValue(values[i]);

                    var config = new Configuration();
                    config.UpdatePreset("副本.预设", Capture(options));
                    Assert(config.Presets[0].Options.Count == 6, "纯说明不能保存，清空的文本也必须保存。");
                    config.Save(path);
                    var saved = Configuration.Load(path).Presets.Single();
                    Assert(saved.Options.Single(item => item.Name == "Radius").Value == "1.5", "小数必须与地区设置无关。");

                    // 新对象、不同下拉顺序，仍按枚举键恢复，不能持久化下标或随机结果。
                    options = Options(reverse: true);
                    editor.Build(options, option =>
                    {
                        var value = saved.Options.Find(item => item.Name == option.PropertyName);
                        if (value != null)
                            option.RestoreValue(value.Value);
                    });
                    for (var i = 0; i < values.Length; i++)
                        Assert(options[i].SerializeValue() == values[i], "选项恢复后必须保留原始选择，包括随机和空文本。");

                    options = Options();
                    editor.Build(options);
                    config.UpdatePreset("副本.预设", Capture(options));
                    config.Save(path);
                    Assert(!File.ReadAllText(path).Contains("<Presets"), "全部恢复默认后，XML 中也不应留空节点。");
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        public static void DefaultsAreRemovedAndPresetsAndDraftsRemainIndependent()
        {
            using (var table = new TableLayoutPanel())
            {
                var editor = new OptionEditor(table, null);
                var options = Options();
                editor.Build(options);
                var config = new Configuration();
                Assert(!config.UpdatePreset("一", Capture(options)) && !config.ShouldSerializePresets(), "全默认不能产生空配置节点。");
                options[1].RestoreValue("True");
                Assert(config.UpdatePreset("一", Capture(options)), "修改选项应产生记录。");
                Assert(!config.UpdatePreset("一", Capture(options)), "未变更的值不能反复写盘。");
                config.UpdatePreset("二", Capture(options));
                var copy = config.Copy();
                copy.Presets[0].Options[0].Value = "False";
                Assert(config.Presets[0].Options[0].Value == "True", "配置副本不能共用可变的预设记录。");
                options[1].RestoreValue("False");
                config.UpdatePreset("一", Capture(options));
                Assert(config.Presets.Count == 1 && config.Presets[0].Id == "二", "恢复默认只移除对应预设，不改变其他副本。");
                config.UpdatePreset("二", Capture(options));
                Assert(!config.ShouldSerializePresets(), "最后一个非默认项移除后不应留下预设节点。");
            }
        }

        public static void EditorOnlyNotifiesUserChangesAndRejectsObsoleteValues()
        {
            using (var table = new TableLayoutPanel())
            {
                var editor = new OptionEditor(table, null);
                var options = Options();
                var changes = 0;
                editor.ValueChanged += () => changes++;
                editor.Build(options, option =>
                {
                    if (option.PropertyName == "Enabled")
                        option.RestoreValue("True");
                });
                Assert(changes == 0, "初次创建和配置恢复不能触发自动保存。");
                ((CheckBox)options[1].Control).Checked = false;
                ((NumericTextBoxEx)options[2].Control).Value = 9;
                ((TextBox)options[5].Control.Controls[0]).Text = "新的路径";
                Assert(changes == 3, "普通控件和复合选项的子控件都应自动通知改动。");
                editor.Clear();
                Assert(changes == 3, "释放控件不能把销毁后的默认值保存回配置。");
                editor.Build(options);
                ((CheckBox)options[1].Control).Checked = true;
                Assert(changes == 4, "重建后不能累积订阅。");
                foreach (var pair in new[]
                {
                    (Index: 0, Value: "Removed"), (Index: 0, Value: "42"),
                    (Index: 3, Value: "NaN"), (Index: 3, Value: "200")
                })
                {
                    var rejected = false;
                    try
                    {
                        options[pair.Index].RestoreValue(pair.Value);
                    }
                    catch (Exception)
                    {
                        rejected = true;
                    }
                    Assert(rejected && options[pair.Index].SerializeValue() == null, "失效或越界的存档应拒绝，保留默认值。");
                }
            }
        }

        private static List<SimOptionBase> Options(bool reverse = false)
        {
            var choices = new Map<Assignment>();
            var values = new[]
            {
                Assignment.Left, Assignment.Right, Assignment.Random
            };
            foreach (var value in reverse ? values.Reverse() : values)
                choices[value] = value.ToString();
            return new List<SimOptionBase>
            {
                new ComboBoxOption<Assignment>("Assignment", "点名", Assignment.Left, choices),
                new CheckBoxOption("Enabled", "启用", false),
                new IntegerOption("Count", "人数", 1),
                new DecimalOption("Radius", "半径", 1),
                new TextBoxOption("Name", "名称", "原名"),
                new FilePathOption("Path", "路径", "", "客户端|*.exe"),
                new LabelOption("说明")
            };
        }

        private static IEnumerable<KeyValuePair<string, string>> Capture(IEnumerable<SimOptionBase> options)
            => options.Select(option => new KeyValuePair<string, string>(option.PropertyName, option.SerializeValue()));

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}

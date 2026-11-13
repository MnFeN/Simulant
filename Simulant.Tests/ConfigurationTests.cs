using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Simulant.Tests
{
    internal static class ConfigurationTests
    {
        public static void AutomaticUpdatesDefaultToEnabledAndPersist()
        {
            WithPath(path =>
            {
                Assert(Configuration.Load(path).AutoInstallUpdates, "新配置应默认自动安装更新。");
                new Configuration().Save(path);
                File.WriteAllText(path, "<Simulant />");
                var configuration = Configuration.Load(path);
                Assert(configuration.AutoInstallUpdates, "旧配置缺少更新选项时应默认开启。");
                configuration.AutoInstallUpdates = false;
                configuration.Copy().Save(path);
                Assert(!Configuration.Load(path).AutoInstallUpdates, "关闭自动更新后，编辑副本与保存读取都应保留设置。");
            });
        }

        public static void ConfigurationRoundTripsAndKeepsDefaultsForMissingOptions()
        {
            WithPath(path =>
            {
                var config = Configuration.Load(path);
                Assert(config.SimulatedParty.AttackTarget && config.SimulatedParty.DelayArrival &&
                    config.SimulatedParty.IdleRadius == 3 && config.SimulatedParty.RouteAngleDegrees == 30,
                    "没有配置文件时应默认攻击、延迟到达、空闲半径三米、绕路角度三十度。");
                config.SimulatedParty.AttackTarget = false;
                config.SimulatedParty.DelayArrival = false;
                config.SimulatedParty.IdleRadius = 1.3;
                config.SimulatedParty.RouteAngleDegrees = 27;
                var culture = Thread.CurrentThread.CurrentCulture;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    config.Save(path);
                }
                finally { Thread.CurrentThread.CurrentCulture = culture; }
                var restored = Configuration.Load(path).SimulatedParty;
                Assert(!restored.AttackTarget && !restored.DelayArrival && restored.IdleRadius == 1.3 && restored.RouteAngleDegrees == 27,
                    "所有模拟队员设置应跨区域格式完整保存和读取。");
                config.SimulatedParty.AttackTarget = true;
                config.Save(path);
                Assert(Configuration.Load(path).SimulatedParty.AttackTarget, "已有文件应能被新配置完整替换。");
                File.WriteAllText(path, "<Simulant><SimulatedParty><Enabled>false</Enabled><VaryArrival>false</VaryArrival>" +
                    "<RouteWidth>0.8</RouteWidth><AttackTarget>false</AttackTarget><IdleRadius>0.6</IdleRadius></SimulatedParty></Simulant>");
                restored = Configuration.Load(path).SimulatedParty;
                Assert(!restored.AttackTarget && restored.IdleRadius == 0.6 && restored.DelayArrival && restored.RouteAngleDegrees == 30,
                    "旧配置应保留仍有效的设置，新选项使用默认值，不能把旧绕路米数当成角度。");
                new Configuration { SimulatedParty = restored }.Save(path);
                var saved = new System.Xml.XmlDocument();
                saved.Load(path);
                Assert(saved.SelectSingleNode("/Simulant/SimulatedParty").ChildNodes.Count == 4 &&
                    saved.SelectSingleNode("//Enabled|//VaryArrival|//RouteWidth") == null,
                    "再次保存后只应留下四项配置，不能继续写入已删除的选项。");
            });
        }

        public static void InvalidConfigurationAndFailedSavesPreserveTheOriginalFile()
        {
            WithPath(path =>
            {
                var config = new Configuration();
                config.Save(path);
                var original = File.ReadAllText(path);
                config.SimulatedParty.RouteAngleDegrees = double.NaN;
                Reject(() => config.Save(path));
                Assert(File.ReadAllText(path) == original, "无效设置不得破坏已保存的配置。");
                config.SimulatedParty.RouteAngleDegrees = 30;
                using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    Reject(() => config.Save(path));
                Assert(File.ReadAllText(path) == original && Directory.GetFiles(Path.GetDirectoryName(path)).Length == 1,
                    "替换文件失败时应保留旧文件并回收临时文件。");
                File.WriteAllText(path, "<Simulant><SimulatedParty><IdleRadius>-1</IdleRadius></SimulatedParty></Simulant>");
                Reject(() => Configuration.Load(path));
                var invalid = "<Simulant><SimulatedParty>";
                File.WriteAllText(path, invalid);
                Reject(() => Configuration.Load(path));
                Assert(File.ReadAllText(path) == invalid, "读取损坏的配置时不得覆盖原文件。");
            });
        }

        private static void WithPath(Action<string> action)
        {
            var root = Path.Combine(Path.GetDirectoryName(typeof(ConfigurationTests).Assembly.Location), "config-test-" + Guid.NewGuid().ToString("N"));
            var directory = Path.Combine(root, "Config");
            try { action(Path.Combine(directory, "Simulant.config.xml")); }
            finally
            {
                if (Directory.Exists(directory))
                {
                    foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                    Directory.Delete(directory);
                }
                if (Directory.Exists(root)) Directory.Delete(root);
            }
        }

        private static void Reject(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            catch (IOException) { return; }
            throw new Exception("无效配置或不可写文件应明确报告失败。");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}

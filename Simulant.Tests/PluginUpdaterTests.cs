using Simulant.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Tests
{
    internal static class PluginUpdaterTests
    {
        public static void ReleaseOrderAndVersionNumbersAreRespected()
        {
            var tag = PluginUpdater.ParseReleaseTag(Page("v0.2.0.0") + Page("v9.0.0.0"));
            Assert(tag == "v0.2.0.0", "应使用发布页首个版本，不能取数值最大或其他版本。");
            Assert(PluginUpdater.ParseVersion("\uFEFF 0.1.0.0\r\n") == new Version(0, 1, 0, 0), "备用版本文本应接受换行与 BOM。");
            Assert(PluginUpdater.ParseVersion("v0.2") == new Version(0, 2, 0, 0), "省略的版本段应补零。");
            Assert(PluginUpdater.ParseVersion("0.10.0.0") > PluginUpdater.ParseVersion("0.9.0.0"), "版本必须按数值比较。");
        }

        public static void DisabledAutomaticInstallationOnlyReportsNewVersion()
        {
            WithUpdater(false, (url, token) => Text(Page("v0.2.0.0")), (updater, warnings, requests, path) =>
            {
                updater.CheckAsync().GetAwaiter().GetResult();
                Assert(requests.SequenceEqual(new[] { PluginUpdater.ReleasesUrl }), "正常检查不能访问备用源或下载 DLL。");
                Assert(warnings.Single() == "当前版本 0.1.0.0，检查到新版本 0.2.0.0，请在左侧菜单中下载更新（数据源：GitHub）", "关闭自动安装时应使用指定提示并注明来源。");
                Assert(File.ReadAllText(path) == "原插件", "仅检查时不能修改插件。");
            });
        }

        public static void FailedGithubCheckFallsBackToVersionText()
        {
            WithUpdater(false, (url, token) => url == PluginUpdater.ReleasesUrl ? FailedResponse() : Text("0.2.0.0\r\n"),
                (updater, warnings, requests, path) =>
                {
                    updater.CheckAsync().GetAwaiter().GetResult();
                    Assert(requests.SequenceEqual(new[] { PluginUpdater.ReleasesUrl, PluginUpdater.FallbackVersionUrl }), "应按 GitHub、网盘的顺序回退。");
                    Assert(warnings.Single().Contains("检查到新版本 0.2.0.0") && warnings.Single().EndsWith("（数据源：123 网盘）"), "网盘检查成功后应提示新版本并注明实际来源。");
                });
        }

        public static void InvalidVersionResponsesReportCheckFailure()
        {
            WithUpdater(true, (url, token) => Text("<html>服务暂不可用</html>"), (updater, warnings, requests, path) =>
            {
                updater.CheckAsync().GetAwaiter().GetResult();
                Assert(warnings.Single() == "从 GitHub 及 123 网盘尝试获取更新均失败。（数据源：GitHub、123 网盘）", "无效网页不能当作有效版本。");
                Assert(requests.Count == 2 && File.ReadAllText(path) == "原插件", "检查失败不能尝试安装。");
            });
        }

        public static void FallbackCannotInstallAnOlderGithubRelease()
        {
            var githubCalls = 0;
            WithUpdater(true, (url, token) => url == PluginUpdater.FallbackVersionUrl ? Text("0.3.0.0") :
                ++githubCalls == 1 ? FailedResponse() : Text(Page("v0.2.0.0")), (updater, warnings, requests, path) =>
            {
                updater.CheckAsync().GetAwaiter().GetResult();
                Assert(warnings.Single() == "当前版本 0.1.0.0，检查到新版本 0.3.0.0，但访问 GitHub 下载失败，请在左侧菜单中手动安装更新（数据源：123 网盘）", "安装不可用时应保留已发现的新版本及其来源。");
                Assert(requests.Count == 3 && File.ReadAllText(path) == "原插件", "不能把旧版当作网盘提示的新版安装。");
            });
        }

        public static void InvalidDllDoesNotOverwriteOriginalOrLeaveTemporaryFiles()
        {
            WithUpdater(true, (url, token) => Text(url == PluginUpdater.ReleasesUrl ? Page("v0.2.0.0") : "下载错误页面"),
                (updater, warnings, requests, path) =>
                {
                    updater.CheckAsync().GetAwaiter().GetResult();
                    Assert(warnings.Single().Contains("访问 GitHub 下载失败"), "非 DLL 内容应明确报告安装失败。");
                    Assert(File.ReadAllText(path) == "原插件" && Directory.GetFiles(Path.GetDirectoryName(path)).Length == 1,
                        "下载校验失败时应保留原插件并删除临时文件。");
                });
        }

        public static void ValidDllIsReplacedOnceAndRequestsRestart()
        {
            WithAssembly(new Version(0, 2, 0, 0), bytes =>
                WithUpdater(true, (url, token) => url == PluginUpdater.ReleasesUrl ? Text(Page("v0.2.0.0")) : Binary(bytes),
                    (updater, warnings, requests, path) =>
                    {
                        updater.CheckAsync().GetAwaiter().GetResult();
                        updater.CheckAsync().GetAwaiter().GetResult();
                        Assert(File.ReadAllBytes(path).SequenceEqual(bytes), "应替换 ACT 原始路径中的 DLL。");
                        Assert(requests.Count(url => url.Contains("/download/")) == 1, "等待重启期间不应重复下载。");
                        Assert(warnings.Count == 2 && warnings.All(text => text == "已从 0.1.0.0 版本更新至 0.2.0.0 版本，请重启 ACT（数据源：GitHub）"), "安装完成后应提示重启 ACT 并注明来源。");
                        Assert(Directory.GetFiles(Path.GetDirectoryName(path)).Length == 1, "安装成功不应保留备份或临时文件。");
                    }));
        }

        public static void DllVersionMismatchPreservesOriginal()
        {
            WithAssembly(new Version(0, 1, 0, 0), bytes =>
                WithUpdater(true, (url, token) => url == PluginUpdater.ReleasesUrl ? Text(Page("v0.2.0.0")) : Binary(bytes),
                    (updater, warnings, requests, path) =>
                    {
                        updater.CheckAsync().GetAwaiter().GetResult();
                        Assert(warnings.Single().Contains("访问 GitHub 下载失败") && File.ReadAllText(path) == "原插件",
                            "发布附件版本不符时不能覆盖原 DLL。");
                    }));
        }

        public static void ManualInstallIgnoresAutomaticInstallationSetting()
        {
            WithAssembly(new Version(0, 2, 0, 0), bytes =>
                WithUpdater(false, (url, token) => url == PluginUpdater.ReleasesUrl ? Text(Page("v0.2.0.0")) : Binary(bytes),
                    (updater, warnings, requests, path) =>
                    {
                        updater.InstallAsync().GetAwaiter().GetResult();
                        Assert(requests.Count == 2 && !requests.Contains(PluginUpdater.FallbackVersionUrl) &&
                            File.ReadAllBytes(path).SequenceEqual(bytes), "手动安装应直接使用 GitHub，不受自动安装开关影响。");
                    }));
        }

        public static void CurrentOrOlderReleaseIsNotDownloaded()
        {
            foreach (var version in new[] { "v0.1.0.0", "v0.0.4.2" })
            foreach (var fallback in new[] { false, true })
            {
                var logs = new List<string>();
                WithUpdater(true, (url, token) => url == PluginUpdater.FallbackVersionUrl ? Text(version) :
                    fallback ? FailedResponse() : Text(Page(version)), (updater, warnings, requests, path) =>
                {
                    updater.CheckAsync().GetAwaiter().GetResult();
                    Assert(requests.Count == (fallback ? 2 : 1) && warnings.Count == 0 && File.ReadAllText(path) == "原插件", "相同或更旧的版本不应自动安装。");
                    Assert(logs.Single() == "当前版本 0.1.0.0，暂无更新。（数据源：" + (fallback ? "123 网盘" : "GitHub") + "）",
                        "暂无更新时应注明实际使用的数据源。");
                }, logs.Add);
            }
        }

        public static void UnloadingCancelsRequestsAndRejectsConcurrentUpdates()
        {
            WithUpdater(true, async (url, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, (updater, warnings, requests, path) =>
            {
                var pending = updater.CheckAsync();
                updater.InstallAsync().GetAwaiter().GetResult();
                Assert(requests.Count == 1, "已有检查运行时不能并发开始另一次安装。");
                updater.Dispose();
                Assert(pending.Wait(3000), "卸载应取消尚未完成的网络请求。");
                updater.CheckAsync().GetAwaiter().GetResult();
                Assert(requests.Count == 1 && warnings.Count == 0 && File.ReadAllText(path) == "原插件", "卸载后不能回退网盘、写入 DLL 或报告失败。");
            });
        }

        private static string Page(string tag) => "<a href=\"/MnFeN/Simulant/releases/tag/" + tag + "\">发布版本</a>";
        private static Task<HttpResponseMessage> Text(string value) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value) });
        private static Task<HttpResponseMessage> Binary(byte[] value) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(value) });
        private static Task<HttpResponseMessage> FailedResponse() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        private static void WithUpdater(bool autoInstall, Func<string, CancellationToken, Task<HttpResponseMessage>> respond,
            Action<PluginUpdater, List<string>, List<string>, string> test, Action<string> runtime = null)
        {
            var root = Path.Combine(Path.GetDirectoryName(typeof(PluginUpdaterTests).Assembly.Location), "update-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "Simulant.dll");
            File.WriteAllText(path, "原插件");
            var warnings = new List<string>();
            var requests = new List<string>();
            try
            {
                using (var updater = new PluginUpdater(() => path, new Version(0, 1, 0, 0), () => autoInstall, warnings.Add, runtime ?? (_ => { }),
                    new ResponseHandler((url, token) => { requests.Add(url); return respond(url, token); })))
                    test(updater, warnings, requests, path);
            }
            finally
            {
                foreach (var file in Directory.GetFiles(root)) File.Delete(file);
                Directory.Delete(root);
            }
        }

        private static void WithAssembly(Version version, Action<byte[]> test)
        {
            var root = Path.Combine(Path.GetDirectoryName(typeof(PluginUpdaterTests).Assembly.Location), "update-dll-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Simulant") { Version = version }, AssemblyBuilderAccess.Save, root);
                assembly.DefineDynamicModule("Simulant", "Simulant.dll").DefineType("模拟插件").CreateType();
                assembly.Save("Simulant.dll");
                test(File.ReadAllBytes(Path.Combine(root, "Simulant.dll")));
            }
            finally
            {
                foreach (var file in Directory.GetFiles(root)) File.Delete(file);
                Directory.Delete(root);
            }
        }

        private sealed class ResponseHandler : HttpMessageHandler
        {
            private readonly Func<string, CancellationToken, Task<HttpResponseMessage>> _respond;
            internal ResponseHandler(Func<string, CancellationToken, Task<HttpResponseMessage>> respond) { _respond = respond; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
                => _respond(request.RequestUri.AbsoluteUri, token);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}

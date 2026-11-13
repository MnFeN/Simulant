using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Core
{
    /// <summary>启动检查和菜单共用的更新入口；检查可回退网盘，安装始终使用 GitHub。</summary>
    internal sealed class PluginUpdater : IDisposable
    {
        internal const string ReleasesUrl = "https://github.com/MnFeN/Simulant/releases";
        internal const string FallbackVersionUrl = "https://1824544011.cdn.123clouddisk.com/1824544011/Simulant/version.txt";
        private readonly Func<string> _pluginPath;
        private readonly Version _currentVersion;
        private readonly Func<bool> _autoInstall;
        private readonly Action<string> _warning;
        private readonly Action<string> _runtime;
        private readonly HttpClient _http;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly object _gate = new object();
        private bool _running;
        private bool _disposed;
        private Version _installedVersion;

        internal PluginUpdater(Func<string> pluginPath, Version currentVersion, Func<bool> autoInstall,
            Action<string> warning, Action<string> runtime, HttpMessageHandler handler = null)
        {
            _pluginPath = pluginPath;
            _currentVersion = currentVersion;
            _autoInstall = autoInstall;
            _warning = warning;
            _runtime = runtime;
            _http = new HttpClient(handler ?? new HttpClientHandler { SslProtocols = System.Security.Authentication.SslProtocols.Tls12 });
            _http.Timeout = Timeout.InfiniteTimeSpan;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Simulant/" + currentVersion);
            _http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        }

        internal Task CheckAsync() => RunAsync(false);
        internal Task InstallAsync() => RunAsync(true);

        private async Task RunAsync(bool install)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (_running)
                {
                    _runtime("正在检查或安装更新，请稍候。");
                    return;
                }
                _running = true;
            }

            Version available = null;
            try
            {
                // 在调用线程取得 ACT 登记的原始路径；从字节加载的程序集没有 Location。
                var path = _pluginPath();
                string tag = null;
                try
                {
                    tag = ParseReleaseTag(await GetTextAsync(ReleasesUrl).ConfigureAwait(false));
                    available = ParseVersion(Uri.UnescapeDataString(tag));
                }
                catch (Exception) when (!_stop.IsCancellationRequested && !install)
                {
                    available = ParseVersion(await GetTextAsync(FallbackVersionUrl).ConfigureAwait(false));
                }

                _stop.Token.ThrowIfCancellationRequested();
                if (_installedVersion != null && available <= _installedVersion)
                {
                    _warning($"已从 {_currentVersion} 版本更新至 {_installedVersion} 版本，请重启 ACT");
                    return;
                }
                if (available < _currentVersion || (!install && available == _currentVersion))
                {
                    _runtime($"当前版本 {_currentVersion}，暂无更新。");
                    return;
                }
                if (!install && !_autoInstall())
                {
                    _warning($"当前版本 {_currentVersion}，检查到新版本 {available}，请在左侧菜单中下载更新");
                    return;
                }

                // 网盘仅提供版本号；安装前重新读取真实标签，避免猜测 v 前缀或安装旧版。
                if (tag == null)
                {
                    tag = ParseReleaseTag(await GetTextAsync(ReleasesUrl).ConfigureAwait(false));
                    var githubVersion = ParseVersion(Uri.UnescapeDataString(tag));
                    if (githubVersion < available) throw new InvalidDataException("GitHub 尚未提供检查到的新版本。");
                    available = githubVersion;
                }
                await DownloadAndReplaceAsync(tag, available, path).ConfigureAwait(false);
                _warning($"已从 {_currentVersion} 版本更新至 {available} 版本，请重启 ACT");
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (_stop.IsCancellationRequested) return;
                if (available != null)
                    _warning($"当前版本 {_currentVersion}，检查到新版本 {available}，但访问 GitHub 下载失败，请在左侧菜单中手动安装更新");
                else if (install)
                    _warning("访问 GitHub 安装更新失败，请稍后重试，或在左侧菜单中打开发布页面。");
                else
                    _warning("从 GitHub 及 123 网盘尝试获取更新均失败。");
                _runtime("更新失败原因：" + error.Message);
            }
            finally
            {
                lock (_gate)
                {
                    _running = false;
                    if (_disposed) ReleaseResources();
                }
            }
        }

        private async Task<string> GetTextAsync(string url)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using (var response = await _http.GetAsync(url, timeout.Token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task DownloadAndReplaceAsync(string tag, Version version, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new IOException("未找到 ACT 登记的插件文件，无法确定更新位置。");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(3));
                    var url = ReleasesUrl + "/download/" + tag + "/Simulant.dll";
                    using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                            await input.CopyToAsync(output, 81920, timeout.Token).ConfigureAwait(false);
                        if (response.Content.Headers.ContentLength.HasValue &&
                            new FileInfo(temporary).Length != response.Content.Headers.ContentLength.Value)
                            throw new InvalidDataException("下载的 DLL 长度不完整。");
                    }
                }

                var assembly = AssemblyName.GetAssemblyName(temporary);
                if (assembly.Name != "Simulant" || assembly.Version != version)
                    throw new InvalidDataException("下载的 DLL 名称或版本与发布版本不符。");
                lock (_gate)
                {
                    _stop.Token.ThrowIfCancellationRequested();
                    // 同目录完整替换，不创建备份；失败时原文件仍可使用。
                    File.Replace(temporary, path, null);
                    _installedVersion = version;
                }
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        internal static string ParseReleaseTag(string html)
        {
            var match = Regex.Match(html, "href=[\"'](?:https://github\\.com)?/MnFeN/Simulant/releases/tag/([^\"'#?]+)[\"']",
                RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (!match.Success) throw new InvalidDataException("GitHub 发布页面没有可识别的版本。");
            var tag = WebUtility.HtmlDecode(match.Groups[1].Value);
            ParseVersion(Uri.UnescapeDataString(tag));
            return tag;
        }

        internal static Version ParseVersion(string text)
        {
            var value = text.Trim().TrimStart('\uFEFF').Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            if (!Version.TryParse(value, out var version)) throw new InvalidDataException("更新版本号格式无效。");
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _stop.Cancel();
                _disposed = true;
                if (!_running) ReleaseResources();
            }
        }

        private void ReleaseResources()
        {
            _http.Dispose();
            _stop.Dispose();
        }
    }
}

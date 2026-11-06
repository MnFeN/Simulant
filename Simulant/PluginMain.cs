using System.Windows.Forms;
using Advanced_Combat_Tracker;
using Simulant.Core;

namespace Simulant
{
    public class PluginMain : IActPluginV1
    {
        private PluginHost _host;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            DeInitPlugin();
            var host = new PluginHost(pluginScreenSpace, pluginStatusText);
            _host = host;
            PluginHost.Instance = host;
        }

        public void DeInitPlugin()
        {
            var host = _host ?? PluginHost.Instance;
            if (host == null) return;
            // Dispose 失败时两处引用都保留，下一次启用前先重试旧宿主。
            host.Dispose();
            if (ReferenceEquals(PluginHost.Instance, host)) PluginHost.Instance = null;
            _host = null;
        }
    }
}

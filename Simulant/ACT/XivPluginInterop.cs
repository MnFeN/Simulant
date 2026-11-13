using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Simulant.ACT
{
    internal static class XivPluginInterop
    {
        /// <summary>
        /// 解析插件实例
        /// </summary>
        internal static FFXIV_ACT_Plugin.FFXIV_ACT_Plugin plugin;

        /// <summary>暂停解析插件的自动断流提示，返回恢复原阈值的操作；不修改收包时间戳或失败计数。</summary>
        internal static Action SuspendNetworkTimeout()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var current = plugin;
            var collection = current?.GetType().GetField("_dataCollection", flags)?.GetValue(current);
            var monitor = collection?.GetType().GetField("_monitor", flags)?.GetValue(collection);
            var timeout = monitor?.GetType().GetField("<Timeout>k__BackingField", flags);
            if (timeout?.FieldType != typeof(int))
                throw new InvalidOperationException("未找到解析插件的收包计时器阈值。");

            var original = (int)timeout.GetValue(monitor);
            timeout.SetValue(monitor, int.MaxValue);
            // 捕获原实例，避免插件重新绑定后把旧阈值写入新实例。
            return () => timeout.SetValue(monitor, original);
        }

        /// <summary>
        /// 获取FFXIV解析插件的区域信息。
        /// </summary>
        /// <param name="ffxiv_plugin"></param>
        /// <returns></returns>
        public static string GetRegion(FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxiv_plugin)
        {
            switch (GetLanguageId(ffxiv_plugin))
            {
                case 1:
                case 2:
                case 3:
                case 4:
                    return "intl";
                case 5:
                    return "cn";
                case 6:
                    return "ko";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 直接获取原始的FFXIV解析插件语言ID。
        /// </summary>
        /// <param name="ffxiv_plugin"></param>
        /// <returns></returns>
        public static int GetLanguageId(FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxiv_plugin)
        {
            if (ffxiv_plugin == null)
            {
                if (plugin == null) return 0;
                ffxiv_plugin = plugin;
            }
            try
            {
                return (int)ffxiv_plugin.DataRepository.GetSelectedLanguageID();
            }
            catch
            {
                return 0;
            }
        }
    }
}

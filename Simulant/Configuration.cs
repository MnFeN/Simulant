using Simulant.Simulation.Runtime;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Simulant
{
    /// <summary>插件全局配置；窗口编辑副本，保存成功后才替换当前配置。</summary>
    [XmlRoot("Simulant")]
    public sealed class Configuration
    {
        public SimActorBehaviorOptions SimulatedParty { get; set; } = new SimActorBehaviorOptions();

        internal Configuration Copy()
        {
            if (SimulatedParty == null) throw new InvalidOperationException("全局配置缺少模拟队员设置。");
            return new Configuration { SimulatedParty = SimulatedParty.Copy() };
        }

        internal static Configuration Load(string path)
        {
            if (!File.Exists(path)) return new Configuration();
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return ((Configuration)new XmlSerializer(typeof(Configuration)).Deserialize(reader)).Copy();
        }

        internal void Save(string path)
        {
            var snapshot = Copy();
            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                    new XmlSerializer(typeof(Configuration)).Serialize(writer, snapshot);
                // 完整写入临时文件后再替换，保存失败时保留上一份有效配置。
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}

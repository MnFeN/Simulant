using Simulant.Core.Environment;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Simulant
{
    /// <summary>插件全局配置；窗口编辑副本，保存成功后才替换当前配置。</summary>
    [XmlRoot("Simulant")]
    public sealed class Configuration
    {
        public SimActorBehaviorOptions SimulatedParty { get; set; } = new SimActorBehaviorOptions();
        public bool AutoInstallUpdates { get; set; } = true;
        public bool QuickControlsEnabled { get; set; } = true;
        public Point? QuickControlsLocation { get; set; }
        public bool ShouldSerializeQuickControlsLocation() => QuickControlsLocation.HasValue;
        public int SelectedTerritoryId { get; set; }
        public bool ShouldSerializeSelectedTerritoryId() => SelectedTerritoryId > 0;
        public string SelectedPhaseKey { get; set; }
        public double SimLatency { get; set; } = 0.35;
        public bool GenerateActLogs { get; set; }
        public bool DeveloperMode { get; set; }
        public string FfxivPath { get; set; } = string.Empty;

        public StudioSettings Studio { get; set; } = StudioSettings.Default;

        public bool ShouldSerializeStudio() => !Studio.Equals(StudioSettings.Default);

        [XmlArrayItem("Preset")]
        public List<PresetConfiguration> Presets { get; set; } = new List<PresetConfiguration>();

        public bool ShouldSerializePresets() => Presets.Count != 0;

        internal Configuration Copy()
        {
            if (SimulatedParty == null)
                throw new InvalidOperationException("全局配置缺少模拟队员设置。");

            if (double.IsNaN(SimLatency) || SimLatency < 0 || SimLatency > 1)
                throw new InvalidOperationException("模拟延迟必须在 0 到 1 秒之间。");

            return new Configuration
            {
                SimulatedParty = SimulatedParty.Copy(),
                AutoInstallUpdates = AutoInstallUpdates,
                QuickControlsEnabled = QuickControlsEnabled,
                QuickControlsLocation = QuickControlsLocation,
                SelectedTerritoryId = SelectedTerritoryId,
                SelectedPhaseKey = SelectedPhaseKey,
                SimLatency = SimLatency,
                GenerateActLogs = GenerateActLogs,
                DeveloperMode = DeveloperMode,
                FfxivPath = FfxivPath?.Trim() ?? string.Empty,
                Studio = Studio.WithClampedSize(),
                Presets = (Presets ?? new List<PresetConfiguration>()).Where(preset => preset.Options.Count != 0)
                    .Select(preset => new PresetConfiguration
                    {
                        Id = preset.Id,
                        Options = preset.Options.Select(option => new PresetOptionConfiguration
                        {
                            Name = option.Name,
                            Value = option.Value
                        }).ToList()
                    }).ToList()
            };
        }

        /// <summary>只记录非默认选项；全部恢复默认时连同预设节点一起移除。</summary>
        internal bool UpdatePreset(string id, IEnumerable<KeyValuePair<string, string>> values)
        {
            var options = values.Where(value => !string.IsNullOrEmpty(value.Key) && value.Value != null)
                .Select(value => new PresetOptionConfiguration
                {
                    Name = value.Key,
                    Value = value.Value
                }).ToList();
            var previous = Presets.Find(preset => preset.Id == id);
            if (options.Count == (previous?.Options.Count ?? 0) && (previous == null ||
                options.Zip(previous.Options, (a, b) => a.Name == b.Name && a.Value == b.Value).All(equal => equal)))
                return false;

            Presets.RemoveAll(preset => preset.Id == id);
            if (options.Count != 0)
                Presets.Add(new PresetConfiguration
                {
                    Id = id,
                    Options = options
                });
            return true;
        }

        internal static Configuration Load(string path)
        {
            if (!File.Exists(path))
                return new Configuration();

            using (var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            }))
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
                using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings
                {
                    Indent = true
                }))
                    new XmlSerializer(typeof(Configuration)).Serialize(writer, snapshot);

                // 完整写入临时文件后再替换，保存失败时保留上一份有效配置。
                if (File.Exists(fullPath))
                    File.Replace(temporary, fullPath, null);
                else
                    File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }

    public sealed class PresetConfiguration
    {
        [XmlAttribute]
        public string Id { get; set; }

        [XmlElement("Option")]
        public List<PresetOptionConfiguration> Options { get; set; } = new List<PresetOptionConfiguration>();
    }

    public sealed class PresetOptionConfiguration
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlText]
        public string Value { get; set; } = string.Empty;
    }
}

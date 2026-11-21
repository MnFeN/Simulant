using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Simulant.Game.Data
{
    internal readonly struct ActionTimelineModel
    {
        private const string SkeletonToken = "[SKL_ID]";
        private static readonly Regex FixedSkeletonPath = new Regex(@"^(?:mon_sp|human_sp)/([cmd][0-9]{4})/.+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal uint BaseId { get; }
        internal uint NameId { get; }
        internal string Skeleton { get; }
        internal string Name { get; }
        internal bool Recorded { get; }

        private ActionTimelineModel(uint baseId, uint nameId, string skeleton, string name, bool recorded)
        {
            BaseId = baseId;
            NameId = nameId;
            Skeleton = skeleton;
            Name = name;
            Recorded = recorded;
        }

        public override string ToString() => (Recorded ? "[实战施法] " : "") +
            $"{(string.IsNullOrEmpty(Name) ? "未记录名称" : Name)} · BNpcBase {BaseId} · {Skeleton}";

        internal static bool IsModelTimeline(string key)
            => IsSkeletonTemplate(key) || GetFixedSkeleton(key) != null;

        internal static bool IsSkeletonTemplate(string key) => key?.IndexOf(SkeletonToken, StringComparison.Ordinal) >= 0;

        internal static bool IsSharedTimeline(string key) => key?.StartsWith("mon_sp/common/", StringComparison.OrdinalIgnoreCase) == true ||
            key?.StartsWith("mon_sp/gimmick/", StringComparison.OrdinalIgnoreCase) == true;

        internal static bool IsPlayerTimeline(string key) => key.StartsWith("ws/", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("magic/", StringComparison.OrdinalIgnoreCase) || key.StartsWith("ability/", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("limitbreak/", StringComparison.OrdinalIgnoreCase);

        internal static string GetName(ExcelSheet<BNpcName> names, uint nameId)
            => nameId != 0 && names.TryGetRow(nameId, out var row) ? row.Singular.ExtractText() : "";

        internal static ActionTimelineModel? FindCompatible(GameDataService data, string key,
            (uint BaseId, uint NameId) caster, bool recorded)
        {
            if (!(IsModelTimeline(key) || IsSharedTimeline(key)) || !data.GetSheet<BNpcBase>().TryGetRow(caster.BaseId, out var npc) ||
                !(npc.ModelChara.ValueNullable is ModelChara appearance))
                return null;

            var skeleton = GetSkeleton(appearance);
            var fixedSkeleton = GetFixedSkeleton(key);
            return skeleton != null && (fixedSkeleton == null || string.Equals(skeleton, fixedSkeleton, StringComparison.OrdinalIgnoreCase)) &&
                HasTimeline(data, key, skeleton)
                ? new ActionTimelineModel(caster.BaseId, caster.NameId, skeleton, GetName(data.GetSheet<BNpcName>(), caster.NameId), recorded)
                : (ActionTimelineModel?)null;
        }

        internal static ActionTimelineModel[] Find(GameDataService data, string key,
            uint[] casters, CancellationToken cancellation)
        {
            if (!(IsModelTimeline(key) || IsSharedTimeline(key)))
                return Array.Empty<ActionTimelineModel>();

            var fixedSkeleton = GetFixedSkeleton(key);
            var template = IsSkeletonTemplate(key);
            if (!template && !HasTimeline(data, key, fixedSkeleton ?? ""))
                return Array.Empty<ActionTimelineModel>();

            var skeletons = data.GetSheet<ModelChara>().Select(model => (model.RowId, Skeleton: GetSkeleton(model)))
                .Where(model => model.Skeleton != null && (fixedSkeleton == null ||
                    string.Equals(fixedSkeleton, model.Skeleton, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(model => model.RowId, model => model.Skeleton);
            var npcNames = data.GetSheet<BNpcName>();
            var candidates = new List<ActionTimelineModel>();
            var checkedSkeletons = new Dictionary<string, bool>();
            var recorded = new HashSet<uint>(casters);

            foreach (var npc in data.GetSheet<BNpcBase>())
            {
                cancellation.ThrowIfCancellationRequested();

                if (npc.RowId == 0 || !skeletons.TryGetValue(npc.ModelChara.RowId, out var skeleton))
                    continue;

                // 同骨架共用 TMB 路径，只查一次文件。每个 BaseId 都保留，以便选择具体外观。
                if (template)
                {
                    if (!checkedSkeletons.TryGetValue(skeleton, out var compatible))
                        checkedSkeletons.Add(skeleton, compatible = HasTimeline(data, key, skeleton));
                    if (!compatible)
                        continue;
                }

                var nameId = BNpcNameIndex.GetNameId(npc.RowId);
                candidates.Add(new ActionTimelineModel(npc.RowId, nameId, skeleton, GetName(npcNames, nameId),
                    recorded.Contains(npc.RowId)));
            }

            return candidates.OrderByDescending(model => model.Recorded).ThenBy(model => model.BaseId).ToArray();
        }

        private static string GetSkeleton(ModelChara model) => model.Model != 0 && model.Type >= 1 && model.Type <= 3
            ? " cdm"[model.Type] + model.Model.ToString("D4", CultureInfo.InvariantCulture)
            : null;

        internal static string GetFixedSkeleton(string key)
        {
            var match = FixedSkeletonPath.Match(key ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static bool HasTimeline(GameDataService data, string key, string skeleton)
            => data.FileExists("chara/action/" + key.Replace(SkeletonToken, skeleton) + ".tmb");
    }
}

using Simulant.ACT;
using System.Globalization;
using System.Text;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Simulation.Runtime
{
    /// <summary>只负责绘制和移除预兆；延迟绘制由读条控制器安排。</summary>
    internal sealed class SimOmenRenderer
    {
        public bool Show(SimActor actor, ActionRow action, double sustain, string tag)
        {
            if (!actor.IsActive || action.Omen.RowId == 0 || sustain <= 0) return false;
            float x, y;
            switch (action.CastType)
            {
                case 2:
                case 10:
                case 13:
                    x = y = action.EffectRange;
                    break;
                case 3:
                case 5:
                    x = y = action.EffectRange + actor.Entity.HitboxRadius;
                    break;
                case 12:
                case 11:
                case 14:
                    x = (action.XAxisModifier / 2f);
                    y = action.EffectRange;
                    break;
                case 4:
                    x = (action.XAxisModifier / 2f);
                    y = action.EffectRange + actor.Entity.HitboxRadius;
                    break;
                default:
                    return false;
            }

            // 在预兆出现时读取位置和朝向，包含延迟期间移动产生的变化。
            var center = actor.Entity.Pos3D;
            var heading = actor.Entity.Heading;
            var command = new StringBuilder();
            command.AppendLine("Tag: " + tag);
            command.AppendLine("Omen: " + action.Omen.Value.Path.ExtractText());
            command.AppendFormat(CultureInfo.InvariantCulture,
                "t: {0}\nO: {1}, {2}, {3}\nθ: {4}\nScale: {5}, {6}, 1\n",
                sustain, center.X, center.Y, center.Z, heading, x, y);
            TriggernometryInterop.InvokeNamedCallback("PictoACT", command.ToString());
            return true;
        }

        public void Remove(string tag)
            => TriggernometryInterop.InvokeNamedCallback("PictoACT", "Action: Remove\nTag: " + tag);
    }
}

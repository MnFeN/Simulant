using Simulant.Core;
using System.Numerics;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Simulation.Runtime
{
    /// <summary>只负责绘制和移除预兆；延迟绘制由读条控制器安排。</summary>
    internal sealed class SimOmenRenderer
    {
        public bool Show(SimActor actor, ActionRow action, double sustain, string tag,
            Vector3? position = null, float? rotation = null)
        {
            if (!actor.IsActive || action.Omen.RowId == 0 || sustain <= 0)
                return false;

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

            // 未指定时在绘制时读取；同帧发出的移动包尚未消费时，可传入已知的技能落点。
            var center = position ?? actor.Entity.Pos3D;
            var heading = rotation ?? actor.Entity.Heading;
            VfxManager.ShowOmen(action.Omen.Value.Path.ExtractText(), center, heading,
                new Vector3(x, y, 1), sustain, tag);
            return true;
        }

        public void Remove(string tag)
            => VfxManager.Remove(tag);
    }
}

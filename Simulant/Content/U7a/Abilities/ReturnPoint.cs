using Simulant.Simulation.Runtime;
using System.Numerics;

namespace Simulant.Content.U7a.Abilities
{
    internal readonly struct ReturnPoint
    {
        private readonly Vector3 _position;
        internal Vector2 GroundPosition => new Vector2(_position.X, _position.Y);

        internal ReturnPoint(SimActor member) => _position = member.JudgementPosition3D;

        // 回放通过自身的 ActorControlSelf 0xB7 播放位置特效，没有生成或删除 EObj。
        internal void ShowTo(SimActor self) => self.Entity.PlayPositionVfx(1013, _position);

        // 回返保留传送当下的面向，0xF1 让客户端在 return_warp 动画中完成位移。
        internal void Return(SimActor member) => member.Entity.WarpWithAnimation(_position, member.Heading, 8488);
    }
}

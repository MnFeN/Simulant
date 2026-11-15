using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    /// <summary>固定容量的自身位置及防击退状态记录；取不晚于判定时刻的样本，不在传送前后插值。</summary>
    internal sealed class PositionHistory
    {
        private readonly double[] _times = new double[128];
        private readonly Vector3[] _positions = new Vector3[128];
        private readonly bool[] _immunity = new bool[128];
        private int _next;
        private int _count;

        internal void Add(double time, Vector2 position) => Add(time, new Vector3(position, 0), false);

        internal void Add(double time, Vector3 position, bool immune)
        {
            _times[_next] = time;
            _positions[_next] = position;
            _immunity[_next] = immune;
            _next = (_next + 1) % _times.Length;
            if (_count < _times.Length)
                _count++;
        }

        internal Vector2 At(double time)
        {
            var position = PositionAt(time);
            return new Vector2(position.X, position.Y);
        }

        internal Vector3 PositionAt(double time) => _positions[Find(time)];
        internal bool ImmunityAt(double time) => _immunity[Find(time)];

        private int Find(double time)
        {
            var oldest = (_next - _count + _times.Length) % _times.Length;
            for (var i = 1; i <= _count; i++)
            {
                var index = (_next - i + _times.Length) % _times.Length;
                if (_times[index] <= time)
                    return index;
            }
            return oldest;
        }
    }
}

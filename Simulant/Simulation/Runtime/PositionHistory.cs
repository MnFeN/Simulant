using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    /// <summary>固定容量的自身位置、朝向和无敌状态记录；击退与防击退仅用当前值，不保留历史。</summary>
    internal sealed class PositionHistory
    {
        private readonly double[] _times = new double[128];
        private readonly Vector3[] _positions = new Vector3[128];
        private readonly bool[] _invulnerability = new bool[128];
        private readonly float[] _headings = new float[128];
        private int _next;
        private int _count;

        internal void Add(double time, Vector2 position) => Add(time, new Vector3(position, 0));

        internal void Add(double time, Vector3 position, bool invulnerable = false, float heading = 0)
        {
            _times[_next] = time;
            _positions[_next] = position;
            _invulnerability[_next] = invulnerable;
            _headings[_next] = heading;
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
        internal bool InvulnerabilityAt(double time) => _invulnerability[Find(time)];
        internal float HeadingAt(double time) => _headings[Find(time)];

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

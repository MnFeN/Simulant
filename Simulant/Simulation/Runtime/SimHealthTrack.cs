using Simulant.Simulation.Timeline;
using System;

namespace Simulant.Simulation.Runtime
{
    /// <summary>按会话时间播放实体的绝对血量，采样点之间做线性插值。</summary>
    public sealed class SimHealthTrack
    {
        private const double UpdateInterval = 0.2;
        private readonly (double Seconds, uint Hp)[] _samples;

        public SimHealthTrack(params (double Seconds, uint Hp)[] samples)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("血量时间轴至少需要一个采样点。", nameof(samples));

            _samples = ((double Seconds, uint Hp)[])samples.Clone();
            for (var index = 0; index < _samples.Length; index++)
            {
                var time = _samples[index].Seconds;
                SimTimeline.ValidateSeconds(time, nameof(samples));
                if (index > 0 && time <= _samples[index - 1].Seconds)
                    throw new ArgumentException("血量采样时间必须严格递增。", nameof(samples));
            }
        }

        /// <summary>超出采样区间时取对应端点的血量。</summary>
        public uint HpAt(double seconds)
        {
            if (seconds <= _samples[0].Seconds)
                return _samples[0].Hp;

            for (var index = 1; index < _samples.Length; index++)
            {
                var next = _samples[index];
                if (seconds > next.Seconds)
                    continue;

                var previous = _samples[index - 1];
                var fraction = (seconds - previous.Seconds) / (next.Seconds - previous.Seconds);
                return (uint)Math.Round(previous.Hp + ((double)next.Hp - previous.Hp) * fraction);
            }

            return _samples[_samples.Length - 1].Hp;
        }

        /// <summary>实体就绪后从指定的会话时间开始播放，采样点结束后保留最后值。</summary>
        public void Play(SimActor actor, SimScheduler scheduler, double startSeconds)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            if (scheduler == null)
                throw new ArgumentNullException(nameof(scheduler));
            SimTimeline.ValidateSeconds(startSeconds, nameof(startSeconds));

            var end = startSeconds + _samples[_samples.Length - 1].Seconds;
            scheduler.Schedule(startSeconds, Update, "开始回放实体血量");

            void Update()
            {
                if (!actor.IsActive)
                    return;

                var now = scheduler.ElapsedSeconds;
                var hp = HpAt(now - startSeconds);
                if (actor.Entity.HP != hp)
                    actor.SetHp(hp);

                if (now < end)
                    scheduler.Schedule(Math.Min(end, now + UpdateInterval), Update, "插值更新实体血量");
            }
        }
    }
}

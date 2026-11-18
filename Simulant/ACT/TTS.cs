using Advanced_Combat_Tracker;

namespace Simulant.ACT
{
    /// <summary>通过 ACT 当前配置的 TTS 播报文本。</summary>
    public static class TTS
    {
        public static void Say(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                ActGlobals.oFormActMain?.PlayTtsMethod?.Invoke(text);
        }
    }
}

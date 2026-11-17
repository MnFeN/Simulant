using Simulant.Game;
using SimuMagic;

namespace Simulant.Tests.Native
{
    internal static class NativeTestContext
    {
        internal static void Use(ExternalProcessMemory memory, INativeCalls calls)
        {
            typeof(GameInterop).GetProperty(nameof(GameInterop.Connection)).GetSetMethod(true)
                .Invoke(null, new object[] { new GameConnection(memory, calls) });
        }
    }
}

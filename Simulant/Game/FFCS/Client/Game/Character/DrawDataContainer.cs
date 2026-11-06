using System;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    /// <summary>角色的武器、装备与自定义外观数据。</summary>
    public struct DrawDataContainer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // FFXIVClientStructs aad0de66 的 DrawDataContainer 大小为 0x268。
        // 一次读取后由快照解码，避免为十个装备槽分别跨进程读取。
        public byte[] ReadAppearanceData() => Ptr.ReadBytes(0x268);
    }
}

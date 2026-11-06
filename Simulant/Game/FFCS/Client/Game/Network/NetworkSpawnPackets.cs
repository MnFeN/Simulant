using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    public unsafe partial struct CommonSpawnData
    {
        [FieldOffset(0x10)] public ulong MainhandWeaponModel;
        [FieldOffset(0x18)] public ulong OffhandWeaponModel;
        [FieldOffset(0x1FC)] public fixed uint Equipment[10];
        [FieldOffset(0x224)] public fixed byte ModelStain2[10];
        [FieldOffset(0x22E)] public fixed ushort Glasses[2];
        [FieldOffset(0x232)] public fixed byte Name[32];
        [FieldOffset(0x252)] public fixed byte Customize[26];
    }

    /// <summary>当前客户端角色删除分支使用的网络槽位编号。</summary>
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct DespawnCharacterPacket
    {
        [FieldOffset(0)] public byte Index;
    }
}

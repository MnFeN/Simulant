using System;

namespace Simulant.Core.Entity
{
    /// <summary>生成包使用的玩家外观副本；不保存游戏内存地址。</summary>
    internal sealed partial class PlayerAppearanceSnapshot
    {
        internal string Name;
        internal byte Level;
        internal ushort HomeWorld;
        internal ushort CurrentWorld;
        internal ulong MainhandWeaponModel;
        internal ulong OffhandWeaponModel;
        internal uint[] Equipment = new uint[10];
        internal byte[] Stain2 = new byte[10];
        internal byte[] Customize = new byte[26];
        internal ushort[] Glasses = new ushort[2];

        internal static PlayerAppearanceSnapshot FromDrawData(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length != 0x268)
                throw new ArgumentException("玩家外观数据长度必须为 0x268 字节。", nameof(data));

            // FFXIVClientStructs aad0de66：DrawDataContainer.cs。
            // 原生装备每项八字节，网络包使用前四字节，并单独存放第二染色。
            var result = new PlayerAppearanceSnapshot
            {
                MainhandWeaponModel = BitConverter.ToUInt64(data, 0x10),
                OffhandWeaponModel = BitConverter.ToUInt64(data, 0x80)
            };
            for (var i = 0; i < result.Equipment.Length; i++)
            {
                result.Equipment[i] = BitConverter.ToUInt32(data, 0x1D0 + i * 8);
                result.Stain2[i] = data[0x1D4 + i * 8];
            }
            Array.Copy(data, 0x220, result.Customize, 0, result.Customize.Length);
            for (var i = 0; i < result.Glasses.Length; i++)
                result.Glasses[i] = BitConverter.ToUInt16(data, 0x240 + i * 2);
            return result;
        }
    }
}

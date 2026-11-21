using Simulant.Game.FFCS.Client.Game.Event;
using System;
using System.IO;

namespace Simulant.Game.FFCS.Client.Game.InstanceContent
{
    // Client::Game::InstanceContent::ContentDirector
    //   Client::Game::Event::Director
    //     Client::Game::Event::LuaEventHandler
    //       Client::Game::Event::EventHandler
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/InstanceContent/ContentDirector.cs
    public struct ContentDirector : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<Director>
        // private Director Director => Ptr.As<Director>();
        // #region Director
        // #endregion
        public MemoryField<byte> ContentTypeRowId => Ptr.Field<byte>(0x4E6);
        public MapEffectList MapEffects => Ptr.ReadPtr(0xC90).As<MapEffectList>();
        /// <remarks> This might also be a countdown until the content starts (e.g. Frontlines), then the actual time left of the content. </remarks>
        public MemoryField<float> ContentTimeLeft => Ptr.Field<float>(0xCF0);

        // ECommons 的 Hooks/MapEffect.cs 定位的是上层 ApplyMapEffect。
        // 这里的签名定位它调用的 PlayMapEffectTimeline。
        [SigPattern("E8 * * * * 3C ? 75 ? 80 64 B3 ? ?",
                    "48 89 6C 24 ? 56 48 83 EC ? 8B C2 41 0F B7 E8 45 33 C0 48 8D 14 40 48 8B 81 ? ? ? ? B1 ?")]
        public static IntPtr PlayMapEffectTimelineFuncPtr { get; set; }

        /// <summary>播放场地效果绑定的动画。<br />
        /// · index 为 MapEffects 中的槽位。<br />
        /// · timelineIndex 为要播放的 16 位时间轴掩码。
        /// </summary>
        public bool PlayMapEffectTimeline(uint index, ushort timelineIndex)
        {
            if (Ptr == IntPtr.Zero || index >= MapEffectList.Capacity)
                return false;

            var effects = MapEffects;
            if (effects.Ptr == IntPtr.Zero || index >= effects.ItemCount.Get() || effects.GetItem((int)index).LayoutId.Get() == 0)
                return false;

            return PlayMapEffectTimelineFuncPtr.Call<bool>(Ptr, index, timelineIndex);
        }

        public uint GetCurrentLevel() => Ptr.CallVFunc<uint>(304);

        public uint GetMaxLevel() => Ptr.CallVFunc<uint>(305);

        // 7.58 中副本和公共副本的 331 槽均读取时限字段并将分钟换算为秒。
        /// <summary>获取副本时限，单位为秒。</summary>
        public uint GetContentTimeMax() => Ptr.CallVFunc<uint>(331);

        public struct MapEffectList : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            internal const int Capacity = 128;

            // [FieldOffset(0x00), FixedSizeArray] internal FixedSizeArray128<MapEffectItem> _items;
            public MapEffectItem GetItem(int index) => Ptr.As<MapEffectItem>(0x00 + index * MapEffectItemSize); // temp until we implement the array struct
            public MemoryField<ushort> ItemCount => Ptr.Field<ushort>(0x602);
            public MemoryField<byte> Dirty => Ptr.Field<byte>(0x604);
        }

        private const int MapEffectItemSize = 0x0C;
        public struct MapEffectItem : IMemoryObject
        {
            // 本机客户端 2026.11.03.0000.0000：待应用槽位播放成功后置位（RVA 0xB71077、0xB731E6）。
            internal const byte TimelineAppliedFlag = 0x02;

            public IntPtr Ptr { get; set; }

            public MemoryField<uint> LayoutId => Ptr.Field<uint>(0x00); // ContentDirectorManagedSG.Unknown0
            public MemoryField<byte> Unknown1 => Ptr.Field<byte>(0x05); // ContentDirectorManagedSG.Unknown1
            public MemoryField<ushort> State => Ptr.Field<ushort>(0x08);
            public MemoryField<byte> Flags => Ptr.Field<byte>(0x0A);
        }
    }
}

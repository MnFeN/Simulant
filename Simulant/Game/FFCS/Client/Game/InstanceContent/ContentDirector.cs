using Simulant.Game.FFCS.Client.Game.Event;
using System;
using System.IO;

namespace Simulant.Game.FFCS.Client.Game.InstanceContent
{
    // Client::Game::InstanceContent::ContentDirector
    //   Client::Game::Event::Director
    //     Client::Game::Event::LuaEventHandler
    //       Client::Game::Event::EventHandler
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

        // ECommons/ECommons/Hooks/MapEffect.cs
        // MapEffect 函数：48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B FA 41 0F B7 E8
        // 这里使用的是其下一层函数：
        [SigPattern("E8 * * * * 3C ? 75 ? 80 64 B3 ? ?",
                    "48 89 6C 24 ? 56 48 83 EC ? 8B C2 41 0F B7 E8 45 33 C0 48 8D 14 40 48 8B 81 ? ? ? ? B1 ?")]
        public static IntPtr MapEffectFuncPtr { get; set; }

        /// <returns>Success</returns>
        public bool MapEffect(uint index, ushort flag) // byte index?
        => MapEffectFuncPtr.Call<bool>(Ptr, index, flag);

        public uint GetCurrentLevel() => Ptr.CallVFunc<uint>(302);

        public uint GetMaxLevel() => Ptr.CallVFunc<uint>(303);

        /// <summary>
        /// Gets the max time for the content in seconds
        /// </summary>
        /// <returns>Time in seconds</returns>
        public uint GetContentTimeMax() => Ptr.CallVFunc<uint>(328);

        public struct MapEffectList : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            // [FieldOffset(0x00), FixedSizeArray] internal FixedSizeArray128<MapEffectItem> _items;
            public MapEffectItem GetItem(int index) => Ptr.As<MapEffectItem>(0x00 + index * MapEffectItemSize); // temp until we implement the array struct
            public MemoryField<ushort> ItemCount => Ptr.Field<ushort>(0x602);
            public MemoryField<byte> Dirty => Ptr.Field<byte>(0x604);
        }

        private const int MapEffectItemSize = 0x0C;
        public struct MapEffectItem : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            public MemoryField<uint> LayoutId => Ptr.Field<uint>(0x00); // ContentDirectorManagedSG.Unknown0
            public MemoryField<byte> Unknown1 => Ptr.Field<byte>(0x05); // ContentDirectorManagedSG.Unknown1
            public MemoryField<ushort> State => Ptr.Field<ushort>(0x08);
            public MemoryField<byte> Flags => Ptr.Field<byte>(0x0A);
        }
    }
}

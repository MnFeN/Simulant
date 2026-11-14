using System;

namespace Simulant.Game.FFCS.Client.Game
{
    // Client::Game::BGMSystem
    public struct BGMSystem : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("4C 8B 15 * * * * 4D 85 D2 74 77 41 83 7A",
                    "48 8B 05 * * * * 48 85 C0 74 51 83 78 08 0B")]
        public static IntPtr InstancePtr { get; set; }

        public static BGMSystem Instance()
            => InstancePtr.ReadPtr().As<BGMSystem>();

        public MemoryField<uint> NumScenes => Ptr.Field<uint>(0x8); // equals the amount of rows in the BGMScene sheet
        public SceneList Scenes => Ptr.ReadPtr(0xC0).As<SceneList>(); // StdVector<Scene>
        public MemoryField<SituationKind> CurrentSituationKind => Ptr.Field<SituationKind>(0xD8);
        public MemoryField<bool> PlayBattleBGM => Ptr.Field<bool>(0xDC); // is set in update loop, based on combat
        public MemoryField<bool> LastingBGM => Ptr.Field<bool>(0xF8);
        public MemoryField<bool> ContinueBGMUntilWarp => Ptr.Field<bool>(0xF9);

        [SigPattern("4C 8B 1D ? ? ? ? 4D 85 DB 0F 84 ? ? ? ? 41 3B 53")]
        public static IntPtr SetBGMFuncPtr { get; set; }

        public static void SetBGM(
            ushort bgmId,
            uint sceneId,
            byte a3 = 0,
            bool enableCustomFade = false,
            uint fadeOutMs = 0,
            uint fadeInMs = 0,
            uint fadeInStartMs = 0,
            byte a8 = 0,
            byte a9 = 0,
            float initialVolume = 1f)
            => SetBGMFuncPtr.Call(bgmId, sceneId, a3, enableCustomFade, fadeOutMs, fadeInMs, fadeInStartMs, a8, a9, initialVolume);

        [SigPattern("4C 8B 05 * * * * 4D 85 C0 74 3D")]
        public static IntPtr ResetBGMFuncPtr { get; set; }

        public void ResetBGM(uint sceneId)
            => ResetBGMFuncPtr.Call(Ptr, sceneId);

        public struct SceneList : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            public Scene this[int index] => Ptr.As<Scene>(index * Scene.Size);
        }

        // 同时参考：https://github.com/perchbirdd/OrchestrionPlugin/blob/main/Orchestrion/BGMSystem/BGMScene.cs
        public struct Scene : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            public const int Size = 0xA0;

            /// <summary> The index of <see cref="Scenes"/> and the RowId in the BGMScene sheet. </summary>
            /// <remarks>
            /// 0 = Event<br/>
            /// 1 = Battle<br/>
            /// 2 = MiniGame (RhythmAction, TurnBreak)<br/>
            /// 3 = Content<br/>
            /// 4 = GFate<br/>
            /// 5 = Duel<br/>
            /// 6 = Mount<br/>
            /// 7 = Unknown, no xrefs<br/>
            /// 8 = Unknown, via packet (near PlayerState stuff)<br/>
            /// 9 = Wedding<br/>
            /// 10 = Town<br/>
            /// 11 = Territory
            /// </remarks>
            public MemoryField<uint> SceneId => Ptr.Field<uint>(0x00);
            public MemoryField<SceneFlags> SceneFlags => Ptr.Field<SceneFlags>(0x04);
            public MemoryField<SituationKind> SituationKind => Ptr.Field<SituationKind>(0x08);
            public MemoryField<ushort> BgmId => Ptr.Field<ushort>(0x0C);
            public MemoryField<ushort> PlayingBgmId => Ptr.Field<ushort>(0x0E);
            public MemoryField<ushort> PreviousBgmId => Ptr.Field<ushort>(0x10); // holds BgmId until after BGMSwitch selection
            public MemoryField<byte> TimerEnabled => Ptr.Field<byte>(0x12);
            public MemoryField<float> Timer => Ptr.Field<float>(0x14);

            // fade times in ms
            public MemoryField<bool> EnableCustomFade => Ptr.Field<bool>(0x30);
            public MemoryField<uint> FadeOutTime => Ptr.Field<uint>(0x38);
            public MemoryField<uint> FadeInTime => Ptr.Field<uint>(0x3C);
            public MemoryField<uint> FadeInStartTime => Ptr.Field<uint>(0x40);
            public MemoryField<uint> ResumeFadeInTime => Ptr.Field<uint>(0x44); // unused? it's present in the BGMFadeType sheet
            public MemoryField<PlayState> PlayState => Ptr.Field<PlayState>(0x50);
            public MemoryField<float> InitialVolume => Ptr.Field<float>(0x58);
        }

        // https://github.com/perchbirdd/OrchestrionPlugin/blob/main/Orchestrion/BGMSystem/SceneFlags.cs
        [Flags]
        public enum SceneFlags : byte
        {
            None = 0,
            Unknown = 1,
            Resume = 2,
            EnablePassEnd = 4,
            ForceAutoReset = 8,
            EnableDisableRestart = 16,
            IgnoreBattle = 32,
        }

        public enum SituationKind : uint
        {
            None = 0,
            Daytime = 1,
            Night = 2,
            Battle = 3,
            Daybreak = 4,
            Twilight = 5
        }

        public enum PlayState : uint
        {
            Paused = 0,
            Playing = 1,
        }
    }
}

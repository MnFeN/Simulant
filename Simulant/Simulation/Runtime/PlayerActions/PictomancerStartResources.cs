using System;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    public enum PictomancerMotif
    {
        Pom,
        Wings,
        Claw,
        Maw
    }

    public enum PictomancerPortrait
    {
        None,
        Moogle,
        Madeen
    }

    /// <summary>绘灵所有模拟预设共用的起始资源，从每次开始模拟时应用。</summary>
    public sealed class PictomancerStartResources
    {
        public bool Enabled { get; set; }
        public int Mp { get; set; } = 10000;
        public int Palette { get; set; }
        public int WhitePaint { get; set; }
        public bool BlackPaint { get; set; }
        internal bool Monochrome { get; set; }
        public int SubtractiveStacks { get; set; }
        public int HueStep { get; set; }
        public int HueSeconds { get; set; } = 30;
        public PictomancerMotif NextMotif { get; set; }
        public bool CreaturePainted { get; set; }
        public bool WeaponPainted { get; set; }
        public bool LandscapePainted { get; set; }
        public PictomancerPortrait Portrait { get; set; }
        public int CreatureCharges { get; set; } = 3;
        public int CreatureRecharge { get; set; } = 40;
        public int HammerCharges { get; set; } = 2;
        public int HammerRecharge { get; set; } = 60;
        public int HammerStacks { get; set; }
        public int HammerSeconds { get; set; } = 30;
        public int StarrySeconds { get; set; }
        public int HyperphantasiaStacks { get; set; } = 5;
        public int FreeSubtractiveSeconds { get; set; }
        public int StarPrismSeconds { get; set; }
        public int RainbowSeconds { get; set; }
        public int SwiftcastSeconds { get; set; }
        public int StarryCooldown { get; set; }
        public int PortraitCooldown { get; set; }
        public int SwiftcastCooldown { get; set; }
        public int LucidCooldown { get; set; }

        internal PictomancerStartResources Copy()
        {
            var copy = (PictomancerStartResources)MemberwiseClone();
            copy.Mp = Clamp(Mp, 10000);
            copy.Palette = Clamp(Palette, 100);
            copy.WhitePaint = Clamp(WhitePaint, BlackPaint ? 4 : 5);
            copy.SubtractiveStacks = Clamp(SubtractiveStacks, 3);
            copy.HueStep = Clamp(HueStep, 2);
            copy.HueSeconds = Clamp(HueSeconds, 30);
            copy.CreatureCharges = Clamp(CreatureCharges, 3);
            copy.CreatureRecharge = Math.Max(1, Clamp(CreatureRecharge, 40));
            copy.HammerCharges = Clamp(HammerCharges, 2);
            copy.HammerRecharge = Math.Max(1, Clamp(HammerRecharge, 60));
            copy.HammerStacks = Clamp(HammerStacks, 3);
            copy.HammerSeconds = Clamp(HammerSeconds, 30);
            copy.StarrySeconds = Clamp(StarrySeconds, 20);
            copy.HyperphantasiaStacks = Clamp(HyperphantasiaStacks, 5);
            copy.FreeSubtractiveSeconds = Clamp(FreeSubtractiveSeconds, 30);
            copy.StarPrismSeconds = Clamp(StarPrismSeconds, 20);
            copy.RainbowSeconds = Clamp(RainbowSeconds, 30);
            copy.SwiftcastSeconds = Clamp(SwiftcastSeconds, 10);
            copy.StarryCooldown = Clamp(StarryCooldown, 120);
            copy.PortraitCooldown = Clamp(PortraitCooldown, 30);
            copy.SwiftcastCooldown = Clamp(SwiftcastCooldown, 60);
            copy.LucidCooldown = Clamp(LucidCooldown, 60);
            if (!Enum.IsDefined(typeof(PictomancerMotif), NextMotif))
                copy.NextMotif = PictomancerMotif.Pom;
            if (!Enum.IsDefined(typeof(PictomancerPortrait), Portrait))
                copy.Portrait = PictomancerPortrait.None;
            return copy;
        }

        private static int Clamp(int value, int maximum) => Math.Max(0, Math.Min(maximum, value));
    }
}

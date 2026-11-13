namespace Simulant.Game.FFCS.Client.Game.Character
{
    public enum Job : byte
    {
        ADV = 0, GLA = 1, PGL = 2, MRD = 3, LNC = 4, ARC = 5, CNJ = 6, THM = 7,
        CRP = 8, BSM = 9, ARM = 10, GSM = 11, LTW = 12, WVR = 13, ALC = 14, CUL = 15,
        MIN = 16, BTN = 17, FSH = 18, PLD = 19, MNK = 20, WAR = 21, DRG = 22, BRD = 23,
        WHM = 24, BLM = 25, ACN = 26, SMN = 27, SCH = 28, ROG = 29, NIN = 30, MCH = 31,
        DRK = 32, AST = 33, SAM = 34, RDM = 35, BLU = 36, GNB = 37, DNC = 38, RPR = 39,
        SGE = 40, VPR = 41, PCT = 42, _43 = 43, _44 = 44,
    }

    // LogMessages for errors starting at 7700
    public enum CharacterModes : byte
    {
        None = 0, // Mode is never used
        Normal = 1, // Param always 0
        Dead = 2,
        EmoteLoop = 3, // Param is an EmoteMode entry
        Mounted = 4, // Param always 0
        Crafting = 5, // Param always 0
        Gathering = 6,
        MateriaAttach = 7,
        AnimLock = 8, // Param always 0
        Carrying = 9, // Param is a Carry entry
        RidingPillion = 10, // Param is the pillion seat number
        InPositionLoop = 11, // Param is an EmoteMode entry
        RaceChocobo = 12,
        TripleTriad = 13,
        Lovm = 14, // Lord of Verminion
        // CustomMatch = 15, // PvP, untested
        Performance = 16, // Param is Perform row id (the instrument)
    }
}

using System;

namespace BethFile
{
    [Flags]
    public enum BethesdaRecordFlags : uint
    {
        None                         = 0b00000000000000000000000000000000,
        Master                       = 0b00000000000000000000000000000001,
        Deleted                      = 0b00000000000000000000000000100000,
        Constant                     = 0b00000000000000000000000001000000,
        REFR_HiddenFromLocalMap      = 0b00000000000000000000000001000000,
        IsPerch                      = 0b00000000000000000000000010000000,
        TES4_Localized               = 0b00000000000000000000000010000000,
        PHZD_TurnOffFire             = 0b00000000000000000000000010000000,
        MustUpdateAnims              = 0b00000000000000000000000100000000,
        REFR_Inaccessible            = 0b00000000000000000000000100000000,
        ////REFR_HiddenFromLocalMap  = 0b00000000000000000000001000000000,
        ACHR_StartsDead              = 0b00000000000000000000001000000000,
        REFR_MotionBlurCastsShadows  = 0b00000000000000000000001000000000,
        QuestItem                    = 0b00000000000000000000010000000000,
        PersistentReference          = 0b00000000000000000000010000000000,
        LSCR_DisplaysInMainMenu      = 0b00000000000000000000010000000000,
        InitiallyDisabled            = 0b00000000000000000000100000000000,
        Ignored                      = 0b00000000000000000001000000000000,
        VisibleWhenDistant           = 0b00000000000000001000000000000000,
        ACTI_RandomAnimationStart    = 0b00000000000000010000000000000000,
        ACTI_Dangerous               = 0b00000000000000100000000000000000,
        OffLimits                    = 0b00000000000000100000000000000000,
        Compressed                   = 0b00000000000001000000000000000000,
        CantWait                     = 0b00000000000010000000000000000000,
        ACTI_IgnoreObjectInteraction = 0b00000000000100000000000000000000,
        IsMarker                     = 0b00000000100000000000000000000000,
        ACTI_Obstacle                = 0b00000010000000000000000000000000,
        REFR_NoAiAcquire             = 0b00000010000000000000000000000000,
        NavMeshGenFilter             = 0b00000100000000000000000000000000,
        NavMeshGenBoundingBox        = 0b00001000000000000000000000000000,
        FURN_MustExitToTalk          = 0b00010000000000000000000000000000,
        REFR_ReflectedByAutoWater    = 0b00010000000000000000000000000000,
        FURN_ChildCanUse             = 0b00100000000000000000000000000000,
        IDLM_ChildCanUse             = 0b00100000000000000000000000000000,
        REFR_DontHavokSettle         = 0b00100000000000000000000000000000,
        NavMeshGenGround             = 0b01000000000000000000000000000000,
        REFR_NoRespawn               = 0b01000000000000000000000000000000,
        REFR_MultiBound              = 0b10000000000000000000000000000000,
    }
}

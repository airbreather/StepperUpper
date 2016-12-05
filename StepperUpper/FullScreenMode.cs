using System;

namespace StepperUpper
{
    [Flags]
    public enum FullScreenMode
    {
        None         = 0b00,
        IsFullScreen = 0b01,
        IsBorderless = 0b10,

        Windowed = None,
        WindowedNoBorders = IsBorderless,
        FullScreen = IsFullScreen,
        FullScreenNoBorders = IsFullScreen | IsBorderless,
    }
}

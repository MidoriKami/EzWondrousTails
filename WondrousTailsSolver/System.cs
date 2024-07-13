using KamiLib.Window;
using KamiToolKit;

namespace WondrousTailsSolver;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
public static class System {
    public static Configuration Configuration { get; set; }
    public static WindowManager WindowManager { get; set; }
    public static NativeController NativeController { get; set; }
    public static AddonWeeklyBingoController AddonWeeklyBingoController { get; set; }
    public static PerfectTails PerfectTails { get; set; }
}
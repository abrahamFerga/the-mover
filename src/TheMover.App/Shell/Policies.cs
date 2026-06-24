// TheMover.App — ARCH.md: Cross-cutting wiring / RBAC (forward-compatibility constants)
namespace TheMover.App.Shell;

public static class Policies
{
    public const string ScheduleConfigure = "Schedule.Configure";
    public const string ScheduleSkipBreak = "Schedule.SkipBreak";
    public const string ScheduleSnoozeBreak = "Schedule.SnoozeBreak";
    public const string CalendarConnect = "Calendar.Connect";
    public const string CalendarDisconnect = "Calendar.Disconnect";
    public const string LibraryBrowse = "Library.Browse";
    public const string AppConfigureStartup = "App.ConfigureStartup";
    public const string AppQuit = "App.Quit";
}

// TheMover.App — ARCH.md: Containers / TheMover.App
using System.Windows;

namespace TheMover.App;

public partial class App : Application
{
    // Lifecycle is controlled by WpfHostedService / Generic Host.
    // No StartupUri — no MainWindow. TrayIconService manages visibility.
}

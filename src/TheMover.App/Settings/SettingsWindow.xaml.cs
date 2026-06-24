// TheMover.App — ARCH.md: Components / SettingsWindow (skeleton — filled by later epics)
using System.Windows;

namespace TheMover.App.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

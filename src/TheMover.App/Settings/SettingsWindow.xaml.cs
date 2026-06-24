// TheMover.App — Settings window (Break Schedule tab wired to ConfigManager)
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using TheMover.App.Config;

namespace TheMover.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;

    public SettingsWindow(ConfigManager configManager)
    {
        _configManager = configManager;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var s = _configManager.Current;
        MicroIntervalBox.Text = s.MicroBreak.IntervalMinutes.ToString();
        MicroDurationBox.Text = s.MicroBreak.DurationSeconds.ToString();
        LongIntervalBox.Text = s.LongBreak.IntervalMinutes.ToString();
        LongDurationBox.Text = s.LongBreak.DurationSeconds.ToString();
        AutoStartBox.IsChecked = s.AutoStartWithWindows;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseSettings(out var settings, out var error))
        {
            MessageBox.Show(error, "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await _configManager.SaveAsync(settings);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private bool TryParseSettings(
        [NotNullWhen(true)] out AppSettings? settings,
        [NotNullWhen(false)] out string? error)
    {
        settings = null;

        if (!int.TryParse(MicroIntervalBox.Text, out var microInterval) || microInterval < 1 || microInterval > 240)
        { error = "Micro-break interval must be between 1 and 240 minutes."; return false; }

        if (!int.TryParse(MicroDurationBox.Text, out var microDuration) || microDuration < 10 || microDuration > 600)
        { error = "Micro-break duration must be between 10 and 600 seconds."; return false; }

        if (!int.TryParse(LongIntervalBox.Text, out var longInterval) || longInterval < 1 || longInterval > 240)
        { error = "Long-break interval must be between 1 and 240 minutes."; return false; }

        if (!int.TryParse(LongDurationBox.Text, out var longDuration) || longDuration < 10 || longDuration > 600)
        { error = "Long-break duration must be between 10 and 600 seconds."; return false; }

        var current = _configManager.Current;
        settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = microInterval, DurationSeconds = microDuration },
            LongBreak = new BreakTierSettings { IntervalMinutes = longInterval, DurationSeconds = longDuration },
            AutoStartWithWindows = AutoStartBox.IsChecked == true,
            Snooze = current.Snooze,
            Calendar = current.Calendar,
        };
        error = null;
        return true;
    }
}

// TheMover.App — Settings window (Break Schedule + Exercise browse + Outlook connect/disconnect)
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using TheMover.App.Config;
using TheMover.Calendar;
using TheMover.Content;

namespace TheMover.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;
    private readonly ICalendarClient _calendarClient;

    public SettingsWindow(ConfigManager configManager, ICalendarClient calendarClient)
    {
        _configManager = configManager;
        _calendarClient = calendarClient;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var s = _configManager.Current;
        MicroIntervalBox.Text = s.MicroBreak.IntervalMinutes.ToString();
        MicroDurationBox.Text = s.MicroBreak.DurationSeconds.ToString();
        LongIntervalBox.Text = s.LongBreak.IntervalMinutes.ToString();
        LongDurationBox.Text = s.LongBreak.DurationSeconds.ToString();
        AutoStartBox.IsChecked = s.AutoStartWithWindows;

        ExercisesList.ItemsSource = ExerciseLibrary.All;

        TenantIdBox.Text = s.Calendar.TenantId ?? string.Empty;
        ClientIdBox.Text = s.Calendar.ClientId ?? string.Empty;
        await RefreshCalendarStatusAsync();
    }

    private async Task RefreshCalendarStatusAsync()
    {
        var connected = await _calendarClient.IsConnectedAsync();
        CalendarStatusLabel.Text = connected
            ? "Status: Connected"
            : "Status: Not connected";
        ConnectButton.IsEnabled = !connected;
        DisconnectButton.IsEnabled = connected;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        ConnectButton.IsEnabled = false;
        CalendarStatusLabel.Text = "Status: Connecting...";
        try
        {
            // Save tenant/client IDs first so GraphCalendarClient uses them
            await SaveCalendarCredentialsAsync();
            await _calendarClient.ConnectAsync();
            await SaveCalendarEnabledAsync(enabled: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Connection failed: {ex.Message}", "Outlook Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await RefreshCalendarStatusAsync();
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        DisconnectButton.IsEnabled = false;
        await _calendarClient.DisconnectAsync();
        await SaveCalendarEnabledAsync(enabled: false);
        await RefreshCalendarStatusAsync();
    }

    private async Task SaveCalendarCredentialsAsync()
    {
        var current = _configManager.Current;
        var updated = new AppSettings
        {
            MicroBreak = current.MicroBreak,
            LongBreak = current.LongBreak,
            AutoStartWithWindows = current.AutoStartWithWindows,
            Snooze = current.Snooze,
            Calendar = new CalendarSettings
            {
                Enabled = current.Calendar.Enabled,
                TenantId = TenantIdBox.Text.Trim().NullIfEmpty(),
                ClientId = ClientIdBox.Text.Trim().NullIfEmpty(),
            }
        };
        await _configManager.SaveAsync(updated);
    }

    private async Task SaveCalendarEnabledAsync(bool enabled)
    {
        var current = _configManager.Current;
        var updated = new AppSettings
        {
            MicroBreak = current.MicroBreak,
            LongBreak = current.LongBreak,
            AutoStartWithWindows = current.AutoStartWithWindows,
            Snooze = current.Snooze,
            Calendar = new CalendarSettings
            {
                Enabled = enabled,
                TenantId = current.Calendar.TenantId,
                ClientId = current.Calendar.ClientId,
            }
        };
        await _configManager.SaveAsync(updated);
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

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

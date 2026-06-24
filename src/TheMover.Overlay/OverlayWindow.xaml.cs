// TheMover.Overlay — in-process WPF overlay (not a toast; bypasses Focus Assist)
using System.Windows;
using System.Windows.Threading;

namespace TheMover.Overlay;

public partial class OverlayWindow : Window
{
    private readonly int _totalSeconds;
    private readonly Action _onSnooze;
    private readonly Action _onSkip;
    private readonly DispatcherTimer _timer;
    private int _remaining;
    private bool _actionTaken;

    public OverlayWindow(string tierLabel, int durationSeconds, Action onSnooze, Action onSkip)
    {
        _totalSeconds = durationSeconds;
        _remaining = durationSeconds;
        _onSnooze = onSnooze;
        _onSkip = onSkip;

        InitializeComponent();

        TierLabel.Text = tierLabel;
        UpdateDisplay();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;
        UpdateDisplay();

        if (_remaining <= 0)
        {
            _timer.Stop();
            Close();
        }
    }

    private void UpdateDisplay()
    {
        var ts = TimeSpan.FromSeconds(_remaining);
        CountdownLabel.Text = ts.TotalSeconds >= 60
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
            : $"0:{_remaining:D2}";

        CountdownBar.Value = _totalSeconds > 0 ? (double)_remaining / _totalSeconds : 0;
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        if (_actionTaken) return;
        _actionTaken = true;
        _timer.Stop();
        _onSnooze();
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_actionTaken) return;
        _actionTaken = true;
        _timer.Stop();
        _onSkip();
        Close();
    }
}

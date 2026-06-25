// TheMover.Overlay — in-process WPF overlay (Topmost=True bypasses Focus Assist; ADR-0003)
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TheMover.Content;

namespace TheMover.Overlay;

public partial class OverlayWindow : Window
{
    private readonly int _totalSeconds;
    private readonly Action _onComplete;
    private readonly Action _onSnooze;
    private readonly Action _onSkip;
    private readonly DispatcherTimer _countdown;
    private int _remaining;
    private bool _actionTaken;

    public OverlayWindow(string tierLabel, int durationSeconds, Exercise exercise, Action onComplete, Action onSnooze, Action onSkip)
    {
        _totalSeconds = durationSeconds;
        _remaining = durationSeconds;
        _onComplete = onComplete;
        _onSnooze = onSnooze;
        _onSkip = onSkip;

        InitializeComponent();

        TierLabel.Text = tierLabel;
        ExerciseTitle.Text = exercise.Title;
        InstructionText.Text = exercise.Instruction;
        UpdateDisplay();

        _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdown.Tick += OnTick;
    }

    private void OnContentRendered(object sender, EventArgs e)
    {
        // Ensure the borderless window has keyboard focus so Escape/S shortcuts work.
        Activate();
        _countdown.Start();
        StartBreatheAnimation();
    }

    private void StartBreatheAnimation()
    {
        var anim = new DoubleAnimation
        {
            From = 0.7,
            To = 1.0,
            Duration = TimeSpan.FromSeconds(4),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        BreathScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        BreathScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;
        UpdateDisplay();

        if (_remaining <= 0 && !_actionTaken)
        {
            _actionTaken = true;
            _countdown.Stop();
            _onComplete();
            Close();
        }
    }

    private void UpdateDisplay()
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, _remaining));
        CountdownLabel.Text = $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        CountdownBar.Value = _totalSeconds > 0 ? (double)_remaining / _totalSeconds : 0;
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        if (_actionTaken) return;
        _actionTaken = true;
        _countdown.Stop();
        _onSnooze();
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_actionTaken) return;
        _actionTaken = true;
        _countdown.Stop();
        _onSkip();
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Skip_Click(this, new RoutedEventArgs());
                break;
            case Key.S:
                Snooze_Click(this, new RoutedEventArgs());
                break;
        }
    }

    // Stop the countdown whenever the window closes — including external closes
    // triggered by the cancellation-token handler in OverlayService.  Without this,
    // the DispatcherTimer keeps ticking on a closed window until _remaining reaches
    // zero, then fires _onComplete and a redundant Close() call.
    protected override void OnClosed(EventArgs e)
    {
        _countdown.Stop();
        base.OnClosed(e);
    }
}

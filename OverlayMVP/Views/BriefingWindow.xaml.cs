// filename: Views/BriefingWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace OverlayMVP.Views
{
    /// <summary>
    /// "Incoming transmission" popup that autoplays a step's officer lore-briefing
    /// clip (with sound), with play/pause and a seek bar for re-listening.
    /// Opaque window so the MediaElement always renders.
    /// </summary>
    public partial class BriefingWindow : Window
    {
        private static BriefingWindow? _current;
        private readonly DispatcherTimer _timer;
        private bool _isPlaying;
        private bool _userDragging;   // user is dragging the seek thumb
        private bool _suppressSeek;   // slider value change came from the timer, not the user

        public BriefingWindow(string clipPath, string title)
        {
            InitializeComponent();

            // Only one briefing at a time
            _current?.Close();
            _current = this;
            Closed += (_, _) => { if (_current == this) _current = null; };

            HeaderText.Text = title;
            Player.Source = new Uri(clipPath, UriKind.Absolute);
            Player.Volume = 1.0;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += Timer_Tick;

            // Autoplay with sound once the window is up
            Loaded += (_, _) => Play();
        }

        // ── Playback state ──────────────────────────────────────────────────────
        private void Play()
        {
            Player.Play();
            _isPlaying = true;
            PlayPauseBtn.Content = "⏸ Pause";
            _timer.Start();
        }

        private void Pause()
        {
            Player.Pause();
            _isPlaying = false;
            PlayPauseBtn.Content = "▶ Play";
        }

        private void TogglePlayPause()
        {
            if (_isPlaying) { Pause(); return; }
            // If we're at (or past) the end, restart from the beginning
            if (Player.NaturalDuration.HasTimeSpan &&
                Player.Position >= Player.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(200))
                Player.Position = TimeSpan.Zero;
            Play();
        }

        // ── MediaElement events ─────────────────────────────────────────────────
        private void Player_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (Player.NaturalDuration.HasTimeSpan)
            {
                SeekBar.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds;
                UpdateTimeText();
            }
        }

        private void Player_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Freeze on the last frame; flip to a "play to replay" state.
            _isPlaying = false;
            PlayPauseBtn.Content = "▶ Play";
        }

        private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _timer.Stop();
            HeaderText.Text = "TRANSMISSION UNAVAILABLE";
        }

        // ── Timer drives the seek bar + clock while playing ─────────────────────
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_userDragging || !Player.NaturalDuration.HasTimeSpan) return;
            _suppressSeek = true;
            SeekBar.Value = Player.Position.TotalSeconds;
            _suppressSeek = false;
            UpdateTimeText();
        }

        // ── Seek bar interaction ────────────────────────────────────────────────
        private void SeekBar_DragStarted(object sender, DragStartedEventArgs e) => _userDragging = true;

        private void SeekBar_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _userDragging = false;
            SeekTo(SeekBar.Value);
        }

        private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSeek) return;          // timer update — ignore
            // User dragging: scrub live (ScrubbingEnabled shows frames).
            // Click-to-point (no drag): seek immediately.
            SeekTo(e.NewValue);
        }

        private void SeekTo(double seconds)
        {
            if (!Player.NaturalDuration.HasTimeSpan) return;
            Player.Position = TimeSpan.FromSeconds(seconds);
            UpdateTimeText();
        }

        /// <summary>Skip relative to current position, clamped to [0, duration].</summary>
        private void Skip(double deltaSeconds)
        {
            if (!Player.NaturalDuration.HasTimeSpan) return;
            var max    = Player.NaturalDuration.TimeSpan;
            var target = Player.Position + TimeSpan.FromSeconds(deltaSeconds);
            if (target < TimeSpan.Zero) target = TimeSpan.Zero;
            if (target > max)           target = max;
            Player.Position = target;
            _suppressSeek = true;
            SeekBar.Value = target.TotalSeconds;
            _suppressSeek = false;
            UpdateTimeText();
        }

        // ── Volume ──────────────────────────────────────────────────────────────
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Player is null) return;
            Player.Volume = e.NewValue;
            VolumeIcon.Text = e.NewValue <= 0.001 ? "🔇" : e.NewValue < 0.5 ? "🔉" : "🔊";
        }

        // ── Keyboard shortcuts (space = play/pause, ←/→ = ±5s, ↑/↓ = volume) ─────
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: TogglePlayPause(); e.Handled = true; break;
                case Key.Left:  Skip(-5);          e.Handled = true; break;
                case Key.Right: Skip(5);           e.Handled = true; break;
                case Key.Up:    VolumeSlider.Value = Math.Min(1, VolumeSlider.Value + 0.1); e.Handled = true; break;
                case Key.Down:  VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 0.1); e.Handled = true; break;
                case Key.Escape: Close();          e.Handled = true; break;
            }
        }

        private void UpdateTimeText()
        {
            var pos = Player.Position;
            var dur = Player.NaturalDuration.HasTimeSpan
                ? Player.NaturalDuration.TimeSpan : TimeSpan.Zero;
            TimeText.Text = $"{Fmt(pos)} / {Fmt(dur)}";
        }

        private static string Fmt(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

        // ── Buttons / window chrome ─────────────────────────────────────────────
        private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePlayPause();

        private void Video_MouseDown(object sender, MouseButtonEventArgs e) => TogglePlayPause();

        private void ReplayButton_Click(object sender, RoutedEventArgs e)
        {
            Player.Position = TimeSpan.Zero;
            Play();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            Player.Stop();
            Player.Close();
            base.OnClosed(e);
        }
    }
}

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SimTools.Models;
using SimTools.Services;

namespace SimTools.Views
{
    public partial class UpdateCheckWindow : Window
    {
        // Manifest endpoint + timeouts (tweak to taste)
        private const string ManifestUrl = "https://example.com/simtools/update.json";
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);

        private readonly UpdateService _service = new();
        private UpdateManifest? _manifest;

        // Splash/progress animation
        private readonly DispatcherTimer _progressTimer;
        private double _progress;          // 0..100
        private double _progressTarget;    // we ease towards this
        private bool _checkingDone;

        public UpdateCheckWindow()
        {
            InitializeComponent();

            // Smooth “Discord-like” progress: crawl up to 90% while checking
            _progressTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            Loaded += UpdateCheckWindow_Loaded;
            Unloaded += (_, __) => _progressTimer.Stop();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            // Ease towards target
            const double ease = 0.12; // smaller = smoother
            var delta = (_progressTarget - _progress) * ease;
            if(Math.Abs(delta) < 0.1) delta = Math.Sign(delta) * 0.1;
            _progress = Math.Clamp(_progress + delta, 0, 100);

            Bar.Value = _progress;
            PercentText.Text = $"{(int)_progress}%";
        }

        private async void UpdateCheckWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TitleText.Text = "Getting things ready…";
            StatusText.Text = "Checking for updates…";
            _progress = 0;
            _progressTarget = 90; // while we’re connecting
            _progressTimer.Start();

            bool success;
            try
            {
                using var cts = new CancellationTokenSource(ConnectTimeout);
                _manifest = await _service.FetchManifestAsync(ManifestUrl, cts.Token);
                success = true;
            }
            catch
            {
                success = false;
            }

            _checkingDone = true;

            if(!success)
            {
                // Connection failed: keep splash open, show error + Close button
                _progressTarget = Math.Max(_progress, 92); // give a near-full “completed” look
                await CompleteBarAsync();
                TitleText.Text = "We hit a snag";
                StatusText.Text = "Couldn’t verify updates.";
                ErrorText.Visibility = Visibility.Visible;
                CloseBtn.Visibility = Visibility.Visible;
                UpdateBtn.Visibility = Visibility.Collapsed;
                return; // leave window up; user decides to close
            }

            // Success: decide based on version
            var current = Assembly.GetEntryAssembly()!.GetName().Version ?? new Version(0, 0, 0, 0);
            if(_manifest!.Version > current)
            {
                // Update available
                _progressTarget = 100;
                await CompleteBarAsync();

                TitleText.Text = $"Update available: v{_manifest.Version}";
                StatusText.Text = string.IsNullOrWhiteSpace(_manifest.ReleaseNotes)
                    ? "A new version is ready to install."
                    : _manifest.ReleaseNotes;

                ErrorText.Visibility = Visibility.Collapsed;
                CloseBtn.Visibility = Visibility.Visible;   // user can bail
                UpdateBtn.Visibility = Visibility.Visible;  // or install
            }
            else
            {
                // No update -> continue into the app
                _progressTarget = 100;
                await CompleteBarAsync();
                DialogResult = true;
                Close();
            }
        }

        private async Task CompleteBarAsync()
        {
            _progressTarget = 100;
            // let the easing tick catch up for a short beat
            await Task.Delay(300);
            _progressTimer.Stop();
            Bar.Value = 100;
            PercentText.Text = "100%";
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if(_manifest == null) return;

            try
            {
                UpdateBtn.IsEnabled = false;
                CloseBtn.IsEnabled = false;

                TitleText.Text = "Downloading update…";
                StatusText.Text = "";
                ErrorText.Visibility = Visibility.Collapsed;

                _progress = 0;
                _progressTarget = 10;
                _progressTimer.Start();

                string? downloaded = null;
                downloaded = await _service.DownloadAsync(
                    _manifest.Url,
                    _manifest.Sha256,
                    p =>
                    {
                        // Map 10..100 to download progress
                        var mapped = 10 + (p * 90.0);
                        _progressTarget = Math.Max(mapped, _progressTarget);
                    },
                    CancellationToken.None);

                await CompleteBarAsync();

                TitleText.Text = "Launching installer…";
                StatusText.Text = "SimTools will close during the update.";
                _service.RunInstaller(downloaded);

                // Tell App to exit while installer runs
                DialogResult = false;
                Close();
            }
            catch(Exception ex)
            {
                _progressTimer.Stop();
                TitleText.Text = "Update failed";
                StatusText.Text = ex.Message;
                ErrorText.Text = "Something went wrong while downloading. Please try again later.";
                ErrorText.Visibility = Visibility.Visible;
                CloseBtn.Visibility = Visibility.Visible;
                UpdateBtn.IsEnabled = true;
                CloseBtn.IsEnabled = true;
            }
        }

        private void CloseBtn_Click(object? sender, RoutedEventArgs e)
        {
            // User chooses to close; App will see false and exit.
            DialogResult = false;
            Close();
        }
    }
}

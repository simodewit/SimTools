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
        // Use GitHub Pages for the manifest
        private const string ManifestUrl =
#if DEBUG
            "https://simodewit.github.io/SimTools/update-dev.json"; // optional dev feed
#else
            "https://simodewit.github.io/SimTools/update.json";     // prod feed
#endif

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);

        private readonly UpdateService _service = new();
        private UpdateManifest? _manifest;

        private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> Completion => _tcs.Task;

        private readonly DispatcherTimer _progressTimer;
        private double _progress;       // 0..100
        private double _progressTarget; // ease towards

        public UpdateCheckWindow()
        {
            InitializeComponent();

            _progressTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            Loaded += UpdateCheckWindow_Loaded;
            Closed += (_, __) => _progressTimer.Stop();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            const double ease = 0.12;
            var delta = (_progressTarget - _progress) * ease;
            if(Math.Abs(delta) < 0.1) delta = Math.Sign(delta) * 0.1;
            _progress = Math.Clamp(_progress + delta, 0, 100);
            Bar.Value = _progress;
            PercentText.Text = $"{(int)_progress}%";
        }

        private async void UpdateCheckWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Topmost = true; Topmost = false;

            TitleText.Text = "Getting things ready…";
            StatusText.Text = "Checking for updates…";
            _progress = 0;
            _progressTarget = 90;
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

            if(!success)
            {
                _progressTarget = Math.Max(_progress, 92);
                await CompleteBarAsync();
                TitleText.Text = "We hit a snag";
                StatusText.Text = "Couldn’t verify updates.";
                ErrorText.Visibility = Visibility.Visible;
                CloseBtn.Visibility = Visibility.Visible;
                UpdateBtn.Visibility = Visibility.Collapsed;
                return;
            }

            var current = Assembly.GetEntryAssembly()!.GetName().Version ?? new Version(0, 0, 0, 0);
            if(_manifest!.Version > current)
            {
                _progressTarget = 100;
                await CompleteBarAsync();

                TitleText.Text = $"Update available: v{_manifest.Version}";
                StatusText.Text = string.IsNullOrWhiteSpace(_manifest.ReleaseNotes)
                    ? "A new version is ready to install."
                    : _manifest.ReleaseNotes;

                ErrorText.Visibility = Visibility.Collapsed;
                CloseBtn.Visibility = Visibility.Visible;
                UpdateBtn.Visibility = Visibility.Visible;
            }
            else
            {
                _progressTarget = 100;
                await CompleteBarAsync();
                _tcs.TrySetResult(true); // continue into app
                Close();
            }
        }

        private async Task CompleteBarAsync()
        {
            _progressTarget = 100;
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

                var downloaded = await _service.DownloadAsync(
                    _manifest.Url,
                    _manifest.Sha256,
                    p =>
                    {
                        var mapped = 10 + (p * 90.0); // 10..100
                        _progressTarget = Math.Max(mapped, _progressTarget);
                    },
                    CancellationToken.None);

                await CompleteBarAsync();

                TitleText.Text = "Launching installer…";
                StatusText.Text = "SimTools will close during the update.";
                _service.RunInstaller(downloaded);

                _tcs.TrySetResult(false); // tell App to exit while installer runs
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
            _tcs.TrySetResult(false); // user chose to exit
            Close();
        }
    }
}

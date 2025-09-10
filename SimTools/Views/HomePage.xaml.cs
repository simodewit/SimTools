using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SimTools.Views
{
    public partial class HomePage : UserControl
    {
        private const string DiscordUrl = "https://discord.gg/qTUgra7h";
        private const string WebsiteUrl = "https://your-website.com";

        public HomePage()
        {
            InitializeComponent();
        }

        private static void OpenUrl(string url)
        {
            if(string.IsNullOrWhiteSpace(url)) return;

            try
            {
                var psi = new ProcessStartInfo { FileName = url, UseShellExecute = true };
                Process.Start(psi);
            }
            catch { }
        }

        private void Discord_Click(object sender, RoutedEventArgs e) => OpenUrl(DiscordUrl);
        private void Website_Click(object sender, RoutedEventArgs e) => OpenUrl(WebsiteUrl);

        private void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var url = btn?.Tag as string;
            OpenUrl(url ?? string.Empty);
        }

        // Ensures the races panel starts at the top
        private void RacesScroll_Loaded(object sender, RoutedEventArgs e)
        {
            if(sender is ScrollViewer sv) sv.ScrollToHome();
        }
    }
}

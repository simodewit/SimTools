using System.Diagnostics;
using System.Windows.Controls;
// using System.Windows.Navigation; // <-- remove

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
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { /* optionally log */ }
        }

        // Removed: NewsLink_RequestNavigate(...)

        void OpenLink_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var url = btn?.Tag as string;
            OpenUrl(url ?? string.Empty);
        }

        private void Discord_Click(object sender, System.Windows.RoutedEventArgs e) => OpenUrl(DiscordUrl);
        private void Website_Click(object sender, System.Windows.RoutedEventArgs e) => OpenUrl(WebsiteUrl);
    }
}

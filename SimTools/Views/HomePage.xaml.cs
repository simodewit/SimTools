using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

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

        private void NewsLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(e.Uri.AbsoluteUri); } catch { }
            e.Handled = true;
        }

        private void OpenLink_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var btn = sender as Button;
            var url = btn != null ? btn.Tag as string : null;
            if (!string.IsNullOrWhiteSpace(url))
            {
                try { Process.Start(url); } catch { }
            }
        }

        private void Discord_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try { Process.Start(DiscordUrl); } catch { }
        }

        private void Website_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try { Process.Start(WebsiteUrl); } catch { }
        }
    }
}

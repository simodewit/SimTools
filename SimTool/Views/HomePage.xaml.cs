using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SimTools.Views
{
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private void NewsLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(e.Uri.AbsoluteUri);
            }
            catch
            {
                // ignore
            }
            e.Handled = true;
        }
    }
}

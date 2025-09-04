using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;

namespace SimTools.Views
{
    public partial class ScreenPickerWindow : Window
    {
        public sealed class ScreenItem
        {
            public Forms.Screen Screen { get; }
            public string Display { get; }
            public ScreenItem(Forms.Screen s)
            {
                Screen = s;
                var b = s.Bounds; // device px
                var primary = s.Primary ? " (Primary)" : "";
                Display = $"{s.DeviceName.Replace(@"\\.\DISPLAY", "Display ")}{primary} — {b.Width}×{b.Height} @ ({b.X},{b.Y})";
            }
        }

        public ScreenItem Selected => ScreenList.SelectedItem as ScreenItem;

        public ObservableCollection<ScreenItem> Screens { get; } = new();

        public ScreenPickerWindow()
        {
            InitializeComponent();

            foreach(var s in Forms.Screen.AllScreens)
                Screens.Add(new ScreenItem(s));

            ScreenList.ItemsSource = Screens;
            ScreenList.SelectedItem = Screens.FirstOrDefault(si => si.Screen.Primary) ?? Screens.FirstOrDefault();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if(Selected == null) { DialogResult = false; return; }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

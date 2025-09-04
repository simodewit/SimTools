using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace SimTools.Views
{
    public partial class ScreenPickerWindow : Window
    {
        public sealed class ScreenItem
        {
            public Forms.Screen Screen { get; }
            public string Display { get; }
            public string Details { get; }
            public ScreenItem(Forms.Screen s)
            {
                Screen = s;
                var b = s.Bounds;
                var primary = s.Primary ? "Primary" : "Secondary";
                Display = $"{s.DeviceName.Replace(@"\\.\DISPLAY", "Display ")}";
                Details = $"{primary}  •  {b.Width}×{b.Height}  •  ({b.X}, {b.Y})";
            }
        }

        public ScreenItem Selected => ScreenList.SelectedItem as ScreenItem;
        public ObservableCollection<ScreenItem> Screens { get; } = new();

        public ScreenPickerWindow()
        {
            InitializeComponent();

            foreach (var s in Forms.Screen.AllScreens)
                Screens.Add(new ScreenItem(s));

            ScreenList.ItemsSource = Screens;
            ScreenList.SelectedItem = Screens.FirstOrDefault(si => si.Screen.Primary) ?? Screens.FirstOrDefault();
        }

        private void TitleRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) { DialogResult = false; return; }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}

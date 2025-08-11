using System.Windows;
using System.Windows.Input;

namespace SimTools.Views
{
    public partial class InputCaptureDialog : Window
    {
        public string Device { get; private set; }       // "Keyboard" / "Mouse"
        public string DeviceKey { get; private set; }    // "Ctrl+K" / "Left Button"
        public ModifierKeys Modifiers { get; private set; }
        public Key Key { get; private set; }
        public bool Accepted { get; private set; }

        public InputCaptureDialog()
        {
            InitializeComponent();
            KeyDown += OnKeyDown;
            MouseDown += OnMouseDown;
            Loaded += (s, e) => Activate();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            // Ignore pure modifier keys as "key"
            if (key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt || key == Key.LWin || key == Key.RWin)
            {
                // wait for a non-modifier
                return;
            }

            Device = "Keyboard";
            Modifiers = Keyboard.Modifiers;
            Key = key;

            // Build display like "Ctrl+Alt+K"
            string mods = "";
            if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control) mods += "Ctrl+";
            if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) mods += "Shift+";
            if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) mods += "Alt+";
            DeviceKey = string.IsNullOrEmpty(mods) ? key.ToString() : (mods + key);

            Status.Text = "Captured: " + Device + " — " + DeviceKey;

            Accepted = true;
            DialogResult = true;
            Close();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && (e.Source is FrameworkElement fe) && fe.Name == "Cancel")
                return; // ignore click on Cancel border

            Device = "Mouse";
            DeviceKey = e.ChangedButton.ToString() + " Button";
            Modifiers = ModifierKeys.None;
            Key = Key.None;

            Status.Text = "Captured: " + Device + " — " + DeviceKey;

            Accepted = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

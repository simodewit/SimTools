using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimTools.Models
{
    // Represents a UI overlay item with position (X, Y) and a Type label.
    // Implements INotifyPropertyChanged so the UI updates when values change.
    // Used by the overlay editor/window to place indicators on screen.

    public class OverlayElement : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private string _type = "MappingIndicator";

        public double X { get { return _x; } set { if (_x != value) { _x = value; OnPropertyChanged(); } } }
        public double Y { get { return _y; } set { if (_y != value) { _y = value; OnPropertyChanged(); } } }
        public string Type { get { return _type; } set { if (_type != value) { _type = value; OnPropertyChanged(); } } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }
}

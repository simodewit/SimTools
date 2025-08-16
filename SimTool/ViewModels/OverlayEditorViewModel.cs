using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SimTools.ViewModels
{
    // Minimal keybind bridge you can replace with your real service.
    public interface IKeybindResolver
    {
        // Return a short, user-facing string for the action id.
        // e.g., "Maps.Previous" -> "Ctrl+Alt+Up"
        string GetDisplayForAction(string actionId);
    }

    internal sealed class NullKeybindResolver : IKeybindResolver
    {
        public static readonly NullKeybindResolver Instance = new NullKeybindResolver();
        public string GetDisplayForAction(string actionId) { return "(unassigned)"; }
    }

    // Lets the tool provide names of the "above/below" maps without the VM
    // knowing your Map type or a Maps list.
    public interface IMapNeighborResolver
    {
        // Return null when there is no previous/next (top/bottom).
        string GetPrevMapName(AppState state);
        string GetNextMapName(AppState state);
    }

    internal sealed class NullMapNeighborResolver : IMapNeighborResolver
    {
        public static readonly NullMapNeighborResolver Instance = new NullMapNeighborResolver();
        public string GetPrevMapName(AppState state) { return null; }
        public string GetNextMapName(AppState state) { return null; }
    }

    // ViewModel for the Overlay Editor.
    // - Drives which centered panel is shown (General vs Keybinds)
    // - Exposes Current/Prev/Next map names
    // - Exposes keybind display strings
    // - Holds overlay elements (kept but not rendered right now)
    public class OverlayEditorViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        public RelayCommand OpenOverlay { get; private set; }

        public ObservableCollection<OverlayElement> OverlayElements { get; private set; }

        private OverlayElement _selectedElement;
        public OverlayElement SelectedElement
        {
            get { return _selectedElement; }
            set
            {
                if (!object.ReferenceEquals(_selectedElement, value))
                {
                    _selectedElement = value;
                    Raise(nameof(SelectedElement));
                }
            }
        }

        // ---- Tab state: true when "Keybinds" tab is active
        private bool _isKeybindsActive;
        public bool IsKeybindsActive
        {
            get { return _isKeybindsActive; }
            set
            {
                if (_isKeybindsActive != value)
                {
                    _isKeybindsActive = value;
                    Raise(nameof(IsKeybindsActive));
                }
            }
        }

        // ---- KEYBINDS ----
        private IKeybindResolver _keybinds = NullKeybindResolver.Instance;

        public void SetKeybindResolver(IKeybindResolver resolver)
        {
            _keybinds = resolver ?? NullKeybindResolver.Instance;
            Raise(nameof(PrevMapKeyDisplay));
            Raise(nameof(NextMapKeyDisplay));
        }

        public string PrevMapKeyDisplay { get { return _keybinds.GetDisplayForAction("Maps.Previous"); } }
        public string NextMapKeyDisplay { get { return _keybinds.GetDisplayForAction("Maps.Next"); } }

        // ---- MAP NEIGHBORS (Above/Current/Below) via injectable resolver ----
        private IMapNeighborResolver _neighbors = NullMapNeighborResolver.Instance;

        public void SetMapNeighborResolver(IMapNeighborResolver resolver)
        {
            _neighbors = resolver ?? NullMapNeighborResolver.Instance;
            Raise(nameof(PrevMapName));
            Raise(nameof(NextMapName));
        }

        public string CurrentMapName
        {
            get
            {
                return (State != null && State.CurrentMap != null && !string.IsNullOrEmpty(State.CurrentMap.Name))
                    ? State.CurrentMap.Name
                    : "(no map)";
            }
        }

        public string PrevMapName
        {
            get
            {
                var name = _neighbors.GetPrevMapName(State);
                return string.IsNullOrEmpty(name) ? "(top)" : name;
            }
        }

        public string NextMapName
        {
            get
            {
                var name = _neighbors.GetNextMapName(State);
                return string.IsNullOrEmpty(name) ? "(bottom)" : name;
            }
        }

        public OverlayEditorViewModel(AppState state)
        {
            State = state;
            OverlayElements = new ObservableCollection<OverlayElement>();

            OpenOverlay = new RelayCommand(delegate
            {
                var win = new Views.OverlayWindow { DataContext = this };
                win.Show();
            });

            // Optional: add a default element for future layout tab (currently not visible)
            if (OverlayElements.Count == 0)
            {
                AddElement("MapSwitcher", 40, 40);
            }

            // If AppState implements INotifyPropertyChanged, react to changes.
            var inpc = State as INotifyPropertyChanged;
            if (inpc != null)
            {
                inpc.PropertyChanged += delegate (object sender, PropertyChangedEventArgs args)
                {
                    RefreshAll();
                };
            }
        }

        // Call this if maps or keybinds change.
        public void RefreshAll()
        {
            Raise(nameof(CurrentMapName));
            Raise(nameof(PrevMapName));
            Raise(nameof(NextMapName));
            Raise(nameof(PrevMapKeyDisplay));
            Raise(nameof(NextMapKeyDisplay));
        }

        // Still useful for programmatically adding elements (e.g., from menus/buttons later)
        public void AddElement(string type, double x, double y)
        {
            var el = new OverlayElement { Type = type, X = x, Y = y };
            OverlayElements.Add(el);
            SelectedElement = el; // auto-select when added
        }

        public void SelectElement(OverlayElement element)
        {
            SelectedElement = element;
        }
    }
}

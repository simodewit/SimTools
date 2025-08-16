using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;
using System.Collections.ObjectModel;

namespace SimTools.ViewModels
{
    // ViewModel for the Overlay Editor.
    // - Holds overlay elements
    // - Opens the live OverlayWindow with this VM as DataContext
    // - Exposes CurrentMapName, Refresh()
    // - Exposes SelectedElement for the centered settings panel
    public class OverlayEditorViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        public RelayCommand OpenOverlay { get; private set; }

        public ObservableCollection<OverlayElement> OverlayElements { get; private set; }

        private OverlayElement _selectedElement;
        public OverlayElement SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (_selectedElement != value)
                {
                    _selectedElement = value;
                    Raise(nameof(SelectedElement));
                }
            }
        }

        public OverlayEditorViewModel(AppState state)
        {
            State = state;
            OverlayElements = new ObservableCollection<OverlayElement>();

            OpenOverlay = new RelayCommand(() =>
            {
                var win = new Views.OverlayWindow { DataContext = this };
                win.Show();
            });
        }

        public string CurrentMapName => State.CurrentMap != null ? State.CurrentMap.Name : "(no map)";

        public void Refresh() => Raise(nameof(CurrentMapName));

        // Still useful for programmatically adding elements (e.g., from menus/buttons later)
        public void AddElement(string type, double x, double y)
        {
            var el = new OverlayElement { Type = type, X = x, Y = y };
            OverlayElements.Add(el);
            SelectedElement = el; // optional: auto-select when added
        }

        public void SelectElement(OverlayElement element)
        {
            SelectedElement = element;
        }
    }
}

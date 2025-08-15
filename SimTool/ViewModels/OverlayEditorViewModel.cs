using SimTools.Helpers;
using SimTools.Models;
using SimTools.Services;
using System.Collections.ObjectModel;

namespace SimTools.ViewModels
{
    // ViewModel for the Overlay Editor.
    // Holds overlay elements and opens the live OverlayWindow with this VM as DataContext.
    // Exposes CurrentMapName, Refresh(), and AddElement(...) for the editor UI.

    public class OverlayEditorViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        public RelayCommand OpenOverlay { get; private set; }

        public ObservableCollection<OverlayElement> OverlayElements { get; private set; }

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

        public string CurrentMapName
        {
            get { return State.CurrentMap != null ? State.CurrentMap.Name : "(no map)"; }
        }

        public void Refresh()
        {
            Raise("CurrentMapName");
        }

        public void AddElement(string type, double x, double y)
        {
            var el = new OverlayElement { Type = type, X = x, Y = y };
            OverlayElements.Add(el);
        }
    }
}

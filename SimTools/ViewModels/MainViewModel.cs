using SimTools.Helpers;
using SimTools.Services;

namespace SimTools.ViewModels
{
    public enum Page { Home, Keybinds, Overlay }

    // App-wide VM: loads/saves profiles, holds shared AppState, and handles page navigation.
    // Exposes commands (Home/Keybinds/Overlay/Save) and sets CurrentPage with the right DataContext.
    // Default start = Home page.

    public class MainViewModel : ViewModelBase
    {
        public AppState State { get; private set; }
        public StorageService Storage { get; private set; }

        private object _currentPage;
        public object CurrentPage
        {
            get { return _currentPage; }
            set { Set(ref _currentPage, value); }
        }

        private Page _selectedPage;
        public Page SelectedPage
        {
            get { return _selectedPage; }
            set { Set(ref _selectedPage, value); }
        }

        public RelayCommand NavigateHome { get; private set; }
        public RelayCommand NavigateKeybinds { get; private set; }
        public RelayCommand NavigateOverlay { get; private set; }
        public RelayCommand SaveAll { get; private set; }

        public MainViewModel()
        {
            State = new AppState();
            Storage = new StorageService();
            State.Profiles = Storage.Load();
            State.EnsureSelections();

            NavigateHome = new RelayCommand(() =>
            {
                SelectedPage = Page.Home;
                CurrentPage = CreatePage(Page.Home);
            });
            NavigateKeybinds = new RelayCommand(() =>
            {
                SelectedPage = Page.Keybinds;
                CurrentPage = CreatePage(Page.Keybinds);
            });
            NavigateOverlay = new RelayCommand(() =>
            {
                SelectedPage = Page.Overlay;
                CurrentPage = CreatePage(Page.Overlay);
            });
            SaveAll = new RelayCommand(() => Storage.Save(State.Profiles));

            SelectedPage = Page.Home;
            CurrentPage = CreatePage(Page.Home);
        }

        private object CreatePage(Page page)
        {
            switch (page)
            {
                case Page.Keybinds:
                    return new Views.KeybindsPage { DataContext = new KeybindsViewModel(State, Storage) };
                case Page.Overlay:
                    return new Views.OverlayEditorPage { DataContext = new OverlayEditorViewModel(State) };
                default:
                    return new Views.HomePage { DataContext = new HomeViewModel() };
            }
        }
    }
}

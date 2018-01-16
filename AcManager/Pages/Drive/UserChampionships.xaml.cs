using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.DiscordRpc;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Drive {
    public partial class UserChampionships : ILoadableContent {
        public Task LoadAsync(CancellationToken cancellationToken) {
            return UserChampionshipsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            UserChampionshipsManager.Instance.EnsureLoaded();
        }

        private ViewModel Model => (ViewModel)DataContext;
        private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(10, "Preparing to race", "Championships").Default();

        public void Initialize() {
            this.OnActualUnload(_discordPresence);
            DataContext = new ViewModel(Filter.Create(AcObjectTester.Instance, "enabled+"));
            InitializeComponent();
        }

        private ScrollViewer _scroll;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Model.Load();

            _scroll = ListBox.FindVisualChild<ScrollViewer>();
            if (_scroll != null) {
                _scroll.LayoutUpdated += ScrollLayoutUpdated;
            }
        }

        private bool _positionLoaded;

        private void ScrollLayoutUpdated(object sender, EventArgs e) {
            if (_positionLoaded) return;
            var value = ValuesStorage.Get<double>(KeyScrollValue);
            _scroll?.ScrollToHorizontalOffset(value);
            _positionLoaded = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private void OnListBoxDoubleClick(object sender, MouseButtonEventArgs e) {
            Model.SelectSeriesCommand.Execute(null);
        }

        private const string KeyScrollValue = "UserChampionships.ListBox.Scroll";

        private void OnListBoxScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scroll == null || !_positionLoaded) return;
            ValuesStorage.Set(KeyScrollValue, _scroll.HorizontalOffset);
        }

        public static void NavigateToChampionshipPage([CanBeNull] UserChampionshipObject championship) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;
            var group = mainWindow?.MenuLinkGroups.FirstOrDefault(x => x.GroupKey == "drive" && x.DisplayName == AppStrings.Main_Single);
            var links = group?.Links;
            links?.Remove(links.OfType<CustomLink>().FirstOrDefault(x => x.Source.OriginalString.StartsWith(@"/Pages/Drive/UserChampionships_SelectedPage.xaml")));

            if (championship == null) {
                mainWindow?.NavigateTo(new Uri("/Pages/Drive/UserChampionships.xaml", UriKind.RelativeOrAbsolute));
                return;
            }

            var uri = UriExtension.Create("/Pages/Drive/UserChampionships_SelectedPage.xaml?Id={0}", championship.Id);
            if (links == null) {
                LinkCommands.NavigateLink.Execute(uri, null);
                return;
            }

            var link = new CustomLink {
                DisplayName = championship.DisplayName,
                Source = uri
            };

            var index = links.FindIndex(x => x.Source.OriginalString == "/Pages/Drive/UserChampionships.xaml");
            if (index != -1) {
                links.Insert(index + 1, link);
            }

            mainWindow.NavigateTo(link.Source);
        }

        public class ViewModel : AcObjectListCollectionViewWrapperBase<UserChampionshipObject> {
            public ViewModel(IFilter<UserChampionshipObject> listFilter)
                    : base(UserChampionshipsManager.Instance, listFilter, false) {
            }

            public UserChampionshipsManager Manager { get; } = UserChampionshipsManager.Instance;

            private bool _loaded;

            public override void Load() {
                base.Load();
                if (_loaded) return;
                _loaded = true;
                Manager.PropertyChanged += OnManagerPropertyChanged;
            }

            public override void Unload() {
                base.Unload();
                if (!_loaded) return;
                _loaded = false;
                Manager.PropertyChanged -= OnManagerPropertyChanged;
            }

            private void OnManagerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(UserChampionshipsManager.Current)) {
                    MainList.MoveCurrentTo(Manager.Current);
                }
            }

            protected override string LoadCurrentId() {
                return UserChampionshipsManager.Instance.CurrentId;
            }

            protected override void SaveCurrentKey(string id) {
                UserChampionshipsManager.Instance.CurrentId = id;
            }

            protected override void OnCurrentChanged(object sender, EventArgs e) {
                base.OnCurrentChanged(sender, e);
                if (CurrentItem == null) return;
                CurrentItem.Loaded();
                _selectSeriesCommand?.RaiseCanExecuteChanged();

                FancyBackgroundManager.Instance.ChangeBackground((CurrentItem.Value as UserChampionshipObject)?.PreviewImage);
            }

            private CommandBase _selectSeriesCommand;

            public ICommand SelectSeriesCommand => _selectSeriesCommand ?? (_selectSeriesCommand = new DelegateCommand(() => {
                var career = CurrentItem?.Loaded() as UserChampionshipObject;
                if (career == null) return;
                NavigateToChampionshipPage(career);
            }, () => {
                var career = CurrentItem?.Loaded() as UserChampionshipObject;
                if (career == null) return false;
                return !career.HasErrors;
            }));
        }
    }
}

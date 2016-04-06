using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Windows;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using StringBasedFilter;

namespace AcManager.Pages.Drive {
    public partial class KunosCareer : ILoadableContent {
        public Task LoadAsync(CancellationToken cancellationToken) {
            return KunosCareerManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            KunosCareerManager.Instance.EnsureLoaded();
        }

        private KunosCareerViewModel Model => (KunosCareerViewModel)DataContext;

        public void Initialize() {
            DataContext = new KunosCareerViewModel(Filter.Create(AcObjectTester.Instance, "enabled+"));
            InitializeComponent();

            if (!KunosCareerManager.Instance.ShowIntro) return;

            var startVideo = Path.Combine(FileUtils.GetKunosCareerDirectory(AcRootDirectory.Instance.Value), "start.ogv");
            if (!File.Exists(startVideo) || !VideoViewer.IsSupported()) return;

            new VideoViewer(startVideo, @"Kunos Career").ShowDialog();
            KunosCareerManager.Instance.ShowIntro = false;
        }

        private ScrollViewer _scrollViewer;

        private void KunosCareer_OnLoaded(object sender, RoutedEventArgs e) {
            Model.Load();

            _scrollViewer = ListBox.FindVisualChild<ScrollViewer>();
            _scrollViewer?.ScrollToHorizontalOffset(ValuesStorage.GetDoubleNullable(KeyScrollValue) ?? 0d);
        }

        private void KunosCareer_OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private void ListBox_OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            Model.SelectSeriesCommand.Execute(null);
        }

        private const string KeyScrollValue = "KunosCareer.ListBox.Scroll";

        private void ListBox_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scrollViewer == null) return;
            ValuesStorage.Set(KeyScrollValue, _scrollViewer.HorizontalOffset);
        }

        public static void NavigateToCareerPage(KunosCareerObject kunosCareer) {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            var group = mainWindow?.MenuLinkGroups.FirstOrDefault(x => x.GroupKey == "drive" && x.DisplayName == "single");
            var links = group?.Links;

            if (group != null) {
                links.Remove(links.OfType<CustomLink>().FirstOrDefault());
            }

            if (kunosCareer == null) {
                mainWindow?.NavigateTo(new Uri("/Pages/Drive/KunosCareer.xaml", UriKind.RelativeOrAbsolute));
                return;
            }

            var uri = UriExtension.Create("/Pages/Drive/KunosCareer_SelectedPage.xaml?Id={0}", kunosCareer.Id);
            if (group == null) {
                LinkCommands.NavigateLink.Execute(uri, null);
                return;
            }

            var link = new CustomLink {
                DisplayName = kunosCareer.DisplayName,
                Source = uri
            };
            links.Insert(2, link);
            mainWindow.NavigateTo(link.Source);
        }

        public class KunosCareerViewModel : BaseAcObjectListCollectionViewWrapper<KunosCareerObject> {
            public KunosCareerViewModel(IFilter<KunosCareerObject> listFilter)
                    : base(KunosCareerManager.Instance, listFilter, false) {
            }

            public KunosCareerManager Manager { get; } = KunosCareerManager.Instance;

            private bool _loaded;

            public override void Load() {
                base.Load();
                if (_loaded) return;
                _loaded = true;
                Manager.PropertyChanged += Manager_PropertyChanged;
            }

            public override void Unload() {
                base.Unload();
                if (!_loaded) return;
                _loaded = false;
                Manager.PropertyChanged -= Manager_PropertyChanged;
            }

            private void Manager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(KunosCareerManager.Current)) {
                    MainList.MoveCurrentTo(Manager.Current);
                }
            }

            private Uri _selectedCareerUri;

            public Uri SelectedCareerUri {
                get { return _selectedCareerUri; }
                set {
                    if (Equals(value, _selectedCareerUri)) return;
                    _selectedCareerUri = value;
                    OnPropertyChanged();
                }
            }

            protected override string LoadCurrentId() {
                return KunosCareerManager.Instance.CurrentId;
            }

            protected override void SaveCurrentKey(string id) {
                if ((CurrentItem.Value as KunosCareerObject)?.IsAvailable == false) return;
                KunosCareerManager.Instance.CurrentId = id;
            }

            protected override void OnCurrentChanged(object sender, EventArgs e) {
                base.OnCurrentChanged(sender, e);
                if (CurrentItem == null) return;
                CurrentItem.Loaded();
                SelectSeriesCommand.OnCanExecuteChanged();
                
                FancyBackgroundManager.Instance.ChangeBackground((CurrentItem.Value as KunosCareerObject)?.PreviewImage);
            }

            private RelayCommand _selectSeriesCommand;

            public RelayCommand SelectSeriesCommand => _selectSeriesCommand ?? (_selectSeriesCommand = new RelayCommand(o => {
                NavigateToCareerPage((KunosCareerObject)CurrentItem.Loaded());
            }, o => {
                var career = CurrentItem?.Loaded() as KunosCareerObject;
                if (career == null) return false;
                return !career.HasErrors && career.IsAvailable;
            }));
        }
    }
}

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo: ILoadableContent {
        public class AcVideoViewModel : NotifyPropertyChanged {
            internal AcVideoViewModel() {
                SetFilter();
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(Video, nameof(PropertyChanged), Handler);
            }

            private void Handler(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(Video.PostProcessingFilter)) {
                    SetFilter();
                }
            }

            private void SetFilter() {
                SelectedFilter = Video.PostProcessingFilter == null ? null : PpFiltersManager.Instance.GetByAcId(Video.PostProcessingFilter);
            }

            public AcSettingsHolder.VideoSettings Video => AcSettingsHolder.Video;

            public AcLoadedOnlyCollection<PpFilterObject> Filters => PpFiltersManager.Instance.LoadedOnlyCollection;

            private PpFilterObject _selectedFilter;

            public PpFilterObject SelectedFilter {
                get { return _selectedFilter; }
                set {
                    if (Equals(value, _selectedFilter)) return;
                    _selectedFilter = value;
                    OnPropertyChanged();

                    Video.PostProcessingFilter = _selectedFilter?.AcId;
                }
            }

            private RelayCommand _manageFiltersCommand;

            public RelayCommand ManageFiltersCommand => _manageFiltersCommand ?? (_manageFiltersCommand = new RelayCommand(o => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/PpFiltersListPage.xaml", UriKind.RelativeOrAbsolute));
            }, o => AppKeyHolder.IsAllRight));
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return PpFiltersManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            PpFiltersManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new AcVideoViewModel();
        }
    }
}

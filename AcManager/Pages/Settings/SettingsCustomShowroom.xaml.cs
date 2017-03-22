using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsCustomShowroom : ILoadableContent {
        public class ViewModel : NotifyPropertyChanged {
            public class NoneShowroomInner {
                public override string ToString() => Tools.ToolsStrings.Common_None;
            }

            public static readonly object NoneShowroom = new NoneShowroomInner();

            public SettingsHolder.CustomShowroomSettings Holder => SettingsHolder.CustomShowroom;

            public ObservableCollection<object> Showrooms { get; }

            private object _selectedShowroom;

            public object SelectedShowroom {
                get { return _selectedShowroom; }
                set {
                    if (Equals(value, _selectedShowroom)) return;
                    _selectedShowroom = value;
                    Holder.ShowroomId = (value as ShowroomObject)?.Id;
                    OnPropertyChanged();
                }
            }

            internal ViewModel() {
                Showrooms = new ObservableCollection<object>(
                        ShowroomsManager.Instance.EnabledOnlyCollection.OrderBy(x => x.DisplayName).Prepend(NoneShowroom));
                SelectedShowroom = Holder.ShowroomId == null ? NoneShowroom : ShowroomsManager.Instance.GetById(Holder.ShowroomId);
            }

            private DelegateCommand _resetCommand;

            public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
                ValuesStorage.Remove("__DarkRendererSettings");
            }));
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return ShowroomsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            ShowroomsManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new ViewModel();
        }
    }
}

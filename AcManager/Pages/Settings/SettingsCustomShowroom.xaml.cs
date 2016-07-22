using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsCustomShowroom : ILoadableContent {
        public class ViewModel : NotifyPropertyChanged {
            public class NoneShowroom {
                public override string ToString() => Tools.Resources.Common_None;
            }

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
                        ShowroomsManager.Instance.LoadedOnlyCollection.OrderBy(x => x.DisplayName).Prepend((object)new NoneShowroom()));
                SelectedShowroom = Holder.ShowroomId == null ? (object)new NoneShowroom() : ShowroomsManager.Instance.GetById(Holder.ShowroomId);
            }
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

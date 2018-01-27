using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Pages.ContentTools {
    public partial class CupList {
        protected override async Task<bool> LoadOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            progress.Report("Loading list of updates…", 0.01);
            await CupClient.Instance.LoadRegistries();

            var list = CupClient.Instance.List.Select(x => {
                var m = CupClient.Instance.GetAssociatedManager(x.Key.Type);
                return (ICupSupportedObject)m?.GetObjectById(x.Key.Id);
            }).Where(x => x?.IsCupUpdateAvailable == true).OrderBy(x => x.CupContentType).ToList();
            ItemsToUpdate = new BetterObservableCollection<ICupSupportedObject>(list);
            SelectedItem = list.FirstOrDefault();
            return list.Count > 0;
        }

        protected override void InitializeOverride(Uri uri) {}

        private BetterObservableCollection<ICupSupportedObject> _itemsToUpdate;

        public BetterObservableCollection<ICupSupportedObject> ItemsToUpdate {
            get => _itemsToUpdate;
            set {
                if (Equals(value, _itemsToUpdate)) return;
                _itemsToUpdate = value;
                OnPropertyChanged();
            }
        }

        private ICupSupportedObject _selectedItem;

        public ICupSupportedObject SelectedItem {
            get => _selectedItem;
            set {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;
                OnPropertyChanged();
            }
        }
    }
}

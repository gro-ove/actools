using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.ContentTools {
    public partial class CupList {
        protected override async Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
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
            set => this.Apply(value, ref _itemsToUpdate);
        }

        private ICupSupportedObject _selectedItem;

        public ICupSupportedObject SelectedItem {
            get => _selectedItem;
            set => this.Apply(value, ref _selectedItem);
        }
    }
}

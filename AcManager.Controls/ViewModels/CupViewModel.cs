using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.ViewModels {
    public class CupViewModel : NotifyPropertyChanged {
        public static CupViewModel Instance { get; } = new CupViewModel();

        private readonly Queue<CupEventArgs> _cupToProcess = new Queue<CupEventArgs>();
        private bool _cupProcessing;

        private ChangeableObservableCollection<ICupSupportedObject> CupSupportedObjects { get; } = new ChangeableObservableCollection<ICupSupportedObject>();

        public BetterListCollectionView ToUpdate { get; }

        public event EventHandler<EventArgs> NewUpdate;

        private CupViewModel() {
            CupSupportedObjects.ItemPropertyChanged += OnCupSupportedObjectPropertyChanged;
            ToUpdate = new BetterListCollectionView(CupSupportedObjects) {
                Filter = x => (x as ICupSupportedObject)?.IsCupUpdateAvailable == true
            };
            ToUpdate.SortDescriptions.Add(new SortDescription(nameof(AcObjectNew.DisplayName), ListSortDirection.Ascending));
        }

        private void OnCupSupportedObjectPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(ICupSupportedObject.IsCupUpdateAvailable)) {
                ToUpdate.Refresh();
            }
        }

        public static void Initialize() {
            if (CupClient.Instance != null) {
                foreach (var item in CupClient.Instance.List) {
                    Instance.AddItem(new CupEventArgs(item.Key, item.Value));
                }
                CupClient.Instance.NewLatestVersion += Instance.OnNewUpdateInformation;
            }
        }

        private void OnNewUpdateInformation(object sender, CupEventArgs e) {
            AddItem(e);
        }

        private async Task RunCupItem(CupEventArgs e) {
            var manager = CupClient.Instance?.GetAssociatedManager(e.Key.Type);
            if (manager == null) return;
            if (await manager.GetObjectByIdAsync(e.Key.Id) is ICupSupportedObject obj && !CupSupportedObjects.Contains(obj)) {
                CupSupportedObjects.Add(obj);
                if (obj.IsCupUpdateAvailable) {
                    NewUpdate?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private AsyncCommand _installAllCommand;

        public AsyncCommand InstallAllCommand => _installAllCommand ?? (_installAllCommand = new AsyncCommand(() => ToUpdate.OfType<ICupSupportedObject>()
                .Select(x => CupClient.Instance?.InstallUpdateAsync(x.CupContentType, x.Id)).NonNull().WhenAll(4)));

        private async Task RunCupProcessing() {
            _cupProcessing = true;
            while (_cupToProcess.Count > 0) {
                await RunCupItem(_cupToProcess.Dequeue());
            }
            _cupProcessing = false;
        }

        private void AddItem(CupEventArgs e) {
            _cupToProcess.Enqueue(e);
            if (!_cupProcessing) {
                RunCupProcessing().Ignore();
            }
        }
    }
}
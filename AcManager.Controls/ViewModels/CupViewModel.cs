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
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.ViewModels {
    public class CupViewModel : NotifyPropertyChanged {
        public static CupViewModel Instance { get; } = new CupViewModel();

        private readonly Queue<CupEventArgs> _cupToProcess = new Queue<CupEventArgs>();
        private readonly Busy _refreshBusy = new Busy();
        private readonly Dictionary<CupContentType, bool> _listening = new Dictionary<CupContentType, bool>();
        private bool _cupProcessing;

        private ChangeableObservableCollection<ICupSupportedObject> CupSupportedObjects { get; } = new ChangeableObservableCollection<ICupSupportedObject>();

        public BetterListCollectionView ToUpdate { get; }

        public event EventHandler<EventArgs> NewUpdate;

        private CupViewModel() {
            DelayCupProcessing().Ignore();
            CupSupportedObjects.ItemPropertyChanged += OnCupSupportedObjectPropertyChanged;
            ToUpdate = new BetterListCollectionView(CupSupportedObjects) {
                Filter = x => (x as ICupSupportedObject)?.IsCupUpdateAvailable == true
            };
            ToUpdate.SortDescriptions.Add(new SortDescription(nameof(AcObjectNew.DisplayName), ListSortDirection.Ascending));
        }

        private void RefreshToUpdate() {
            _refreshBusy.DoDelay(() => {
                for (var i = CupSupportedObjects.Count - 1; i >= 0; --i) {
                    var entry = CupSupportedObjects[i];
                    var manager = CupClient.Instance?.GetAssociatedManager(entry.CupContentType, true);
                    var existing = manager?.GetWrapperById(entry.Id);
                    if (existing == null || !existing.IsLoaded) {
                        CupSupportedObjects.RemoveAt(i);
                    }
                }
            }, 100);
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
            var client = CupClient.Instance;
            if (client == null) {
                return;
            }
            var manager = client.GetAssociatedManager(e.Key.Type, false);
            if (manager == null) {
                if (client.IsPostponed(e.Key.Type)) {
                    Task.Delay(3000).ContinueWithInMainThread(r => AddItem(e)).Ignore();
                }
                return;
            }
            // Logging.Debug($"ID: {e.Key.Id}, manager: {manager}, item: {await manager.GetObjectByIdAsync(e.Key.Id)}");
            if (await manager.GetObjectByIdAsync(e.Key.Id) is ICupSupportedObject obj && !CupSupportedObjects.Contains(obj)) {
                CupSupportedObjects.Add(obj);
                if (obj.IsCupUpdateAvailable) {
                    if (!_listening.ContainsKey(e.Key.Type)) {
                        _listening[e.Key.Type] = true;
                        manager.WrappersAsIList.CollectionChanged += (sender, args) => RefreshToUpdate();
                        manager.WrappersAsIList.WrappedValueChanged += (sender, args) => RefreshToUpdate();
                    }
                    NewUpdate?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private AsyncCommand _installAllCommand;

        public AsyncCommand InstallAllCommand => _installAllCommand ?? (_installAllCommand = new AsyncCommand(() => ToUpdate.OfType<ICupSupportedObject>()
                .Select(x => CupClient.Instance?.InstallUpdateAsync(x.CupContentType, x.Id)).NonNull().WhenAll(4)));

        private async Task DelayCupProcessing() {
            _cupProcessing = true;
            await Task.Delay(5000);
            await RunCupProcessing();
        }

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
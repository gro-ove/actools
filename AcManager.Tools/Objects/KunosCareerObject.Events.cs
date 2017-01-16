using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class KunosCareerObject : IAcManagerScanWrapper {
        /* for UI car’s skins manager */
        public KunosCareerEventsManager EventsManager { get; private set; }

        public IAcWrapperObservableCollection EventsWrappers => EventsManager.WrappersList;

        public IEnumerable<KunosCareerEventObject> Events => EventsManager.LoadedOnly;

        public AcEnabledOnlyCollection<KunosCareerEventObject> EnabledOnlyEvents => EventsManager.EnabledOnlyCollection;

        private KunosCareerEventObject _selectedEvent;
        private bool _selectedEventSkipSaving;

        [CanBeNull]
        public KunosCareerEventObject SelectedEvent {
            get {
                if (!EventsManager.IsScanned) {
                    EventsManager.Scan();
                }
                return _selectedEvent;
            }
            set {
                if (Equals(value, _selectedEvent)) return;
                _selectedEvent = value;
                OnPropertyChanged();

                if (_selectedEventSkipSaving || _selectedEvent == null) return;
                SaveProgress(false);
            }
        }

        private void SelectPreviousOrDefaultEvent() {
            _selectedEventSkipSaving = true;
            var number = KunosCareerProgress.Instance.Entries.GetValueOrDefault(Id)?.SelectedEvent;
            SelectedEvent = (number.HasValue ? EventsManager.GetByNumber(number.Value) : null) ?? EventsManager.GetDefault();
            _selectedEventSkipSaving = false;
        }

        void IAcManagerScanWrapper.AcManagerScan() {
            EventsManager.ActualScan();
            SelectPreviousOrDefaultEvent();
        }

        public void EnsureEventsLoaded() {
            EventsManager.EnsureLoaded();
        }

        public async Task EnsureEventsLoadedAsync() {
            await EventsManager.EnsureLoadedAsync();
        }

        public bool IsEventsScanned => EventsManager.IsScanned;

        public bool IsEventsLoaded => EventsManager.IsLoaded;

        [CanBeNull]
        public KunosCareerEventObject GetEventById([NotNull]string skinId) {
            return EventsManager.GetById(skinId);
        }

        [CanBeNull]
        public KunosCareerEventObject GetFirstEventOrNull() {
            return EventsManager.GetFirstOrNull();
        }
    }
}

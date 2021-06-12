using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public class CarSetupValues : NotifyPropertyChanged, IDisposable {
        private static string SavedKeyToSetupKey(string savedKey) {
            if (savedKey.StartsWith(@"INTERNAL_GEAR_")) {
                var gear = savedKey.Substring(@"INTERNAL_GEAR_".Length).As<int>();
                return $@"GEAR_{gear - 1}";
            }

            return savedKey;
        }

        private static string SetupKeyToSavedKey(string setupKey) {
            if (setupKey.StartsWith(@"GEAR_")) {
                var gear = setupKey.Substring(@"GEAR_".Length).As<int>();
                return $@"INTERNAL_GEAR_{gear + 1}";
            }

            return setupKey;
        }

        public sealed class SetupEntriesTab : Displayable {
            public SetupEntriesTab(string key, CarSetupEntry[] entries, AcLocaleProvider localeProvider) {
                Key = key;
                DisplayName = CarSetupObject.FixEntryName(localeProvider.GetString("TABS", key) ?? key, true);
                Entries = entries;
            }

            public string Key { get; }

            public CarSetupEntry[] Entries { get; }
        }

        [NotNull]
        private readonly ChangeableObservableCollection<CarSetupEntry> _entries;

        public SetupEntriesTab[] Tabs { get; }
        public SettingEntry[] Tyres { get; }
        private SettingEntry _selectedTyres;

        [CanBeNull]
        public SettingEntry SelectedTyres {
            get => _selectedTyres;
            set {
                if (!Tyres.ArrayContains(value)) value = Tyres[0];
                if (Equals(value, _selectedTyres)) return;
                _selectedTyres = value;
                OnPropertyChanged();
                UpdateMaxSpeed();

                if (_loading || !(_selectedObject is CarSetupObject)) return;
                try {
                    _loading = true;
                    ((CarSetupObject)_selectedObject).Tyres = value?.IntValue ?? 0;
                } finally {
                    _loading = false;
                }
            }
        }

        private readonly CarObject _car;
        private readonly ICarSetupObject _selectedObject;
        private readonly bool _hasGearEntries;

        public CarSetupValues(CarObject car, ICarSetupObject selectedObject, CarSetupValues previous) {
            _car = car;
            _selectedObject = selectedObject;

            if (previous?._entries != null) {
                _entries = previous._entries;
                Tabs = previous.Tabs;
            } else {
                var localeProvider = new AcLocaleProvider(AcRootDirectory.Instance.RequireValue, SettingsHolder.Locale.LocaleName, "setup");
                _entries = new ChangeableObservableCollection<CarSetupEntry>(car
                        .AcdData?.GetIniFile("setup.ini")
                        .Where(x => x.Value.ContainsKey(@"TAB") || x.Value.ContainsKey(@"RATIOS"))
                        .Select(x => new CarSetupEntry(x.Key, x.Value, localeProvider, car.AcdData))
                        .Where(x => x.Step != 0d)); // TODO!
                Tabs = _entries?
                        .GroupBy(x => x.TabKey)
                        .Select(x => new SetupEntriesTab(x.Key ?? @"?", x.ToArray(), localeProvider))
                        .OrderBy(x => x.DisplayName)
                        .ToArray();
            }

            _hasGearEntries = _entries.Any(x => x.Key.StartsWith(@"GEAR_"));

            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            Tyres = tyres?.GetSections("FRONT", -1).Select((x, i) => new SettingEntry(i, x.GetPossiblyEmpty("NAME"))).ToArray() ?? new SettingEntry[0];

            _entries.ItemPropertyChanged += OnEntryChanged;
            EnsureLoaded().Ignore();
        }

        private bool _isLoaded;

        public bool IsLoaded {
            get => _isLoaded;
            set => Apply(value, ref _isLoaded);
        }

        private async Task EnsureLoaded() {
            await _selectedObject.EnsureDataLoaded();
            IsLoaded = true;
            LoadValues();
            SelectedTyres = Tyres?.ArrayElementAtOrDefault(_selectedObject.Tyres ?? -1);
            _selectedObject.SubscribeWeak(Handler);
        }

        private bool _loading;

        private void LoadValues() {
            if (_loading) return;
            try {
                _loading = true;
                foreach (var entry in _entries) {
                    entry.LoadValue(_selectedObject.GetValue(SetupKeyToSavedKey(entry.Key)));
                }
            } finally {
                _loading = false;
            }
        }

        private void OnEntryChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(CarSetupEntry.Value) || _loading) return;
            try {
                _loading = true;
                var entry = (CarSetupEntry)sender;
                _selectedObject.SetValue(SetupKeyToSavedKey(entry.Key), entry.Value);

                if (entry.Key.StartsWith(@"GEAR_") || entry.Key == "FINAL_GEAR_RATIO") {
                    UpdateMaxSpeed();
                }
            } finally {
                _loading = false;
            }
        }

        private void UpdateMaxSpeed() {
            if (!_hasGearEntries) return;

            var data = _car.AcdData;
            if (data == null) return;

            CarSetupEntry.UpdateRatioMaxSpeed(_entries, data, SelectedTyres?.IntValue, true);
        }

        private void Handler(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(_selectedObject.Tyres):
                    if (!_loading) {
                        try {
                            _loading = true;
                            SelectedTyres = Tyres.ArrayElementAtOrDefault(_selectedObject.Tyres ?? -1);
                            UpdateMaxSpeed();
                        } finally {
                            _loading = false;
                        }
                    }
                    break;
                case nameof(_selectedObject.Values):
                    LoadValues();
                    UpdateMaxSpeed();
                    break;
            }
        }

        public void Dispose() {
            _entries.ItemPropertyChanged -= OnEntryChanged;
        }
    }
}
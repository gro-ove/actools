using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        public class SetupItem : NotifyPropertyChanged, IDraggable {
            public const string DraggableFormat = "X-SetupItem";

            string IDraggable.DraggableFormat => DraggableFormat;

            [NotNull]
            public string Filename { get; }

            [NotNull]
            public string CarId { get; }

            private bool _isValidCar;

            public bool IsValidCar {
                get => _isValidCar;
                set => Apply(value, ref _isValidCar);
            }

            private SetupItem([NotNull] string filename, [NotNull] string carId, bool isDefault) {
                Filename = filename ?? throw new ArgumentNullException(nameof(filename));
                CarId = carId;
                IsDefault = isDefault;

                try {
                    DisplayName = Path.GetFileNameWithoutExtension(filename);
                } catch (Exception e) {
                    Logging.Error(e);
                    DisplayName = filename;
                }
            }

            [CanBeNull]
            public static SetupItem Create(string filename, bool isDefault) {
                if (string.IsNullOrWhiteSpace(filename)) return null;
                var carId = new IniFile(filename)["CAR"].GetNonEmpty("MODEL");
                if (carId == null) return null;
                return new SetupItem(filename, carId, isDefault);
            }

            public string CarDisplayName => CarsManager.Instance.GetById(CarId)?.DisplayName ?? CarId;

            public string DisplayName { get; }

            private bool _isDefault;

            public bool IsDefault {
                get => _isDefault;
                set => Apply(value, ref _isDefault);
            }

            private bool _isDeleted;

            public bool IsDeleted {
                get => _isDeleted;
                set => Apply(value, ref _isDeleted);
            }

            private DelegateCommand _viewInDirectoryCommand;

            public DelegateCommand ViewInDirectoryCommand
                => _viewInDirectoryCommand ?? (_viewInDirectoryCommand = new DelegateCommand(() => WindowsHelper.ViewFile(Filename)));

            private DelegateCommand _deleteCommand;

            public DelegateCommand DeleteCommand
                => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => IsDeleted = true,
                        () => !IsDeleted)).ListenOnWeak(this, nameof(IsDeleted));
        }

        private class SetupsDraggableConverter : IDraggableDestinationConverter  {
            public object Convert(IDataObject data) {
                if (data.GetData(SetupItem.DraggableFormat) is SetupItem set) {
                    return set;
                }

                var files = data.GetInputFiles().ToList();
                if (files.Count > 0 && files[0] != null) {
                    return SetupItem.Create(files[0], false);
                }

                return true;
            }
        }

        public IDraggableDestinationConverter SetupsDraggableConverterInstance { get; } = new SetupsDraggableConverter();

        public ChangeableObservableCollection<SetupItem> SetupItems { get; } = new ChangeableObservableCollection<SetupItem>();

        private SetupItem _defaultSetupItem;

        public SetupItem DefaultSetupItem {
            get => _defaultSetupItem;
            set {
                var oldValue = _defaultSetupItem;
                if (Apply(value, ref _defaultSetupItem)) {
                    if (oldValue != null) oldValue.IsDefault = false;
                    if (value != null) value.IsDefault = true;

                    if (Loaded) {
                        Changed = true;
                    }
                }
            }
        }

        private void InitSetupsItems() {
            SetupItems.ItemPropertyChanged += OnSetupItemPropertyChanged;
            SetupItems.CollectionChanged += OnSetupItemsCollectionChanged;
        }

        private void RefreshSetupCarsValidity() {
            foreach (var item in SetupItems) {
                item.IsValidCar = CarIds.Any(x => string.Equals(x, item.CarId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void OnSetupItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (!SetupItems.Contains(DefaultSetupItem)) DefaultSetupItem = null;
            if (e.Action == NotifyCollectionChangedAction.Reset || e.Action == NotifyCollectionChangedAction.Add) {
                var defaultFound = false;
                foreach (var setupItem in SetupItems) {
                    if (!setupItem.IsDefault) continue;
                    if (defaultFound) {
                        setupItem.IsDefault = false;
                    } else {
                        DefaultSetupItem = setupItem;
                        defaultFound = true;
                    }
                }
            }

            if (Loaded) {
                Changed = true;
            }

            RefreshSetupCarsValidity();
        }

        private void OnSetupItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            var item = (SetupItem)sender;
            if (e.PropertyName == nameof(item.IsDeleted) && item.IsDeleted) {
                SetupItems.Remove(item);
            } else if (e.PropertyName == nameof(item.IsDefault) && item.IsDefault) {
                foreach (var setupItem in SetupItems) {
                    if (setupItem != item) {
                        setupItem.IsDefault = false;
                    }
                }
                DefaultSetupItem = item;
            }
        }
    }
}
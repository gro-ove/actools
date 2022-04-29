using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.UserControls;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetEntryList : INotifyPropertyChanged {
        public ServerPresetEntryList() {
            InitializeComponent();
            this.AddWidthCondition(720)
                    .Add(SavedDriversPanel);
            this.AddWidthCondition(960)
                    .Add(BallastColumn)
                    .Add(RestrictorColumn)
                    .AddInverted(SomethingMightBeHiddenPanel);
            this.AddWidthCondition(1080)
                    .Add(TeamColumn);

            if (AppArguments.Has(AppFlag.ServerManagementMode)) {
                EntriesGrid.RowStyle = TryFindResource("RowStyleWithSpacing") as Style ?? EntriesGrid.RowStyle;
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject ??
                    (e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry)?.Car;
            var driverEntry = e.Data.GetData(ServerPresetDriverEntry.DraggableFormat) as ServerPresetDriverEntry;
            var savedDriver = e.Data.GetData(ServerSavedDriver.DraggableFormat) as ServerSavedDriver;

            if (carObject == null && driverEntry == null && savedDriver == null || Model == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            var newIndex = ((ItemsControl)sender).GetMouseItemIndex();
            var list = Model.SelectedObject.DriverEntries;

            if (driverEntry != null) {
                list.DragAndDrop(newIndex, e.IsCopyAction() ? driverEntry.Clone() : driverEntry);
            } else if (carObject != null) {
                if (e.IsCopyAction() || e.IsSpecificAction() || newIndex == -1) {
                    list.DragAndDrop(newIndex, new ServerPresetDriverEntry(carObject));
                } else {
                    list[newIndex].CarId = carObject.Id;
                    list[newIndex].CarSkinId = carObject.SelectedSkin?.Id;
                }
            } else {
                if (e.IsCopyAction() || e.IsSpecificAction() || newIndex == -1) {
                    list.DragAndDrop(newIndex, new ServerPresetDriverEntry(savedDriver));
                } else {
                    savedDriver.CopyTo(list[newIndex]);
                }
            }

            e.Effects = DragDropEffects.Move;
        }

        private void OnOpponentCellClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                var entry = (sender as FrameworkElement)?.DataContext as RaceGridEntry;
                if (entry?.SpecialEntry != false) return;

                var control = new CarBlock {
                    Car = entry.Car,
                    SelectedSkin = entry.CarSkin ?? entry.Car.SelectedSkin,
                    SelectSkin = true,
                    OpenShowroom = true
                };

                var dialog = new ModernDialog {
                    Content = control,
                    Width = 640,
                    Height = 720,
                    MaxWidth = 640,
                    MaxHeight = 720,
                    SizeToContent = SizeToContent.Manual,
                    Title = entry.Car.DisplayName
                };

                dialog.Buttons = new[] { dialog.OkButton, dialog.CancelButton };
                dialog.ShowDialog();

                if (dialog.IsResultOk) {
                    entry.CarSkin = control.SelectedSkin;
                }
            }
        }

        private void OnItemsControlDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(ServerPresetDriverEntry.DraggableFormat) is ServerPresetDriverEntry entry) {
                if (string.IsNullOrWhiteSpace(entry.DriverName) || string.IsNullOrWhiteSpace(entry.Guid)) {
                    NonfatalError.Notify("Can’t save driver entry", "GUID and driver’s name are required.");
                } else {
                    ServerPresetsManager.Instance.StoreDriverEntry(entry);
                }
            }
        }

        private void OnCarClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;

            if (((FrameworkElement)sender).DataContext is ServerPresetDriverEntry entry) {
                var skin = entry.CarSkinObject;
                var selected = SelectCarDialog.Show(entry.CarObject, ref skin);
                if (selected != null) {
                    entry.CarObject = selected;
                    entry.CarSkinObject = skin;
                }
            }

            e.Handled = true;
        }

        private const string KeySelectedCar = ".ServerEntry.EntryList:SelectedCar";

        [CanBeNull]
        private static CarObject LoadSelected() {
            var saved = ValuesStorage.Get<string>(KeySelectedCar);
            return saved == null ? null : CarsManager.Instance.GetById(saved);
        }

        private CarObject _selectedCar;

        public CarObject SelectedCar {
            get => _selectedCar;
            set {
                if (Equals(value, _selectedCar) || value == null) return;
                _selectedCar = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeySelectedCar, value.Id);
                _addOpponentCarCommand?.RaiseCanExecuteChanged();
            }
        }

        private ICommand _closeAddingPopupCommand;

        public ICommand ClosePopupsCommand
            => _closeAddingPopupCommand ?? (_closeAddingPopupCommand = new DelegateCommand(() => { SelectCarPopup.IsOpen = false; }));

        private CommandBase _addOpponentCarCommand;

        public ICommand AddOpponentCarCommand => _addOpponentCarCommand ??
                (_addOpponentCarCommand = new DelegateCommand(AddSelected, () => SelectedCar != null));

        private void AddSelected() {
            var list = Model.SelectedObject.DriverEntries;
            var selectCar = (SelectCarPopup.Content as DependencyObject)?.FindLogicalChild<SelectCar>();
            if (list == null || selectCar == null) return;

            foreach (var car in selectCar.GetSelectedCars()) {
                list.Add(new ServerPresetDriverEntry(car));
            }
        }

        private void SelectCar_OnItemChosen(object sender, ItemChosenEventArgs<CarObject> e) {
            Model.SelectedObject.DriverEntries.Add(new ServerPresetDriverEntry(e.ChosenItem));
        }

        private void SelectCarPopup_OnOpened(object sender, EventArgs e) {
            if (SelectedCar == null) {
                SelectedCar = LoadSelected() ?? Model?.Cars.LastOrDefault() ?? CarsManager.Instance.GetDefault();
            }
        }

        private void OnSavedDriversKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None) {
                (SavedDriversList.SelectedItem as ServerSavedDriver)?.DeleteCommand.Execute();
            }
        }

        public SelectedPage.ViewModel Model => (SelectedPage.ViewModel)DataContext;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void CspTweaksResetAllClick(object sender, RoutedEventArgs e) {
            foreach (var entry in Model.SelectedObject.DriverEntries) {
                entry.CspOptions.ResetCommand.Execute();
            }
        }

        private void CspTweaksCopyToOtherCarsClick(object sender, RoutedEventArgs e) {
            // ReSharper disable once RedundantAssignment
            // ReSharper disable once InlineOutVariableDeclaration
            string result = null;
            if (((sender as FrameworkElement)?.DataContext as ServerPresetDriverEntry)?.CspOptions?.Pack(out result) == true) {
                foreach (var entry in Model.SelectedObject.DriverEntries) {
                    entry.CspOptions.LoadPacked(result);
                }
            }
        }
    }
}
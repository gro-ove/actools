using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls;
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
    public class ServerPresetDriverEntryDraggableConverter : IDraggableDestinationConverter {
        object IDraggableDestinationConverter.Convert(IDataObject data) {
            var entry = data.GetData(ServerPresetDriverEntry.DraggableFormat) as ServerPresetDriverEntry;
            if (entry != null) return entry;

            var car = data.GetData(CarObject.DraggableFormat) as CarObject;
            if (car != null) return new ServerPresetDriverEntry(car);

            var saved = data.GetData(ServerSavedDriver.DraggableFormat) as ServerSavedDriver;
            if (saved != null) return new ServerPresetDriverEntry(saved);

            return null;
        }
    }

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

        private void OnOpponentSkinClick(object sender, MouseButtonEventArgs e) {
            var entry = (sender as FrameworkElement)?.DataContext as RaceGridEntry;
            if (entry?.SpecialEntry != false) return;

            /*var dataGrid = (DetailsPopup.Content as FrameworkElement)?.FindVisualChild<DataGrid>();
            if (dataGrid != null) {
                dataGrid.SelectedItem = entry;
            }

            DetailsPopup.StaysOpen = true;*/

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

            // DetailsPopup.StaysOpen = false;
        }

        private void OnOpponentCellClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                OnOpponentSkinClick(sender, e);
            }
        }

        private void OnItemsControlDrop(object sender, DragEventArgs e) {
            var entry = e.Data.GetData(ServerPresetDriverEntry.DraggableFormat) as ServerPresetDriverEntry;
            if (entry != null) {
                if (string.IsNullOrWhiteSpace(entry.DriverName) || string.IsNullOrWhiteSpace(entry.Guid)) {
                    NonfatalError.Notify("Can’t save driver entry", "GUID and driver’s name are required.");
                } else {
                    ServerPresetsManager.Instance.StoreDriverEntry(entry);
                }
            }
        }

        private void OnCarClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;

            var entry = ((FrameworkElement)sender).DataContext as ServerPresetDriverEntry;
            if (entry != null) {
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
            var saved = ValuesStorage.GetString(KeySelectedCar);
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

        public ICommand ClosePopupsCommand => _closeAddingPopupCommand ?? (_closeAddingPopupCommand = new DelegateCommand(() => {
            SelectCarPopup.IsOpen = false;
            // DetailsPopup.IsOpen = false;
        }));

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
            //Model?.AddEntry(e.ChosenItem);
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
    }
}

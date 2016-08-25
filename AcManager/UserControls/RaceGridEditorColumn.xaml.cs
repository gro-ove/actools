using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.UserControls {
    /// <summary>
    /// Interaction logic for GridEditorColumn.xaml
    /// </summary>
    public partial class RaceGridEditorColumn {
        public RaceGridEditorColumn() {
            InputBindings.AddRange(new[] {
                new InputBinding(new RelayCommand(o => {
                    foreach (var entry in ListBox.SelectedItems.OfType<RaceGridEntry>().ToList()) {
                        entry.DeleteCommand.Execute(o);
                    }
                }), new KeyGesture(Key.Delete)),
            });
            InitializeComponent();
        }

        [CanBeNull]
        public RaceGridViewModel Model => DataContext as RaceGridViewModel;

        private ICommand _cloneSelectedCommand;

        public ICommand CloneSelectedCommand => _cloneSelectedCommand ?? (_cloneSelectedCommand = new RelayCommand(o => {
            var items = ListBox.SelectedItems.OfType<RaceGridEntry>().Select(x => x.Clone()).ToList();
            if (items.Count == 0 || Model == null) return;

            var destination = Model.NonfilteredList.IndexOf(ListBox.SelectedItems.OfType<RaceGridEntry>().Last()) + 1;
            foreach (var item in items) {
                Model.InsertEntry(destination++, item);
            }

            ListBox.SelectedItems.Clear();
            foreach (var item in items) {
                ListBox.SelectedItems.Add(item);
            }
        }));

        private ICommand _deleteSelectedCommand;

        public ICommand DeleteSelectedCommand => _deleteSelectedCommand ?? (_deleteSelectedCommand = new RelayCommand(o => {
            var items = ListBox.SelectedItems.OfType<RaceGridEntry>().ToList();
            if (items.Count == 0 || Model == null) return;
            
            foreach (var item in items) {
                Model.DeleteEntry(item);
            }
        }));

        public ICommand SavePresetCommand => Model?.SavePresetCommand;

        private ICommand _addOpponentCarCommand;

        public ICommand AddOpponentCarCommand => _addOpponentCarCommand ?? (_addOpponentCarCommand = new AsyncCommand(async o => {
            var model = Model;
            if (model == null) return;

            var dialog = new SelectCarDialog(CarsManager.Instance.GetDefault());
            await (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? dialog.ShowAndWaitAsync() : dialog.ShowDialogAsync());
            if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

            model.AddEntry(dialog.SelectedCar);
        }));

        private ICommand _setupCommand;

        public ICommand SetupCommand => _setupCommand ?? (_setupCommand = new RelayCommand(o => {
            var model = Model;
            if (model == null) return;

            new SetupRaceGridDialog(model).ShowDialog();
        }));

        [ValueConversion(typeof(bool), typeof(string))]
        private class InnerModeToLabelConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value as bool? == true ? "Candidates:" : "Opponents:";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ModeToLabelConverter = new InnerModeToLabelConverter();

        private void Item_OnPreviewDoubleClick(object sender, MouseButtonEventArgs e) {}

        private void ListBox_Drop(object sender, DragEventArgs e) {
            var destination = (ListBox)sender;
            
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null || Model == null) {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            var newIndex = destination.GetMouseItemIndex();
            if (raceGridEntry != null) {
                Model.InsertEntry(newIndex, raceGridEntry);
            } else {
                Model.InsertEntry(newIndex, carObject);
            }

            e.Effects = DragDropEffects.Move;
        }
    }
}

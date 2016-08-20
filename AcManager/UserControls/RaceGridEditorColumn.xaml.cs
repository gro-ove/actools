using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.UserControls {
    /// <summary>
    /// Interaction logic for GridEditorColumn.xaml
    /// </summary>
    public partial class RaceGridEditorColumn {
        public RaceGridEditorColumn() {
            InitializeComponent();
            // TODO: Keybinding for Delete key!
        }

        [CanBeNull]
        public RaceGridViewModel Model => DataContext as RaceGridViewModel;

        private ICommand _addOpponentCarCommand;

        public ICommand AddOpponentCarCommand => _addOpponentCarCommand ?? (_addOpponentCarCommand = new RelayCommand(o => {
            var model = Model;
            if (model == null) return;

            var dialog = new SelectCarDialog(CarsManager.Instance.GetDefault());
            dialog.ShowDialog();
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
                if (value as bool? == true) {
                    return "Candidates:";
                } else {
                    return "Opponents:";
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ModeToLabelConverter = new InnerModeToLabelConverter();

        private void Item_OnPreviewDoubleClick(object sender, MouseButtonEventArgs e) {

        }

        private static class AdditionalDataFormats {
            public const string RaceGridEntry = "Data-RaceGridEntry";
        }

        private void Item_OnPreviewMouseLeftButtonDown(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed || Model?.Mode.CandidatesMode != false) {
                return;
            }

            var item = sender as ListBoxItem;
            if (item == null) return;

            var source = ListBox;
            source.SelectedItem = item;

            using (var dragPreview = new DragPreview(this, source, item)) {
                dragPreview.SetTargets(this);

                var data = new DataObject();
                data.SetData(AdditionalDataFormats.RaceGridEntry, item.DataContext);
                if (DragDrop.DoDragDrop(item, data, DragDropEffects.Move) == DragDropEffects.Move) {
                    // Save();
                }
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e) {
            var destination = (ListBox)sender;
            
            var item = e.Data.GetData(AdditionalDataFormats.RaceGridEntry) as RaceGridEntry;
            if (item == null || Model == null) {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            var newIndex = destination.GetMouseItemIndex();
            Model.InsertEntry(newIndex, item);

            e.Effects = DragDropEffects.Move;
        }
    }
}

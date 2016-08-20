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

            model.AddOpponent(dialog.SelectedCar);
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

        private void Item_OnPreviewMouseLeftButtonDown(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var _draggedItem = sender as ListBoxItem;
            var _draggedValue = _draggedItem?.DataContext as RaceGridEntry;
            if (_draggedValue == null) return;
            
            var layer = InitializeAdornerLayer();
            Application.Current.MainWindow.AllowDrop = false;

            if (DragDrop.DoDragDrop(_draggedItem, _draggedValue, DragDropEffects.Move) == DragDropEffects.Move) {
                Save();
            }

            Application.Current.MainWindow.AllowDrop = true;

            layer.Remove(_dragAdorner);
            _dragAdorner = null;
        }
    }
}

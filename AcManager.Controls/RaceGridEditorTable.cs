using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class RaceGridEditorTable : Control {
        static RaceGridEditorTable() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RaceGridEditorTable), new FrameworkPropertyMetadata(typeof(RaceGridEditorTable)));
        }

        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(RaceGridViewModel),
                typeof(RaceGridEditorTable), new PropertyMetadata(null, (o, e) => {
                    ((RaceGridEditorTable)o)._model = (RaceGridViewModel)e.NewValue;
                }));

        private RaceGridViewModel _model;

        public RaceGridViewModel Model {
            get { return _model; }
            set { SetValue(ModelProperty, value); }
        }

        public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
                typeof(RaceGridEditorTable), new PropertyMetadata(null, (o, e) => {
                    ((RaceGridEditorTable)o)._closeCommand = (ICommand)e.NewValue;
                }));

        private ICommand _closeCommand;

        public ICommand CloseCommand {
            get { return _closeCommand; }
            set { SetValue(CloseCommandProperty, value); }
        }

        public static readonly DependencyProperty AddOpponentCommandProperty = DependencyProperty.Register(nameof(AddOpponentCommand), typeof(ICommand),
                typeof(RaceGridEditorTable), new PropertyMetadata(null, (o, e) => {
                    ((RaceGridEditorTable)o)._addOpponentCommand = (ICommand)e.NewValue;
                }));

        private ICommand _addOpponentCommand = null;

        public ICommand AddOpponentCommand {
            get { return _addOpponentCommand; }
            set { SetValue(AddOpponentCommandProperty, value); }
        }

        private DataGrid _dataGrid;

        public override void OnApplyTemplate() {
            if (_dataGrid != null) {
                _dataGrid.Drop -= OnDrop;
            }

            base.OnApplyTemplate();

            _dataGrid = GetTemplateChild(@"PART_DataGrid") as DataGrid;
            if (_dataGrid != null) {
                _dataGrid.Drop += OnDrop;
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null || Model == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            var newIndex = ((ItemsControl)sender).GetMouseItemIndex();
            if (raceGridEntry != null) {
                Model.InsertEntry(newIndex, raceGridEntry);
            } else {
                Model.InsertEntry(newIndex, carObject);
            }

            e.Effects = DragDropEffects.Move;
        }
    }
}
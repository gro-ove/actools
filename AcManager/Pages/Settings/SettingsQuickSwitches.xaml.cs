using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsQuickSwitches {
        private static string[] _widgets;

        public SettingsQuickSwitchesViewModel Model => (SettingsQuickSwitchesViewModel)DataContext;

        public SettingsQuickSwitches() {
            DataContext = new SettingsQuickSwitchesViewModel();
            InitializeComponent();

            var active = SettingsHolder.Drive.QuickSwitchesList;

            if (_widgets == null) {
                _widgets = Resources.MergedDictionaries.SelectMany(x => x.Keys.OfType<string>()).Where(x => x.StartsWith(@"Widget")).ToArray();
                Logging.Write("Widgets: " + _widgets.JoinToString(@", "));

                if (active.Any(x => !_widgets.Contains(x))) {
                    active = active.Where(x => _widgets.Contains(x)).ToArray();
                    SettingsHolder.Drive.QuickSwitchesList = active;
                }
            }

            foreach (var key in active) {
                var widget = (FrameworkElement)FindResource(key);
                widget.Tag = key;
                (widget.Parent as ListBox)?.Items.Remove(widget);
                AddedItems.Items.Add(widget);
            }

            foreach (var key in _widgets.Where(x => !active.Contains(x))) {
                var widget = (FrameworkElement)FindResource(key);
                widget.Tag = key;
                (widget.Parent as ListBox)?.Items.Remove(widget);
                StoredItems.Items.Add(widget);
            }
        }

        public void Save() {
            SettingsHolder.Drive.QuickSwitchesList =
                    AddedItems.Items.OfType<FrameworkElement>().Select(x => x.Tag).OfType<string>().ToArray();
        }

        public class SettingsQuickSwitchesViewModel : NotifyPropertyChanged {
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;
        }

        private static Point GetMousePosition(Visual relativeTo) {
            var mouse = new User32.Win32Point();
            User32.GetCursorPos(ref mouse);
            return relativeTo.PointFromScreen(new Point(mouse.X, (double)mouse.Y));
        }

        private ListBoxItem _draggedItem;
        private object _draggedValue;
        private ListBox _draggedSource;
        private DragAdorner _dragAdorner;
        private Point _mouseDown, _delta;

        private AdornerLayer InitializeAdornerLayer() {
            _dragAdorner = new DragAdorner(_draggedSource, _draggedItem.RenderSize, new VisualBrush(_draggedItem)) {
                Opacity = 0.9
            };
            
            _mouseDown = GetMousePosition(this);
            _delta = _draggedItem.TranslatePoint(new Point(0, 0), _draggedSource);

            var layer = AdornerLayer.GetAdornerLayer(this);
            layer.Add(_dragAdorner);
            return layer;
        }

        private void Item_OnPreviewMouseLeftButtonDown(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _draggedItem = sender as ListBoxItem;
            if (_draggedItem == null) return;

            _draggedSource = AddedItems.Items.Contains(_draggedItem.DataContext) ? AddedItems : StoredItems;
            _draggedSource.SelectedItem = _draggedItem;
            var layer = InitializeAdornerLayer();

            Application.Current.MainWindow.AllowDrop = false;
            _draggedValue = _draggedItem.DataContext;
            if (DragDrop.DoDragDrop(_draggedItem, _draggedValue, DragDropEffects.Move) == DragDropEffects.Move) {
                Save();
            }

            Application.Current.MainWindow.AllowDrop = true;

            layer.Remove(_dragAdorner);
            _dragAdorner = null;
        }

        private void Item_OnPreviewDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;

            var item = sender as ListBoxItem;
            if (item == null) return;

            var list = AddedItems.Items.Contains(item.DataContext) ? AddedItems : StoredItems;
            var anotherList = ReferenceEquals(list, AddedItems) ? StoredItems : AddedItems;
            var value = item.DataContext;

            list.Items.Remove(value);
            anotherList.Items.Add(value);

            Save();
        }

        void UpdateDragAdornerLocation() {
            if (_dragAdorner == null) return;
            var point = GetMousePosition(this) - _mouseDown + _delta;
            _dragAdorner.SetOffsets(point.X, point.Y);
        }

        private void List_OnDragEnter(object sender, DragEventArgs e) {
            if (_dragAdorner == null) return;
            _dragAdorner.Visibility = Visibility.Visible;
            UpdateDragAdornerLocation();
        }

        private void List_OnDragLeave(object sender, DragEventArgs e) {
            if (_dragAdorner == null) return;
            _dragAdorner.Visibility = Visibility.Collapsed;
        }

        private void List_OnDragOver(object sender, DragEventArgs e) {
            if (_dragAdorner == null) return;
            e.Effects = DragDropEffects.Move;
            UpdateDragAdornerLocation();
        }

        private static Visual GetListViewItem(ItemsControl list, int index) {
            return list.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated
                    ? null : list.ItemContainerGenerator.ContainerFromIndex(index) as Visual;
        }

        private static bool IsMouseOverElement(Visual target) {
            var bounds = VisualTreeHelper.GetDescendantBounds(target);
            var mousePos = GetMousePosition(target);
            return bounds.Contains(mousePos);
        }

        private void List_OnDrop(object sender, DragEventArgs e) {
            var destination = (ListBox)sender;
            if (_draggedValue == null || _draggedSource == null) {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            var newIndex = -1;
            for (var i = 0; i < destination.Items.Count; i++) {
                var item = GetListViewItem(destination, i);
                if (IsMouseOverElement(item)) {
                    newIndex = i;
                    break;
                }
            }
            
            _draggedSource.Items.Remove(_draggedValue);
            if (newIndex == -1) {
                destination.Items.Add(_draggedValue);
            } else {
                destination.Items.Insert(newIndex, _draggedValue);
            }

            e.Effects = DragDropEffects.Move;
        }
    }
}

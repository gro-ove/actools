using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsQuickSwitches {
        private SettingsQuickSwitchesViewModel Model => (SettingsQuickSwitchesViewModel)DataContext;

        public SettingsQuickSwitches() {
            DataContext = new SettingsQuickSwitchesViewModel();
            InitializeComponent();

            /*var list = Model.Holder.IgnoredInterfaces;
            foreach (var item in Model.NetworkInterfaces.Where(x => !list.Contains(x.Id)).ToList()) {
                IgnoredInterfacesListBox.SelectedItems.Add(item);
            }*/

            //AddedItems.ItemContainerStyle.Setters.Add(new EventSetter(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(AddedItems_OnPreviewMouseLeftButtonDown)));
        }

        private void IgnoredInterfacesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            //var selected = IgnoredInterfacesListBox.SelectedItems.OfType<NetworkInterface>().Select(x => x.Id).ToList();
            //Model.Holder.IgnoredInterfaces = Model.NetworkInterfaces.Where(x => !selected.Contains(x.Id)).Select(x => x.Id);
        }

        public class SettingsQuickSwitchesViewModel : NotifyPropertyChanged {
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            public SettingsQuickSwitchesViewModel() {
                /*NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(
                        x => x.GetIPProperties().UnicastAddresses.Any(
                                y => y.Address.AddressFamily == AddressFamily.InterNetwork)).ToList();*/
            }
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

        private AdornerLayer InitializeAdornerLayer(ListBox list, ListBoxItem itemToDrag) {
            _dragAdorner = new DragAdorner(list, itemToDrag.RenderSize, new VisualBrush(itemToDrag)) {
                Opacity = 0.9
            };
            
            _mouseDown = GetMousePosition(this);
            _draggedItem = itemToDrag;
            _draggedSource = list;
            _delta = _draggedItem.TranslatePoint(new Point(0, 0), list);

            var layer = AdornerLayer.GetAdornerLayer(this);
            layer.Add(_dragAdorner);
            return layer;
        }

        private void Item_OnPreviewMouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var draggedItem = sender as ListBoxItem;
            if (draggedItem == null) return;

            var list = AddedItems.Items.Contains(draggedItem.DataContext) ? AddedItems : StoredItems;
            var layer = InitializeAdornerLayer(list, draggedItem);
            
            Application.Current.MainWindow.AllowDrop = false;
            _draggedValue = draggedItem.DataContext;
            DragDrop.DoDragDrop(draggedItem, _draggedValue, DragDropEffects.Move);
            Application.Current.MainWindow.AllowDrop = true;

            layer.Remove(_dragAdorner);
            _dragAdorner = null;
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
            if (_draggedValue == null) {
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

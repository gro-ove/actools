using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;

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

        private void Item_OnPreviewMouseLeftButtonDown(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var item = sender as ListBoxItem;
            if (item == null) return;

            var source = AddedItems.Items.Contains(item.DataContext) ? AddedItems : StoredItems;
            source.SelectedItem = item;

            using (new DragPreview(item)) {
                var data = new DataObject();
                data.SetData(AdditionalDataFormats.Widget, item.DataContext);
                data.SetData(AdditionalDataFormats.Source, source);
                if (DragDrop.DoDragDrop(item, data, DragDropEffects.Move) == DragDropEffects.Move) {
                    Save();
                }
            }
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

        private static class AdditionalDataFormats {
            public const string Widget = "Data-Widget";
            public const string Source = "Data-Source";
        }
        
        private void List_OnDrop(object sender, DragEventArgs e) {
            var destination = (ListBox)sender;

            var widget = e.Data.GetData(AdditionalDataFormats.Widget);
            var source = e.Data.GetData(AdditionalDataFormats.Source) as ItemsControl;

            if (widget == null || source == null) {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            var newIndex = destination.GetMouseItemIndex();
            
            source.Items.Remove(widget);
            if (newIndex == -1) {
                destination.Items.Add(widget);
            } else {
                destination.Items.Insert(newIndex, widget);
            }

            e.Effects = DragDropEffects.Move;
        }
    }
}

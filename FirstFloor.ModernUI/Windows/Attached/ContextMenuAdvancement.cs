using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class InheritingContextMenu : ContextMenu {
        private List<ContextMenu> _menus;
        private List<List<object>> _items;
        private Separator _separator;

        public InheritingContextMenu() {
            DefaultStyleKey = typeof(InheritingContextMenu);
        }

        protected override void OnOpened(RoutedEventArgs e) {
            if (_items != null) {
                Clear();
            }

            _menus = ContextMenuAdvancement.ParentContextMenu;
            if (_items != null || _menus.Count == 0) return;

            _menus = _menus.ToList();
            ContextMenuAdvancement.ParentContextMenu.Clear();
            
            _items = new List<List<object>>(_menus.Count);
            foreach (var menu in _menus) {
                _items.Add(menu.Items.OfType<object>().ToList());
                menu.Items.Clear();
            }

            var i = 0;
            foreach (var item in _items.SelectMany(x => x)) {
                Items.Insert(i++, item);
            }

            _separator = new Separator();
            Items.Insert(i, _separator);

            base.OnOpened(e);
        }

        private void Clear() {
            if (_items == null) return;

            if (_items.Count == _menus.Count) {
                for (var i = 0; i < _items.Count; i++) {
                    var list = _items[i];
                    var menu = _menus[i];

                    foreach (var item in list) {
                        Items.Remove(item);
                        menu.Items.Add(item);
                    }
                }
            }

            _items.Clear();
            _items = null;

            Items.Remove(_separator);
            _separator = null;
        }

        protected override void OnClosed(RoutedEventArgs e) {
            Clear();
            base.OnClosed(e);
        }
    }

    public class ContextMenuAdvancement {
        internal static readonly List<ContextMenu> ParentContextMenu = new List<ContextMenu>();

        public static bool GetPropagateToChildren(DependencyObject obj) {
            return (bool)obj.GetValue(PropagateToChildrenProperty);
        }

        public static void SetPropagateToChildren(DependencyObject obj, bool value) {
            obj.SetValue(PropagateToChildrenProperty, value);
        }

        public static readonly DependencyProperty PropagateToChildrenProperty = DependencyProperty.RegisterAttached("PropagateToChildren", typeof(bool),
                typeof(ContextMenuAdvancement), new UIPropertyMetadata(OnPropagateToChildrenChanged));

        private static void OnPropagateToChildrenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || !(e.NewValue is bool)) return;

            var newValue = (bool)e.NewValue;
            if (newValue) {
                element.PreviewMouseRightButtonDown += Element_PreviewMouseRightButtonDown;
            } else {
                element.PreviewMouseRightButtonDown -= Element_PreviewMouseRightButtonDown;
            }
        }

        private static void Element_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            ParentContextMenu.Add((ContextMenu)((FrameworkElement)sender).GetValue(FrameworkElement.ContextMenuProperty));
        }
    }
}

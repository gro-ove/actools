using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(Menu))]
    public class ContextMenuButton : Button {
        public ContextMenuButton() {
            DefaultStyleKey = typeof(ContextMenuButton);
        }

        private bool Open(bool near) {
            if (Menu != null) {
                if (near) {
                    Menu.Placement = PlacementMode.Bottom;
                    Menu.PlacementTarget = this;
                } else {
                    Menu.Placement = PlacementMode.MousePoint;
                    Menu.PlacementTarget = null;
                }

                Menu.IsOpen = true;
                return true;
            }

            return false;
        }
        
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
            if (Open(true)) e.Handled = true;
            base.OnPreviewMouseLeftButtonUp(e);
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            var fe = oldParent as FrameworkElement;
            if (fe != null) {
                fe.PreviewMouseRightButtonDown -= Parent_PreviewMouseRightButtonDown;
            }

            fe = Parent as FrameworkElement;
            if (fe != null) {
                fe.PreviewMouseRightButtonDown += Parent_PreviewMouseRightButtonDown;
            }

            base.OnVisualParentChanged(oldParent);
        }

        private void Parent_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (Open(false)) e.Handled = true;
        }

        public static readonly DependencyProperty MenuProperty = DependencyProperty.Register(nameof(Menu), typeof(ContextMenu),
                typeof(ContextMenuButton), new PropertyMetadata(OnMenuChanged));

        public ContextMenu Menu {
            get { return (ContextMenu)GetValue(MenuProperty); }
            set { SetValue(MenuProperty, value); }
        }

        private static void OnMenuChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ContextMenuButton)o).OnMenuChanged((ContextMenu)e.OldValue, (ContextMenu)e.NewValue);
        }

        private void OnMenuChanged(ContextMenu oldValue, ContextMenu newValue) {
            if (oldValue != null) {
                BindingOperations.ClearBinding(oldValue, DataContextProperty);
            }

            if (newValue != null && newValue.DataContext == null) {
                newValue.SetBinding(DataContextProperty, new Binding {
                    Path = new PropertyPath(nameof(DataContext)),
                    Source = this
                });
            }
        }
    }
}
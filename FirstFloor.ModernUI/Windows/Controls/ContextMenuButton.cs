using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ContextMenuButtonEventArgs : EventArgs {
        internal ContextMenuButtonEventArgs() {}

        public object Menu { internal get; set; }
    }

    [ContentProperty(nameof(Menu))]
    public class ContextMenuButton : Control {
        public ContextMenuButton() {
            DefaultStyleKey = typeof(ContextMenuButton);
        }

        public event EventHandler<ContextMenuButtonEventArgs> Click;

        private bool Open(bool near) {
            var args = new ContextMenuButtonEventArgs();
            Click?.Invoke(this, args);

            var menu = args.Menu ?? Menu;

            var popup = menu as Popup;
            if (popup != null) {
                if (popup.IsOpen) {
                    popup.IsOpen = false;
                } else {
                    popup.Placement = near ? PlacementMode.Bottom : PlacementMode.MousePoint;
                    popup.PlacementTarget = near ? this : null;
                    popup.IsOpen = true;
                    popup.StaysOpen = false;
                }
                return true;
            }

            var contextMenu = menu as ContextMenu;
            if (contextMenu != null) {
                contextMenu.Placement = near ? PlacementMode.Bottom : PlacementMode.MousePoint;
                contextMenu.PlacementTarget = near ? this : null;
                contextMenu.IsOpen = true;
                return true;
            }

            var command = Command;
            if (command != null) {
                command.Execute(null);
                return true;
            }

            return false;
        }

        private FrameworkElement _button;

        public override void OnApplyTemplate() {
            if (_button != null) {
                _button.PreviewMouseLeftButtonUp -= OnButtonClick;
                _button.MouseRightButtonUp -= OnButtonDown;
            }

            base.OnApplyTemplate();

            _button = GetTemplateChild(@"PART_Button") as FrameworkElement;
            if (_button != null) {
                _button.PreviewMouseLeftButtonUp += OnButtonClick;
                _button.MouseRightButtonUp += OnButtonDown;
            }
        }

        private void OnButtonDown(object sender, MouseButtonEventArgs e) {
            if (!e.Handled && Command != null) {
                e.Handled = true;
            }
        }

        private void OnButtonClick(object sender, MouseButtonEventArgs e) {
            if (!e.Handled && Open(true)) {
                e.Handled = true;
            }
        }

        private async void OnContextMenuClick(object sender, MouseButtonEventArgs e) {
            await Task.Delay(1);
            if (!e.Handled && Open(false)) {
                e.Handled = true;
            }
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            var fe = oldParent as FrameworkElement;
            if (fe != null) {
                fe.PreviewMouseRightButtonUp -= OnContextMenuPreviewClick;
                fe.MouseRightButtonUp -= OnContextMenuClick;
            }

            fe = Parent as FrameworkElement;
            if (fe != null) {
                fe.PreviewMouseRightButtonUp += OnContextMenuPreviewClick;
                fe.MouseRightButtonUp += OnContextMenuClick;
            }

            base.OnVisualParentChanged(oldParent);
        }

        private void OnContextMenuPreviewClick(object sender, MouseButtonEventArgs e) {
            var menu = Menu as ContextMenu;
            if (menu != null) {
                ContextMenuAdvancement.Add(menu);
            }
        }

        public static readonly DependencyProperty MenuProperty = DependencyProperty.Register(nameof(Menu), typeof(FrameworkElement),
                typeof(ContextMenuButton), new PropertyMetadata(OnMenuChanged));

        public FrameworkElement Menu {
            get { return (FrameworkElement)GetValue(MenuProperty); }
            set { SetValue(MenuProperty, value); }
        }

        private static void OnMenuChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ContextMenuButton)o).OnMenuChanged((FrameworkElement)e.OldValue, (FrameworkElement)e.NewValue);
        }

        private void OnMenuChanged(FrameworkElement oldValue, FrameworkElement newValue) {
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

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand),
            typeof(ContextMenuButton));

        public ICommand Command {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty PropagateToChildrenProperty = DependencyProperty.Register(nameof(PropagateToChildren), typeof(bool),
                typeof(ContextMenuButton));

        public bool PropagateToChildren {
            get { return (bool)GetValue(PropagateToChildrenProperty); }
            set { SetValue(PropagateToChildrenProperty, value); }
        }
    }
}
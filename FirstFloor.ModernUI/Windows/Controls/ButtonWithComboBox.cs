using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(@"MenuItems")]
    public class ButtonWithComboBox : ContentControl {
        public ButtonWithComboBox() {
            DefaultStyleKey = typeof(ButtonWithComboBox);
            MenuItems = new Collection<DependencyObject>();
            PreviewMouseRightButtonUp += OnRightClick;
        }

        private void OnRightClick(object sender, MouseButtonEventArgs args) {
            if (_item != null) {
                args.Handled = true;
                _item.IsSubmenuOpen = true;
            }
        }

        private MenuItem _item;

        public override void OnApplyTemplate() {
            if (_item != null) {
                _item.KeyDown -= OnMenuKeyDown;
            }

            base.OnApplyTemplate();

            _item = GetTemplateChild("PART_MenuItem") as MenuItem;
            if (_item != null) {
                _item.KeyDown += OnMenuKeyDown;
            }
        }

        private void OnMenuKeyDown(object sender, KeyEventArgs args) {
            if (_item == null) return;
            if (_item.IsSubmenuOpen != true && (args.Key == Key.Enter || args.Key == Key.Space)) {
                _item.IsSubmenuOpen = true;
            }
        }

        public IList MenuItems {
            get => (Collection<DependencyObject>)GetValue(MenuItemsProperty);
            set => SetValue(MenuItemsProperty, value);
        }

        public static readonly DependencyProperty MenuItemsProperty = DependencyProperty.Register("MenuItems", typeof(IList),
                typeof(ButtonWithComboBox));

        public object ButtonToolTip {
            get => (object)GetValue(ButtonToolTipProperty);
            set => SetValue(ButtonToolTipProperty, value);
        }

        public static readonly DependencyProperty ButtonToolTipProperty = DependencyProperty.Register("ButtonToolTip", typeof(object),
                typeof(ButtonWithComboBox));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand),
                typeof(ButtonWithComboBox));

        public ICommand Command {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object),
                typeof(ButtonWithComboBox));

        public object CommandParameter {
            get => (object)GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }
    }
}

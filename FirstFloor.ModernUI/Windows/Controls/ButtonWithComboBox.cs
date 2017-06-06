using System;
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
            PreviewMouseRightButtonDown += OnRightClick;
        }

        private void OnRightClick(object sender, MouseButtonEventArgs mouseButtonEventArgs) {
            if (_item != null) {
                _item.IsSubmenuOpen = true;
            }
        }

        private MenuItem _item;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            _item = GetTemplateChild("PART_MenuItem") as MenuItem;
        }

        public IList MenuItems {
            get { return (Collection<DependencyObject>)GetValue(MenuItemsProperty); }
            set { SetValue(MenuItemsProperty, value); }
        }

        public static readonly DependencyProperty MenuItemsProperty = DependencyProperty.Register("MenuItems", typeof(IList),
                typeof(ButtonWithComboBox));

        public object ButtonToolTip {
            get { return (object)GetValue(ButtonToolTipProperty); }
            set { SetValue(ButtonToolTipProperty, value); }
        }

        public static readonly DependencyProperty ButtonToolTipProperty = DependencyProperty.Register("ButtonToolTip", typeof(object),
                typeof(ButtonWithComboBox));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand),
                typeof(ButtonWithComboBox));

        public ICommand Command {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object),
                typeof(ButtonWithComboBox));

        public object CommandParameter {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }
    }
}

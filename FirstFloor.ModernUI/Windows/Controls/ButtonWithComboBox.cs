using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(@"MenuItems")]
    public class ButtonWithComboBox : Button {
        public ButtonWithComboBox() {
            DefaultStyleKey = typeof(ButtonWithComboBox);
            MenuItems = new Collection<DependencyObject>();
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
    }
}

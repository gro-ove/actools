using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernSubMenuItemContainerStyleSelector : StyleSelector {
        public override Style SelectStyle(object item, DependencyObject container) => item is LinkInputEmpty ?
                LinkInputEmptyStyle : item is LinkInput ? LinkInputStyle : item is Link ? LinkStyle : null;

        public Style LinkInputStyle { get; set; }

        public Style LinkInputEmptyStyle { get; set; }

        public Style LinkStyle { get; set; }
    }
}
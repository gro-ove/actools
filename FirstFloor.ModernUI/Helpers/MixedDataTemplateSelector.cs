using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Helpers {
    public class MixedDataTemplateSelector : DataTemplateSelector {
        public DataTemplate UIElementTemplate { get; set; }

        public DataTemplate DataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is UIElement ? UIElementTemplate : DataTemplate;
        }
    }
}
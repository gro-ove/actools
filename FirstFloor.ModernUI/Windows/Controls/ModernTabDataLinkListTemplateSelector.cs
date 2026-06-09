using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernTabDataLinkListTemplateSelector : DataTemplateSelector {
        public DataTemplate ListLinkDataTemplate { get; set; }

        public DataTemplate LinkDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is LinksList ? ListLinkDataTemplate : LinkDataTemplate;
        }
    }
}
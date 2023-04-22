using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernTabDataTemplateSelector : DataTemplateSelector {
        public DataTemplate LinkDataTemplate { get; set; }

        public DataTemplate PinnedLinkDataTemplate { get; set; }

        public DataTemplate TitleDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is Link l ? l.IsPinned ? PinnedLinkDataTemplate : LinkDataTemplate : TitleDataTemplate;
        }
    }
}
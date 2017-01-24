using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace AcManager.Controls {
    [ContentProperty(nameof(DataTemplate))]
    public class FlexibleDataTemplateSelector : DataTemplateSelector {
        public DataTemplate DataTemplate { get; set; }

        private DataTemplate _uiElementTemplate;

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (_uiElementTemplate == null) {
                var visualTree = new FrameworkElementFactory(typeof(ContentPresenter));
                visualTree.SetBinding(ContentPresenter.ContentProperty, new Binding());
                _uiElementTemplate = new DataTemplate {
                    VisualTree = visualTree
                };
            }

            return item is UIElement ? _uiElementTemplate : DataTemplate;
        }
    }
}
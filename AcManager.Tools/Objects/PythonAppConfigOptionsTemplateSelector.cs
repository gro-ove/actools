using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Helpers;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigOptionsTemplateSelector : DataTemplateSelector {
        public DataTemplate DataTemplate { get; set; }
        
        public DataTemplate NullTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is SettingEntry ? DataTemplate : NullTemplate;
        }
    }
}
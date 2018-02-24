using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.ContentInstallation.Entries;

namespace AcManager.UserControls {
    public class AdditionalContentEntryTemplateSelector : DataTemplateSelector {
        public DataTemplate BasicTemplate { get; set; }
        public DataTemplate TrackTemplate { get; set; }
        public DataTemplate FontTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            switch (item) {
                case TrackContentEntry _:
                    return TrackTemplate;
                case FontContentEntry _:
                    return FontTemplate;
                default:
                    return BasicTemplate;
            }
        }
    }
}
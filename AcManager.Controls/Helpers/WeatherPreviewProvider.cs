using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public class WeatherPreviewProvider : IHierarchicalItemPreviewProvider {
        private static ResourceDictionary _dictionary;

        private static ResourceDictionary Dictionary => _dictionary ?? (_dictionary = new SharedResourceDictionary {
            Source = new Uri("/AcManager.Controls;component/Assets/ToolTips.xaml", UriKind.Relative)
        });

        object IHierarchicalItemPreviewProvider.GetPreview(object item) {
            if (item is WeatherObject weather) {
                var t = (FrameworkElement)((ToolTip)Dictionary["WeatherPreviewTooltip"]).Content;
                t.DataContext = weather;
                t.Margin = new Thickness();
                return t;
            }

            return null;
        }

        PlacementMode IHierarchicalItemPreviewProvider.GetPlacementMode(object item) {
            return PlacementMode.Left;
        }
    }
}
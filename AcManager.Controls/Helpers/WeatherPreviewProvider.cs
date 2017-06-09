using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public class WeatherPreviewProvider : IHierarchicalItemPreviewProvider {
        public object GetPreview(object item) {
            if (item is WeatherObject weather) {
                return new BetterImage {
                    Filename = weather.PreviewImage,
                    MaxWidth = 400
                };
            }

            return null;
        }
    }
}
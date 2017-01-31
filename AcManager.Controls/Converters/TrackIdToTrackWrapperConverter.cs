using System;
using System.Globalization;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(string), typeof(AcItemWrapper))]
    public class TrackIdToTrackWrapperConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var id = value?.ToString();
            if (id == null) return null;

            var delimiter = id.IndexOf('/');
            if (delimiter != -1) {
                return new AcItemWrapper(TracksManager.Instance.GetLayoutByShortenId(id));
                // id = id.Substring(0, delimiter);
            }

            var wrapper = TracksManager.Instance.GetWrapperById(id);
            if (wrapper == null) return null;

            if (!wrapper.IsLoaded) wrapper.LoadedAsync().Forget();
            return wrapper;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as AcItemWrapper)?.Id;
        }
    }
}
using System;
using System.Globalization;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(string), typeof(AcItemWrapper))]
    public class CarIdToCarWrapperConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var id = value?.ToString();
            if (id == null) return null;

            var wrapper = CarsManager.Instance.GetWrapperById(id);
            if (wrapper == null) return null;

            if (!wrapper.IsLoaded) wrapper.LoadedAsync().Ignore();
            return wrapper;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as AcItemWrapper)?.Id;
        }
    }
}
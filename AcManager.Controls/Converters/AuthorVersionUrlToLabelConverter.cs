using System;
using System.Globalization;
using System.Windows.Data;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(string), typeof(string))]
    public class AuthorVersionUrlToLabelConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length != 3) return null;

            if (values[0] != null) return Resources.AcObject_AuthorLabel;
            if (values[1] != null) return Resources.AcObject_VersionLabel;
            if (values[2] != null) return Resources.AcObject_UrlLabel;
            return Resources.AcObject_AuthorLabel;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
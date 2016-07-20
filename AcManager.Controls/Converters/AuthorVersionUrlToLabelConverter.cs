using System;
using System.Globalization;
using System.Windows.Data;

namespace AcManager.Controls.Converters {
    public class AuthorVersionUrlToLabelConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length != 3) return null;

            if (values[0] != null) return "Author:";
            if (values[1] != null) return "Version:";
            if (values[2] != null) return "URL";
            return "Author:";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
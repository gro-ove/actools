using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Pages.Selected {
    public class NumberToColumnsConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var v = value.As<int>();
            var max = parameter.As<int>();
            if (max < 1 || v <= max) return v;

            var from = max / 2;
            return Enumerable.Range(from, max - from + 1).Select(x => new {
                Colums = x,
                Rows = (int)Math.Ceiling((double)v / x)
            }).MinEntry(x => (x.Colums * x.Rows - v) * from - x.Colums).Colums;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
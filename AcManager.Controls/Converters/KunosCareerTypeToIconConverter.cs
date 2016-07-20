using System;
using System.Globalization;
using System.Windows.Data;
using AcManager.Tools.Objects;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(KunosCareerObjectType), typeof(object))]
    public class KunosCareerTypeToIconConverter : IValueConverter {
        public object ChampionshipIconData { get; set; }

        public object SingleEventsIconData { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            switch (value as KunosCareerObjectType?) {
                case KunosCareerObjectType.Championship:
                    return ChampionshipIconData;
                case KunosCareerObjectType.SingleEvents:
                    return SingleEventsIconData;
                default:
                    return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
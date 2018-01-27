using System;
using System.Globalization;
using System.Windows.Data;
using AcManager.Tools;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetAssists {
        public ServerPresetAssists() {
            InitializeComponent();
        }

        public static IValueConverter SpecialOffForNegativeConverter { get; } = new SpecialOffForNegativeConverterInner();

        [ValueConversion(typeof(int), typeof(string))]
        private class SpecialOffForNegativeConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                var number = value.As<int>();
                return number < 0 ? ToolsStrings.AcSettings_Off : number.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                return value as string == ToolsStrings.AcSettings_Off ? -1 : value.As<int>();
            }
        }
    }
}

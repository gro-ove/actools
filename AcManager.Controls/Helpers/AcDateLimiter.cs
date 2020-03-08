using System;
using System.Windows;
using System.Windows.Controls;
using AcTools.Utils.Helpers;

namespace AcManager.Controls.Helpers {
    public class AcDateLimiter {
        private void OnAfter1970OnlyChanged(bool oldValue, bool newValue) { }

        public static bool GetAfter1970Only(DependencyObject obj) {
            return (bool)obj.GetValue(After1970OnlyProperty);
        }

        public static void SetAfter1970Only(DependencyObject obj, bool value) {
            obj.SetValue(After1970OnlyProperty, value);
        }

        public static readonly DependencyProperty After1970OnlyProperty = DependencyProperty.RegisterAttached("After1970Only", typeof(bool),
                typeof(AcDateLimiter), new UIPropertyMetadata(OnAfter1970OnlyChanged));

        private static void OnAfter1970OnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as DatePicker;
            if (element == null || !(e.NewValue is bool)) return;

            if ((bool)e.NewValue) {
                element.BlackoutDates.Add(new CalendarDateRange(DateTime.MinValue, 0L.ToDateTime()));
            } else {
                element.BlackoutDates.Clear();
            }
        }
    }
}
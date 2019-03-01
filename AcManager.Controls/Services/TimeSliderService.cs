using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Data;
using AcTools;
using AcTools.Utils;

namespace AcManager.Controls.Services {
    public static class TimeSliderService {
        public static bool GetIsTimeSlider(DependencyObject obj) {
            return (bool)obj.GetValue(IsTimeSliderProperty);
        }

        public static void SetIsTimeSlider(DependencyObject obj, bool value) {
            obj.SetValue(IsTimeSliderProperty, value);
        }

        public static readonly DependencyProperty IsTimeSliderProperty = DependencyProperty.RegisterAttached("IsTimeSlider", typeof(bool),
                typeof(TimeSliderService), new UIPropertyMetadata(OnIsTimeSliderChanged));

        private static void OnIsTimeSliderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as Slider;
            if (element == null || !(e.NewValue is bool)) return;

            var newValue = (bool)e.NewValue;
            if (!newValue) return;

            if (PatchHelper.IsFeatureSupported(PatchHelper.FeatureFullDay)) {
                element.Minimum = 0;
                element.Maximum = CommonAcConsts.TimeAbsoluteMaximum;
            } else {
                element.Minimum = CommonAcConsts.TimeMinimum;
                element.Maximum = CommonAcConsts.TimeMaximum;
            }
        }
    }
}
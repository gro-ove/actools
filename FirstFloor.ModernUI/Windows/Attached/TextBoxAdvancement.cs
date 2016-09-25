using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Attached {
    // temporary
    // todo: replace by BetterTextBox
    public static class TextBoxAdvancement {
        [Obsolete]
        public static SpecialMode GetSpecialMode(DependencyObject obj) {
            return (SpecialMode)obj.GetValue(SpecialModeProperty);
        }

        [Obsolete]
        public static void SetSpecialMode(DependencyObject obj, SpecialMode value) {
            obj.SetValue(SpecialModeProperty, value);
        }

        [Obsolete]
        public static readonly DependencyProperty SpecialModeProperty = DependencyProperty.RegisterAttached("SpecialMode",
            typeof(SpecialMode), typeof(TextBoxAdvancement), new UIPropertyMetadata(OnSpecialModePropertyChanged));

        [Obsolete]
        static void OnSpecialModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as TextBox;
            if (element == null) return;

            if (e.NewValue is SpecialMode && (SpecialMode)e.NewValue != SpecialMode.None) {
                element.PreviewKeyDown += Element_KeyDown;
            } else {
                element.PreviewKeyDown -= Element_KeyDown;
            }
        }

        public static double GetMinValue(DependencyObject obj) {
            return (double)obj.GetValue(MinValueProperty);
        }

        public static void SetMinValue(DependencyObject obj, double value) {
            obj.SetValue(MinValueProperty, value);
        }

        public static readonly DependencyProperty MinValueProperty = DependencyProperty.RegisterAttached("MinValue", typeof(double),
                typeof(TextBoxAdvancement), new UIPropertyMetadata(double.MinValue));

        public static double GetMaxValue(DependencyObject obj) {
            return (double)obj.GetValue(MaxValueProperty);
        }

        public static void SetMaxValue(DependencyObject obj, double value) {
            obj.SetValue(MaxValueProperty, value);
        }

        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.RegisterAttached("MaxValue", typeof(double),
                typeof(TextBoxAdvancement), new UIPropertyMetadata(double.MaxValue));

        [Obsolete]
        private static void Element_KeyDown(object sender, KeyEventArgs e) {
            if (!e.Key.Equals(Key.Up) && !e.Key.Equals(Key.Down)) return;

            var element = sender as TextBox;
            if (element == null) return;
            
            var processed = BetterTextBox.ProcessText(GetSpecialMode(element), element.Text, e.Key == Key.Up ? 1d : -1d, GetMinValue(element), GetMaxValue(element));
            if (processed != null) {
                BetterTextBox.SetTextBoxText(element, processed);
            }

            e.Handled = true;
        }
    }
}

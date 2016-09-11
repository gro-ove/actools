using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace FirstFloor.ModernUI.Windows.Attached {
    // temporary
    // todo: replace by BetterTextBox
    [Obsolete]
    public static class WatermarkService {
        [Obsolete]
        public static readonly DependencyProperty WatermarkProperty;
        private static readonly IDictionary<object, ItemsControl> ItemsControls = new Dictionary<object, ItemsControl>();

        static WatermarkService() {
            WatermarkProperty = DependencyProperty.RegisterAttached("Watermark", typeof(object), typeof(WatermarkService), 
                new FrameworkPropertyMetadata(null, OnWatermarkChanged));
        }

        [Obsolete]
        public static object GetWatermark(DependencyObject d) {
            return d.GetValue(WatermarkProperty);
        }

        [Obsolete]
        public static void SetWatermark(DependencyObject d, object value) {
            d.SetValue(WatermarkProperty, value);
        }

        private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            // return;

            var control = d as Control;
            if (control == null) {
                var textBlock = d as TextBlock;
                if (textBlock != null) {
                    textBlock.Loaded += Control_Loaded;
                    DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))
                                                .AddValueChanged(textBlock, Control_TextChanged);
                }
                return;
            }

            control.Loaded += Control_Loaded;

            var asTextBox = d as TextBox;
            if (asTextBox != null) {
                control.GotKeyboardFocus += Control_GotKeyboardFocus;
                control.LostKeyboardFocus += Control_Loaded;
                asTextBox.TextChanged += Control_TextChanged;
            } else {
                var asComboBox = d as ComboBox;
                if (asComboBox != null) {
                    control.GotKeyboardFocus += Control_GotKeyboardFocus;
                    control.LostKeyboardFocus += Control_Loaded;
                    asComboBox.SelectionChanged += Control_SelectionChanged;
                    return;
                }
            }

            var itemsControl = d as ItemsControl;
            if (itemsControl == null) return;

            itemsControl.ItemContainerGenerator.ItemsChanged += ItemsChanged;
            ItemsControls.Add(itemsControl.ItemContainerGenerator, itemsControl);
                
            DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, itemsControl.GetType())
                                        .AddValueChanged(itemsControl, ItemsSourceChanged);
        }

        private static void Control_TextChanged(object sender, EventArgs e) {
            var control = (UIElement)sender;
            if (ShouldShowWatermark(control)) {
                ShowWatermark(control);
            } else {
                RemoveWatermark(control);
            }
        }

        private static void Control_TextChanged(object sender, RoutedEventArgs e) {
            var control = (UIElement)sender;
            if (ShouldShowWatermark(control)) {
                ShowWatermark(control);
            } else {
                RemoveWatermark(control);
            }
        }

        private static void Control_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var control = (UIElement)sender;
            if (ShouldShowWatermark(control)) {
                ShowWatermark(control);
            } else {
                RemoveWatermark(control);
            }
        }

        private static void Control_GotKeyboardFocus(object sender, RoutedEventArgs e) {
            var c = (UIElement)sender;
            if (ShouldShowWatermark(c)) {
                RemoveWatermark(c);
            }
        }

        private static void Control_Loaded(object sender, RoutedEventArgs e) {
            var c = (UIElement)sender;
            if (ShouldShowWatermark(c)) {
                ShowWatermark(c);
            }
        }

        private static void ItemsSourceChanged(object sender, EventArgs e) {
            var c = (ItemsControl)sender;
            if (c.ItemsSource == null || ShouldShowWatermark(c)) {
                ShowWatermark(c);
            } else {
                RemoveWatermark(c);
            }
        }

        private static void ItemsChanged(object sender, ItemsChangedEventArgs e) {
            ItemsControl control;
            if (!ItemsControls.TryGetValue(sender, out control)) return;

            if (ShouldShowWatermark(control)) {
                ShowWatermark(control);
            } else {
                RemoveWatermark(control);
            }
        }

        private static void RemoveWatermark(UIElement control) {
            var layer = AdornerLayer.GetAdornerLayer(control);

            // layer could be null if control is no longer in the visual tree
            var adorners = layer?.GetAdorners(control);
            if (adorners == null) return;

            foreach (var adorner in adorners.OfType<WatermarkAdorner>()) {
                adorner.Visibility = Visibility.Hidden;
                layer.Remove(adorner);
            }
        }

        private static void ShowWatermark(UIElement control) {
            var layer = AdornerLayer.GetAdornerLayer(control);
            if (layer == null) return;

            // layer could be null if control is no longer in the visual tree
            var adorners = layer.GetAdorners(control);
            if (adorners != null) {
                foreach (var adorner in adorners.OfType<WatermarkAdorner>()) {
                    adorner.Visibility = Visibility.Hidden;
                    layer.Remove(adorner);
                }
            }

            layer.Add(new WatermarkAdorner(control, GetWatermark(control)));
        }

        private static bool ShouldShowWatermark(UIElement c) {
            var comboBox = c as ComboBox;
            if (comboBox != null) {
                return comboBox.SelectedItem == null && string.IsNullOrEmpty(comboBox.Text);
            }

            var textBlock = c as TextBlock;
            if (textBlock != null) {
                return string.IsNullOrEmpty(textBlock.Text);
            }

            var textBox = c as TextBox;
            if (textBox != null) {
                return string.IsNullOrEmpty(textBox.Text);
            }

            var passwordBox = c as PasswordBox;
            if (passwordBox != null) {
                return string.IsNullOrEmpty(passwordBox.Password);
            }
            var itemsControl = c as ItemsControl;
            return itemsControl?.Items.Count == 0;
        }
    }
}
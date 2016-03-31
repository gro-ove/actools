using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Controls {
    public static class InputBindingBehavior {
        public static bool GetPropagateInputBindingsToWindow(FrameworkElement obj) {
            return (bool)obj.GetValue(PropagateInputBindingsToWindowProperty);
        }

        public static void SetPropagateInputBindingsToWindow(FrameworkElement obj, bool value) {
            obj.SetValue(PropagateInputBindingsToWindowProperty, value);
        }

        public static readonly DependencyProperty PropagateInputBindingsToWindowProperty =
            DependencyProperty.RegisterAttached("PropagateInputBindingsToWindow", typeof(bool), typeof(InputBindingBehavior),
            new PropertyMetadata(false, OnPropagateInputBindingsToWindowChanged));

        private static void OnPropagateInputBindingsToWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = (FrameworkElement)d;
            element.Loaded += FrameworkElement_Loaded;
            element.Unloaded += FrameworkElement_Unloaded;
        }

        /* sadly, I don't know how to get window from unloaded FrameworkElement, so I'm going a stupid way */
        private static readonly List<Window> OpenedWindows = new List<Window>(); 

        private static void FrameworkElement_Unloaded(object sender, RoutedEventArgs e) {
            var frameworkElement = (FrameworkElement)sender;
            frameworkElement.Unloaded -= FrameworkElement_Unloaded;

            foreach (var window in OpenedWindows) {
                for (var i = window.InputBindings.Count - 1; i >= 0; i--) {
                    var binding = window.InputBindings[i];
                    if (Equals(binding.CommandTarget, frameworkElement)) {
                        window.InputBindings.RemoveAt(i);
                    }
                }
            }
        }

        private static void FrameworkElement_Loaded(object sender, RoutedEventArgs e) {
            var frameworkElement = (FrameworkElement)sender;
            frameworkElement.Loaded -= FrameworkElement_Loaded;

            var window = Window.GetWindow(frameworkElement);
            if (window == null) {
                return;
            }

            if (!OpenedWindows.Contains(window)) {
                OpenedWindows.Add(window);
                window.Closed += Window_Closed;
            }

            for (var i = frameworkElement.InputBindings.Count - 1; i >= 0; i--) {
                var binding = frameworkElement.InputBindings[i];
                binding.CommandTarget = frameworkElement;
                window.InputBindings.Add(binding);
                frameworkElement.InputBindings.RemoveAt(i);
            }
        }

        private static void Window_Closed(object sender, System.EventArgs e) {
            var window = (Window)sender;
            window.Closed -= Window_Closed;
            OpenedWindows.Remove(window);
        }
    }
}

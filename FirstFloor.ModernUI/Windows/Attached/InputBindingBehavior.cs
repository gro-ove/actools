using System.Collections.Generic;
using System.Windows;

namespace FirstFloor.ModernUI.Windows.Attached {
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

        /* sadly, I don’t know how to get window from unloaded FrameworkElement, so I’m going a stupid way */
        private static readonly List<Window> OpenedWindows = new List<Window>();

        public static void UpdateBindings(FrameworkElement element) {
            RemoveBindings(element);
            SetBindings(element);
        }

        private static void SetBindings(UIElement element) {
            var window = Window.GetWindow(element);
            if (window == null) {
                return;
            }

            if (!OpenedWindows.Contains(window)) {
                OpenedWindows.Add(window);
                window.Closed += Window_Closed;
            }

            for (var i = element.InputBindings.Count - 1; i >= 0; i--) {
                var binding = element.InputBindings[i];
                binding.CommandTarget = element;
                window.InputBindings.Add(binding);
                element.InputBindings.RemoveAt(i);
            }
        }

        private static void RemoveBindings(UIElement element) {
            foreach (var window in OpenedWindows) {
                for (var i = window.InputBindings.Count - 1; i >= 0; i--) {
                    var binding = window.InputBindings[i];
                    if (Equals(binding.CommandTarget, element)) {
                        window.InputBindings.RemoveAt(i);
                    }
                }
            }
        }

        private static void FrameworkElement_Unloaded(object sender, RoutedEventArgs e) {
            var element = (FrameworkElement)sender;
            element.Unloaded -= FrameworkElement_Unloaded;
            RemoveBindings(element);
        }

        private static void FrameworkElement_Loaded(object sender, RoutedEventArgs e) {
            var element = (FrameworkElement)sender;
            element.Loaded -= FrameworkElement_Loaded;
            SetBindings(element);
        }

        private static void Window_Closed(object sender, System.EventArgs e) {
            var window = (Window)sender;
            window.Closed -= Window_Closed;
            OpenedWindows.Remove(window);
        }
    }
}

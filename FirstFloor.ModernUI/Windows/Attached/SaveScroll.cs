using System;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class SaveScroll {
        static SaveScroll() {}

        public static string GetKey(DependencyObject obj) {
            return (string)obj.GetValue(KeyProperty);
        }

        public static void SetKey(DependencyObject obj, string value) {
            obj.SetValue(KeyProperty, value);
        }

        public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(string),
                typeof(SaveScroll), new UIPropertyMetadata(OnKeyChanged));

        private static void ApplyKeyChanged(ScrollViewer scrollViewer, DependencyPropertyChangedEventArgs e) {
            if (scrollViewer != null && (e.NewValue == null || e.NewValue is string)) {
                var newValue = (string)e.NewValue;
                if (newValue != null) {
                    if (e.OldValue == null) {
                        scrollViewer.ScrollChanged += OnScroll;
                        scrollViewer.ScrollChanged += OnViewportResize;
                    }

                    if (scrollViewer.IsLoaded) {
                        LoadScroll(scrollViewer);
                    } else {
                        scrollViewer.Loaded += OnLoaded;
                    }
                } else {
                    if (e.OldValue == null) {
                        scrollViewer.ScrollChanged -= OnScroll;
                        scrollViewer.ScrollChanged -= OnViewportResize;
                    }

                    scrollViewer.Loaded -= OnLoaded;
                }
            }
        }

        private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var scrollViewer = d as ScrollViewer;
            if (scrollViewer != null) {
                ApplyKeyChanged(scrollViewer, e);
                return;
            }

            var listBox = d as ListBox;
            if (listBox == null) return;
            if (listBox.IsLoaded) {
                scrollViewer = listBox.FindVisualChild<ScrollViewer>();
                ApplyKeyChanged(scrollViewer, e);
            }

            RoutedEventHandler handler = null;
            handler = (sender, args) => {
                listBox.Loaded -= handler;
                scrollViewer = listBox.FindVisualChild<ScrollViewer>();
                ApplyKeyChanged(scrollViewer, e);
            };
            listBox.Loaded += handler;
        }

        [CanBeNull]
        private static string GetProperKey(object v) {
            var viewer = v as ScrollViewer;
            if (viewer == null) return null;

            var r = GetKey(viewer);
            if (r == null) {
                var p = viewer.GetParent<ListBox>();
                if (p == null) return null;
                r = GetKey(p);
            }

            return r == null ? null : @".scroll:" + r;
        }

        private static DateTime _lastScrolled;

        private static void LoadScroll(ScrollViewer viewer) {
            var k = GetProperKey(viewer);
            if (k != null) {
                _lastScrolled = DateTime.Now;
                viewer.ScrollToVerticalOffset(ValuesStorage.GetDouble(k));
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) {
            var c = (ScrollViewer)sender;
            c.Loaded -= OnLoaded;
            LoadScroll(c);
        }

        private static void OnScroll(object sender, ScrollChangedEventArgs e) {
            var c = (ScrollViewer)sender;
            var k = GetProperKey(sender);
            if ((DateTime.Now - _lastScrolled).TotalSeconds > 0.5) {
                c.ScrollChanged -= OnViewportResize;
                if (k != null) {
                    ValuesStorage.Set(k, c.VerticalOffset);
                }
            }
        }

        private static void OnViewportResize(object sender, ScrollChangedEventArgs e) {
            if (Equals(e.ViewportHeightChange, 0d)) {
                var c = (ScrollViewer)sender;
                LoadScroll(c);
            }
        }
    }
}
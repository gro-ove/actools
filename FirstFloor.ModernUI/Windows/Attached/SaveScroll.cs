using System;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class SaveScroll {
        static SaveScroll() {}

        /*public static string GetTriggerLoad(DependencyObject obj) {
            return (string)obj.GetValue(TriggerLoadProperty);
        }

        public static void SetTriggerLoad(DependencyObject obj, string value) {
            obj.SetValue(TriggerLoadProperty, value);
        }

        public static readonly DependencyProperty TriggerLoadProperty = DependencyProperty.RegisterAttached("TriggerLoad", typeof(string),
                typeof(SaveScroll), new UIPropertyMetadata(OnTriggerLoadChanged));

        private static void OnTriggerLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            OnKeyChanged(d, e);
        }*/

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
            if (d is ScrollViewer scrollViewer) {
                ApplyKeyChanged(scrollViewer, e);
                return;
            }

            if (d is ListBox listBox) {
                if (listBox.IsLoaded) {
                    scrollViewer = listBox.FindVisualChild<ScrollViewer>();
                    ApplyKeyChanged(scrollViewer, e);
                }

                void Handler(object sender, RoutedEventArgs args) {
                    listBox.Loaded -= Handler;
                    scrollViewer = listBox.FindVisualChild<ScrollViewer>();
                    ApplyKeyChanged(scrollViewer, e);
                }

                listBox.Loaded += Handler;
            }
        }

        [CanBeNull]
        private static string GetProperKey(object v) {
            if (v is ScrollViewer viewer) {
                var r = GetKey(viewer);
                if (r == null) {
                    var p = viewer.GetParent<ListBox>();
                    if (p == null) return null;
                    r = GetKey(p);
                }

                return r == null ? null : @".scroll:" + r;
            }

            return null;
        }

        private static DateTime _lastScrolled;

        private static void LoadScroll(ScrollViewer viewer) {
            var k = GetProperKey(viewer);
            if (k != null) {
                _lastScrolled = DateTime.Now;
                viewer.ScrollToVerticalOffset(ValuesStorage.Get<double>(k));
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) {
            var c = (ScrollViewer)sender;
            c.Loaded -= OnLoaded;
            LoadScroll(c);
        }

        private static void OnScroll(object sender, ScrollChangedEventArgs e) {
            var c = (ScrollViewer)sender;
            if (!c.IsLoaded) return;

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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class RenameEntry {
        public string Key { get; set; }
        public string SourceKey { get; set; }
    }

    public class RenameEntries : Collection<RenameEntry> { }

    public class ReplaceEntry {
        public string ReplacementKey { get; set; }
        public string PatternKey { get; set; }
        public string TargetKey { get; set; }
    }

    public class ReplaceEntries : Collection<ReplaceEntry> { }

    public static class Resources {
        public static IList GetRenames(DependencyObject obj) {
            var collection = (IList)obj.GetValue(RenamesProperty);
            if (collection == null) {
                collection = new List<object>();
                obj.SetValue(RenamesProperty, collection);
            }

            return collection ;
        }

        public static void SetRenames(DependencyObject obj, IList value) {
            obj.SetValue(RenamesProperty, value);
        }

        public static readonly DependencyProperty RenamesProperty = DependencyProperty.RegisterAttached("Renames", typeof(IList),
                typeof(Resources), new UIPropertyMetadata(OnRenamesChanged));

        private static void OnRenamesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || !(e.NewValue is IList)) return;

            var newValue = (IList)e.NewValue;
            if (newValue != null) {
                element.Loaded += (sender, args) => {
                    foreach (var v in newValue.OfType<RenameEntry>()) {
                        element.Resources[v.Key] = element.TryFindResource(v.SourceKey);
                    }
                };
            }
        }

        public static IList GetReplaces(DependencyObject obj) {
            var collection = (IList)obj.GetValue(ReplacesProperty);
            if (collection == null) {
                collection = new List<object>();
                obj.SetValue(ReplacesProperty, collection);
            }

            return collection ;
        }

        // TODO: Extend if needed
        private static bool AreSame(object a, object b) {
            if (Equals(a, b)) return true;

            var ab = a as SolidColorBrush;
            var bb = b as SolidColorBrush;
            if (ab != null && ab.Color == bb?.Color) return true;

            return false;
        }

        public static void SetReplaces(DependencyObject obj, IList value) {
            obj.SetValue(ReplacesProperty, value);
        }

        public static readonly DependencyProperty ReplacesProperty = DependencyProperty.RegisterAttached("Replaces", typeof(IList),
                typeof(Resources), new UIPropertyMetadata(OnReplacesChanged));

        private static void OnReplacesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || !(e.NewValue is IList)) return;

            var newValue = (IList)e.NewValue;
            if (newValue != null) {
                element.Loaded += (sender, args) => {
                    foreach (var v in newValue.OfType<ReplaceEntry>()) {
                        if (AreSame(element.TryFindResource(v.TargetKey), element.TryFindResource(v.PatternKey))) {
                            element.Resources[v.TargetKey] = element.TryFindResource(v.ReplacementKey);
                        }
                    }
                };
            }
        }
    }
}
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ToUpperSubMenuConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString().ToUpperInvariant();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString().ToLowerInvariant();
    }

    public class ModernSubMenuItemContainerStyleSelector : StyleSelector {
        public override Style SelectStyle(object item, DependencyObject container) => item is LinkInputEmpty ?
                LinkInputEmptyStyle : item is LinkInput ? LinkInputStyle : item is Link ? LinkStyle : null;

        public Style LinkInputStyle { get; set; }

        public Style LinkInputEmptyStyle { get; set; }

        public Style LinkStyle { get; set; }
    }

    public static class DependencyObjectHelper {
        [CanBeNull]
        public static T FindChild<T>([CanBeNull] this DependencyObject parent, [CanBeNull] string childName) where T : DependencyObject {
            // Confirm parent and childName are valid.
            if (parent == null) return null;

            T foundChild = null;

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                var childType = child as T;
                if (childType == null) {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child.
                    if (foundChild != null) break;
                } else if (!string.IsNullOrEmpty(childName)) {
                    var frameworkElement = child as FrameworkElement;
                    // If the child’s name is set for search
                    if (frameworkElement == null || frameworkElement.Name != childName) continue;
                    // if the child’s name is of the request name
                    foundChild = (T)child;
                    break;
                } else {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }
    }
}

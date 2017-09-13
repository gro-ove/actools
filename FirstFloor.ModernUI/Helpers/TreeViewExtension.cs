using System.Linq;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class TreeViewExtension {
        public static bool SetSelectedItem([CanBeNull] this TreeView treeView, [CanBeNull] object item) {
            return SetSelected(treeView, item);
        }

        private static bool SetSelected([CanBeNull] ItemsControl parent, [CanBeNull] object child) {
            if (parent == null || child == null) {
                return false;
            }

            var childNode = parent.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
            if (childNode != null) {
                childNode.Focus();
                return childNode.IsSelected = true;
            }

            if (parent.Items.Count > 0) {
                return (from object childItem in parent.Items select parent.ItemContainerGenerator.ContainerFromItem(childItem) as ItemsControl).Any(
                        childControl => SetSelected(childControl, child));
            }

            return false;
        }
    }
}
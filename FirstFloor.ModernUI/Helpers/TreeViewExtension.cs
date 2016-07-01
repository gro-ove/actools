using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Helpers {
    public static class TreeViewExtension {
        public static bool SetSelectedItem(this TreeView treeView, object item) {
            return SetSelected(treeView, item);
        }

        private static bool SetSelected(ItemsControl parent, object child) {
            if (parent == null || child == null) {
                return false;
            }

            var childNode = parent.ItemContainerGenerator
                                           .ContainerFromItem(child) as TreeViewItem;

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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.About;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.About {
    public partial class Credits {
        public Credits() {
            InitializeComponent();

            foreach (var license in Licenses.Entries.OrderBy(x => x.DisplayName)) {
                TreeView.Items.Add(new TreeViewItem {
                    Header = license.DisplayName,
                    Items = {
                        new BbCodeBlock {
                            BbCode = $"[url=\"{license.Url}\"]Homepage[/url]" + (license.Content == null ? "" : $"\n\n[mono]{license.Content}[/mono]")
                        }
                    }
                });
            }
        }

        private void TreeView_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            e.Handled = true;
            (((FrameworkElement)sender).Parent as UIElement)?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            });
        }
    }
}

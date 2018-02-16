using System;
using System.Threading.Tasks;
using System.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Lists {
    public partial class ToolsListPage {
        internal class HolderInner : NotifyPropertyChanged {
            private bool _active;

            public bool Active {
                get => _active;
                set => Apply(value, ref _active);
            }
        }

        internal static readonly HolderInner Holder = new HolderInner();

        public static Task Launch(string displayName, Uri uri, string filter = null) {
            if (Holder.Active) return Task.Delay(0);

            Holder.Active = true;
            var dialog = new ModernDialog {
                Title = displayName,
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = uri.ToString().Split('?')[0],
                MinWidth = 800,
                MinHeight = 480,
                Width = 800,
                Height = 640,
                MaxWidth = 99999,
                MaxHeight = 99999,
                Content = new ModernFrame { Source = string.IsNullOrWhiteSpace(filter) ? uri : uri.AddQueryParam("Filter", filter) }
            };

            dialog.Closed += OnDialogClosed;
            return dialog.ShowAndWaitAsync();
        }

        private static void OnDialogClosed(object sender, EventArgs e) {
            Holder.Active = false;
        }

        public ToolsListPage() {
            InitializeComponent();
        }
    }
}

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Lists {
    public class ToolLink : Link {
        public string Description { get; set; }

        public Geometry Icon { get; set; }

        protected virtual Uri LaunchSource => Source;

        protected void Launch() {
            ToolsListPage.Launch(DisplayName, LaunchSource);
        }

        private DelegateCommand _launchCommand;

        public DelegateCommand LaunchCommand => _launchCommand ?? (_launchCommand = new DelegateCommand(Launch,
                () => !ToolsListPage.Holder.Active).ListenOnWeak(ToolsListPage.Holder, nameof(ToolsListPage.Holder.Active)));
    }

    public class FilteredToolLink : ToolLink {
        public string FilterDescription { get; set; }

        public string DefaultFilter { get; set; }

        protected override Uri LaunchSource {
            get {
                var key = $@".FilteredToolLink:{Source.OriginalString}";
                var defaultValue = ValuesStorage.GetString(key) ?? DefaultFilter;
                var filter = Prompt.Show(FilterDescription ?? "Optional filter:", "Optional Filter", watermark: @"*", defaultValue: defaultValue,
                        suggestions: ValuesStorage.GetStringList("AcObjectListBox:FiltersHistory:car"));
                if (filter != null) {
                    ValuesStorage.Set(key, filter);
                }

                return string.IsNullOrWhiteSpace(filter) ? base.LaunchSource : base.LaunchSource.AddQueryParam("Filter", filter);
            }
        }
    }

    public partial class ToolsListPage {
        internal class HolderInner : NotifyPropertyChanged {
            private bool _active;

            public bool Active {
                get { return _active; }
                set {
                    if (Equals(value, _active)) return;
                    _active = value;
                    OnPropertyChanged();
                }
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

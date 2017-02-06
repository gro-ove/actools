using System;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Dialogs;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Lists {
    public class ToolLink : Link {
        public string Description { get; set; }

        public string LocationSizeKey { get; set; }

        public Geometry Icon { get; set; }

        private class HolderInner : NotifyPropertyChanged {
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

        private static readonly HolderInner Holder = new HolderInner();

        protected virtual Uri LaunchSource => Source;

        protected virtual void Launch() {
            if (Holder.Active) return;

            Holder.Active = true;
            var dialog = new ModernDialog {
                Title = DisplayName,
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = LocationSizeKey,
                MinWidth = 800,
                MinHeight = 480,
                Width = 800,
                Height = 640,
                MaxWidth = 99999,
                MaxHeight = 99999,
                Content = new ModernFrame { Source = LaunchSource }
            };
            dialog.Closed += OnDialogClosed;
            dialog.Show();
        }

        private DelegateCommand _launchCommand;

        public DelegateCommand LaunchCommand => _launchCommand ?? (_launchCommand = new DelegateCommand(Launch,
                () => !Holder.Active).ListenOnWeak(Holder, nameof(Holder.Active)));

        private static void OnDialogClosed(object sender, EventArgs e) {
            Holder.Active = false;
        }
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
        public ToolsListPage() {
            InitializeComponent();
        }
    }
}

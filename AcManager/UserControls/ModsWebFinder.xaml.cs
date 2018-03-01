using AcManager.Pages.Miscellaneous;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.UserControls {
    public partial class ModsWebFinder {
        public ViewModel Model => (ViewModel)DataContext;

        public ModsWebFinder(ModsWebBrowser.WebSource source) {
            DataContext = new ViewModel(source);
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ModsWebBrowser.WebSource Source { get; }

            public ViewModel(ModsWebBrowser.WebSource source) {
                Source = source;
                Value = source.AutoDownloadRule;
            }

            private string _value;

            [CanBeNull]
            public string Value {
                get => _value;
                set => Apply(value, ref _value);
            }
        }
    }
}
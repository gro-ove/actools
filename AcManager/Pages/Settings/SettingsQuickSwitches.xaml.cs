using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsQuickSwitches : ILoadableContent {
        public async Task LoadAsync(CancellationToken cancellationToken) {
            InitializeComponent();
            if (_widgets == null) {
                // creating a list of widget’s IDs
                _widgets = Resources.MergedDictionaries.SelectMany(x => x.Keys.OfType<string>()).Where(x => x.StartsWith(@"Widget")).ToArray();
            }

            var widgets = new Dictionary<string, WidgetEntry>(_widgets.Length);
            foreach (var key in _widgets) {
                widgets[key] = new WidgetEntry(key, (FrameworkElement)FindResource(key));
                await Task.Delay(10, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
            }

            DataContext = new ViewModel(widgets);
        }

        public void Load() {
            InitializeComponent();
            if (_widgets == null) {
                // creating a list of widget’s IDs
                _widgets = Resources.MergedDictionaries.SelectMany(x => x.Keys.OfType<string>()).Where(x => x.StartsWith(@"Widget")).ToArray();
            }

            Logging.Write(_widgets.JoinToString("\n"));
            DataContext = new ViewModel(_widgets.Select(x => new WidgetEntry(x, (FrameworkElement)FindResource(x))).ToDictionary(x => x.Key, x => x));
        }

        public void Initialize() {}

        private static string[] _widgets;

        public class WidgetEntry : IDraggable {
            public const string DraggableFormat = "Data-Widget";

            public WidgetEntry(string key, FrameworkElement element) {
                Key = key;
                Element = element;
            }

            public string Key { get; }

            public FrameworkElement Element { get; }

            public object ToolTip => Element.ToolTip;

            string IDraggable.DraggableFormat => DraggableFormat;
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public IReadOnlyDictionary<string, WidgetEntry> Widgets { get; }

            public BetterObservableCollection<WidgetEntry> Added { get; }

            public BetterObservableCollection<WidgetEntry> Stored { get; }

            public ViewModel(IReadOnlyDictionary<string, WidgetEntry> widgets) {
                Widgets = widgets;

                var active = SettingsHolder.Drive.QuickSwitchesList;

                // clean up all invalid entries from saved list
                if (active.Any(x => !widgets.Keys.Contains(x))) {
                    active = active.Where(widgets.Keys.Contains).ToArray();
                    SettingsHolder.Drive.QuickSwitchesList = active;
                }

                Stored = new BetterObservableCollection<WidgetEntry>(Widgets.Values.Where(x => !active.ArrayContains(x.Key)));
                Added = new BetterObservableCollection<WidgetEntry>(active.Select(x => Widgets.GetValueOrDefault(x)).NonNull());
                Added.CollectionChanged += Added_CollectionChanged;
            }

            private void Added_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                SettingsHolder.Drive.QuickSwitchesList = Added.Select(x => x.Key).ToArray();
            }

            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;
        }
    }
}

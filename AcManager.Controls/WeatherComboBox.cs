using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms.VisualStyles;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    public sealed class WeatherTypeWrapped : Displayable {
        public WeatherType Type { get; }

        public WeatherTypeWrapped(WeatherType type) {
            Type = type;
            DisplayName = type.GetDescription();
        }

        private bool Equals(WeatherTypeWrapped other) {
            return Type == other.Type;
        }

        public override bool Equals(object obj) {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj is WeatherTypeWrapped w && Equals(w));
        }

        public override int GetHashCode() {
            return (int)Type;
        }

        public override string ToString() {
            return $@"W. TYPE: {Type}";
        }
    }

    public class WeatherComboBox : HierarchicalComboBox {
        public static AcEnabledOnlyCollection<WeatherObject> WeatherList { get; } = WeatherManager.Instance.EnabledOnlyCollection;
        public static readonly Displayable RandomWeather = new Displayable { DisplayName = ToolsStrings.Weather_Random };

        public static WeatherObject Unwrap(object obj) {
            return obj is WeatherTypeWrapped weatherTypeWrapped
                    ? WeatherManager.Instance.EnabledOnlyCollection.Where(x => x.Type == weatherTypeWrapped.Type).RandomElementOrDefault()
                    : obj as WeatherObject;
        }

        public WeatherComboBox() {
            PreviewProvider = new WeatherPreviewProvider();
            ItemSelected += OnItemSelected;
            Loaded += OnLoaded;
            this.OnActualUnload(Unload);
        }

        private bool _loaded;

        private void Unload() {
            if (!_loaded) return;
            WeakEventManager<IBaseAcObjectObservableCollection, EventArgs>.RemoveHandler(WeatherManager.Instance.WrappersList,
                    nameof(IBaseAcObjectObservableCollection.CollectionReady), OnWeatherListUpdated);
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            if (_loaded) return;
            _loaded = true;

            UpdateHierarchicalWeatherList().Forget();
            WeakEventManager<IBaseAcObjectObservableCollection, EventArgs>.AddHandler(WeatherManager.Instance.WrappersList,
                    nameof(IBaseAcObjectObservableCollection.CollectionReady), OnWeatherListUpdated);
        }

        private void OnItemSelected(object sender, SelectedItemChangedEventArgs selectedItemChangedEventArgs) {
            _busy.Do(() => SelectedWeather = Unwrap(SelectedItem));
        }

        private readonly Busy _busy = new Busy();

        public static readonly DependencyProperty SelectedWeatherProperty = DependencyProperty.Register(nameof(SelectedWeather), typeof(WeatherObject),
                typeof(WeatherComboBox), new PropertyMetadata(OnSelectedWeatherChanged));

        public WeatherObject SelectedWeather {
            get => (WeatherObject)GetValue(SelectedWeatherProperty);
            set => SetValue(SelectedWeatherProperty, value);
        }

        private static void OnSelectedWeatherChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WeatherComboBox)o).OnSelectedWeatherChanged((WeatherObject)e.NewValue);
        }

        private void OnSelectedWeatherChanged(WeatherObject newValue) {
            _busy.Do(() => SelectedItem = newValue);
        }

        public static readonly DependencyProperty AllowRandomWeatherProperty = DependencyProperty.Register(nameof(AllowRandomWeather), typeof(bool),
                typeof(WeatherComboBox), new PropertyMetadata(false, (o, e) => {
                    var c = (WeatherComboBox)o;
                    c._allowRandomWeather = (bool)e.NewValue;
                    c.UpdateHierarchicalWeatherList().Forget();
                }));

        private bool _allowRandomWeather = false;

        public bool AllowRandomWeather {
            get => _allowRandomWeather;
            set => SetValue(AllowRandomWeatherProperty, value);
        }

        public static readonly DependencyProperty AllowWeatherByTypeProperty = DependencyProperty.Register(nameof(AllowWeatherByType), typeof(bool),
                typeof(WeatherComboBox), new PropertyMetadata(false, (o, e) => {
                    var c = (WeatherComboBox)o;
                    c._allowWeatherByType = (bool)e.NewValue;
                    c.UpdateHierarchicalWeatherList().Forget();
                }));

        private bool _allowWeatherByType = false;

        public bool AllowWeatherByType {
            get => _allowWeatherByType;
            set => SetValue(AllowWeatherByTypeProperty, value);
        }

        #region Constants and other non-changeable values
        private static bool IsKunosWeather(WeatherObject o) {
            switch (o.Id) {
                case "1_heavy_fog":
                case "2_light_fog":
                case "3_clear":
                case "4_mid_clear":
                case "5_light_clouds":
                case "6_mid_clouds":
                case "7_heavy_clouds":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsGbwWeather(WeatherObject o) {
            return o.Id.Contains("gbW");
        }

        private static bool IsModWeather(WeatherObject o) {
            return !IsKunosWeather(o) && !IsGbwWeather(o);
        }

        private class GbwConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value?.ToString().Replace("gbW_", "");
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        private async Task UpdateHierarchicalWeatherList() {
            await WeatherManager.Instance.EnsureLoadedAsync();

            HierarchicalGroup list;
            if (WeatherList.All(IsKunosWeather)) {
                if (AllowRandomWeather) {
                    list = new HierarchicalGroup { RandomWeather, new Separator() };
                    list.AddRange(WeatherList);
                } else {
                    list = new HierarchicalGroup(WeatherList);
                }
            } else {
                list = AllowRandomWeather ? new HierarchicalGroup { RandomWeather } : new HierarchicalGroup();

                if (WeatherList.Any(IsKunosWeather)) {
                    list.Add(new HierarchicalGroup(ToolsStrings.Weather_Original, WeatherList.Where(IsKunosWeather)));
                }

                if (AllowWeatherByType && WeatherList.Any(x => x.Type != WeatherType.None)) {
                    list.Add(new HierarchicalGroup("By Type", WeatherList.Select(x => x.Type).ApartFrom(WeatherType.None).Distinct()
                                                                         .Select(x => new WeatherTypeWrapped(x)).OrderBy(x => x.DisplayName)));
                }

                if (WeatherList.Any(IsGbwWeather)) {
                    list.Add(new HierarchicalGroup("GBW", WeatherList.Where(IsGbwWeather)) {
                        HeaderConverter = new GbwConverter()
                    });
                }

                if (WeatherList.Any(IsModWeather)) {
                    list.Add(new HierarchicalGroup(ToolsStrings.Weather_Mods, WeatherList.Where(x => !IsKunosWeather(x) && !IsGbwWeather(x))));
                }
            }

            ItemsSource = list;
        }

        private void OnWeatherListUpdated(object sender, EventArgs e) {
            UpdateHierarchicalWeatherList().Forget();
        }
        #endregion
    }
}
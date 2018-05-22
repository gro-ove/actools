using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    /// <summary>
    /// Bind to SelectedItem if you’re gonna use Random or Select-By-Type features, or to SelectedWeather otherwise.
    /// </summary>
    public class WeatherComboBox : HierarchicalComboBox {
        public static AcEnabledOnlyCollection<WeatherObject> WeatherList { get; } = WeatherManager.Instance.Enabled;

        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(nameof(Time), typeof(int?),
                typeof(WeatherComboBox), new PropertyMetadata(null, (o, e) => {
                    var w = (WeatherComboBox)o;
                    w._time = (int?)e.NewValue;
                    w.UpdateWeather();
                }));

        private int? _time;

        public int? Time {
            get => _time;
            set => SetValue(TimeProperty, value);
        }

        public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(nameof(Temperature), typeof(double?),
                typeof(WeatherComboBox), new PropertyMetadata(null, (o, e) => {
                    var w = (WeatherComboBox)o;
                    w._temperature = (double?)e.NewValue;
                    w.UpdateWeather();
                }));

        private double? _temperature;

        public double? Temperature {
            get => _temperature;
            set => SetValue(TemperatureProperty, value);
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

        private readonly Busy _updateBusy = new Busy();

        private void UpdateWeather() {
            _updateBusy.DoDelay(() => _busy.Do(() => { OnItemSelected(null, null); }), 500);
        }

        public event EventHandler WeatherChanged;

        private readonly Busy _busy = new Busy();

        private void OnItemSelected(object sender, SelectedItemChangedEventArgs selectedItemChangedEventArgs) {
            _busy.Do(() => SelectedWeather = WeatherTypeWrapped.Unwrap(SelectedItem, Time, Temperature));
            WeatherChanged?.Invoke(this, EventArgs.Empty);
        }

        public static readonly DependencyProperty SelectedWeatherProperty = DependencyProperty.Register(nameof(SelectedWeather), typeof(WeatherObject),
                typeof(WeatherComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedWeatherChanged));

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

        private bool _allowRandomWeather;

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

        private bool _allowWeatherByType;

        public bool AllowWeatherByType {
            get => _allowWeatherByType;
            set => SetValue(AllowWeatherByTypeProperty, value);
        }

        #region Constants and other non-changeable values
        private static bool IsKunosWeather(WeatherObject o) {
            return o.Author == AcCommonObject.AuthorKunos;
        }

        private static bool IsGbwWeather(WeatherObject o) {
            return o.Id.Contains(@"gbW");
        }

        private static bool IsA4SWeather(WeatherObject o) {
            return o.Id.Contains(@"a4s");
        }

        private static bool IsModWeather(WeatherObject o) {
            return !IsKunosWeather(o) && !IsGbwWeather(o);
        }

        private class GbwConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value?.ToString().Replace(@"gbW_", "");
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
                    list = new HierarchicalGroup { WeatherTypeWrapped.RandomWeather, new Separator() };
                    list.AddRange(WeatherList);
                } else {
                    list = new HierarchicalGroup(WeatherList);
                }
            } else {
                list = AllowRandomWeather ? new HierarchicalGroup { WeatherTypeWrapped.RandomWeather } : new HierarchicalGroup();

                if (WeatherList.Any(IsKunosWeather)) {
                    list.Add(new HierarchicalGroup(ToolsStrings.Weather_Original,
                            WeatherList.Where(IsKunosWeather)));
                }

                if (AllowWeatherByType && WeatherList.Any(x => x.Type != WeatherType.None)) {
                    list.Add(new HierarchicalGroup("By type",
                            WeatherList.Select(x => x.Type).ApartFrom(WeatherType.None).Distinct()
                                       .Select(x => new WeatherTypeWrapped(x)).OrderBy(x => x.DisplayName)));
                }

                if (WeatherList.Any(IsGbwWeather)) {
                    list.Add(new HierarchicalGroup(@"GBW",
                            WeatherList.Where(IsGbwWeather)) {
                                HeaderConverter = new GbwConverter()
                            });
                }

                if (WeatherList.Any(IsA4SWeather)) {
                    list.Add(new HierarchicalGroup(@"A4S",
                            WeatherList.Where(IsA4SWeather)));
                }

                if (WeatherList.Any(IsModWeather)) {
                    list.Add(new HierarchicalGroup(ToolsStrings.Weather_Mods,
                            WeatherList.Where(x => !IsKunosWeather(x) && !IsGbwWeather(x) && !IsA4SWeather(x))));
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

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
            WeakEventManager<WeatherFxControllerData.Holder, EventArgs>.RemoveHandler(WeatherFxControllerData.Instance,
                    nameof(WeatherFxControllerData.Holder.Reloaded), OnWeatherControllersUpdated);
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            if (_loaded) return;
            _loaded = true;

            UpdateHierarchicalWeatherList().Ignore();
            WeakEventManager<IBaseAcObjectObservableCollection, EventArgs>.AddHandler(WeatherManager.Instance.WrappersList,
                    nameof(IBaseAcObjectObservableCollection.CollectionReady), OnWeatherListUpdated);
            WeakEventManager<WeatherFxControllerData.Holder, EventArgs>.AddHandler(WeatherFxControllerData.Instance,
                    nameof(WeatherFxControllerData.Holder.Reloaded), OnWeatherControllersUpdated);
            UpdateReferences();
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
                    c.UpdateHierarchicalWeatherList().Ignore();
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
                    c.UpdateHierarchicalWeatherList().Ignore();
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

        private static bool IsAbnWeather(WeatherObject o) {
            return o.Id.StartsWith(@"abn_")
                    || o.Id.Contains(@" (Set Time at ")
                    || o.Id.Contains(@" (Time Range ");
        }

        private static bool IsSolWeather(WeatherObject o) {
            return o.Id.StartsWith(@"sol_");
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

        private static WeatherType?[] _weathersOrdered = {
            WeatherType.Clear,
            WeatherType.FewClouds,
            WeatherType.ScatteredClouds,
            WeatherType.BrokenClouds,
            WeatherType.OvercastClouds,
            null,
            WeatherType.Mist,
            WeatherType.Fog,
            null,
            WeatherType.LightDrizzle,
            WeatherType.Drizzle,
            WeatherType.HeavyDrizzle,
            WeatherType.LightRain,
            WeatherType.Rain,
            WeatherType.HeavyRain,
            WeatherType.LightThunderstorm,
            WeatherType.Thunderstorm,
            WeatherType.HeavyThunderstorm,
            WeatherType.LightSleet,
            WeatherType.Sleet,
            WeatherType.HeavySleet,
            WeatherType.LightSnow,
            WeatherType.Snow,
            WeatherType.HeavySnow,
            null,
            WeatherType.Tornado,
            WeatherType.Hurricane,
            null,
            WeatherType.Smoke,
            WeatherType.Haze,
            WeatherType.Sand,
            WeatherType.Dust,
            WeatherType.Squalls,
            WeatherType.Cold,
            WeatherType.Hot,
            WeatherType.Windy,
            WeatherType.Hail
        };

        private static object[] _virtualWeathers;

        private static IReadOnlyCollection<object> VirtualWeathers => _virtualWeathers ?? (_virtualWeathers = _weathersOrdered
                .Select(x => x == null ? (object)new Separator() : new WeatherTypeWrapped(x.Value)).ToArray());

        private static SharedResourceDictionary _settingsDictionary;

        private static SharedResourceDictionary SettingsDictionary => _settingsDictionary ?? (_settingsDictionary = new SharedResourceDictionary {
            Source = new Uri("/AcManager.Controls;component/Assets/AcSettingsSpecific.xaml", UriKind.Relative)
        });

        protected override bool IsItemPresent(IList newValue, object item) {
            if (item is WeatherTypeWrapped w && w.ControllerId != null && PatchHelper.IsWeatherFxActive()) {
                return true;
            }
            return base.IsItemPresent(newValue, item);
        }

        private WeatherType _lastWeatherType = WeatherType.Clear;

        protected override void OnSelectedItemChanged(object oldValue, object newValue) {
            base.OnSelectedItemChanged(oldValue, newValue);
            if (newValue is WeatherTypeWrapped w) {
                if (w.TypeOpt != WeatherType.None) {
                    _lastWeatherType = w.TypeOpt;
                } else {
                    UpdateReferences();
                }
            }
        }

        private async Task UpdateHierarchicalWeatherList() {
            if (!_loaded) return;
            await WeatherManager.Instance.EnsureLoadedAsync();

            HierarchicalGroup list;
            if (WeatherList.All(IsKunosWeather) && !PatchHelper.IsWeatherFxActive()) {
                if (AllowRandomWeather) {
                    list = new HierarchicalGroup { WeatherTypeWrapped.RandomWeather, new Separator() };
                    list.AddRange(WeatherList);
                } else {
                    list = new HierarchicalGroup(WeatherList);
                }
            } else {
                list = AllowRandomWeather ? new HierarchicalGroup { WeatherTypeWrapped.RandomWeather } : new HierarchicalGroup();

                if (PatchHelper.IsWeatherFxActive()) {
                    IEnumerable<object> weathers = VirtualWeathers;
                    if (!PatchHelper.IsRainFxActive()) {
                        weathers = weathers.Where(x => {
                            if (x is WeatherTypeWrapped w) {
                                return w.TypeOpt >= WeatherType.Clear && w.TypeOpt != WeatherType.Hurricane && w.TypeOpt != WeatherType.Tornado;
                            }
                            return true;
                        }).SubsequentDistinct(x => x is Separator ? null : x);
                    } else if (!PatchHelper.IsFeatureSupported(PatchHelper.FeatureSnow)) {
                        weathers = weathers.Where(x => {
                            if (x is WeatherTypeWrapped w) {
                                return w.TypeOpt < WeatherType.LightSnow || w.TypeOpt > WeatherType.HeavySnow;
                            }
                            return true;
                        });
                    }
                    
                    var items = WeatherFxControllerData.Instance.Items;
                    var baseItems = items.Where(x => x.FollowsSelectedWeather)
                            .Select(x => new WeatherTypeWrapped(x)).ToList();
                    if (baseItems.Count > 1) {
                        weathers = weathers.Append(new Separator());
                        weathers = weathers.Append(new TextBlock { 
                            Text = "Controller:", Style = (Style)SettingsDictionary["Label"], 
                            Margin = new Thickness(-20d, 0d, 0d, 0d) 
                        });

                        weathers = weathers.Concat(baseItems.Select(x => {
                            var menuItem = new MenuItem {
                                Header = x.ControllerRef?.DisplayName ?? x.DisplayName, 
                                ToolTip = x.ControllerRef?.GetToolTip(),
                                IsCheckable = true, 
                                StaysOpenOnClick = true
                            };
                            menuItem.SetBinding(MenuItem.IsCheckedProperty, new Binding {
                                Path = new PropertyPath(nameof(WeatherFxControllerData.IsSelectedAsBase)),
                                Source = x.ControllerRef,
                                Mode = BindingMode.OneWay
                            });
                            menuItem.Click += (s, a) => {
                                a.Handled = true;
                                if (x.ControllerRef != null) {
                                    x.ControllerRef.IsSelectedAsBase = true;
                                    var item = Flatten(ItemsSource).OfType<WeatherTypeWrapped>()
                                            .FirstOrDefault(y => y.TypeOpt == _lastWeatherType);
                                    if (item != null) {
                                        SelectedItem = item;
                                    }
                                }
                            };
                            return menuItem;
                        }));
                    }
                    
                    list.Add(new Separator());
                    list.Add(new WeatherTypeWrapped(WeatherType.Clear));
                    list.Add(new WeatherTypeWrapped(WeatherType.ScatteredClouds));
                    list.Add(new WeatherTypeWrapped(WeatherType.OvercastClouds));
                    list.Add(new WeatherTypeWrapped(WeatherType.Fog));
                    if (PatchHelper.IsRainFxActive()) {
                        list.Add(new WeatherTypeWrapped(WeatherType.Rain));
                    }
                    list.Add(new HierarchicalGroup("More", weathers));

                    var dynamicItems = items.Where(x => !x.FollowsSelectedWeather)
                            .Select(x => new WeatherTypeWrapped(x)).ToList();
                    if (dynamicItems.Count > 0) {
                        list.Add(new Separator());
                        list.Add(new TextBlock { 
                            Text = "Dynamic:", Style = (Style)SettingsDictionary["Label"], 
                            Margin = new Thickness(-20d, 0d, 0d, 0d) 
                        });

                        foreach (var item in dynamicItems) {
                            if (item.ControllerRef == null) continue;
                            if (item.ControllerRef.Config != null) {
                                var editor = new ContentControl {
                                    ContentTemplate = (DataTemplate)SettingsDictionary[item.ControllerRef.Config.Sections.Count == 1 
                                            ? "PythonAppConfig.Compact.InlineSingle" : "PythonAppConfig.Compact.Inline"],
                                    Content = item.ControllerRef.Config
                                };
                                editor.MouseUp += (sender, args) => {
                                    if (args.ChangedButton == MouseButton.Left) {
                                        args.Handled = true;
                                    }
                                };
                            
                                var menuItem = new MenuItem {
                                    Header = item.DisplayName,
                                    ToolTip = item.ControllerRef.GetToolTip(),
                                    Style = (Style)SettingsDictionary["WeatherLiveControllerMenuItem"],
                                    ItemsSource = new[] { editor }
                                };
                                menuItem.PreviewMouseDown += (sender, args) => {
                                    var selected = (MenuItem)sender;
                                    SelectedItem = item;
                                    if (selected.FindVisualChild<Popup>()?.IsOpen != true) {
                                        if (this.FindVisualChild<Popup>() is Popup p) p.IsOpen = false;
                                    }
                                };
                                
                                list.Add(menuItem);
                            } else {
                                list.Add(item);
                            }
                        }
                    }
                } else {
                    if (WeatherList.Any(IsKunosWeather)) {
                        list.Add(new HierarchicalGroup(ToolsStrings.Weather_Original,
                                WeatherList.Where(IsKunosWeather)));
                    }
                    
                    if (AllowWeatherByType && WeatherList.Any(x => x.Type != WeatherType.None)) {
                        list.Add(new HierarchicalGroup(ControlsStrings.Weather_Group_ByType,
                                WeatherList.Select(x => x.Type).ApartFrom(WeatherType.None).Distinct()
                                        .Select(x => new WeatherTypeWrapped(x)).OrderBy(x => _weathersOrdered.IndexOf(x.TypeOpt))));
                    }

                    if (WeatherList.Any(IsSolWeather)) {
                        list.Add(new HierarchicalGroup(@"Sol", WeatherList.Where(IsSolWeather)) {
                            HeaderConverter = new GbwConverter()
                        });
                    }

                    if (WeatherList.Any(IsGbwWeather)) {
                        list.Add(new HierarchicalGroup(@"GBW", WeatherList.Where(IsGbwWeather)) {
                            HeaderConverter = new GbwConverter()
                        });
                    }

                    if (WeatherList.Any(IsA4SWeather)) {
                        list.Add(new HierarchicalGroup(@"A4S", WeatherList.Where(IsA4SWeather)));
                    }

                    if (WeatherList.Any(IsAbnWeather)) {
                        list.Add(new HierarchicalGroup(@"AssettoByNight", WeatherList.Where(IsAbnWeather)));
                    }

                    if (WeatherList.Any(IsModWeather)) {
                        list.Add(new HierarchicalGroup(ToolsStrings.Weather_Mods,
                                WeatherList.Where(x => !IsKunosWeather(x) && !IsGbwWeather(x) && !IsA4SWeather(x))));
                    }
                }
            }

            ItemsSource = list;
        }

        private void OnWeatherListUpdated(object sender, EventArgs e) {
            UpdateHierarchicalWeatherList().Ignore();
        }

        private void UpdateReferences() {
            if (SelectedItem is WeatherTypeWrapped w) {
                w.RefreshReference();
            }
        }

        private void OnWeatherControllersUpdated(object sender, EventArgs e) {
            UpdateHierarchicalWeatherList().Ignore();
            UpdateReferences();
        }
        #endregion
    }
}
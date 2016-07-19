using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writer;

namespace AcManager.Pages.Selected {
    public partial class SelectedWeatherPage : ILoadableContent, IParametrizedUriContent, INotifyPropertyChanged {
        private const string KeyEditMode = "weather.editmode";

        public class ViewModel : SelectedAcObjectViewModel<WeatherObject> {
            public ViewModel([NotNull] WeatherObject acObject) : base(acObject) {
                SelectedObject.PropertyChanged += SelectedObject_PropertyChanged;
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= SelectedObject_PropertyChanged;
            }

            private void SelectedObject_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(SelectedObject.TemperatureCoefficient)) {
                    OnPropertyChanged(nameof(RoadTemperature));
                }
            }

            private const string KeyTemperature = "swp.temp";

            private double _temperature = ValuesStorage.GetDouble(KeyTemperature, 20d);
            
            public double Temperature {
                get { return _temperature; }
                set {
                    value = value.Round(0.5);
                    if (Equals(value, _temperature)) return;
                    _temperature = value.Clamp(QuickDrive.TemperatureMinimum, QuickDrive.TemperatureMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    ValuesStorage.Set(KeyTemperature, value);
                }
            }

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedObject.TemperatureCoefficient);

            private const string KeyTime = "swp.time";

            private int _time = ValuesStorage.GetInt(KeyTime, 12 * 60 * 60);

            public int Time {
                get { return _time; }
                set {
                    if (value == _time) return;
                    _time = value.Clamp((int)QuickDrive.TimeMinimum, (int)QuickDrive.TimeMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(RoadTemperature));
                    ValuesStorage.Set(KeyTime, value);
                }
            }

            public string DisplayTime {
                get { return $"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}"; }
                set {
                    int time;
                    if (!FlexibleParser.TryParseTime(value, out time)) return;
                    Time = time;
                }
            }

            #region Weather types
            public WeatherType[] WeatherTypes { get; } = WeatherTypesArray;

            private static readonly WeatherType[] WeatherTypesArray = {
                WeatherType.None,
                WeatherType.LightThunderstorm,
                WeatherType.Thunderstorm,
                WeatherType.HeavyThunderstorm,
                WeatherType.LightDrizzle,
                WeatherType.Drizzle,
                WeatherType.HeavyDrizzle,
                WeatherType.LightRain,
                WeatherType.Rain,
                WeatherType.HeavyRain,
                WeatherType.LightSnow,
                WeatherType.Snow,
                WeatherType.HeavySnow,
                WeatherType.LightSleet,
                WeatherType.Sleet,
                WeatherType.HeavySleet,
                WeatherType.Clear,
                WeatherType.FewClouds,
                WeatherType.ScatteredClouds,
                WeatherType.BrokenClouds,
                WeatherType.OvercastClouds,
                WeatherType.Fog,
                WeatherType.Mist,
                WeatherType.Smoke,
                WeatherType.Haze,
                WeatherType.Sand,
                WeatherType.Dust,
                WeatherType.Squalls,
                WeatherType.Tornado,
                WeatherType.Hurricane,
                WeatherType.Cold,
                WeatherType.Hot,
                WeatherType.Windy,
                WeatherType.Hail
            };
            #endregion

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async o => {
                byte[] data = null;

                try {
                    if (new FileInfo(SelectedObject.IniFilename).Length > 10000 ||
                            new FileInfo(SelectedObject.ColorCurvesIniFilename).Length > 5000) {
                        NonfatalError.Notify("Can’t share weather", "Files are too big.");
                        return;
                    }
                    
                    await Task.Run(() => {
                        using (var memory = new MemoryStream()) {
                            using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                writer.Write("weather.ini", SelectedObject.IniFilename);
                                writer.Write("colorCurves.ini", SelectedObject.ColorCurvesIniFilename);
                            }

                            data = memory.ToArray();
                        }
                    });
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t share weather", "Make sure files are readable.", e);
                    return;
                }

                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.Weather, SelectedObject.Name, SelectedObject.Id, data);
            }));

            private ICommand _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new AsyncCommand(o => {
                SelectedObject.SaveCommand.Execute(null);
                return QuickDrive.RunAsync(weatherId: SelectedObject.Id);
            }, o => SelectedObject.Enabled));

            private ICommand _viewTemperatureReadmeCommand;

            public ICommand ViewTemperatureReadmeCommand => _viewTemperatureReadmeCommand ?? (_viewTemperatureReadmeCommand = new RelayCommand(o => {
                ModernDialog.ShowMessage(
                        @"We are using an equation to create a graph that determines the asphalt temperature relatively to ambient temperature, weather and day time.

Check the graph in [url=""http://fooplot.com/plot/3x7y44pfli""]this link[/url].

The equation used is:
[mono](((-10×α)*x)+10*α)*2((exp(-6*x)*(0.4*sin(6*x))+0.1)*(15/1.5)*sin(0.9*x))+15[/mono]

Change the 1 values in (((-10*1)*x)+10*1) to see the results in your graphs.
Accepted values are from -1 to 1.");
            }));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private WeatherObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await WeatherManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = WeatherManager.Instance.GetById(_id);
        }

        public bool EditMode { get; private set; } = ValuesStorage.GetBool(KeyEditMode);

        private FrameworkElement _editMode;

        private ICommand _toggleEditModeCommand;

        public ICommand ToggleEditModeCommand => _toggleEditModeCommand ?? (_toggleEditModeCommand = new RelayCommand(o => {
            EditMode = !EditMode;
            OnPropertyChanged(nameof(EditMode));
            ValuesStorage.Set(KeyEditMode, EditMode);

            if (EditMode) {
                ToEditMode();
            } else {
                Wrapper.Children.Remove(_editMode);
                _editMode = null;
            }
        }));

        private void ToEditMode() {
            _model.SelectedObject.EnsureLoadedExtended();
            _editMode = (FrameworkElement)FindResource("EditMode");
            Wrapper.Children.Add(_editMode);
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(ToggleEditModeCommand, new KeyGesture(Key.E, ModifierKeys.Control)),
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });
            InitializeComponent();

            if (EditMode) {
                ToEditMode();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

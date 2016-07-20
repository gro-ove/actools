using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.About;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
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
using Microsoft.Win32;
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

            private const long SharingSizeLimit = 2 * 1024 * 1024;

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async o => {
                byte[] data = null;

                try {
                    if (!File.Exists(SelectedObject.IniFilename)) {
                        NonfatalError.Notify("Can’t share weather", $"File “{Path.GetFileName(SelectedObject.IniFilename)}” is missing.");
                        return;
                    }

                    if (!File.Exists(SelectedObject.ColorCurvesIniFilename)) {
                        NonfatalError.Notify("Can’t share weather", $"File “{Path.GetFileName(SelectedObject.ColorCurvesIniFilename)}” is missing.");
                        return;
                    }
                    
                    await Task.Run(() => {
                        using (var memory = new MemoryStream()) {
                            using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                writer.Write("weather.ini", SelectedObject.IniFilename);
                                writer.Write("colorCurves.ini", SelectedObject.ColorCurvesIniFilename);

                                if (File.Exists(SelectedObject.PreviewImage)) {
                                    writer.Write("preview.jpg", SelectedObject.PreviewImage);
                                }

                                var clouds = Path.Combine(SelectedObject.Location, "clouds");
                                if (Directory.Exists(clouds)) {
                                    foreach (var cloud in Directory.GetFiles(clouds, "*.dds")) {
                                        writer.Write(FileUtils.GetRelativePath(cloud, SelectedObject.Location), cloud);
                                    }
                                }
                            }

                            data = memory.ToArray();
                        }
                    });

                    if (data.Length > SharingSizeLimit) {
                        NonfatalError.Notify("Can’t share weather", $"Files are too big. Limit is {SharingSizeLimit.ToReadableSize()}.");
                        return;
                    }
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

                int time;
                return QuickDrive.RunAsync(weatherId: SelectedObject.Id, time: FlexibleParser.TryParseTime(o as string, out time) ? time : (int?)null);
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

            private const string KeyUpdatePreviewMessageShown = "swp.upms";

            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new AsyncCommand(async o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    UpdatePreviewDirectCommand.Execute(o);
                    return;
                }

                if (!ValuesStorage.GetBool(KeyUpdatePreviewMessageShown) && ModernDialog.ShowMessage(
                        ImportantTips.Entries.GetByIdOrDefault("trackPreviews")?.Content, "How-To", MessageBoxButton.OK) !=
                        MessageBoxResult.OK) {
                    return;
                }

                var directory = FileUtils.GetDocumentsScreensDirectory();
                var shots = Directory.GetFiles(directory);

                await QuickDrive.RunAsync(weatherId: SelectedObject.Id);
                if (ScreenshotsConverter.CurrentConversion?.IsCompleted == false) {
                    await ScreenshotsConverter.CurrentConversion;
                }

                var newShots = Directory.GetFiles(directory).Where(x => !shots.Contains(x) && (
                        x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                x.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                x.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))).ToList();

                if (!newShots.Any()) {
                    NonfatalError.Notify("Can’t update preview", "You were supposed to make at least one screenshot.");
                    return;
                }

                ValuesStorage.Set(KeyUpdatePreviewMessageShown, true);

                var shot = new ImageViewer(newShots) {
                    Model = {
                        MaxImageHeight = 575d,
                        MaxImageWidth = 1022d
                    }
                }.ShowDialogInSelectFileMode();
                if (shot == null) return;

                try {
                    ImageUtils.ApplyPreview(shot, SelectedObject.PreviewImage, 1022d, 575d);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t update preview", e);
                }
            }, o => SelectedObject.Enabled));

            private RelayCommand _updatePreviewDirectCommand;

            public RelayCommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ?? (_updatePreviewDirectCommand = new RelayCommand(o => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = "Select New Preview Image",
                    InitialDirectory = FileUtils.GetDocumentsScreensDirectory(),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    try {
                        ImageUtils.ApplyPreview(dialog.FileName, SelectedObject.PreviewImage, 1022d, 575d);
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t update preview", e);
                    }
                }
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
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = "9:00" },
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = "12:00" },
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = "15:00" },
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = "18:00" }
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

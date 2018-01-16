using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.About;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace AcManager.Pages.Selected {
    public partial class SelectedWeatherPage : ILoadableContent, IParametrizedUriContent, INotifyPropertyChanged, IImmediateContent {
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

            private double _temperature = ValuesStorage.Get(KeyTemperature, 20d);

            public double Temperature {
                get => _temperature;
                set {
                    value = value.Round(0.5);
                    if (Equals(value, _temperature)) return;
                    _temperature = value.Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    ValuesStorage.Set(KeyTemperature, value);
                }
            }

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedObject.TemperatureCoefficient);

            private const string KeyTime = "swp.time";

            private int _time = ValuesStorage.Get(KeyTime, 12 * 60 * 60);

            public int Time {
                get => _time;
                set {
                    if (value == _time) return;
                    _time = value.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(RoadTemperature));
                    ValuesStorage.Set(KeyTime, value);
                }
            }

            public string DisplayTime {
                get => $@"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}";
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

            private CommandBase _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async () => {
                byte[] data = null;

                try {
                    if (!File.Exists(SelectedObject.IniFilename)) {
                        NonfatalError.Notify(AppStrings.Weather_CannotShare,
                                string.Format(AppStrings.Common_FileIsMissingDot, Path.GetFileName(SelectedObject.IniFilename)));
                        return;
                    }

                    if (!File.Exists(SelectedObject.ColorCurvesIniFilename)) {
                        NonfatalError.Notify(AppStrings.Weather_CannotShare,
                                string.Format(AppStrings.Common_FileIsMissingDot, Path.GetFileName(SelectedObject.ColorCurvesIniFilename)));
                        return;
                    }

                    await Task.Run(() => {
                        using (var memory = new MemoryStream()) {
                            using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                writer.Write(@"weather.ini", SelectedObject.IniFilename);
                                writer.Write(@"colorCurves.ini", SelectedObject.ColorCurvesIniFilename);

                                if (File.Exists(SelectedObject.PreviewImage)) {
                                    writer.Write(@"preview.jpg", SelectedObject.PreviewImage);
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
                        NonfatalError.Notify(AppStrings.Weather_CannotShare,
                                string.Format(AppStrings.Weather_CannotShare_Commentary, SharingSizeLimit.ToReadableSize()));
                        return;
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Weather_CannotShare, e);
                    return;
                }

                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.Weather, SelectedObject.Name, SelectedObject.Id, data);
            }));

            private ICommand _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new AsyncCommand<string>(o => {
                SelectedObject.SaveCommand.Execute();
                return QuickDrive.RunAsync(weatherId: SelectedObject.Id, time: FlexibleParser.TryParseTime(o, out var time) ? time : (int?)null);
            }, o => SelectedObject.Enabled));

            private ICommand _viewTemperatureReadmeCommand;

            public ICommand ViewTemperatureReadmeCommand => _viewTemperatureReadmeCommand ?? (_viewTemperatureReadmeCommand = new DelegateCommand(() => {
                ModernDialog.ShowMessage(AppStrings.Weather_KunosReadme);
            }));

            private const string KeyUpdatePreviewMessageShown = "swp.upms";

            private CommandBase _updatePreviewCommand;

            public ICommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    UpdatePreviewDirectCommand.Execute(null);
                    return;
                }

                if (!ValuesStorage.Get<bool>(KeyUpdatePreviewMessageShown) && ModernDialog.ShowMessage(
                        ImportantTips.Entries.GetByIdOrDefault("trackPreviews")?.Content, AppStrings.Common_HowTo_Title, MessageBoxButton.OK) !=
                        MessageBoxResult.OK) {
                    return;
                }

                var directory = AcPaths.GetDocumentsScreensDirectory();
                var shots = FileUtils.GetFilesSafe(directory);

                await QuickDrive.RunAsync(weatherId: SelectedObject.Id);
                if (ScreenshotsConverter.CurrentConversion?.IsCompleted == false) {
                    await ScreenshotsConverter.CurrentConversion;
                }

                var newShots = FileUtils.GetFilesSafe(directory)
                                        .Where(x => !shots.Contains(x) && Regex.IsMatch(x, @"\.(jpe?g|png|bmp)$", RegexOptions.IgnoreCase)).ToList();
                if (!newShots.Any()) {
                    NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, ControlsStrings.AcObject_CannotUpdatePreview_TrackCommentary);
                    return;
                }

                ValuesStorage.Set(KeyUpdatePreviewMessageShown, true);

                var shot = new ImageViewer(newShots, details: x => Path.GetFileName(x as string)) {
                    Model = {
                        MaxImageHeight = CommonAcConsts.PreviewHeight,
                        MaxImageWidth = CommonAcConsts.PreviewWidth
                    }
                }.ShowDialogInSelectFileMode();
                if (shot == null) return;

                try {
                    ImageUtils.ApplyPreview(shot, SelectedObject.PreviewImage, CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight, null);
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
                }
            }, () => SelectedObject.Enabled));

            private CommandBase _updatePreviewDirectCommand;

            public ICommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ?? (_updatePreviewDirectCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = AppStrings.Common_SelectImageForPreview,
                    InitialDirectory = AcPaths.GetDocumentsScreensDirectory(),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    try {
                        ImageUtils.ApplyPreview(dialog.FileName, SelectedObject.PreviewImage, CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight, null);
                    } catch (Exception e) {
                        NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
                    }
                }
            }));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private WeatherObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await WeatherManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = WeatherManager.Instance.GetById(_id);
        }

        public bool EditMode { get; private set; } = ValuesStorage.Get<bool>(KeyEditMode);

        private FrameworkElement _editMode;

        private ICommand _toggleEditModeCommand;

        public ICommand ToggleEditModeCommand => _toggleEditModeCommand ?? (_toggleEditModeCommand = new DelegateCommand(() => {
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
            _editMode = (FrameworkElement)FindResource(@"EditMode");
            Wrapper.Children.Add(_editMode);
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();

            if (EditMode) {
                ToEditMode();
            }
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = WeatherManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private void SetModel() {
            if (EditMode) {
                _object.EnsureLoadedExtended();
            }

            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(ToggleEditModeCommand, new KeyGesture(Key.E, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = @"9:00" },
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = @"12:00" },
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = @"15:00" },
                new InputBinding(_model.TestCommand, new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = @"18:00" }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

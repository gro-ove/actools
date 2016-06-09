using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.TextEditing;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedWeatherPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedWeatherPageViewModel : SelectedAcObjectViewModel<WeatherObject> {
            public SelectedWeatherPageViewModel([NotNull] WeatherObject acObject) : base(acObject) { }

            #region Weather types
            public WeatherType?[] WeatherTypes { get; } = WeatherTypesArray;

            private static readonly WeatherType?[] WeatherTypesArray = {
                null,
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
                        var tempFilename = FilesStorage.Instance.GetTemporaryFilename("Shared Weather.zip");
                        using (var zip = ZipFile.Open(tempFilename, ZipArchiveMode.Create)) {
                            zip.CreateEntryFromFile(SelectedObject.IniFilename, "weather.ini");
                            zip.CreateEntryFromFile(SelectedObject.ColorCurvesIniFilename, "colorCurves.ini");
                        }

                        data = File.ReadAllBytes(tempFilename);
                        try {
                            File.Delete(tempFilename);
                        } catch (Exception e) {
                            Logging.Warning("[SelectedWeatherPage] Can’t clean up: " + e);
                        }
                    });
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t share weather", "Make sure files are readable.", e);
                    return;
                }

                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.Weather, SelectedObject.Name, SelectedObject.Type?.GetDescription(), data);
            }));

            private RelayCommand _testCommand;

            public RelayCommand TestCommand => _testCommand ?? (_testCommand = new RelayCommand(o => {
                ;
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
            //_object?.PrepareForEditing();
        }

        void ILoadableContent.Load() {
            _object = WeatherManager.Instance.GetById(_id);
            //_object?.PrepareForEditing();
        }

        private SelectedWeatherPageViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedWeatherPageViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });
            InitializeComponent();

            //TextEditor.SetAsIniEditor(v => { _object.Content = v; });
            //TextEditor.SetDocument(_object.Content);
            //_object.PropertyChanged += SelectedObject_PropertyChanged;
        }

        private void SelectedObject_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            //if (TextEditor.IsBusy()) return;
            //if (e.PropertyName == nameof(_object.Content)) {
            //    TextEditor.SetDocument(_object.Content);
            //}
        }

        private void SelectedWeatherPage_OnUnloaded(object sender, RoutedEventArgs e) {
            _object.PropertyChanged -= SelectedObject_PropertyChanged;
        }
    }
}

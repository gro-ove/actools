using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedWeatherPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedWeatherPageViewModel : SelectedAcObjectViewModel<WeatherObject> {
            public SelectedWeatherPageViewModel([NotNull] WeatherObject acObject) : base(acObject) { }

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

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(new SelectedWeatherPageViewModel(_object));
            InitializeComponent();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedWeatherPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedWeatherPageViewModel : SelectedAcObjectViewModel<WeatherObject> {
            public SelectedWeatherPageViewModel([NotNull] WeatherObject acObject) : base(acObject) { }
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

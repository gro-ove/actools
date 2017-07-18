using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Miscellaneous {
    public partial class LapTimes_Grid : ILoadableContent {
        public async Task LoadAsync(CancellationToken cancellationToken) {
            await CarsManager.Instance.EnsureLoadedAsync();
            await TracksManager.Instance.EnsureLoadedAsync();
            await LapTimesManager.Instance.UpdateAsync();
        }

        public void Load() {
            CarsManager.Instance.EnsureLoaded();
            TracksManager.Instance.EnsureLoaded();
            LapTimesManager.Instance.UpdateAsync().Forget();
        }

        public void Initialize() {
            DataContext = new LapTimes_Recent.ViewModel(null);
            InitializeComponent();

            _cars = Model.List.Select(x => x.Car).ToArray();
        }

        private CarObject[] _cars;

        protected override void OnRender(DrawingContext drawingContext) {
            // var cellSize = 200;
            // base.OnRender(drawingContext);
        }

        private LapTimes_Recent.ViewModel Model => (LapTimes_Recent.ViewModel)DataContext;
    }
}

using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Miscellaneous {
    /// <summary>
    /// Interaction logic for LapTimes_Table.xaml
    /// </summary>
    public partial class LapTimes_Table : ILoadableContent {
        public Task LoadAsync(CancellationToken cancellationToken) {
            return LapTimesManager.Instance.UpdateAsync();
        }

        public void Load() {
            LapTimesManager.Instance.UpdateAsync().Forget();
        }

        public void Initialize() {
            DataContext = new LapTimes_List.LapTimesViewModel(null);
            InitializeComponent();
        }

        private LapTimes_List.LapTimesViewModel Model => (LapTimes_List.LapTimesViewModel)DataContext;
    }
}

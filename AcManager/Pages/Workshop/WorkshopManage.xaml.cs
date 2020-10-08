using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopManage {
        private ViewModel Model => (ViewModel)DataContext;

        public WorkshopManage() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {

            }
        }
    }
}
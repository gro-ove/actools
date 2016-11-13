using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    public partial class TrackMapEditorT : ModernDialog {
        private TrackMapEditorViewModel Model => (TrackMapEditorViewModel)DataContext;

        public TrackMapEditorT(TrackObjectBase track) {
            DataContext = new TrackMapEditorViewModel();
            InitializeComponent();
            Buttons = new[] { OkButton, CancelButton };
        }

        public class TrackMapEditorViewModel : NotifyPropertyChanged {
            public TrackMapEditorViewModel() {}
        }
    }
}

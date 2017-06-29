using AcManager.Tools.Objects;

namespace AcManager.Pages.Dialogs {
    public partial class FmodCarPlayerDialog {
        public CarObject Car { get; }

        public FmodCarPlayerDialog(CarObject car) {
            Car = car;
            InitializeComponent();
            Buttons = new[] { OkButton, CancelButton };
        }
    }
}

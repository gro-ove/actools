using AcManager.Tools.AcObjectsNew;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class VersionInfoEditor {
        [NotNull]
        public AcJsonObjectNew AcObj { get; }

        public VersionInfoEditor([NotNull]AcJsonObjectNew acObj) {
            InitializeComponent();
            DataContext = this;

            Buttons = new [] { OkButton, CancelButton };
            AcObj = acObj;

            Closing += VersionInfoEditor_Closing;
        }

        void VersionInfoEditor_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!IsResultOk) return;

            AcObj.Author = AuthorInput.Text;
            AcObj.Version = VersionInput.Text;
            AcObj.Url = UrlInput.Text;
        }
    }
}

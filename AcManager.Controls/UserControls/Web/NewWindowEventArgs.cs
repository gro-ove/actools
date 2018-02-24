using System.ComponentModel;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class NewWindowEventArgs : CancelEventArgs {
        public NewWindowEventArgs([NotNull] string url) {
            Url = url;
        }

        [NotNull]
        public string Url { get; }
    }
}
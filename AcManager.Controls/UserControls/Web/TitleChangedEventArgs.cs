using System;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class TitleChangedEventArgs : EventArgs {
        public TitleChangedEventArgs([NotNull] string title) {
            Title = title;
        }

        [NotNull]
        public string Title { get; }
    }
}
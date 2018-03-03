using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class WebInjectEventArgs : EventArgs {
        public Collection<string> ToInject { get; } = new Collection<string>();
        public Collection<KeyValuePair<string, string>> Replacements { get; } = new Collection<KeyValuePair<string, string>>();

        public WebInjectEventArgs([NotNull] string url) {
            Url = url;
        }

        [NotNull]
        public string Url { get; }
    }
}
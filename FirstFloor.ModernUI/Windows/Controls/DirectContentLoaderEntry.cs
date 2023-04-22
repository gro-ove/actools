using System;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class DirectContentLoaderEntry {
        public Uri Source { get; set; }

        public string Key {
            get => Source.OriginalString;
            set => Source = new Uri(value, UriKind.Relative);
        }

        public object Content { get; set; }
    }
}
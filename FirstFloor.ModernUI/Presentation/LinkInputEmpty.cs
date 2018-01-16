using System;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Presentation {
    public sealed class LinkInputEmpty : Link {
        public override bool NonSelectable { get; } = true;

        internal LinkInputEmpty(Uri uri) {
            Source = uri.AddQueryParam("Special", "Input");
        }

        public override string DisplayName {
            get => string.Empty;
            set {
                value = value.Trim();
                if (string.IsNullOrEmpty(value)) return;

                NewLink?.Invoke(this, new NewLinkEventArgs(value));
            }
        }

        public event NewLinkEventHandler NewLink;
    }
}
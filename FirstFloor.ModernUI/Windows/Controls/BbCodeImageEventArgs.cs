using System;
using System.Collections.Generic;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BbCodeImageEventArgs : EventArgs {
        public readonly string ImageUrl;

        [CanBeNull]
        public readonly List<Tuple<string, string>> BlockImages;

        public BbCodeImageEventArgs(string imageUrl, [CanBeNull] List<Tuple<string, string>> blockImages) {
            BlockImages = blockImages;
            ImageUrl = imageUrl;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BbCodeImageInformation {
        public string Url, Description;

        public BbCodeImageInformation(string url, string description) {
            Url = url;
            Description = description;
        }
    }

    public class BbCodeImageEventArgs : EventArgs {
        [NotNull]
        public readonly string ImageUrl;

        [CanBeNull]
        public readonly List<BbCodeImageInformation> BlockImages;

        public BbCodeImageEventArgs([NotNull] string imageUrl, [CanBeNull] List<BbCodeImageInformation> blockImages) {
            BlockImages = blockImages;
            ImageUrl = imageUrl;
        }

        [NotNull]
        private static string ToString([NotNull] Uri uri) {
            switch (uri.Scheme) {
                case "file":
                    return uri.AbsolutePath;
                default:
                    return uri.OriginalString;
            }
        }

        public BbCodeImageEventArgs([NotNull] Uri imageUri, [CanBeNull] IEnumerable<Tuple<Uri, string>> blockImages) {
            BlockImages = blockImages?.Select(x => new BbCodeImageInformation(ToString(x.Item1), x.Item2)).ToList();
            ImageUrl = ToString(imageUri);
        }
    }
}
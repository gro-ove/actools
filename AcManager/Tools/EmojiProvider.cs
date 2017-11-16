using System;
using System.Windows.Media;
using AcManager.Internal;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools {
    internal class EmojiProvider : IEmojiProvider {
        private readonly string _url = InternalUtils.GetEmojiUrl();

        public Uri GetUri(string emojiCode) {
            return new Uri(string.Format(_url, emojiCode));
        }

        public ImageSource GetImageSource(string emojiCode) {
            return null;
        }
    }
}
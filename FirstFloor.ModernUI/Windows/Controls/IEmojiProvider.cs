using System;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Define one of those methods, another should return NULL.
    /// </summary>
    public interface IEmojiProvider {
        [CanBeNull]
        Uri GetUri([NotNull] string emojiCode);

        [CanBeNull]
        ImageSource GetImageSource([NotNull] string emojiCode);
    }
}
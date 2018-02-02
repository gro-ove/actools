using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public interface IKeyboardListener : IDisposable {
        event EventHandler<KeyboardEventArgs> PreviewKeyUp, PreviewKeyDown;
        event EventHandler<KeyboardEventArgs> KeyUp, KeyDown;
        void WatchFor(IEnumerable<Keys> key);
        void Subscribe();
        void Unsubscribe();
    }

    public static class KeyboardListenerExtension {
        public static void WatchFor(this IKeyboardListener keyboardListener, params Keys[] keys) {
            // ReSharper disable once RedundantCast
            keyboardListener.WatchFor((IEnumerable<Keys>)keys);
        }
    }
}
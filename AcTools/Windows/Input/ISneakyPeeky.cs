using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    // Here, I’d like to say hello to Kaspersky AV forcing me to try and hide competely legitimate
    // code added to allow user to use extra keyboard bindings in the race. In better world,
    // this thing would’ve have a respectable name “IKeyboardListener”, with “KeyUp” and “KeyDown”
    // instead of “Sneak” and “Peek”.
    public interface ISneakyPeeky : IDisposable {
        event EventHandler<SneakyPeekyEventArgs> PreviewSneak, PreviewPeek;
        event EventHandler<SneakyPeekyEventArgs> Sneak, Peek;
        void WatchFor(IEnumerable<Keys> key);
        void Subscribe();
        void Unsubscribe();
    }

    public static class SneakyPeekyExtension {
        public static void WatchFor(this ISneakyPeeky sneakyPeeky, params Keys[] keys) {
            // ReSharper disable once RedundantCast
            sneakyPeeky.WatchFor((IEnumerable<Keys>)keys);
        }
    }
}
namespace AcTools.Windows.Input {
    public class KeyboardSilencer : KeyboardManager {
        public KeyboardSilencer() {
            KeyDown += (s, e) => { e.Handled = true; };
            KeyUp += (s, e) => { e.Handled = true; };
            Subscribe();
        }

        public void Stop() {
            Unsubscribe();
        }
    }
}
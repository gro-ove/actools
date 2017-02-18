using System.Windows.Forms;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public static class KeyHelper {
        public static bool IsInputAssignable(this Key key) {
            if (key >= Key.A && key <= Key.Z ||
                    key >= Key.D0 && key <= Key.D9 ||
                    key >= Key.NumPad0 && key <= Key.NumPad9 ||
                    key >= Key.Left && key <= Key.Down ||
                    key == Key.Home || key == Key.Insert || key == Key.Delete || key == Key.Back ||
                    key == Key.LeftCtrl || key == Key.LeftAlt || key == Key.LeftShift || key == Key.LWin ||
                    key == Key.RightCtrl || key == Key.RightAlt || key == Key.RightShift || key == Key.RWin) {
                return true;
            }

            switch (key) {
                case Key.Space:
                case Key.Tab:
                case Key.OemPlus:
                case Key.OemMinus:
                case Key.OemTilde:
                case Key.OemBackslash:
                case Key.OemPeriod:
                case Key.OemComma:
                case Key.OemPipe:
                case Key.OemQuestion:
                case Key.OemQuotes:
                case Key.OemSemicolon:
                    return true;

                default:
                    return false;
            }
        }
    }

    public sealed class KeyboardInputButton : BaseInputProvider<bool> {
        public KeyboardInputButton(int keyCode) : base(keyCode) {
            Key = (Keys)keyCode;
            ShortName = Key.ToReadableKey();
            DisplayName = ShortName;
        }

        public Keys Key { get; }

        internal int Used;
    }
}
using System.Windows.Forms;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class KeyboardInputButton : InputProviderBase<bool> {
        public KeyboardInputButton(int keyCode) : base(keyCode) {
            Key = (Keys)keyCode;
            ShortName = Key.ToReadableKey();
            DisplayName = ShortName;
        }

        public Keys Key { get; }

        internal int Used;

        protected override void SetDisplayName(string displayName) { }
    }
}
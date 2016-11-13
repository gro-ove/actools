using System;
using System.Threading;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class KeyboardManager : KeyboardListener {
        private readonly int _delay;
        private int _ignoreDown, _ignoreUp;
        private bool[] _pressed, _unpressed;

        public KeyboardManager(int delay = 20) {
            _delay = delay;
            _pressed = new bool[256];
        }

        protected override bool RaiseKeyDown(int virtualKeyCode) {
            if (_unpressed != null && virtualKeyCode < 256 && _unpressed[virtualKeyCode]) {
                return true;
            }

            if (_ignoreDown > 0) {
                _ignoreDown--;
            } else if (base.RaiseKeyDown(virtualKeyCode)) {
                return true;
            } else {
                _pressed[virtualKeyCode] = true;
            }

            return false;
        }

        protected override bool RaiseKeyUp(int virtualKeyCode) {
            if (virtualKeyCode < 256) {
                _pressed[virtualKeyCode] = false;
            }

            if (_ignoreUp > 0) {
                _ignoreUp--;
            } else {
                return base.RaiseKeyUp(virtualKeyCode);
            }

            return false;
        }

        public void SendWait(params string[] args) {
            foreach (var keys in args) {
                _ignoreDown += keys.Length;
                _ignoreUp += keys.Length;
                SendKeys.SendWait(keys);
            }
        }

        public void Press(params Keys[] values) {
            foreach (var t in values) {
                _ignoreDown++;
                User32.keybd_event((byte)t, 0x45, User32.KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                Thread.Sleep(_delay);
            }

            for (var i = values.Length - 1; i >= 0; i--) {
                _ignoreUp++;
                User32.keybd_event((byte)values[i], 0x45, User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP, (UIntPtr)0);
                Thread.Sleep(_delay);
            }
        }

        public void Unpress(params Keys[] values) {
            for (var i = values.Length - 1; i >= 0; i--) {
                _ignoreUp++;
                User32.keybd_event((byte)values[i], 0x45, User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP, (UIntPtr)0);
                Thread.Sleep(_delay);
            }
        }

        public void UnpressAll() {
            _unpressed = (bool[])_pressed.Clone();
            for (var i = 0; i < _pressed.Length; i++) {
                if (!_pressed[i]) continue;

                _ignoreUp++;
                _pressed[i] = false;
                User32.keybd_event((byte)i, 0x45, User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP, (UIntPtr)0);
            }
        }
    }
}

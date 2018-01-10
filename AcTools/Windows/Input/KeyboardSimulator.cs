using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace AcTools.Windows.Input {
    public class KeyboardSimulator {
        private static void ModifiersDown(InputBuilder builder, IEnumerable<Keys> modifierKeyCodes) {
            if (modifierKeyCodes == null) return;
            foreach (var key in modifierKeyCodes) {
                builder.AddKeyDown(key);
            }
        }

        private static void ModifiersUp(InputBuilder builder, IEnumerable<Keys> modifierKeyCodes) {
            if (modifierKeyCodes == null) return;
            var stack = new Stack<Keys>(modifierKeyCodes);
            while (stack.Count > 0) {
                builder.AddKeyUp(stack.Pop());
            }
        }

        private void KeysPress(InputBuilder builder, IEnumerable<Keys> keyCodes) {
            if (keyCodes == null) return;
            foreach (var key in keyCodes) {
                builder.AddKeyPress(key);
            }
        }

        private void SendSimulatedInput(User32.Input[] inputList) {
            InputBuilder.DispatchInput(inputList);
        }

        public KeyboardSimulator KeyDown(Keys keyCode) {
            var inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public KeyboardSimulator KeyUp(Keys keyCode) {
            var inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public KeyboardSimulator KeyPress(Keys keyCode) {
            var inputList = new InputBuilder().AddKeyPress(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public KeyboardSimulator KeyPress(params Keys[] keyCodes) {
            var builder = new InputBuilder();
            KeysPress(builder, keyCodes);
            SendSimulatedInput(builder.ToArray());
            return this;
        }

        public KeyboardSimulator ModifiedKeyStroke(Keys modifierKeyCode, Keys keyCode) {
            ModifiedKeyStroke(new[] { modifierKeyCode }, new[] { keyCode });
            return this;
        }

        public KeyboardSimulator ModifiedKeyStroke(IEnumerable<Keys> modifierKeyCodes, Keys keyCode) {
            ModifiedKeyStroke(modifierKeyCodes, new[] { keyCode });
            return this;
        }

        public KeyboardSimulator ModifiedKeyStroke(Keys modifierKey, IEnumerable<Keys> keyCodes) {
            ModifiedKeyStroke(new[] { modifierKey }, keyCodes);
            return this;
        }

        public KeyboardSimulator ModifiedKeyStroke(IEnumerable<Keys> modifierKeyCodes, IEnumerable<Keys> keyCodes) {
            var builder = new InputBuilder();

            var virtualKeyCodes = modifierKeyCodes as IList<Keys> ?? modifierKeyCodes.ToList();
            ModifiersDown(builder, virtualKeyCodes);
            KeysPress(builder, keyCodes);
            ModifiersUp(builder, virtualKeyCodes);

            SendSimulatedInput(builder.ToArray());
            return this;
        }

        public KeyboardSimulator TextEntry(string text) {
            var inputList = new InputBuilder().AddCharacters(text).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public KeyboardSimulator TextEntry(char character) {
            var inputList = new InputBuilder().AddCharacter(character).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public KeyboardSimulator Sleep(int millsecondsTimeout) {
            Thread.Sleep(millsecondsTimeout);
            return this;
        }

        public KeyboardSimulator Sleep(TimeSpan timeout) {
            Thread.Sleep(timeout);
            return this;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AcTools.Windows.Input.Native;

namespace AcTools.Windows.Input {
    public class KeyboardSimulator : IKeyboardSimulator {
        private readonly IInputSimulator _inputSimulator;

        private readonly IInputMessageDispatcher _messageDispatcher;

        public KeyboardSimulator(IInputSimulator inputSimulator) {
            if (inputSimulator == null) {
                throw new ArgumentNullException(nameof(inputSimulator));
            }

            _inputSimulator = inputSimulator;
            _messageDispatcher = new WindowsInputMessageDispatcher();
        }

        internal KeyboardSimulator(IInputSimulator inputSimulator, IInputMessageDispatcher messageDispatcher) {
            if (inputSimulator == null) throw new ArgumentNullException(nameof(inputSimulator));

            if (messageDispatcher == null) {
                throw new InvalidOperationException(
                    string.Format(
                                  "The {0} cannot operate with a null {1}. Please provide a valid {1} instance to use for dispatching {2} messages.",
                                  typeof (KeyboardSimulator).Name, typeof (IInputMessageDispatcher).Name,
                                  typeof (INPUT).Name));
            }

            _inputSimulator = inputSimulator;
            _messageDispatcher = messageDispatcher;
        }

        public IMouseSimulator Mouse => _inputSimulator.Mouse;

        private static void ModifiersDown(InputBuilder builder, IEnumerable<VirtualKeyCode> modifierKeyCodes) {
            if (modifierKeyCodes == null) return;
            foreach (var key in modifierKeyCodes) {
                builder.AddKeyDown(key);
            }
        }

        private static void ModifiersUp(InputBuilder builder, IEnumerable<VirtualKeyCode> modifierKeyCodes) {
            if (modifierKeyCodes == null) return;

            var stack = new Stack<VirtualKeyCode>(modifierKeyCodes);
            while (stack.Count > 0) {
                builder.AddKeyUp(stack.Pop());
            }
        }

        private void KeysPress(InputBuilder builder, IEnumerable<VirtualKeyCode> keyCodes) {
            if (keyCodes == null) return;
            foreach (var key in keyCodes) builder.AddKeyPress(key);
        }

        private void SendSimulatedInput(INPUT[] inputList) {
            _messageDispatcher.DispatchInput(inputList);
        }

        public IKeyboardSimulator KeyDown(VirtualKeyCode keyCode) {
            var inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator KeyUp(VirtualKeyCode keyCode) {
            var inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator KeyPress(VirtualKeyCode keyCode) {
            var inputList = new InputBuilder().AddKeyPress(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator KeyPress(params VirtualKeyCode[] keyCodes) {
            var builder = new InputBuilder();
            KeysPress(builder, keyCodes);
            SendSimulatedInput(builder.ToArray());
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(VirtualKeyCode modifierKeyCode, VirtualKeyCode keyCode) {
            ModifiedKeyStroke(new[] { modifierKeyCode }, new[] { keyCode });
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes, VirtualKeyCode keyCode) {
            ModifiedKeyStroke(modifierKeyCodes, new[] { keyCode });
            return this;
        }

        /// <summary>
        /// Simulates a modified keystroke where there is one modifier and multiple keys like CTRL-K-C where CTRL is the modifierKey and K and C are the keys.
        /// The flow is Modifier KeyDown, Keys Press in order, Modifier KeyUp.
        /// </summary>
        /// <param name="modifierKey">The modifier key</param>
        /// <param name="keyCodes">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(VirtualKeyCode modifierKey, IEnumerable<VirtualKeyCode> keyCodes) {
            ModifiedKeyStroke(new[] { modifierKey }, keyCodes);
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes, IEnumerable<VirtualKeyCode> keyCodes) {
            var builder = new InputBuilder();

            var virtualKeyCodes = modifierKeyCodes as IList<VirtualKeyCode> ?? modifierKeyCodes.ToList();
            ModifiersDown(builder, virtualKeyCodes);
            KeysPress(builder, keyCodes);
            ModifiersUp(builder, virtualKeyCodes);

            SendSimulatedInput(builder.ToArray());
            return this;
        }

        public IKeyboardSimulator TextEntry(string text) {
            var inputList = new InputBuilder().AddCharacters(text).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator TextEntry(char character) {
            var inputList = new InputBuilder().AddCharacter(character).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator Sleep(int millsecondsTimeout) {
            Thread.Sleep(millsecondsTimeout);
            return this;
        }

        public IKeyboardSimulator Sleep(TimeSpan timeout) {
            Thread.Sleep(timeout);
            return this;
        }
    }
}
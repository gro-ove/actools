using System;
using System.Collections;
using System.Collections.Generic;
using AcTools.Windows.Input.Native;

namespace AcTools.Windows.Input {
    /// <summary>
    /// A helper class for building a list of <see cref="InputEntry"/> messages ready to be sent to the native Windows API.
    /// </summary>
    internal class InputBuilder : IEnumerable<InputEntry> {
        /// <summary>
        /// The public list of <see cref="InputEntry"/> messages being built by this instance.
        /// </summary>
        private readonly List<InputEntry> _inputList;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputBuilder"/> class.
        /// </summary>
        public InputBuilder() {
            _inputList = new List<InputEntry>();
        }

        /// <summary>
        /// Returns the list of <see cref="InputEntry"/> messages as a <see cref="System.Array"/> of <see cref="InputEntry"/> messages.
        /// </summary>
        /// <returns>The <see cref="System.Array"/> of <see cref="InputEntry"/> messages.</returns>
        public InputEntry[] ToArray() {
            return _inputList.ToArray();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the list of <see cref="InputEntry"/> messages.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the list of <see cref="InputEntry"/> messages.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<InputEntry> GetEnumerator() {
            return _inputList.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the list of <see cref="InputEntry"/> messages.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the list of <see cref="InputEntry"/> messages.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets the <see cref="InputEntry"/> at the specified position.
        /// </summary>
        /// <value>The <see cref="InputEntry"/> message at the specified position.</value>
        public InputEntry this[int position] => _inputList[position];

        /// <summary>
        /// Determines if the <see cref="VirtualKeyCode"/> is an ExtendedKey
        /// </summary>
        /// <param name="keyCode">The key code.</param>
        /// <returns>true if the key code is an extended key; otherwise, false.</returns>
        /// <remarks>
        /// The extended keys consist of the ALT and CTRL keys on the right-hand side of the keyboard; the INS, DEL, HOME, END, PAGE UP, PAGE DOWN, and arrow keys in the clusters to the left of the numeric keypad; the NUM LOCK key; the BREAK (CTRL+PAUSE) key; the PRINT SCRN key; and the divide (/) and ENTER keys in the numeric keypad.
        /// 
        /// See http://msdn.microsoft.com/en-us/library/ms646267(v=vs.85).aspx Section "Extended-Key Flag"
        /// </remarks>
        public static bool IsExtendedKey(VirtualKeyCode keyCode) {
            return keyCode == VirtualKeyCode.MENU ||
                    keyCode == VirtualKeyCode.LMENU ||
                    keyCode == VirtualKeyCode.RMENU ||
                    keyCode == VirtualKeyCode.CONTROL ||
                    keyCode == VirtualKeyCode.RCONTROL ||
                    keyCode == VirtualKeyCode.INSERT ||
                    keyCode == VirtualKeyCode.DELETE ||
                    keyCode == VirtualKeyCode.HOME ||
                    keyCode == VirtualKeyCode.END ||
                    keyCode == VirtualKeyCode.PRIOR ||
                    keyCode == VirtualKeyCode.NEXT ||
                    keyCode == VirtualKeyCode.RIGHT ||
                    keyCode == VirtualKeyCode.UP ||
                    keyCode == VirtualKeyCode.LEFT ||
                    keyCode == VirtualKeyCode.DOWN ||
                    keyCode == VirtualKeyCode.NUMLOCK ||
                    keyCode == VirtualKeyCode.CANCEL ||
                    keyCode == VirtualKeyCode.SNAPSHOT ||
                    keyCode == VirtualKeyCode.DIVIDE;
        }

        /// <summary>
        /// Adds a key down to the list of <see cref="InputEntry"/> messages.
        /// </summary>
        /// <param name="keyCode">The <see cref="VirtualKeyCode"/>.</param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddKeyDown(VirtualKeyCode keyCode) {
            var down = new InputEntry {
                Type = (uint)InputType.Keyboard,
                Data = {
                    Keyboard = new KeyboardInputEntry {
                        KeyCode = (ushort)keyCode,
                        Scan = 0,
                        Flags = IsExtendedKey(keyCode) ? (uint)KeyboardFlag.ExtendedKey : 0,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };

            _inputList.Add(down);
            return this;
        }

        /// <summary>
        /// Adds a key up to the list of <see cref="InputEntry"/> messages.
        /// </summary>
        /// <param name="keyCode">The <see cref="VirtualKeyCode"/>.</param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddKeyUp(VirtualKeyCode keyCode) {
            var up = new InputEntry {
                Type = (uint)InputType.Keyboard,
                Data = {
                    Keyboard = new KeyboardInputEntry {
                        KeyCode = (ushort)keyCode,
                        Scan = 0,
                        Flags = (uint)(IsExtendedKey(keyCode) ? KeyboardFlag.KeyUp | KeyboardFlag.ExtendedKey : KeyboardFlag.KeyUp),
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };

            _inputList.Add(up);
            return this;
        }

        /// <summary>
        /// Adds a key press to the list of <see cref="InputEntry"/> messages which is equivalent to a key down followed by a key up.
        /// </summary>
        /// <param name="keyCode">The <see cref="VirtualKeyCode"/>.</param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddKeyPress(VirtualKeyCode keyCode) {
            AddKeyDown(keyCode);
            AddKeyUp(keyCode);
            return this;
        }

        /// <summary>
        /// Adds the character to the list of <see cref="InputEntry"/> messages.
        /// </summary>
        /// <param name="character">The <see cref="System.Char"/> to be added to the list of <see cref="InputEntry"/> messages.</param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddCharacter(char character) {
            ushort scanCode = character;

            var down = new InputEntry {
                Type = (uint)InputType.Keyboard,
                Data = {
                    Keyboard = new KeyboardInputEntry {
                        KeyCode = 0,
                        Scan = scanCode,
                        Flags = (uint)KeyboardFlag.Unicode,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };

            var up = new InputEntry {
                Type = (uint)InputType.Keyboard,
                Data = {
                    Keyboard = new KeyboardInputEntry {
                        KeyCode = 0,
                        Scan = scanCode,
                        Flags = (uint)(KeyboardFlag.KeyUp | KeyboardFlag.Unicode),
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Handle extended keys:
            // If the scan code is preceded by a prefix byte that has the value 0xE0 (224),
            // we need to include the KEYEVENTF_EXTENDEDKEY flag in the Flags property. 
            if ((scanCode & 0xFF00) == 0xE000) {
                down.Data.Keyboard.Flags |= (uint)KeyboardFlag.ExtendedKey;
                up.Data.Keyboard.Flags |= (uint)KeyboardFlag.ExtendedKey;
            }

            _inputList.Add(down);
            _inputList.Add(up);
            return this;
        }

        /// <summary>
        /// Adds all of the characters in the specified <see cref="IEnumerable{T}"/> of <see cref="char"/>.
        /// </summary>
        /// <param name="characters">The characters to add.</param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddCharacters(IEnumerable<char> characters) {
            foreach (var character in characters) {
                AddCharacter(character);
            }
            return this;
        }

        /// <summary>
        /// Adds the characters in the specified <see cref="string"/>.
        /// </summary>
        /// <param name="characters">The string of <see cref="char"/> to add.</param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddCharacters(string characters) {
            return AddCharacters(characters.ToCharArray());
        }

        /// <summary>
        /// Moves the mouse relative to its current position.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddRelativeMouseMovement(int x, int y) {
            var movement = new InputEntry { Type = (uint)InputType.Mouse };
            movement.Data.Mouse.Flags = (uint)MouseFlag.Move;
            movement.Data.Mouse.X = x;
            movement.Data.Mouse.Y = y;
            _inputList.Add(movement);
            return this;
        }

        /// <summary>
        /// Move the mouse to an absolute position.
        /// </summary>
        /// <param name="absoluteX"></param>
        /// <param name="absoluteY"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddAbsoluteMouseMovement(int absoluteX, int absoluteY) {
            var movement = new InputEntry { Type = (uint)InputType.Mouse };
            movement.Data.Mouse.Flags = (uint)(MouseFlag.Move | MouseFlag.Absolute);
            movement.Data.Mouse.X = absoluteX;
            movement.Data.Mouse.Y = absoluteY;
            _inputList.Add(movement);
            return this;
        }

        /// <summary>
        /// Move the mouse to the absolute position on the virtual desktop.
        /// </summary>
        /// <param name="absoluteX"></param>
        /// <param name="absoluteY"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddAbsoluteMouseMovementOnVirtualDesktop(int absoluteX, int absoluteY) {
            var movement = new InputEntry { Type = (uint)InputType.Mouse };
            movement.Data.Mouse.Flags = (uint)(MouseFlag.Move | MouseFlag.Absolute | MouseFlag.VirtualDesk);
            movement.Data.Mouse.X = absoluteX;
            movement.Data.Mouse.Y = absoluteY;
            _inputList.Add(movement);
            return this;
        }

        /// <summary>
        /// Adds a mouse button down for the specified button.
        /// </summary>
        /// <param name="button"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseButtonDown(MouseButton button) {
            var buttonDown = new InputEntry { Type = (uint)InputType.Mouse };
            buttonDown.Data.Mouse.Flags = (uint)ToMouseButtonDownFlag(button);
            _inputList.Add(buttonDown);
            return this;
        }

        /// <summary>
        /// Adds a mouse button down for the specified button.
        /// </summary>
        /// <param name="xButtonId"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseXButtonDown(int xButtonId) {
            var buttonDown = new InputEntry { Type = (uint)InputType.Mouse };
            buttonDown.Data.Mouse.Flags = (uint)MouseFlag.XDown;
            buttonDown.Data.Mouse.MouseData = (uint)xButtonId;
            _inputList.Add(buttonDown);
            return this;
        }

        /// <summary>
        /// Adds a mouse button up for the specified button.
        /// </summary>
        /// <param name="button"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseButtonUp(MouseButton button) {
            var buttonUp = new InputEntry { Type = (uint)InputType.Mouse };
            buttonUp.Data.Mouse.Flags = (uint)ToMouseButtonUpFlag(button);
            _inputList.Add(buttonUp);
            return this;
        }

        /// <summary>
        /// Adds a mouse button up for the specified button.
        /// </summary>
        /// <param name="xButtonId"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseXButtonUp(int xButtonId) {
            var buttonUp = new InputEntry { Type = (uint)InputType.Mouse };
            buttonUp.Data.Mouse.Flags = (uint)MouseFlag.XUp;
            buttonUp.Data.Mouse.MouseData = (uint)xButtonId;
            _inputList.Add(buttonUp);
            return this;
        }

        /// <summary>
        /// Adds a single click of the specified button.
        /// </summary>
        /// <param name="button"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseButtonClick(MouseButton button) {
            return AddMouseButtonDown(button).AddMouseButtonUp(button);
        }

        /// <summary>
        /// Adds a single click of the specified button.
        /// </summary>
        /// <param name="xButtonId"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseXButtonClick(int xButtonId) {
            return AddMouseXButtonDown(xButtonId).AddMouseXButtonUp(xButtonId);
        }

        /// <summary>
        /// Adds a double click of the specified button.
        /// </summary>
        /// <param name="button"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseButtonDoubleClick(MouseButton button) {
            return AddMouseButtonClick(button).AddMouseButtonClick(button);
        }

        /// <summary>
        /// Adds a double click of the specified button.
        /// </summary>
        /// <param name="xButtonId"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseXButtonDoubleClick(int xButtonId) {
            return AddMouseXButtonClick(xButtonId).AddMouseXButtonClick(xButtonId);
        }

        /// <summary>
        /// Scroll the vertical mouse wheel by the specified amount.
        /// </summary>
        /// <param name="scrollAmount"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseVerticalWheelScroll(int scrollAmount) {
            var scroll = new InputEntry { Type = (uint)InputType.Mouse };
            scroll.Data.Mouse.Flags = (uint)MouseFlag.VerticalWheel;
            scroll.Data.Mouse.MouseData = (uint)scrollAmount;
            _inputList.Add(scroll);
            return this;
        }

        /// <summary>
        /// Scroll the horizontal mouse wheel by the specified amount.
        /// </summary>
        /// <param name="scrollAmount"></param>
        /// <returns>This <see cref="InputBuilder"/> instance.</returns>
        public InputBuilder AddMouseHorizontalWheelScroll(int scrollAmount) {
            var scroll = new InputEntry { Type = (uint)InputType.Mouse };
            scroll.Data.Mouse.Flags = (uint)MouseFlag.HorizontalWheel;
            scroll.Data.Mouse.MouseData = (uint)scrollAmount;
            _inputList.Add(scroll);
            return this;
        }

        private static MouseFlag ToMouseButtonDownFlag(MouseButton button) {
            switch (button) {
                case MouseButton.LeftButton:
                    return MouseFlag.LeftDown;

                case MouseButton.MiddleButton:
                    return MouseFlag.MiddleDown;

                case MouseButton.RightButton:
                    return MouseFlag.RightDown;

                default:
                    return MouseFlag.LeftDown;
            }
        }

        private static MouseFlag ToMouseButtonUpFlag(MouseButton button) {
            switch (button) {
                case MouseButton.LeftButton:
                    return MouseFlag.LeftUp;

                case MouseButton.MiddleButton:
                    return MouseFlag.MiddleUp;

                case MouseButton.RightButton:
                    return MouseFlag.RightUp;

                default:
                    return MouseFlag.LeftUp;
            }
        }
    }
}
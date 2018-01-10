using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    internal class InputBuilder : IEnumerable<User32.Input> {
        private readonly List<User32.Input> _inputList;

        public InputBuilder() {
            _inputList = new List<User32.Input>();
        }

        public User32.Input[] ToArray() {
            return _inputList.ToArray();
        }

        public IEnumerator<User32.Input> GetEnumerator() {
            return _inputList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public User32.Input this[int position] => _inputList[position];

        public InputBuilder AddKeyDown(Keys keyCode) {
            var down = new User32.Input {
                Type = User32.InputType.Keyboard,
                Keyboard = new User32.KeyboardInput {
                    VirtualKeyCode = (ushort)keyCode,
                    Flags = User32.IsExtendedKey(keyCode) ? User32.KeyboardFlag.ExtendedKey : User32.KeyboardFlag.None,
                }
            };

            _inputList.Add(down);
            return this;
        }

        public InputBuilder AddKeyUp(Keys keyCode) {
            var up = new User32.Input {
                Type = User32.InputType.Keyboard,
                Keyboard = new User32.KeyboardInput {
                    VirtualKeyCode = (ushort)keyCode,
                    Flags = User32.IsExtendedKey(keyCode) ? User32.KeyboardFlag.KeyUp | User32.KeyboardFlag.ExtendedKey : User32.KeyboardFlag.KeyUp,
                }
            };

            _inputList.Add(up);
            return this;
        }

        public InputBuilder AddKeyPress(Keys keyCode) {
            AddKeyDown(keyCode);
            AddKeyUp(keyCode);
            return this;
        }

        public InputBuilder AddCharacter(char character) {
            ushort scanCode = character;

            var down = new User32.Input {
                Type = User32.InputType.Keyboard,
                Keyboard = new User32.KeyboardInput {
                    ScanCode = scanCode,
                    Flags = User32.KeyboardFlag.Unicode,
                }
            };

            var up = new User32.Input {
                Type = User32.InputType.Keyboard,
                Keyboard = new User32.KeyboardInput {
                    ScanCode = scanCode,
                    Flags = User32.KeyboardFlag.KeyUp | User32.KeyboardFlag.Unicode,
                }
            };

            // Handle extended keys:
            // If the scan code is preceded by a prefix byte that has the value 0xE0 (224),
            // we need to include the KEYEVENTF_EXTENDEDKEY flag in the Flags property.
            if ((scanCode & 0xFF00) == 0xE000) {
                down.Keyboard.Flags |= User32.KeyboardFlag.ExtendedKey;
                up.Keyboard.Flags |= User32.KeyboardFlag.ExtendedKey;
            }

            _inputList.Add(down);
            _inputList.Add(up);
            return this;
        }

        public InputBuilder AddCharacters(IEnumerable<char> characters) {
            foreach (var character in characters) {
                AddCharacter(character);
            }
            return this;
        }

        public InputBuilder AddCharacters(string characters) {
            return AddCharacters(characters.ToCharArray());
        }

        public InputBuilder AddRelativeMouseMovement(int x, int y) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.Move,
                    X = x,
                    Y = y
                }
            });
            return this;
        }

        public InputBuilder AddAbsoluteMouseMovement(int absoluteX, int absoluteY) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.Move | User32.MouseFlag.Absolute,
                    X = absoluteX,
                    Y = absoluteY
                }
            });
            return this;
        }

        public InputBuilder AddAbsoluteMouseMovementOnVirtualDesktop(int absoluteX, int absoluteY) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.Move | User32.MouseFlag.Absolute | User32.MouseFlag.VirtualDesk,
                    X = absoluteX,
                    Y = absoluteY
                }
            });
            return this;
        }

        public InputBuilder AddMouseButtonDown(MouseButton button) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = { Flags = ToMouseButtonDownFlag(button) }
            });
            return this;
        }

        public InputBuilder AddMouseXButtonDown(XButton xButtonId) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.XDown,
                    MouseData = (uint)xButtonId
                }
            });
            return this;
        }

        public InputBuilder AddMouseButtonUp(MouseButton button) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = { Flags = ToMouseButtonUpFlag(button) }
            });
            return this;
        }

        public InputBuilder AddMouseXButtonUp(XButton xButtonId) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.XUp,
                    MouseData = (uint)xButtonId
                }
            });
            return this;
        }

        public InputBuilder AddMouseButtonClick(MouseButton button) {
            return AddMouseButtonDown(button).AddMouseButtonUp(button);
        }

        public InputBuilder AddMouseXButtonClick(XButton xButtonId) {
            return AddMouseXButtonDown(xButtonId).AddMouseXButtonUp(xButtonId);
        }

        public InputBuilder AddMouseButtonDoubleClick(MouseButton button) {
            return AddMouseButtonClick(button).AddMouseButtonClick(button);
        }

        public InputBuilder AddMouseXButtonDoubleClick(XButton xButtonId) {
            return AddMouseXButtonClick(xButtonId).AddMouseXButtonClick(xButtonId);
        }

        public InputBuilder AddMouseVerticalWheelScroll(int scrollAmount) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.VerticalWheel,
                    MouseData = (uint)scrollAmount
                }
            });
            return this;
        }

        public InputBuilder AddMouseHorizontalWheelScroll(int scrollAmount) {
            _inputList.Add(new User32.Input {
                Type = User32.InputType.Mouse,
                Mouse = {
                    Flags = User32.MouseFlag.HorizontalWheel,
                    MouseData = (uint)scrollAmount
                }
            });
            return this;
        }

        private static User32.MouseFlag ToMouseButtonDownFlag(MouseButton button) {
            switch (button) {
                case MouseButton.LeftButton:
                    return User32.MouseFlag.LeftDown;

                case MouseButton.MiddleButton:
                    return User32.MouseFlag.MiddleDown;

                case MouseButton.RightButton:
                    return User32.MouseFlag.RightDown;

                default:
                    return User32.MouseFlag.LeftDown;
            }
        }

        private static User32.MouseFlag ToMouseButtonUpFlag(MouseButton button) {
            switch (button) {
                case MouseButton.LeftButton:
                    return User32.MouseFlag.LeftUp;

                case MouseButton.MiddleButton:
                    return User32.MouseFlag.MiddleUp;

                case MouseButton.RightButton:
                    return User32.MouseFlag.RightUp;

                default:
                    return User32.MouseFlag.LeftUp;
            }
        }

        public static void DispatchInput(User32.Input[] inputs) {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Length == 0) throw new ArgumentException(@"The input array was empty", nameof(inputs));
            var successful = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(User32.Input)));
            if (successful != inputs.Length) {
                /* Some simulated input commands were not sent successfully. The most common reason for this
                 happening are the security features of Windows including User Interface Privacy Isolation (UIPI).
                 Your application can only send commands to applications of the same or lower elevation.
                 Similarly certain commands are restricted to Accessibility/UIAutomation applications. */
                throw new Exception("Failed to simulate input");
            }
        }
    }
}
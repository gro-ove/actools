using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Helpers {
    public static class LocalizationHelper {
        public static string MultiplyForm(this int number, string valueOne, string valueTwo) {
            return number == 1 || number > 20 && number % 10 == 1 ? valueOne : valueTwo;
        }

        public static string MultiplyForm(this long number, string valueOne, string valueTwo) {
            return number == 1 || number > 20 && number % 10 == 1 ? valueOne : valueTwo;
        }

        public static string GetOrdinalReadable(this int value) {
            if (value < 0) {
                return "Minus " + GetOrdinalReadable(-value).ToLowerInvariant();
            }

            switch (value) {
                case 0:
                    return "Zeroth";
                case 1:
                    return "First";
                case 2:
                    return "Second";
                case 3:
                    return "Third";
                case 4:
                    return "Fourth";
                case 5:
                    return "Fifth";
                case 6:
                    return "Sixth";
                case 7:
                    return "Seventh";
                case 8:
                    return "Eighth";
                case 9:
                    return "Ninth";
                case 10:
                    return "Tenth";
                case 11:
                    return "Eleventh";
                case 12:
                    return "Twelfth";
                case 13:
                    return "Thirteenth";
                case 14:
                    return "Fourteenth";
                case 15:
                    return "Fifteenth";
                case 16:
                    return "Sixteenth";
                case 17:
                    return "Seventeenth";
                case 18:
                    return "Eighteenth";
                case 19:
                    return "Nineteenth";
                case 20:
                    return "Twentieth";
                case 21:
                    return "Twenty-first";
                case 22:
                    return "Twenty-second";
                case 23:
                    return "Twenty-third";
                case 24:
                    return "Twenty-fourth";
                case 25:
                    return "Twenty-fifth";
                case 26:
                    return "Twenty-sixth";
                case 27:
                    return "Twenty-seventh";
                case 28:
                    return "Twenty-eighth";
                case 29:
                    return "Twenty-ninth";
                case 30:
                    return "Thirtieth";
                case 31:
                    return "Thirty-first";
                default:
                    return value + "th";
            }
        }

        public static string ReadableTime(this long seconds) {
            return ReadableTime(TimeSpan.FromSeconds(seconds));
        }

        public static string ReadableTime(this TimeSpan span) {
            var result = new List<string>();

            if (span.Days > 0) {
                result.Add(span.Days + MultiplyForm(span.Days, " day", " days"));
            }

            if (span.Hours > 0) {
                result.Add(span.Hours + MultiplyForm(span.Hours, " hour", " hours"));
            }

            if (span.Minutes > 0) {
                result.Add(span.Minutes + MultiplyForm(span.Minutes, " minute", " minutes"));
            }

            if (span.Seconds > 0) {
                result.Add(span.Seconds + MultiplyForm(span.Seconds, " second", " seconds"));
            }

            return result.Any() ? string.Join(" ", result.Take(2)) : "0 seconds";
        }

        public static string ReadableSize(this long i, int round = 2) {
            var absoluteI = i < 0 ? -i : i;

            string suffix;
            double readable;
            if (absoluteI >= 0x1000000000000000) {
                suffix = "EB";
                readable = i >> 50;
            } else if (absoluteI >= 0x4000000000000) {
                suffix = "PB";
                readable = i >> 40;
            } else if (absoluteI >= 0x10000000000) {
                suffix = "TB";
                readable = i >> 30;
            } else if (absoluteI >= 0x40000000) {
                suffix = "GB";
                readable = i >> 20;
            } else if (absoluteI >= 0x100000) {
                suffix = "MB";
                readable = i >> 10;
            } else if (absoluteI >= 0x400) {
                suffix = "KB";
                readable = i;
            } else {
                return i.ToString("0 B");
            }

            readable = readable / 1024;

            string format;
            switch (round) {
                case 1:
                    format = "0.# ";
                    break;

                case 2:
                    format = "0.## ";
                    break;

                case 3:
                    format = "0.### ";
                    break;

                default:
                    format = "0 ";
                    break;
            }

            return readable.ToString(format) + suffix;
        }

        public static string ToTitle(this string s) {
            return Regex.Replace(s, @"\b[a-z]", x => x.Value.ToUpper());
        }

        public static string ReadableKey(this Keys key) {
            switch (key) {
                //letters
                case Keys.A:
                case Keys.B:
                case Keys.C:
                case Keys.D:
                case Keys.E:
                case Keys.F:
                case Keys.G:
                case Keys.H:
                case Keys.I:
                case Keys.J:
                case Keys.K:
                case Keys.L:
                case Keys.M:
                case Keys.N:
                case Keys.O:
                case Keys.P:
                case Keys.Q:
                case Keys.R:
                case Keys.S:
                case Keys.T:
                case Keys.U:
                case Keys.V:
                case Keys.W:
                case Keys.X:
                case Keys.Y:
                case Keys.Z:
                    return Enum.GetName(typeof(Keys), key);

                //digits
                case Keys.D0:
                    return "0";
                case Keys.NumPad0:
                    return "Number Pad 0";
                case Keys.D1:
                    return "1";
                case Keys.NumPad1:
                    return "Number Pad 1";
                case Keys.D2:
                    return "2";
                case Keys.NumPad2:
                    return "Number Pad 2";
                case Keys.D3:
                    return "3";
                case Keys.NumPad3:
                    return "Number Pad 3";
                case Keys.D4:
                    return "4";
                case Keys.NumPad4:
                    return "Number Pad 4";
                case Keys.D5:
                    return "5";
                case Keys.NumPad5:
                    return "Number Pad 5";
                case Keys.D6:
                    return "6";
                case Keys.NumPad6:
                    return "Number Pad 6";
                case Keys.D7:
                    return "7";
                case Keys.NumPad7:
                    return "Number Pad 7";
                case Keys.D8:
                    return "8";
                case Keys.NumPad8:
                    return "Number Pad 8";
                case Keys.D9:
                    return "9";
                case Keys.NumPad9:
                    return "Number Pad 9";

                //punctuation
                case Keys.Add:
                    return "Number Pad +";
                case Keys.Subtract:
                    return "Number Pad -";
                case Keys.Divide:
                    return "Number Pad /";
                case Keys.Multiply:
                    return "Number Pad *";
                case Keys.Space:
                    return "Spacebar";
                case Keys.Decimal:
                    return "Number Pad .";

                //function
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                case Keys.F13:
                case Keys.F14:
                case Keys.F15:
                case Keys.F16:
                case Keys.F17:
                case Keys.F18:
                case Keys.F19:
                case Keys.F20:
                case Keys.F21:
                case Keys.F22:
                case Keys.F23:
                case Keys.F24:
                    return Enum.GetName(typeof(Keys), key);

                //navigation
                case Keys.Up:
                    return "Up Arrow";
                case Keys.Down:
                    return "Down Arrow";
                case Keys.Left:
                    return "Left Arrow";
                case Keys.Right:
                    return "Right Arrow";
                case Keys.Prior:
                    return "Page Up";
                case Keys.Next:
                    return "Page Down";
                case Keys.Home:
                    return "Home";
                case Keys.End:
                    return "End";

                //control keys
                case Keys.Back:
                    return "Backspace";
                case Keys.Tab:
                    return "Tab";
                case Keys.Escape:
                    return "Escape";
                case Keys.Enter:
                    return "Enter";
                case Keys.Shift:
                case Keys.ShiftKey:
                    return "Shift";
                case Keys.LShiftKey:
                    return "Shift (Left)";
                case Keys.RShiftKey:
                    return "Shift (Right)";
                case Keys.Control:
                case Keys.ControlKey:
                    return "Control";
                case Keys.LControlKey:
                    return "Control (Left)";
                case Keys.RControlKey:
                    return "Control (Right)";
                case Keys.Menu:
                case Keys.Alt:
                    return "Alt";
                case Keys.LMenu:
                    return "Alt (Left)";
                case Keys.RMenu:
                    return "Alt (Right)";
                case Keys.Pause:
                    return "Pause";
                case Keys.CapsLock:
                    return "Caps Lock";
                case Keys.NumLock:
                    return "Num Lock";
                case Keys.Scroll:
                    return "Scroll Lock";
                case Keys.PrintScreen:
                    return "Print Screen";
                case Keys.Insert:
                    return "Insert";
                case Keys.Delete:
                    return "Delete";
                case Keys.Help:
                    return "Help";
                case Keys.LWin:
                    return "Windows (Left)";
                case Keys.RWin:
                    return "Windows (Right)";
                case Keys.Apps:
                    return "Context Menu";

                //browser keys
                case Keys.BrowserBack:
                    return "Browser Back";
                case Keys.BrowserFavorites:
                    return "Browser Favorites";
                case Keys.BrowserForward:
                    return "Browser Forward";
                case Keys.BrowserHome:
                    return "Browser Home";
                case Keys.BrowserRefresh:
                    return "Browser Refresh";
                case Keys.BrowserSearch:
                    return "Browser Search";
                case Keys.BrowserStop:
                    return "Browser Stop";

                //media keys
                case Keys.VolumeDown:
                    return "Volume Down";
                case Keys.VolumeMute:
                    return "Volume Mute";
                case Keys.VolumeUp:
                    return "Volume Up";
                case Keys.MediaNextTrack:
                    return "Next Track";
                case Keys.Play:
                case Keys.MediaPlayPause:
                    return "Play";
                case Keys.MediaPreviousTrack:
                    return "Previous Track";
                case Keys.MediaStop:
                    return "Stop";
                case Keys.SelectMedia:
                    return "Select Media";

                //IME keys
                case Keys.HanjaMode:
                case Keys.JunjaMode:
                case Keys.HangulMode:
                case Keys.FinalMode: //duplicate values: Hanguel, Kana, Kanji  
                case Keys.IMEAccept:
                case Keys.IMEConvert: //duplicate: IMEAceept
                case Keys.IMEModeChange:
                case Keys.IMENonconvert:
                    return null;

                //special keys
                case Keys.LaunchMail:
                    return "Launch Mail";
                case Keys.LaunchApplication1:
                    return "Launch Favorite Application 1";
                case Keys.LaunchApplication2:
                    return "Launch Favorite Application 2";
                case Keys.Zoom:
                    return "Zoom";

                //oem keys 
                case Keys.OemSemicolon: //oem1
                    return ";";
                case Keys.OemQuestion: //oem2
                    return "?";
                case Keys.Oemtilde: //oem3
                    return "~";
                case Keys.OemOpenBrackets: //oem4
                    return "[";
                case Keys.OemPipe: //oem5
                    return "|";
                case Keys.OemCloseBrackets: //oem6
                    return "]";
                case Keys.OemQuotes: //oem7
                    return "'";
                case Keys.OemBackslash: //oem102
                    return "/";
                case Keys.Oemplus:
                    return "+";
                case Keys.OemMinus:
                    return "-";
                case Keys.Oemcomma:
                    return ",";
                case Keys.OemPeriod:
                    return ".";

                //unsupported oem keys
                case Keys.Oem8:
                case Keys.OemClear:
                    return null;

                //unsupported other keys
                case Keys.None:
                case Keys.LButton:
                case Keys.RButton:
                case Keys.MButton:
                case Keys.XButton1:
                case Keys.XButton2:
                case Keys.Clear:
                case Keys.Sleep:
                case Keys.Cancel:
                case Keys.LineFeed:
                case Keys.Select:
                case Keys.Print:
                case Keys.Execute:
                case Keys.Separator:
                case Keys.ProcessKey:
                case Keys.Packet:
                case Keys.Attn:
                case Keys.Crsel:
                case Keys.Exsel:
                case Keys.EraseEof:
                case Keys.NoName:
                case Keys.Pa1:
                case Keys.KeyCode:
                case Keys.Modifiers:
                    return null;

                default:
                    throw new NotSupportedException(Enum.GetName(typeof(Keys), key));
            }
        }

        public static string ReadableKey(this Key key) {
            switch (key) {
                //letters
                case Key.A:
                case Key.B:
                case Key.C:
                case Key.D:
                case Key.E:
                case Key.F:
                case Key.G:
                case Key.H:
                case Key.I:
                case Key.J:
                case Key.K:
                case Key.L:
                case Key.M:
                case Key.N:
                case Key.O:
                case Key.P:
                case Key.Q:
                case Key.R:
                case Key.S:
                case Key.T:
                case Key.U:
                case Key.V:
                case Key.W:
                case Key.X:
                case Key.Y:
                case Key.Z:
                    return Enum.GetName(typeof(Key), key);

                //digits
                case Key.D0:
                    return "0";
                case Key.NumPad0:
                    return "Number Pad 0";
                case Key.D1:
                    return "1";
                case Key.NumPad1:
                    return "Number Pad 1";
                case Key.D2:
                    return "2";
                case Key.NumPad2:
                    return "Number Pad 2";
                case Key.D3:
                    return "3";
                case Key.NumPad3:
                    return "Number Pad 3";
                case Key.D4:
                    return "4";
                case Key.NumPad4:
                    return "Number Pad 4";
                case Key.D5:
                    return "5";
                case Key.NumPad5:
                    return "Number Pad 5";
                case Key.D6:
                    return "6";
                case Key.NumPad6:
                    return "Number Pad 6";
                case Key.D7:
                    return "7";
                case Key.NumPad7:
                    return "Number Pad 7";
                case Key.D8:
                    return "8";
                case Key.NumPad8:
                    return "Number Pad 8";
                case Key.D9:
                    return "9";
                case Key.NumPad9:
                    return "Number Pad 9";

                //punctuation
                case Key.Add:
                    return "Number Pad +";
                case Key.Subtract:
                    return "Number Pad -";
                case Key.Divide:
                    return "Number Pad /";
                case Key.Multiply:
                    return "Number Pad *";
                case Key.Space:
                    return "Spacebar";
                case Key.Decimal:
                    return "Number Pad .";

                //function
                case Key.F1:
                case Key.F2:
                case Key.F3:
                case Key.F4:
                case Key.F5:
                case Key.F6:
                case Key.F7:
                case Key.F8:
                case Key.F9:
                case Key.F10:
                case Key.F11:
                case Key.F12:
                case Key.F13:
                case Key.F14:
                case Key.F15:
                case Key.F16:
                case Key.F17:
                case Key.F18:
                case Key.F19:
                case Key.F20:
                case Key.F21:
                case Key.F22:
                case Key.F23:
                case Key.F24:
                    return Enum.GetName(typeof(Key), key);

                //navigation
                case Key.Up:
                    return "Up Arrow";
                case Key.Down:
                    return "Down Arrow";
                case Key.Left:
                    return "Left Arrow";
                case Key.Right:
                    return "Right Arrow";
                case Key.Prior:
                    return "Page Up";
                case Key.Next:
                    return "Page Down";
                case Key.Home:
                    return "Home";
                case Key.End:
                    return "End";

                //control Key
                case Key.Back:
                    return "Backspace";
                case Key.Tab:
                    return "Tab";
                case Key.Escape:
                    return "Escape";
                case Key.Enter:
                    return "Enter";
                case Key.LeftShift:
                    return "Shift (Left)";
                case Key.RightShift:
                    return "Shift (Right)";
                case Key.LeftCtrl:
                    return "Control (Left)";
                case Key.RightCtrl:
                    return "Control (Right)";
                case Key.LeftAlt:
                    return "Alt (Left)";
                case Key.RightAlt:
                    return "Alt (Right)";
                case Key.Pause:
                    return "Pause";
                case Key.CapsLock:
                    return "Caps Lock";
                case Key.NumLock:
                    return "Num Lock";
                case Key.Scroll:
                    return "Scroll Lock";
                case Key.PrintScreen:
                    return "Print Screen";
                case Key.Insert:
                    return "Insert";
                case Key.Delete:
                    return "Delete";
                case Key.Help:
                    return "Help";
                case Key.LWin:
                    return "Windows (Left)";
                case Key.RWin:
                    return "Windows (Right)";
                case Key.Apps:
                    return "Context Menu";

                //browser Key
                case Key.BrowserBack:
                    return "Browser Back";
                case Key.BrowserFavorites:
                    return "Browser Favorites";
                case Key.BrowserForward:
                    return "Browser Forward";
                case Key.BrowserHome:
                    return "Browser Home";
                case Key.BrowserRefresh:
                    return "Browser Refresh";
                case Key.BrowserSearch:
                    return "Browser Search";
                case Key.BrowserStop:
                    return "Browser Stop";

                //media Key
                case Key.VolumeDown:
                    return "Volume Down";
                case Key.VolumeMute:
                    return "Volume Mute";
                case Key.VolumeUp:
                    return "Volume Up";
                case Key.MediaNextTrack:
                    return "Next Track";
                case Key.Play:
                case Key.MediaPlayPause:
                    return "Play";
                case Key.MediaPreviousTrack:
                    return "Previous Track";
                case Key.MediaStop:
                    return "Stop";
                case Key.SelectMedia:
                    return "Select Media";

                //IME Key
                case Key.HanjaMode:
                case Key.JunjaMode:
                case Key.HangulMode:
                case Key.FinalMode: //duplicate values: Hanguel, Kana, Kanji  
                    return null;

                //special Key
                case Key.LaunchMail:
                    return "Launch Mail";
                case Key.LaunchApplication1:
                    return "Launch Favorite Application 1";
                case Key.LaunchApplication2:
                    return "Launch Favorite Application 2";
                case Key.Zoom:
                    return "Zoom";

                //oem Key 
                case Key.OemSemicolon:
                    return ";";
                case Key.OemQuestion:
                    return "?";
                case Key.OemTilde:
                    return "~";
                case Key.OemOpenBrackets:
                    return "[";
                case Key.OemPipe:
                    return "|";
                case Key.OemCloseBrackets:
                    return "]";
                case Key.OemQuotes:
                    return "'";
                case Key.OemBackslash:
                    return "/";
                case Key.OemPlus:
                    return "+";
                case Key.OemMinus:
                    return "-";
                case Key.OemComma:
                    return ",";
                case Key.OemPeriod:
                    return ".";

                //unsupported oem Key
                case Key.Oem8:
                case Key.OemClear:
                    return null;

                //unsupported other Key
                case Key.None:
                case Key.Clear:
                case Key.Sleep:
                case Key.Cancel:
                case Key.LineFeed:
                case Key.Select:
                case Key.Print:
                case Key.Execute:
                case Key.Separator:
                case Key.Attn:
                case Key.EraseEof:
                case Key.NoName:
                case Key.Pa1:
                case Key.ImeConvert:
                case Key.ImeNonConvert:
                case Key.ImeAccept:
                case Key.ImeModeChange:
                case Key.AbntC1:
                case Key.AbntC2:
                case Key.ImeProcessed:
                case Key.System:
                case Key.OemAttn:
                case Key.OemFinish:
                case Key.OemCopy:
                case Key.OemAuto:
                case Key.OemEnlw:
                case Key.OemBackTab:
                case Key.CrSel:
                case Key.ExSel:
                case Key.DeadCharProcessed:
                    return null;

                default:
                    throw new NotSupportedException(Enum.GetName(typeof(Key), key));
            }
        }
    }
}

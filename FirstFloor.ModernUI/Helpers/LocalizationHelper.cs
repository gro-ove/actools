using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;

// Localize me!
namespace FirstFloor.ModernUI.Helpers {
    public static class LocalizationHelper {
        private static string MultiplyFormEn(this int number, string valueOne, string valueTwo) {
            return number == 1 ? valueOne : valueTwo;
        }

        private static string MultiplyFormRu(this int number, string valueOne, string valueTwo, string valueFive) {
            if (number == 1 || number > 20 && number % 10 == 1) {
                return valueOne;
            }

            if (number > 1 && number < 5 || number > 20 && number % 10 > 1 && number % 10 < 5) {
                return valueTwo;
            }

            return valueFive;
        }

        private static readonly bool CultureRu = CultureInfo.CurrentUICulture.Name == "ru-RU";

        public static string GetOrdinalReadable(this int value) {
            if (CultureRu) {
                return GetOrdinalReadableRu(value);
            } else {
                return GetOrdinalReadableEn(value);
            }
        }

        public static string GetOrdinalReadableRu(int value) {
            if (value < 0) {
                return "Минус " + GetOrdinalReadableRu(-value).ToLowerInvariant();
            }

            switch (value) {
                case 0:
                    return "Нулевой";
                case 1:
                    return "Первый";
                case 2:
                    return "Второй";
                case 3:
                    return "Третий";
                case 4:
                    return "Четвёртый";
                case 5:
                    return "Пятый";
                case 6:
                    return "Шестой";
                case 7:
                    return "Седьмой";
                case 8:
                    return "Восьмой";
                case 9:
                    return "Девятый";
                case 10:
                    return "Десятый";
                case 11:
                    return "Одиннадцатый";
                case 12:
                    return "Двенадцатый";
                case 13:
                    return "Тринадцатый";
                case 14:
                    return "Четырнадцатый";
                case 15:
                    return "Пятнадцатый";
                case 16:
                    return "Шестнадцатый";
                case 17:
                    return "Семнадцатый";
                case 18:
                    return "Восемнадцатый";
                case 19:
                    return "Девятнадцатый";
                case 20:
                    return "Двадцатый";
                default:
                    return value + "-й";
            }
        }

        public static string GetOrdinalReadableEn(int value) {
            if (value < 0) {
                return "Minus " + GetOrdinalReadableEn(-value).ToLowerInvariant();
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

        public static string ToReadableTime(this long seconds) {
            return ToReadableTime(TimeSpan.FromSeconds(seconds));
        }

        public static string ToReadableTime(this TimeSpan span) {
            if (CultureRu) {
                return ToReadableTimeRu(span);
            } else {
                return ToReadableTimeEn(span);
            }
        }

        public static string ToReadableTimeRu(this TimeSpan span) {
            var result = new List<string>();

            var days = (int)span.TotalDays;
            if (days > 0) {
                result.Add(days + MultiplyFormRu(days, " день", " дня", "дней"));
            }

            if (span.Hours > 0) {
                result.Add(span.Hours + MultiplyFormRu(span.Hours, " час", " часа", " часов"));
            }

            if (span.Minutes > 0) {
                result.Add(span.Minutes + MultiplyFormRu(span.Minutes, " минута", " минуты", " минут"));
            }

            if (span.Seconds > 0) {
                result.Add(span.Seconds + MultiplyFormRu(span.Seconds, " секунда", " секунды", " секунд"));
            }

            return result.Any() ? string.Join(" ", result.Take(2)) : "0 секунд";
        }

        public static string ToReadableTimeEn(this TimeSpan span) {
            var result = new List<string>();

            var days = (int)span.TotalDays;
            if (days > 0) {
                result.Add(days + MultiplyFormEn(days, " day", " days"));
            }

            if (span.Hours > 0) {
                result.Add(span.Hours + MultiplyFormEn(span.Hours, " hour", " hours"));
            }

            if (span.Minutes > 0) {
                result.Add(span.Minutes + MultiplyFormEn(span.Minutes, " minute", " minutes"));
            }

            if (span.Seconds > 0) {
                result.Add(span.Seconds + MultiplyFormEn(span.Seconds, " second", " seconds"));
            }

            return result.Any() ? string.Join(" ", result.Take(2)) : "0 seconds";
        }

        public static double AsMegabytes(this long i) {
            return i / 1024d / 1024d;
        }

        public static string ToReadableSize(this long i, int round = 2) {
            var absoluteI = i < 0 ? -i : i;

            string suffix;
            double readable;
            if (absoluteI >= 0x1000000000000000) {
                suffix = Resources.LocalizationHelper_ReadableSize_EB;
                readable = i >> 50;
            } else if (absoluteI >= 0x4000000000000) {
                suffix = Resources.LocalizationHelper_ReadableSize_PB;
                readable = i >> 40;
            } else if (absoluteI >= 0x10000000000) {
                suffix = Resources.LocalizationHelper_ReadableSize_TB;
                readable = i >> 30;
            } else if (absoluteI >= 0x40000000) {
                suffix = Resources.LocalizationHelper_ReadableSize_GB;
                readable = i >> 20;
            } else if (absoluteI >= 0x100000) {
                suffix = Resources.LocalizationHelper_ReadableSize_MB;
                readable = i >> 10;
            } else if (absoluteI >= 0x400) {
                suffix = Resources.LocalizationHelper_ReadableSize_KB;
                readable = i;
            } else {
                return i.ToString(@"0 " + Resources.LocalizationHelper_ReadableSize_B);
            }

            readable = readable / 1024;

            string format;
            switch (round) {
                case 1:
                    format = @"0.# ";
                    break;

                case 2:
                    format = @"0.## ";
                    break;

                case 3:
                    format = @"0.### ";
                    break;

                default:
                    format = @"0 ";
                    break;
            }

            return readable.ToString(format) + suffix;
        }

        public static string ToTitle(this string s) {
            if (CultureRu) return s;
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
                    return string.Format(Resources.KeyNumberPad, 0);
                case Keys.D1:
                    return "1";
                case Keys.NumPad1:
                    return string.Format(Resources.KeyNumberPad, 1);
                case Keys.D2:
                    return "2";
                case Keys.NumPad2:
                    return string.Format(Resources.KeyNumberPad, 2);
                case Keys.D3:
                    return "3";
                case Keys.NumPad3:
                    return string.Format(Resources.KeyNumberPad, 3);
                case Keys.D4:
                    return "4";
                case Keys.NumPad4:
                    return string.Format(Resources.KeyNumberPad, 4);
                case Keys.D5:
                    return "5";
                case Keys.NumPad5:
                    return string.Format(Resources.KeyNumberPad, 5);
                case Keys.D6:
                    return "6";
                case Keys.NumPad6:
                    return string.Format(Resources.KeyNumberPad, 6);
                case Keys.D7:
                    return "7";
                case Keys.NumPad7:
                    return string.Format(Resources.KeyNumberPad, 7);
                case Keys.D8:
                    return "8";
                case Keys.NumPad8:
                    return string.Format(Resources.KeyNumberPad, 8);
                case Keys.D9:
                    return "9";
                case Keys.NumPad9:
                    return string.Format(Resources.KeyNumberPad, 9);

                //punctuation
                case Keys.Add:
                    return string.Format(Resources.KeyNumberPad, "+");
                case Keys.Subtract:
                    return string.Format(Resources.KeyNumberPad, "-");
                case Keys.Divide:
                    return string.Format(Resources.KeyNumberPad, "/");
                case Keys.Multiply:
                    return string.Format(Resources.KeyNumberPad, "*");
                case Keys.Space:
                    return Resources.KeySpace;
                case Keys.Decimal:
                    return string.Format(Resources.KeyNumberPad, ".");

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
                    return Resources.KeyUpArrow;
                case Keys.Down:
                    return Resources.KeyDownArrow;
                case Keys.Left:
                    return Resources.KeyLeftArrow;
                case Keys.Right:
                    return Resources.KeyRightArrow;
                case Keys.Prior:
                    return Resources.KeyPageUp;
                case Keys.Next:
                    return Resources.KeyPageDown;
                case Keys.Home:
                    return Resources.KeyHome;
                case Keys.End:
                    return Resources.KeyEnd;

                //control keys
                case Keys.Back:
                    return Resources.KeyBackspace;
                case Keys.Tab:
                    return Resources.KeyTab;
                case Keys.Escape:
                    return Resources.KeyEscape;
                case Keys.Enter:
                    return Resources.KeyEnter;
                case Keys.Shift:
                case Keys.ShiftKey:
                    return Resources.KeyShift;
                case Keys.LShiftKey:
                    return Resources.KeyShiftLeft;
                case Keys.RShiftKey:
                    return Resources.KeyShiftRight;
                case Keys.Control:
                case Keys.ControlKey:
                    return Resources.KeyControl;
                case Keys.LControlKey:
                    return Resources.KeyControlLeft;
                case Keys.RControlKey:
                    return Resources.KeyControlRight;
                case Keys.Menu:
                case Keys.Alt:
                    return Resources.KeyAlt;
                case Keys.LMenu:
                    return Resources.KeyAltLeft;
                case Keys.RMenu:
                    return Resources.KeyAltRight;
                case Keys.Pause:
                    return Resources.KeyPause;
                case Keys.CapsLock:
                    return Resources.KeyCapsLock;
                case Keys.NumLock:
                    return Resources.KeyNumLock;
                case Keys.Scroll:
                    return Resources.KeyScrollLock;
                case Keys.PrintScreen:
                    return Resources.KeyPrintScreen;
                case Keys.Insert:
                    return Resources.KeyInsert;
                case Keys.Delete:
                    return Resources.KeyDelete;
                case Keys.Help:
                    return Resources.KeyHelp;
                case Keys.LWin:
                    return Resources.KeyWindowsLeft;
                case Keys.RWin:
                    return Resources.KeyWindowsRight;
                case Keys.Apps:
                    return Resources.KeyContextMenu;

                //browser keys
                case Keys.BrowserBack:
                    return Resources.KeyBrowserBack;
                case Keys.BrowserFavorites:
                    return Resources.KeyBrowserFavorites;
                case Keys.BrowserForward:
                    return Resources.KeyBrowserForward;
                case Keys.BrowserHome:
                    return Resources.KeyBrowserHome;
                case Keys.BrowserRefresh:
                    return Resources.KeyBrowserRefresh;
                case Keys.BrowserSearch:
                    return Resources.KeyBrowserSearch;
                case Keys.BrowserStop:
                    return Resources.KeyBrowserStop;

                //media keys
                case Keys.VolumeDown:
                    return Resources.KeyVolumeDown;
                case Keys.VolumeMute:
                    return Resources.KeyVolumeMute;
                case Keys.VolumeUp:
                    return Resources.KeyVolumeUp;
                case Keys.MediaNextTrack:
                    return Resources.KeyMediaNextTrack;
                case Keys.Play:
                case Keys.MediaPlayPause:
                    return Resources.KeyMediaPlayPause;
                case Keys.MediaPreviousTrack:
                    return Resources.KeyMediaPreviousTrack;
                case Keys.MediaStop:
                    return Resources.KeyMediaStop;
                case Keys.SelectMedia:
                    return Resources.KeySelectMedia;

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
                    return Resources.KeyLaunchMail;
                case Keys.LaunchApplication1:
                    return Resources.KeyLaunchApplication1;
                case Keys.LaunchApplication2:
                    return Resources.KeyLaunchApplication2;
                case Keys.Zoom:
                    return Resources.KeyZoom;

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
                    return string.Format(Resources.KeyNumberPad, 0);
                case Key.D1:
                    return "1";
                case Key.NumPad1:
                    return string.Format(Resources.KeyNumberPad, 1);
                case Key.D2:
                    return "2";
                case Key.NumPad2:
                    return string.Format(Resources.KeyNumberPad, 2);
                case Key.D3:
                    return "3";
                case Key.NumPad3:
                    return string.Format(Resources.KeyNumberPad, 3);
                case Key.D4:
                    return "4";
                case Key.NumPad4:
                    return string.Format(Resources.KeyNumberPad, 4);
                case Key.D5:
                    return "5";
                case Key.NumPad5:
                    return string.Format(Resources.KeyNumberPad, 5);
                case Key.D6:
                    return "6";
                case Key.NumPad6:
                    return string.Format(Resources.KeyNumberPad, 6);
                case Key.D7:
                    return "7";
                case Key.NumPad7:
                    return string.Format(Resources.KeyNumberPad, 7);
                case Key.D8:
                    return "8";
                case Key.NumPad8:
                    return string.Format(Resources.KeyNumberPad, 8);
                case Key.D9:
                    return "9";
                case Key.NumPad9:
                    return string.Format(Resources.KeyNumberPad, 9);

                //punctuation
                case Key.Add:
                    return string.Format(Resources.KeyNumberPad, "+");
                case Key.Subtract:
                    return string.Format(Resources.KeyNumberPad, "-");
                case Key.Divide:
                    return string.Format(Resources.KeyNumberPad, "/");
                case Key.Multiply:
                    return string.Format(Resources.KeyNumberPad, "*");
                case Key.Space:
                    return Resources.KeySpace;
                case Key.Decimal:
                    return string.Format(Resources.KeyNumberPad, ".");

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
                    return Resources.KeyUpArrow;
                case Key.Down:
                    return Resources.KeyDownArrow;
                case Key.Left:
                    return Resources.KeyLeftArrow;
                case Key.Right:
                    return Resources.KeyRightArrow;
                case Key.Prior:
                    return Resources.KeyPageUp;
                case Key.Next:
                    return Resources.KeyPageDown;
                case Key.Home:
                    return Resources.KeyHome;
                case Key.End:
                    return Resources.KeyEnd;

                //control Key
                case Key.Back:
                    return Resources.KeyBackspace;
                case Key.Tab:
                    return Resources.KeyTab;
                case Key.Escape:
                    return Resources.KeyEscape;
                case Key.Enter:
                    return Resources.KeyEnter;
                case Key.LeftShift:
                    return Resources.KeyShiftLeft;
                case Key.RightShift:
                    return Resources.KeyShiftRight;
                case Key.LeftCtrl:
                    return Resources.KeyControlLeft;
                case Key.RightCtrl:
                    return Resources.KeyControlRight;
                case Key.LeftAlt:
                    return Resources.KeyAltLeft;
                case Key.RightAlt:
                    return Resources.KeyAltRight;
                case Key.Pause:
                    return Resources.KeyPause;
                case Key.CapsLock:
                    return Resources.KeyCapsLock;
                case Key.NumLock:
                    return Resources.KeyNumLock;
                case Key.Scroll:
                    return Resources.KeyScrollLock;
                case Key.PrintScreen:
                    return Resources.KeyPrintScreen;
                case Key.Insert:
                    return Resources.KeyInsert;
                case Key.Delete:
                    return Resources.KeyDelete;
                case Key.Help:
                    return Resources.KeyHelp;
                case Key.LWin:
                    return Resources.KeyWindowsLeft;
                case Key.RWin:
                    return Resources.KeyWindowsRight;
                case Key.Apps:
                    return Resources.KeyContextMenu;

                //browser Key
                case Key.BrowserBack:
                    return Resources.KeyBrowserBack;
                case Key.BrowserFavorites:
                    return Resources.KeyBrowserFavorites;
                case Key.BrowserForward:
                    return Resources.KeyBrowserForward;
                case Key.BrowserHome:
                    return Resources.KeyBrowserHome;
                case Key.BrowserRefresh:
                    return Resources.KeyBrowserRefresh;
                case Key.BrowserSearch:
                    return Resources.KeyBrowserSearch;
                case Key.BrowserStop:
                    return Resources.KeyBrowserStop;

                //media Key
                case Key.VolumeDown:
                    return Resources.KeyVolumeDown;
                case Key.VolumeMute:
                    return Resources.KeyVolumeMute;
                case Key.VolumeUp:
                    return Resources.KeyVolumeUp;
                case Key.MediaNextTrack:
                    return Resources.KeyMediaNextTrack;
                case Key.Play:
                case Key.MediaPlayPause:
                    return Resources.KeyMediaPlayPause;
                case Key.MediaPreviousTrack:
                    return Resources.KeyMediaPreviousTrack;
                case Key.MediaStop:
                    return Resources.KeyMediaStop;
                case Key.SelectMedia:
                    return Resources.KeySelectMedia;

                //IME Key
                case Key.HanjaMode:
                case Key.JunjaMode:
                case Key.HangulMode:
                case Key.FinalMode: //duplicate values: Hanguel, Kana, Kanji  
                    return null;

                //special Key
                case Key.LaunchMail:
                    return Resources.KeyLaunchMail;
                case Key.LaunchApplication1:
                    return Resources.KeyLaunchApplication1;
                case Key.LaunchApplication2:
                    return Resources.KeyLaunchApplication2;
                case Key.Zoom:
                    return Resources.KeyZoom;

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

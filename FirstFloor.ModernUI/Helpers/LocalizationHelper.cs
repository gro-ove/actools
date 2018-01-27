using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using FirstFloor.ModernUI.Localizable;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

// Localize me!
namespace FirstFloor.ModernUI.Helpers {
    public static class LocalizationHelper {
        [ContractAnnotation("null => null; notnull => notnull")]
        public static string JoinToReadableString([CanBeNull] this IEnumerable<string> items) {
            if (items == null) return null;

            var list = items as IReadOnlyList<string> ?? items.ToList();
            switch (list.Count) {
                case 0:
                    return string.Empty;
                case 1:
                    return list[0];
                default:
                    return $@"{string.Join(@", ", list.Take(list.Count - 1))} {UiStrings.Common_And} {list.Last()}";
            }
        }

        public static string ToDisplayWindDirection(this int windDirection, bool randomWindDirection = false) {
            return ToDisplayWindDirection((double)windDirection, randomWindDirection);
        }

        public static string ToDisplayWindDirection(this double windDirection, bool randomWindDirection = false) {
            if (randomWindDirection) return "RND";
            switch ((int)Math.Round(windDirection / 22.5)) {
                case 0:
                case 16:
                    return "N";
                case 1:
                    return "NNE";
                case 2:
                    return "NE";
                case 3:
                    return "ENE";
                case 4:
                    return "E";
                case 5:
                    return "ESE";
                case 6:
                    return "SE";
                case 7:
                    return "SSE";
                case 8:
                    return "S";
                case 9:
                    return "SSW";
                case 10:
                    return "SW";
                case 11:
                    return "WSW";
                case 12:
                    return "W";
                case 13:
                    return "WNW";
                case 14:
                    return "NW";
                case 15:
                    return "NNW";
                default:
                    return "?";
            }
        }

        public static string ToReadableBoolean(this bool value) {
            return value ? UiStrings.Yes : UiStrings.No;
        }

        public static string ToOrdinal(this int value, string subject, CultureInfo culture = null) {
            return Ordinalizing.ConvertLong(value, subject);
        }

        public static string ToOrdinalShort(this int value, string subject, CultureInfo culture = null) {
            return Ordinalizing.ConvertShort(value, subject);
        }

        public static string ToReadableTime(this long seconds) {
            return ToReadableTime(TimeSpan.FromSeconds(seconds));
        }

        public static string ToReadableTime(this TimeSpan span) {
            var result = new List<string>();

            var days = (int)span.TotalDays;
            var months = days / 30;
            if (months > 30) {
                result.Add(PluralizingConverter.PluralizeExt(months, UiStrings.Time_Month));
                days = days % 30;
            }

            if (days > 0) {
                result.Add(days % 7 == 0
                        ? PluralizingConverter.PluralizeExt(days / 7, UiStrings.Time_Week) : PluralizingConverter.PluralizeExt(days, UiStrings.Time_Day));
            }

            if (span.Hours > 0) {
                result.Add(PluralizingConverter.PluralizeExt(span.Hours, UiStrings.Time_Hour));
            }

            if (span.Minutes > 0) {
                result.Add(PluralizingConverter.PluralizeExt(span.Minutes, UiStrings.Time_Minute));
            }

            if (span.Seconds > 0) {
                result.Add(PluralizingConverter.PluralizeExt(span.Seconds, UiStrings.Time_Second));
            }

            if (span.Milliseconds > 0 && result.Count == 0) {
                result.Add($@"{span.Milliseconds} ms");
            }

            return result.Count > 0 ? string.Join(@" ", result.Take(2)) : PluralizingConverter.PluralizeExt(0, UiStrings.Time_Second);
        }

        public static double ToMegabytes(this long i) {
            return i / 1024d / 1024d;
        }

        public static string ToReadableSize(this long i, int? round = null) {
            var absoluteI = i < 0 ? -i : i;

            string suffix;
            double readable;
            if (absoluteI >= 0x1000000000000000) {
                suffix = UiStrings.LocalizationHelper_ReadableSize_EB;
                readable = i >> 50;
            } else if (absoluteI >= 0x4000000000000) {
                suffix = UiStrings.LocalizationHelper_ReadableSize_PB;
                readable = i >> 40;
            } else if (absoluteI >= 0x10000000000) {
                suffix = UiStrings.LocalizationHelper_ReadableSize_TB;
                readable = i >> 30;
            } else if (absoluteI >= 0x40000000) {
                suffix = UiStrings.LocalizationHelper_ReadableSize_GB;
                readable = i >> 20;
            } else if (absoluteI >= 0x100000) {
                suffix = UiStrings.LocalizationHelper_ReadableSize_MB;
                readable = i >> 10;
            } else if (absoluteI >= 0x400) {
                suffix = UiStrings.LocalizationHelper_ReadableSize_KB;
                readable = i;
            } else {
                return i.ToString(@"0 " + UiStrings.LocalizationHelper_ReadableSize_B);
            }

            readable = readable / 1024;

            if (!round.HasValue) {
                if (readable < 10) {
                    round = 2;
                } else if (readable < 100) {
                    round = 1;
                } else {
                    round = 0;
                }
            }

            return $@"{readable.ToString($@"F{round}")} {suffix}";
        }

        public static bool TryParseReadableSize([CanBeNull] string size, [CanBeNull] string defaultPostfix, out long bytes) {
            if (string.IsNullOrWhiteSpace(size)) {
                bytes = 0;
                return false;
            }

            var split = -1;
            for (var i = 0; i < size.Length; i++) {
                if (char.IsLetter(size[i])) {
                    split = i;
                    break;
                }
            }

            string postfix;
            double value;

            if (split != -1) {
                if (!double.TryParse(size.Substring(0, split).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                    bytes = 0;
                    return false;
                }

                postfix = size.Substring(split).Trim().ToLower();
                if (string.IsNullOrWhiteSpace(postfix)) {
                    if (defaultPostfix == null) {
                        bytes = 0;
                        return false;
                    }

                    postfix = defaultPostfix;
                }
            } else if (defaultPostfix == null) {
                return long.TryParse(size, NumberStyles.Any, CultureInfo.InvariantCulture, out bytes);
            } else {
                postfix = defaultPostfix;
                if (!double.TryParse(size, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                    bytes = 0;
                    return false;
                }
            }

            if (postfix == "b" || postfix == UiStrings.LocalizationHelper_ReadableSize_B.ToLower()) {
                bytes = (long)value;
            } else if (postfix == "kb" || postfix == UiStrings.LocalizationHelper_ReadableSize_KB.ToLower()) {
                bytes = (long)(1024 * value);
            } else if (postfix == "mb" || postfix == UiStrings.LocalizationHelper_ReadableSize_MB.ToLower()) {
                bytes = (long)(1048576 * value);
            } else if (postfix == "gb" || postfix == UiStrings.LocalizationHelper_ReadableSize_GB.ToLower()) {
                bytes = (long)(1073741824 * value);
            } else if (postfix == "tb" || postfix == UiStrings.LocalizationHelper_ReadableSize_TB.ToLower()) {
                bytes = (long)(1099511627776 * value);
            } else if (postfix == "pb" || postfix == UiStrings.LocalizationHelper_ReadableSize_PB.ToLower()) {
                bytes = (long)(1099511627776 * value);
            } else if (postfix == "eb" || postfix == UiStrings.LocalizationHelper_ReadableSize_EB.ToLower()) {
                bytes = (long)(1099511627776 * value);
            } else {
                bytes = (long)value;
                return false;
            }

            return true;
        }

        public static string ToTitle(this string s) {
            return Titling.Convert(s);
        }

        public static string ToSentenceMember(this string s) {
            if (s.Length == 0) return string.Empty;

            s = s.Length < 2 || char.IsLower(s[0]) || char.IsUpper(s[1]) || s.Length > 2 && char.IsPunctuation(s[1]) && char.IsUpper(s[2]) ? s :
                    char.ToLower(s[0], CultureInfo.CurrentUICulture) + s.Substring(1);
            return s[s.Length - 1] == '.' || s[s.Length - 1] == '…' ? s.Substring(0, s.Length - 1) : s;
        }

        public static string ToTitle(this string s, CultureInfo culture) {
            return Titling.Convert(s, culture);
        }

        public static string ToSentence([NotNull] this string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Length == 0) return string.Empty;

            var l = s[s.Length - 1];
            return l == ';' ? $@"{s.Substring(0, s.Length - 1)}." :
                    char.IsLetterOrDigit(l) ? $@"{s}." : s;
        }

        public static string ToReadableKey(this Keys key) {
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
                    return @"0";
                case Keys.NumPad0:
                    return string.Format(UiStrings.KeyNumberPad, 0);
                case Keys.D1:
                    return @"1";
                case Keys.NumPad1:
                    return string.Format(UiStrings.KeyNumberPad, 1);
                case Keys.D2:
                    return @"2";
                case Keys.NumPad2:
                    return string.Format(UiStrings.KeyNumberPad, 2);
                case Keys.D3:
                    return @"3";
                case Keys.NumPad3:
                    return string.Format(UiStrings.KeyNumberPad, 3);
                case Keys.D4:
                    return @"4";
                case Keys.NumPad4:
                    return string.Format(UiStrings.KeyNumberPad, 4);
                case Keys.D5:
                    return @"5";
                case Keys.NumPad5:
                    return string.Format(UiStrings.KeyNumberPad, 5);
                case Keys.D6:
                    return @"6";
                case Keys.NumPad6:
                    return string.Format(UiStrings.KeyNumberPad, 6);
                case Keys.D7:
                    return @"7";
                case Keys.NumPad7:
                    return string.Format(UiStrings.KeyNumberPad, 7);
                case Keys.D8:
                    return @"8";
                case Keys.NumPad8:
                    return string.Format(UiStrings.KeyNumberPad, 8);
                case Keys.D9:
                    return @"9";
                case Keys.NumPad9:
                    return string.Format(UiStrings.KeyNumberPad, 9);

                //punctuation
                case Keys.Add:
                    return string.Format(UiStrings.KeyNumberPad, @"+");
                case Keys.Subtract:
                    return string.Format(UiStrings.KeyNumberPad, @"-");
                case Keys.Divide:
                    return string.Format(UiStrings.KeyNumberPad, @"/");
                case Keys.Multiply:
                    return string.Format(UiStrings.KeyNumberPad, @"*");
                case Keys.Space:
                    return UiStrings.KeySpace;
                case Keys.Decimal:
                    return string.Format(UiStrings.KeyNumberPad, @".");

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
                    return UiStrings.KeyUpArrow;
                case Keys.Down:
                    return UiStrings.KeyDownArrow;
                case Keys.Left:
                    return UiStrings.KeyLeftArrow;
                case Keys.Right:
                    return UiStrings.KeyRightArrow;
                case Keys.Prior:
                    return UiStrings.KeyPageUp;
                case Keys.Next:
                    return UiStrings.KeyPageDown;
                case Keys.Home:
                    return UiStrings.KeyHome;
                case Keys.End:
                    return UiStrings.KeyEnd;

                //control keys
                case Keys.Back:
                    return UiStrings.KeyBackspace;
                case Keys.Tab:
                    return UiStrings.KeyTab;
                case Keys.Escape:
                    return UiStrings.KeyEscape;
                case Keys.Enter:
                    return UiStrings.KeyEnter;
                case Keys.Shift:
                case Keys.ShiftKey:
                    return UiStrings.KeyShift;
                case Keys.LShiftKey:
                    return UiStrings.KeyShiftLeft;
                case Keys.RShiftKey:
                    return UiStrings.KeyShiftRight;
                case Keys.Control:
                case Keys.ControlKey:
                    return UiStrings.KeyControl;
                case Keys.LControlKey:
                    return UiStrings.KeyControlLeft;
                case Keys.RControlKey:
                    return UiStrings.KeyControlRight;
                case Keys.Menu:
                case Keys.Alt:
                    return UiStrings.KeyAlt;
                case Keys.LMenu:
                    return UiStrings.KeyAltLeft;
                case Keys.RMenu:
                    return UiStrings.KeyAltRight;
                case Keys.Pause:
                    return UiStrings.KeyPause;
                case Keys.CapsLock:
                    return UiStrings.KeyCapsLock;
                case Keys.NumLock:
                    return UiStrings.KeyNumLock;
                case Keys.Scroll:
                    return UiStrings.KeyScrollLock;
                case Keys.PrintScreen:
                    return UiStrings.KeyPrintScreen;
                case Keys.Insert:
                    return UiStrings.KeyInsert;
                case Keys.Delete:
                    return UiStrings.KeyDelete;
                case Keys.Help:
                    return UiStrings.KeyHelp;
                case Keys.LWin:
                    return UiStrings.KeyWindowsLeft;
                case Keys.RWin:
                    return UiStrings.KeyWindowsRight;
                case Keys.Apps:
                    return UiStrings.KeyContextMenu;

                //browser keys
                case Keys.BrowserBack:
                    return UiStrings.KeyBrowserBack;
                case Keys.BrowserFavorites:
                    return UiStrings.KeyBrowserFavorites;
                case Keys.BrowserForward:
                    return UiStrings.KeyBrowserForward;
                case Keys.BrowserHome:
                    return UiStrings.KeyBrowserHome;
                case Keys.BrowserRefresh:
                    return UiStrings.KeyBrowserRefresh;
                case Keys.BrowserSearch:
                    return UiStrings.KeyBrowserSearch;
                case Keys.BrowserStop:
                    return UiStrings.KeyBrowserStop;

                //media keys
                case Keys.VolumeDown:
                    return UiStrings.KeyVolumeDown;
                case Keys.VolumeMute:
                    return UiStrings.KeyVolumeMute;
                case Keys.VolumeUp:
                    return UiStrings.KeyVolumeUp;
                case Keys.MediaNextTrack:
                    return UiStrings.KeyMediaNextTrack;
                case Keys.Play:
                case Keys.MediaPlayPause:
                    return UiStrings.KeyMediaPlayPause;
                case Keys.MediaPreviousTrack:
                    return UiStrings.KeyMediaPreviousTrack;
                case Keys.MediaStop:
                    return UiStrings.KeyMediaStop;
                case Keys.SelectMedia:
                    return UiStrings.KeySelectMedia;

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
                    return UiStrings.KeyLaunchMail;
                case Keys.LaunchApplication1:
                    return UiStrings.KeyLaunchApplication1;
                case Keys.LaunchApplication2:
                    return UiStrings.KeyLaunchApplication2;
                case Keys.Zoom:
                    return UiStrings.KeyZoom;

                //oem keys
                case Keys.OemSemicolon: //oem1
                    return @";";
                case Keys.OemQuestion: //oem2
                    return @"?";
                case Keys.Oemtilde: //oem3
                    return @"~";
                case Keys.OemOpenBrackets: //oem4
                    return @"[";
                case Keys.OemPipe: //oem5
                    return @"|";
                case Keys.OemCloseBrackets: //oem6
                    return @"]";
                case Keys.OemQuotes: //oem7
                    return @"'";
                case Keys.OemBackslash: //oem102
                    return @"/";
                case Keys.Oemplus:
                    return @"+";
                case Keys.OemMinus:
                    return @"-";
                case Keys.Oemcomma:
                    return @",";
                case Keys.OemPeriod:
                    return @".";

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

        public static string ToReadableKey(this Key key) {
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
                    return @"0";
                case Key.NumPad0:
                    return string.Format(UiStrings.KeyNumberPad, 0);
                case Key.D1:
                    return @"1";
                case Key.NumPad1:
                    return string.Format(UiStrings.KeyNumberPad, 1);
                case Key.D2:
                    return @"2";
                case Key.NumPad2:
                    return string.Format(UiStrings.KeyNumberPad, 2);
                case Key.D3:
                    return @"3";
                case Key.NumPad3:
                    return string.Format(UiStrings.KeyNumberPad, 3);
                case Key.D4:
                    return @"4";
                case Key.NumPad4:
                    return string.Format(UiStrings.KeyNumberPad, 4);
                case Key.D5:
                    return @"5";
                case Key.NumPad5:
                    return string.Format(UiStrings.KeyNumberPad, 5);
                case Key.D6:
                    return @"6";
                case Key.NumPad6:
                    return string.Format(UiStrings.KeyNumberPad, 6);
                case Key.D7:
                    return @"7";
                case Key.NumPad7:
                    return string.Format(UiStrings.KeyNumberPad, 7);
                case Key.D8:
                    return @"8";
                case Key.NumPad8:
                    return string.Format(UiStrings.KeyNumberPad, 8);
                case Key.D9:
                    return @"9";
                case Key.NumPad9:
                    return string.Format(UiStrings.KeyNumberPad, 9);

                //punctuation
                case Key.Add:
                    return string.Format(UiStrings.KeyNumberPad, @"+");
                case Key.Subtract:
                    return string.Format(UiStrings.KeyNumberPad, @"-");
                case Key.Divide:
                    return string.Format(UiStrings.KeyNumberPad, @"/");
                case Key.Multiply:
                    return string.Format(UiStrings.KeyNumberPad, @"*");
                case Key.Space:
                    return UiStrings.KeySpace;
                case Key.Decimal:
                    return string.Format(UiStrings.KeyNumberPad, @".");

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
                    return UiStrings.KeyUpArrow;
                case Key.Down:
                    return UiStrings.KeyDownArrow;
                case Key.Left:
                    return UiStrings.KeyLeftArrow;
                case Key.Right:
                    return UiStrings.KeyRightArrow;
                case Key.Prior:
                    return UiStrings.KeyPageUp;
                case Key.Next:
                    return UiStrings.KeyPageDown;
                case Key.Home:
                    return UiStrings.KeyHome;
                case Key.End:
                    return UiStrings.KeyEnd;

                //control Key
                case Key.Back:
                    return UiStrings.KeyBackspace;
                case Key.Tab:
                    return UiStrings.KeyTab;
                case Key.Escape:
                    return UiStrings.KeyEscape;
                case Key.Enter:
                    return UiStrings.KeyEnter;
                case Key.LeftShift:
                    return UiStrings.KeyShiftLeft;
                case Key.RightShift:
                    return UiStrings.KeyShiftRight;
                case Key.LeftCtrl:
                    return UiStrings.KeyControlLeft;
                case Key.RightCtrl:
                    return UiStrings.KeyControlRight;
                case Key.LeftAlt:
                    return UiStrings.KeyAltLeft;
                case Key.RightAlt:
                    return UiStrings.KeyAltRight;
                case Key.Pause:
                    return UiStrings.KeyPause;
                case Key.CapsLock:
                    return UiStrings.KeyCapsLock;
                case Key.NumLock:
                    return UiStrings.KeyNumLock;
                case Key.Scroll:
                    return UiStrings.KeyScrollLock;
                case Key.PrintScreen:
                    return UiStrings.KeyPrintScreen;
                case Key.Insert:
                    return UiStrings.KeyInsert;
                case Key.Delete:
                    return UiStrings.KeyDelete;
                case Key.Help:
                    return UiStrings.KeyHelp;
                case Key.LWin:
                    return UiStrings.KeyWindowsLeft;
                case Key.RWin:
                    return UiStrings.KeyWindowsRight;
                case Key.Apps:
                    return UiStrings.KeyContextMenu;

                //browser Key
                case Key.BrowserBack:
                    return UiStrings.KeyBrowserBack;
                case Key.BrowserFavorites:
                    return UiStrings.KeyBrowserFavorites;
                case Key.BrowserForward:
                    return UiStrings.KeyBrowserForward;
                case Key.BrowserHome:
                    return UiStrings.KeyBrowserHome;
                case Key.BrowserRefresh:
                    return UiStrings.KeyBrowserRefresh;
                case Key.BrowserSearch:
                    return UiStrings.KeyBrowserSearch;
                case Key.BrowserStop:
                    return UiStrings.KeyBrowserStop;

                //media Key
                case Key.VolumeDown:
                    return UiStrings.KeyVolumeDown;
                case Key.VolumeMute:
                    return UiStrings.KeyVolumeMute;
                case Key.VolumeUp:
                    return UiStrings.KeyVolumeUp;
                case Key.MediaNextTrack:
                    return UiStrings.KeyMediaNextTrack;
                case Key.Play:
                case Key.MediaPlayPause:
                    return UiStrings.KeyMediaPlayPause;
                case Key.MediaPreviousTrack:
                    return UiStrings.KeyMediaPreviousTrack;
                case Key.MediaStop:
                    return UiStrings.KeyMediaStop;
                case Key.SelectMedia:
                    return UiStrings.KeySelectMedia;

                //IME Key
                case Key.HanjaMode:
                case Key.JunjaMode:
                case Key.HangulMode:
                case Key.FinalMode: //duplicate values: Hanguel, Kana, Kanji
                    return null;

                //special Key
                case Key.LaunchMail:
                    return UiStrings.KeyLaunchMail;
                case Key.LaunchApplication1:
                    return UiStrings.KeyLaunchApplication1;
                case Key.LaunchApplication2:
                    return UiStrings.KeyLaunchApplication2;
                case Key.Zoom:
                    return UiStrings.KeyZoom;

                //oem Key
                case Key.OemSemicolon:
                    return @";";
                case Key.OemQuestion:
                    return @"?";
                case Key.OemTilde:
                    return @"~";
                case Key.OemOpenBrackets:
                    return @"[";
                case Key.OemPipe:
                    return @"|";
                case Key.OemCloseBrackets:
                    return @"]";
                case Key.OemQuotes:
                    return @"'";
                case Key.OemBackslash:
                    return @"/";
                case Key.OemPlus:
                    return @"+";
                case Key.OemMinus:
                    return @"-";
                case Key.OemComma:
                    return @",";
                case Key.OemPeriod:
                    return @".";

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

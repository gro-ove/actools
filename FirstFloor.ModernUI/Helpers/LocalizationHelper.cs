using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstFloor.ModernUI.Helpers {
    public static class LocalizationHelper {
        public static string MultiplyForm(int number, string valueOne, string valueTwo) {
            return number == 1 || number > 20 && number % 10 == 1 ? valueOne : valueTwo;
        }

        public static string MultiplyForm(long number, string valueOne, string valueTwo) {
            return number == 1 || number > 20 && number % 10 == 1 ? valueOne : valueTwo;
        }

        public static string GetOrdinalReadable(int value) {
            if (value < 0) {
                return "Minus " + GetOrdinalReadable(-value).ToLowerInvariant();
            }

            switch (value) {
                case 0: return "Zeroth";
                case 1: return "First";
                case 2: return "Second";
                case 3: return "Third";
                case 4: return "Fourth";
                case 5: return "Fifth";
                case 6: return "Sixth";
                case 7: return "Seventh";
                case 8: return "Eighth";
                case 9: return "Ninth";
                case 10: return "Tenth";
                case 11: return "Eleventh";
                case 12: return "Twelfth";
                case 13: return "Thirteenth";
                case 14: return "Fourteenth";
                case 15: return "Fifteenth";
                case 16: return "Sixteenth";
                case 17: return "Seventeenth";
                case 18: return "Eighteenth";
                case 19: return "Nineteenth";
                case 20: return "Twentieth";
                case 21: return "Twenty-first";
                case 22: return "Twenty-second";
                case 23: return "Twenty-third";
                case 24: return "Twenty-fourth";
                case 25: return "Twenty-fifth";
                case 26: return "Twenty-sixth";
                case 27: return "Twenty-seventh";
                case 28: return "Twenty-eighth";
                case 29: return "Twenty-ninth";
                case 30: return "Thirtieth";
                case 31: return "Thirty-first";
                default:
                    return value + "th";
            }
        }

        public static string ReadableTime(long seconds) {
            return ReadableTime(TimeSpan.FromSeconds(seconds));
        }

        public static string ReadableTime(TimeSpan span) {
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
    }
}

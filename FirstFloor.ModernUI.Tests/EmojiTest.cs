using NUnit.Framework;

#if DEBUG
using System;
using System.Collections.Generic;
using FirstFloor.ModernUI.Windows.Controls.BbCode;
#endif

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class EmojiTest {
#if DEBUG
        private static IEnumerable<string> ToInts(string source) {
            for (var i = 0; i < source.Length; i++) {
                if (char.IsHighSurrogate(source, i)) {
                    yield return $"0x{char.ConvertToUtf32(source, i):x} (hs)";
                    i++;
                } else {
                    yield return $"0x{(int)source[i]:x}";
                }
            }
        }

        private static string ConvertEmoji(string source) {
            var result = "";

            for (var i = 0; i < source.Length; i++) {
                if (Emoji.IsEmoji(source, i, out var length)) {
                    result += "[" + source.Substring(i, length) + "]";
                    i += length - 1;
                } else {
                    result += source.Substring(i, 1);
                }
            }

            return result;
        }

        [Test]
        public void ComplexTest() {
            var s = "🧚🏾‍♀️";
            Console.WriteLine(string.Join(", ", ToInts(s)));
            Assert.AreEqual("[🧚🏾‍♀️]", ConvertEmoji(s));
        }

        [Test]
        public void SkinTest() {
            var s = "🙆🏾";
            Console.WriteLine(string.Join(", ", ToInts(s)));
            Assert.AreEqual("[🙆🏾]", ConvertEmoji(s));
        }

        // not supported in twemoji pack
        /*[Test]
        public void ComplexJoinedTest() {
            var s = "👩‍❤️‍💋‍👩";
            Console.WriteLine(string.Join(", ", ToInts(s)));
            Assert.AreEqual("[👩‍❤️‍💋‍👩]", ConvertEmoji(s));
        }*/
#endif
    }
}
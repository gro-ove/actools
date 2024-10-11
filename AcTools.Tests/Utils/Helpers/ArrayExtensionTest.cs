using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests.Utils.Helpers {
    [TestFixture, TestOf(typeof(ArrayExtension))]
    public class ArrayExtensionTest {

        [Test]
        public void TestPaddingThing() {
            Assert.AreEqual("a".ToCutBase64().FromCutBase64()?.ToUtf8String(), "a");
            Assert.AreEqual("aa".ToCutBase64().FromCutBase64()?.ToUtf8String(), "aa");
            Assert.AreEqual("aaa".ToCutBase64().FromCutBase64()?.ToUtf8String(), "aaa");
            Assert.AreEqual("aaaa".ToCutBase64().FromCutBase64()?.ToUtf8String(), "aaaa");
            Assert.AreEqual("aaaaa".ToCutBase64().FromCutBase64()?.ToUtf8String(), "aaaaa");
            Assert.AreEqual("aaaaaa".ToCutBase64().FromCutBase64()?.ToUtf8String(), "aaaaaa");
            Assert.AreEqual("\x000'ü\x00À".ToCutBase64().FromCutBase64()?.ToUtf8String(), "\x000'ü\x00À");
            Assert.AreEqual("QURBbi9BREFu".ToCutBase64().FromCutBase64()?.ToUtf8String(), "QURBbi9BREFu");
            Assert.AreEqual("QURBbi9BREFu".ToCutBase64(), "UVVSQmJpOUJSRUZ1");
        }
    }
}
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class StringExtensionTest {
        [Test]
        public void Version() {
            Assert.IsTrue("0.1.2".CompareAsVersionTo("0.0.8") > 0);
            Assert.IsTrue("0.1.2".CompareAsVersionTo("0.0test.8") > 0);
            Assert.IsTrue("0.1.2".CompareAsVersionTo("0.2.8") < 0);
            Assert.IsTrue("0.1.2".CompareAsVersionTo("0.q2.8") < 0);
            Assert.IsTrue("0.1.2".CompareAsVersionTo("0") > 0);
            Assert.IsTrue("1.1.2".CompareAsVersionTo("0") > 0);
            Assert.IsTrue("1.1.2".CompareAsVersionTo("2") < 0);
            Assert.IsTrue("1.8".CompareAsVersionTo("2.0") < 0);
            Assert.IsTrue("2.0".CompareAsVersionTo("1.8") > 0);
        }

        [Test]
        public void ReplaceStuff() {
            Assert.AreEqual("abc abc qwe ab", "abc abc abc ab".ReplaceLastOccurrence("abc", "qwe"));
            Assert.AreEqual("abc abc abc qwe", "abc abc abc ab".ReplaceLastOccurrence("ab", "qwe"));
            Assert.AreEqual("abc abc abc ab", "abc abc abc ab".ReplaceLastOccurrence("abcd", "qwe"));
        }
    }
}
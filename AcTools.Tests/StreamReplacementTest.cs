using System.Collections.Generic;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class StreamReplacementTest {
        [Test]
        public void Test() {
            var replacement = new StreamReplacement(
                    new KeyValuePair<string, string>("abc", "defDEF"),
                    new KeyValuePair<string, string>("qw", "rtRT"));
            Assert.AreEqual(" defDEF rtRT ", replacement.Filter(" abc qw "));

            var replacement2 = new StreamReplacement(
                    new KeyValuePair<string, string>("abc", "defDEF"),
                    new KeyValuePair<string, string>("abd", "rtRT"));
            Assert.AreEqual(" ab defDEF rtRT a a ", replacement2.Filter(" ab abc abd a a "));

            var replacement3 = new StreamReplacement(
                    new KeyValuePair<string, string>("abc", "defDEF"),
                    new KeyValuePair<string, string>("bwe", "rtRT"));
            Assert.AreEqual(" ab defDEF rtRT a a ", replacement3.Filter(" ab abc bwe a a "));

            var hardcoreLevel = new StreamReplacement(
                    new KeyValuePair<string, string>("rylongli", "qwerty"),
                    new KeyValuePair<string, string>("lock", "dick"));
            Assert.AreEqual("veqwertyne", hardcoreLevel.Filter("verylongline"));
        }
    }
}